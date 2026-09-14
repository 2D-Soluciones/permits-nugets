using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DDS.Repository.EntityFramework;

/// <summary>
///     Metodos de extension para <see cref="ModelBuilder" />
/// </summary>
[PublicAPI]
public static class ModelBuilderExtensions
{
	/// <summary>
	///     Fuerza la conversion desde/hacia la base de <see cref="DateTime" /> a <see cref="DateTimeKind.Utc" />.
	/// </summary>
	/// <param name="configurationBuilder"></param>
	/// <returns></returns>
	/// <remarks>
	///     Esto no cambia el valor, solo le pone el kind en Utc: reetiqueta, no convierte, y ese es el
	///     punto: PostgreSQL mapea <see cref="DateTime" /> a <c>timestamptz</c> y Npgsql se niega a escribir cualquier cosa
	///     cuyo <see cref="DateTime.Kind" /> no sea <see cref="DateTimeKind.Utc" />, con lo cual un valor que llegue como
	///     <see cref="DateTimeKind.Unspecified" /> tiraria excepcion en vez de guardarse.
	///     <para>
	///         Toda columna <see cref="DateTime" /> pasa por aca, incluidas las que escriben
	///         <c>UseAutoCreatedLocal()</c> y <c>UseAutoUpdatedLocal()</c>: esas guardan una lectura de reloj de pared cuyo
	///         offset nunca se registro, asi que vuelven marcadas como <see cref="DateTimeKind.Utc" /> igual que todo lo demas.
	///         Leelas tal cual: un <see cref="DateTime.ToLocalTime" /> sobre una de esas corre un reloj que nunca estuvo en UTC.
	///     </para>
	/// </remarks>
	public static ModelConfigurationBuilder ForceUtcDates(this ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Properties<DateTime>().HaveConversion<UtcValueConverter>();
		return configurationBuilder;
	}

	/// <summary>
	///     Configura el borrado de la entidad para que no haya borrado en cascada.
	/// </summary>
	/// <param name="modelBuilder"></param>
	/// <returns></returns>
	public static ModelBuilder RestrictDeleteBehavior(this ModelBuilder modelBuilder)
	{
		var cascadeFKs = modelBuilder.Model.GetEntityTypes()
			.SelectMany(t => t.GetForeignKeys())
			.Where(fk => fk is {IsOwnership: false, DeleteBehavior: DeleteBehavior.Cascade});

		foreach (var fk in cascadeFKs)
		{
			fk.DeleteBehavior = DeleteBehavior.Restrict;
		}

		return modelBuilder;
	}

	/// <param name="entityTypeBuilder"></param>
	/// <typeparam name="T"></typeparam>
	extension<[DynamicallyAccessedMembers(DynamicallyAccessedValue.DYNAMICALLY_ACCESSED_MEMBER_TYPES)] T>(EntityTypeBuilder<T> entityTypeBuilder) where T : class
	{
		/// <summary>
		///     Agrega una shadow property llamada "Created" que se actualiza sola cuando se agrega una entidad nueva al contexto.
		/// </summary>
		/// <param name="addIndex"></param>
		/// <returns></returns>
		public EntityTypeBuilder<T> UseAutoCreated(bool addIndex = false)
		{
			entityTypeBuilder.Property<DateTime>(SaveChangesInterceptor.CREATED).HasAnnotation(SaveChangesInterceptor.AUTO_PROPERTY, true);

			if (addIndex)
			{
				entityTypeBuilder.HasIndex(SaveChangesInterceptor.CREATED);
			}

			return entityTypeBuilder;
		}

		/// <summary>
		///     Agrega una shadow property llamada "CreatedLocal" que se actualiza sola con una fecha-hora local cuando se agrega una
		///     entidad nueva al contexto.
		/// </summary>
		/// <param name="addIndex"></param>
		/// <returns></returns>
		public EntityTypeBuilder<T> UseAutoCreatedLocal(bool addIndex = false)
		{
			entityTypeBuilder.Property<DateTime>(SaveChangesInterceptor.CREATED_LOCAL).HasAnnotation(SaveChangesInterceptor.AUTO_PROPERTY, true);

			if (addIndex)
			{
				entityTypeBuilder.HasIndex(SaveChangesInterceptor.CREATED_LOCAL);
			}

			return entityTypeBuilder;
		}

