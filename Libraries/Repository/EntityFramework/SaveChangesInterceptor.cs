using DDS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DDS.Repository.EntityFramework;

internal sealed class SaveChangesInterceptor : ISaveChangesInterceptor
{
	private readonly Func<TimeProvider?> _timeProviderFactory;
	public const string CREATED = "Created";
	public const string UPDATED = "Updated";
	public const string DELETED = "Deleted";

	/// <summary>
	///     Clave del query filter que registra <c>UseSoftDelete()</c>.
	/// </summary>
	public const string SOFT_DELETE_FILTER = "SoftDeletionFilter";

	public const string CREATED_LOCAL = "CreatedLocal";
	public const string UPDATED_LOCAL = "UpdatedLocal";

	/// <summary>
	///     Anotacion que <c>UseAutoCreated()</c>, <c>UseAutoUpdated()</c> y <c>UseSoftDelete()</c> le ponen a la
	///     propiedad que configuran, para que este interceptor la pueda distinguir de una propiedad del mismo nombre
	///     que el modelo declare por su cuenta con el mismo nombre.
	/// </summary>
	public const string AUTO_PROPERTY = "DDS:AutoProperty";

	public SaveChangesInterceptor(Func<TimeProvider?> timeProviderFactory)
	{
		_timeProviderFactory = timeProviderFactory;
	}

