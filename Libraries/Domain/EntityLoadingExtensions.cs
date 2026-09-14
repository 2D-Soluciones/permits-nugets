using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace DDS.Domain;

/// <summary>
///     Metodo de extension para usar con Entity Framework y cargar entidades y/o colecciones en forma diferida.
/// </summary>
[PublicAPI]
public static class EntityLoadingExtensions
{
	/// <param name="loader">Una referencia al loader de Entity Framework.</param>
	extension(Action<object, string>? loader)
	{
		/// <summary>
		///     Fuerza la carga de una coleccion desde la base, salteando el fix-up del change tracker de EF. Asi la
		///     coleccion queda con todos los items de la base y no solo con las entidades que ya estaban trackeadas.
		/// </summary>
		/// <remarks>Arreglo temporal para https://github.com/dotnet/efcore/issues/37123</remarks>
		public List<TItem> ForceLoadCollection<TItem>(object entity, ref List<TItem>? navigationField, [CallerMemberName] string navigationName = null!) where TItem : class
		{
			// El contexto dueño sale de la propia entidad - ver EntityContextInterceptor para entender por que aca no
			// se puede confiar en un scope ambiente.
			var dbContext = EntityContextInterceptor.FindContext(entity);

			if (dbContext != null)
			{
				var entry = dbContext.Entry(entity);

				// Solo se fuerza la carga si la entidad esta trackeada
				if (entry.State != EntityState.Detached)
				{
					var collectionEntry = entry.Collection(navigationName);

					// Si ya estaba cargada - por un Include, o por una llamada anterior a esto mismo - se deja como esta.
					if (collectionEntry.IsLoaded)
					{
						return navigationField ??= [];
					}

					// Voy directo a la base, salteando el fix-up del change tracker
					var loaded = collectionEntry.Query().Cast<TItem>().ToList();

					// Merge en vez de reemplazo: los hijos agregados en memoria todavia no estan en el resultado de la
					// consulta, y tirarlos dejaria al llamador mirando una coleccion que no coincide con las filas que
					// EF esta por insertar.
					if (navigationField is not null)
					{
						foreach (var pending in navigationField)
						{
							if (!loaded.Contains(pending))
							{
								loaded.Add(pending);
							}
						}
					}

					navigationField = loaded;
					collectionEntry.IsLoaded = true;

					// Se asigna, no se devuelve nomas, para que lo que se agregue al resultado caiga en la entidad.
					return navigationField;
				}
			}

			// Para entidades desconectadas (por ejemplo, AsNoTracking): si el campo ya viene poblado (por un Include),
			// se devuelve directo - aca no aplica el bug de fix-up del change tracker, asi que el dato es confiable.
			if (navigationField != null)
			{
				return navigationField;
			}

			// Ultimo recurso: la carga diferida de siempre (o vacio si no hay loader)
			loader?.Invoke(entity, navigationName);
			return navigationField ??= [];
		}

		/// <summary>
		///     Carga una entidad/coleccion en el <paramref name="navigationField" /> que se le pasa y la devuelve.
		/// </summary>
		/// <param name="entity">La entidad raiz.</param>
		/// <param name="navigationField">El campo donde se va a guardar el resultado.</param>
		/// <param name="navigationName"></param>
		/// <typeparam name="TRelated"></typeparam>
		/// <returns></returns>
		[return: NotNullIfNotNull(nameof(navigationField))]
		public TRelated? Load<TRelated>(object entity, ref TRelated? navigationField, [CallerMemberName] string navigationName = null!)
			where TRelated : class
		{
			loader?.Invoke(entity, navigationName);
			return navigationField;
		}
	}

}