		/// <summary>
		///     Agrega una shadow property llamada "Updated" que se actualiza sola cuando se agrega o modifica una entidad en
		///     el contexto.
		/// </summary>
		/// <param name="addIndex"></param>
		/// <returns></returns>
		public EntityTypeBuilder<T> UseAutoUpdated(bool addIndex = false)
		{
			entityTypeBuilder.Property<DateTime>(SaveChangesInterceptor.UPDATED).HasAnnotation(SaveChangesInterceptor.AUTO_PROPERTY, true);

			if (addIndex)
			{
				entityTypeBuilder.HasIndex(SaveChangesInterceptor.UPDATED);
			}

			return entityTypeBuilder;
		}

		/// <summary>
		///     Agrega una shadow property llamada "UpdatedLocal" que se actualiza sola con una fecha-hora local cuando una entidad nueva
		///     se agrega o modifica en
		///     el contexto.
		/// </summary>
		/// <param name="addIndex"></param>
		/// <returns></returns>
		public EntityTypeBuilder<T> UseAutoUpdatedLocal(bool addIndex = false)
		{
			entityTypeBuilder.Property<DateTime>(SaveChangesInterceptor.UPDATED_LOCAL).HasAnnotation(SaveChangesInterceptor.AUTO_PROPERTY, true);

			if (addIndex)
			{
				entityTypeBuilder.HasIndex(SaveChangesInterceptor.UPDATED_LOCAL);
			}

			return entityTypeBuilder;
		}

		/// <summary>
		///     Agrega una shadow property llamada "Deleted" que se actualiza sola cuando se borra una entidad del contexto.
		/// </summary>
		/// <returns></returns>
		public EntityTypeBuilder<T> UseSoftDelete()
		{
			entityTypeBuilder.Property<bool>(SaveChangesInterceptor.DELETED).HasAnnotation(SaveChangesInterceptor.AUTO_PROPERTY, true);
			entityTypeBuilder.HasIndex(SaveChangesInterceptor.DELETED);
			entityTypeBuilder.HasQueryFilter(SaveChangesInterceptor.SOFT_DELETE_FILTER, instance => !EF.Property<bool>(instance, SaveChangesInterceptor.DELETED));
			return entityTypeBuilder;
		}
	}

	/// <summary>
	///
	/// </summary>
	/// <param name="entities"></param>
	extension(Type[] entities)
	{
		internal IEnumerable<EntityTypeBuilder> FilterByInterface<TInterface>(ModelBuilder modelBuilder)
		{
#pragma warning disable IL2067
			return from t in entities where typeof(TInterface).IsAssignableFrom(t) select modelBuilder.Entity(t);
#pragma warning restore IL2067
		}
	}

	/// <summary>
	///     Los tipos de entidad no-owned del modelo, raices y derivados.
	/// </summary>
	/// <remarks>
	///     Filtrar por BaseType == null dejaba afuera cualquier derivado TPH cuya raiz no implementara la interfaz
	///     buscada, y de paso metia los owned types -que tambien tienen BaseType null- donde un modelBuilder.Entity()
	///     los hubiera promovido a entidades normales.
	/// </remarks>
	internal static Type[] GetEntities(this ModelBuilder modelBuilder)
	{
		return modelBuilder.Model
			.GetEntityTypes()
			.Where(t => !t.IsOwned())
			.Select(t => t.ClrType)
			.Distinct()
			.ToArray();
	}

	private sealed class UtcValueConverter : ValueConverter<DateTime, DateTime>
	{
		public UtcValueConverter()
			: base(v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc), v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
	}
}