	/// <inheritdoc />
	public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		RunUpdaters(eventData.Context?.ChangeTracker);
		return result;
	}

	/// <inheritdoc />
	public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken)
	{
		RunUpdaters(eventData.Context?.ChangeTracker);
		return ValueTask.FromResult(result);
	}

	private void RunUpdaters(ChangeTracker? changeTracker)
	{
		if (changeTracker is null)
		{
			return;
		}

		changeTracker.DetectChanges();

		var timeProvider = _timeProviderFactory();
		var utcNow = timeProvider?.GetUtcNow().DateTime ?? DateTime.UtcNow;
		var localNow = timeProvider?.GetLocalNow().DateTime;

		// La lista se materializa antes de tocar nada: un soft delete cambia el estado de la entrada y el de sus
		// dependientes en cascada, y mutar estados mientras se enumera ChangeTracker.Entries() hace que la
		// enumeracion se saltee justo las que cambiaron - el dependiente recien marcado como Modified nunca llegaba
		// a que le refrescaran "Updated".
		var entries = changeTracker.Entries().ToArray();

		// Primero los soft deletes, que son los unicos que mueven estados.
		foreach (var entry in entries)
		{
			if (entry.State == EntityState.Deleted && UpdateDeleted(entry))
			{
				entry.DetectChanges();
			}
		}

		// Y despues las marcas de tiempo y el ETag, ya con todos los estados definitivos: un soft delete es una
		// modificacion como cualquier otra y tiene que refrescar el timestamp que lee cualquier sync incremental,
		// tanto en la entidad borrada como en los dependientes que se fueron con ella.
		foreach (var entry in entries)
		{
			if (entry.State is not (EntityState.Modified or EntityState.Added))
			{
				continue;
			}

			var runDetect = UpdateCreated(entry, utcNow, localNow);
			runDetect |= UpdateUpdated(entry, utcNow, localNow);
			runDetect |= UpdateETag(entry);

			if (runDetect)
			{
				entry.DetectChanges();
			}
		}
	}

	/// <summary>
	///     Encuentra la propiedad que configuraron <c>UseAutoCreated()</c>, <c>UseAutoUpdated()</c> o <c>UseSoftDelete()</c>,
	///     o <see langword="null" /> cuando el tipo de entidad no fue configurado para eso.
	/// </summary>
	/// <remarks>
	///     El marcador es la propiedad del modelo y no un diccionario indexado por el tipo CLR con el que se llamo al
	///     builder: un tipo derivado por TPH hereda la propiedad, asi que <c>Dog</c> se borra suave junto con
	///     <c>Animal</c>. Lo que la identifica es la anotacion <see cref="AUTO_PROPERTY" /> que le pone el builder, y no
	///     que sea una shadow property: <c>Property&lt;bool&gt;("Deleted")</c> se liga a una propiedad CLR cuando la
	///     entidad declara una, y exigir que fuera shadow ahi borraba de verdad una entidad que habia pedido soft delete.
	///     Una entidad que declara una columna <c>Created</c> o <c>Deleted</c> sin pedir el comportamiento no lleva
	///     anotacion y se la sigue dejando tranquila.
	/// </remarks>
	private static PropertyEntry? FindAutoProperty(EntityEntry entry, string name)
	{
		return entry.Metadata.FindProperty(name)?.FindAnnotation(AUTO_PROPERTY) is not null ? entry.Property(name) : null;
	}

	private static bool UpdateCreated(EntityEntry updatedEntry, DateTime utcNow, DateTime? localNow)
	{
		if (updatedEntry.State != EntityState.Added)
		{
			return false;
		}

		return Stamp(updatedEntry, CREATED, utcNow) | Stamp(updatedEntry, CREATED_LOCAL, localNow);
	}

	private static bool UpdateUpdated(EntityEntry updatedEntry, DateTime utcNow, DateTime? localNow)
	{
		if (updatedEntry.State is not (EntityState.Modified or EntityState.Added))
		{
			return false;
		}

		return Stamp(updatedEntry, UPDATED, utcNow) | Stamp(updatedEntry, UPDATED_LOCAL, localNow);
	}

	private static bool Stamp(EntityEntry updatedEntry, string name, DateTime? value)
	{
		if (value is null || FindAutoProperty(updatedEntry, name) is not { } property)
		{
			return false;
		}

		property.CurrentValue = value.Value;
		return true;
	}

	private static bool UpdateDeleted(EntityEntry updatedEntry)
	{
		if (updatedEntry.State != EntityState.Deleted || FindAutoProperty(updatedEntry, DELETED) is not { } deleted)
		{
			return false;
		}

		deleted.CurrentValue = true;
		updatedEntry.State = EntityState.Modified;

		//Esto revisa cuáles entidades dependientes pertenecen a la entidad original y les cambia el estado, para que no borre las owned...
		CascadeDelete(updatedEntry);
		return true;
	}

	private static void CascadeDelete(EntityEntry ownerEntry)
	{
		foreach (var navigationEntry in ownerEntry.Navigations)
		{
			if (navigationEntry.Metadata is not INavigation {IsOnDependent: false})
			{
				continue;
			}

			switch (navigationEntry)
			{
				case CollectionEntry {CurrentValue: { } items}:
					foreach (var item in items)
					{
						CascadeDeleteDependent(ownerEntry.Context, item);
					}

					break;

				case ReferenceEntry {CurrentValue: { } item}:
					CascadeDeleteDependent(ownerEntry.Context, item);
					break;
			}
		}
	}

	private static void CascadeDeleteDependent(DbContext dbContext, object entity)
	{
		var dependentEntry = dbContext.Entry(entity);

		//Solo se tocan las entradas borradas en cascada: las Added todavia pueden tener claves temporales.
		if (dependentEntry.State != EntityState.Deleted)
		{
			return;
		}

		// La fila duenia sobrevive al soft delete, asi que borrar fisicamente sus dependientes perderia filas que
		// des-borrar al duenio nunca podria recuperar. Los dependientes que soportan soft delete se borran suave con el;
		// al resto simplemente se los deja donde estan.
		var deleted = FindAutoProperty(dependentEntry, DELETED);

		if (deleted is null)
		{
			dependentEntry.State = EntityState.Unchanged;
		}
		else
		{
			deleted.CurrentValue = true;
			dependentEntry.State = EntityState.Modified;
		}

		CascadeDelete(dependentEntry);
	}

	private static bool UpdateETag(EntityEntry updatedEntry)
	{
		if (updatedEntry.Entity is not IETag)
		{
			return false;
		}

		updatedEntry.Property(nameof(IETag.ETag)).CurrentValue = ETag.New();
		return true;
	}
}
