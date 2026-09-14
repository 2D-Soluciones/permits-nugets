using System.Linq.Expressions;
using DDS.Domain;
using DDS.Repository.EntityFramework;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace DDS.Repository;

/// <summary>
/// Metodos de extension para paginar por keyset (con cursor) sobre <see cref="IQueryable{T}"/>.
/// </summary>
[PublicAPI]
public static class KeysetQueryExtensions
{
	/// <summary>
	/// Ejecuta una consulta paginada por keyset y transforma el resultado en un <see cref="KeysetQueryResult{TEntity}"/>.
	/// </summary>
	/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
	/// <typeparam name="TKey">El tipo de la clave unica que identifica la entidad.</typeparam>
	/// <param name="queryable">El <see cref="DbSet{T}"/> de origen a paginar.</param>
	/// <param name="keysetQueryDefinition">La definicion de las columnas del keyset y el orden.</param>
	/// <param name="query">Los parametros de paginacion (tamanio de pagina, cursores, etc.).</param>
	/// <param name="cancellationToken">El token a observar mientras se espera que termine la operacion.</param>
	/// <param name="includeTotalCount">Si hay que incluir el total. Ponelo en false para no pagarlo en tablas grandes. Por defecto true, por compatibilidad.</param>
	/// <returns>Una task que representa la operacion asincronica. El resultado trae la pagina de datos y sus metadatos.</returns>
	public static Task<KeysetQueryResult<TEntity>> ToKeysetQueryResult<TEntity, TKey>(
		this IQueryable<TEntity> queryable,
		KeysetQueryDefinition<TEntity> keysetQueryDefinition,
		KeysetQuery<TKey> query,
		CancellationToken cancellationToken,
		bool includeTotalCount = false) where TEntity : class where TKey : struct
	{
		return queryable.ToKeysetQueryResult(keysetQueryDefinition, query, (key, token) => queryable.FirstOrDefaultAsync(EqualityExpressionForId<TKey, TEntity>.Create(key), token), cancellationToken, includeTotalCount);
	}

	/// <summary>
	/// Ejecuta una consulta paginada por keyset y transforma el resultado en un <see cref="KeysetQueryResult{TEntity}"/>.
	/// </summary>
	/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
	/// <typeparam name="TKey">El tipo de la clave unica que identifica la entidad.</typeparam>
	/// <param name="queryable">El <see cref="IQueryable{T}"/> de origen a paginar.</param>
	/// <param name="keysetQueryDefinition">La definicion de las columnas del keyset y el orden.</param>
	/// <param name="query">Los parametros de paginacion (tamanio de pagina, cursores, etc.).</param>
	/// <param name="getReferenceAsync">Una funcion para traer la entidad de referencia por su clave. Se usa cuando vienen los cursores 'Before' o 'After'.</param>
	/// <param name="cancellationToken">El token a observar mientras se espera que termine la operacion.</param>
	/// <param name="includeTotalCount">Si hay que incluir el total. Ponelo en false para no pagarlo en tablas grandes. Por defecto true, por compatibilidad.</param>
	/// <returns>Una task que representa la operacion asincronica. El resultado trae la pagina de datos y sus metadatos.</returns>
	public static async Task<KeysetQueryResult<TEntity>> ToKeysetQueryResult<TEntity, TKey>(
		this IQueryable<TEntity> queryable,
		KeysetQueryDefinition<TEntity> keysetQueryDefinition,
		KeysetQuery<TKey> query,
		Func<TKey, CancellationToken, Task<TEntity?>> getReferenceAsync,
		CancellationToken cancellationToken,
		bool includeTotalCount = false)
		where TEntity : class where TKey : struct
	{
		const int MAX_PAGE_SIZE = 10_000;

		//el Size llega de la query string, asi que un ?size=0 o ?size=999999 es entrada del usuario, no un bug del
		//llamador: se clampea en vez de tirar, que terminaba en un 500. El limite superior importa igual que el
		//inferior: Take(pageSize + 1) con int.MaxValue desborda a int.MinValue, que en SQLite es LIMIT -1 (o sea,
		//todo) y en SQL Server un error.
		var pageSize = Math.Clamp(query.Size ?? 25, 1, MAX_PAGE_SIZE);

		keysetQueryDefinition = WithKeyTiebreak<TEntity, TKey>(keysetQueryDefinition);

		// El total se calcula solo si lo piden: en tablas grandes sale caro
		var totalCount = includeTotalCount ? await queryable.CountAsync(cancellationToken) : (int?)null;
		var reverse = false;
		var fallbackToFirstPage = false;

		if (query.Last)
		{
			reverse = true;
			queryable = queryable.KeysetPaginateQuery(keysetQueryDefinition, KeysetPaginationDirection.Backward);
		}
		else if (query.After is not null)
		{
			var reference = await getReferenceAsync(query.After.Value, cancellationToken);
			fallbackToFirstPage = reference is null;
			queryable = queryable.KeysetPaginateQuery(keysetQueryDefinition, reference);
		}
		else if (query.Before is not null)
		{
			var reference = await getReferenceAsync(query.Before.Value, cancellationToken);

			// Este es un caso especial.
			// Si la referencia es null (capaz la entidad se borro de la base), queremos
			// devolver siempre la primera pagina, asi que fuerzo la direccion Forward.
			var direction = reference != null ? KeysetPaginationDirection.Backward : KeysetPaginationDirection.Forward;
			reverse = reference != null;
			fallbackToFirstPage = reference is null;
			queryable = queryable.KeysetPaginateQuery(keysetQueryDefinition, reference, direction);
		}
		else
		{
			queryable = queryable.KeysetPaginateQuery(keysetQueryDefinition);
		}

		var data = await queryable
			.Take(pageSize + 1)
			.ToListAsync(cancellationToken);

		var hasMore = data.Count > pageSize;

		if (hasMore)
		{
			data.RemoveAt(data.Count - 1); // Remove the extra item
		}

		if (reverse)
		{
			data.Reverse();
		}

		bool hasPrevious, hasNext;

		if (query.Last)
		{
			hasPrevious = hasMore;
			hasNext = false;
		}
		else if (query.After is not null)
		{
			hasPrevious = !fallbackToFirstPage;
			hasNext = hasMore;
		}
		else if (query.Before != null)
		{
			if (reverse) // reference exists
			{
				hasPrevious = hasMore;
				hasNext = true;
			}
			else // reference was null (deleted), first page fallback
			{
				hasPrevious = false;
				hasNext = hasMore;
			}
		}
		else
		{
			hasPrevious = false;
			hasNext = hasMore;
		}

		return new()
		{
			Data = data,
			PageSize = pageSize,
			TotalCount = totalCount,
			State = GetKeysetQueryState(hasPrevious, hasNext)
		};
	}

	/// <summary>
	///     Le agrega la clave de la entidad al keyset, salvo que ya sea la ultima columna.
	/// </summary>
	/// <remarks>
	///     El cursor de esta API <em>es</em> la clave de la entidad, pero las columnas configuradas no tienen por que incluirla. Sin una
	///     ultima columna unica, la comparacion final es estricta sobre un valor que se repite, asi que toda
	///     fila empatada con la del cursor se saltea: pagina por <c>Created</c> solo y una importacion masiva que comparte timestamp queda inalcanzable.
	/// </remarks>
	private static KeysetQueryDefinition<TEntity> WithKeyTiebreak<TEntity, TKey>(KeysetQueryDefinition<TEntity> keysetQueryDefinition)
		where TEntity : class where TKey : struct
	{
		var columns = keysetQueryDefinition.Columns;

		if (columns.Count == 0 || IsEntityKey<TEntity, TKey>(columns[^1]) || KeyColumn<TEntity, TKey>.Ascending is null)
		{
			return keysetQueryDefinition;
		}

		var withKey = new List<KeysetColumn<TEntity>>(columns.Count + 1);
		withKey.AddRange(columns);
		withKey.Add(columns[^1].IsDescending ? KeyColumn<TEntity, TKey>.Descending! : KeyColumn<TEntity, TKey>.Ascending);
		return new(withKey);
	}

	/// <summary>
	///     Si la columna ya <em>es</em> la clave de la entidad, con lo cual volver a agregarla seria redundante.
	/// </summary>
	/// <remarks>
	///     El nombre del miembro solo no alcanza: <c>x =&gt; x.Owner.Id</c> tambien se llama <c>Id</c> y tiene el tipo
	///     de la clave, pero no es unico por fila, asi que darlo por desempate dejaba justo el bug que este metodo
	///     existe para evitar - las filas empatadas en ese valor se saltean en cada borde de pagina.
	/// </remarks>
	private static bool IsEntityKey<TEntity, TKey>(KeysetColumn<TEntity> column) where TKey : struct
	{
		return column.IsRootMember && column.Type == typeof(TKey) && column.MemberName == nameof(Entity<TKey>.Id);
	}

	/// <summary>
	///     La clave de la entidad como columna de keyset, o <see langword="null" /> cuando <typeparamref name="TEntity" /> no tiene
	///     <c>Id</c> de ese tipo. Se cachea porque cada instancia de columna memoiza su accessor compilado.
	/// </summary>
	private static class KeyColumn<TEntity, TKey> where TKey : struct
	{
		public static readonly KeysetColumn<TEntity>? Ascending = Create(false);
		public static readonly KeysetColumn<TEntity>? Descending = Create(true);

		private static KeysetColumn<TEntity>? Create(bool isDescending)
		{
			if (typeof(TEntity).GetProperty(nameof(Entity<TKey>.Id))?.PropertyType != typeof(TKey))
			{
				return null;
			}

			var parameter = Expression.Parameter(typeof(TEntity), "x");
			var lambda = Expression.Lambda<Func<TEntity, TKey>>(Expression.Property(parameter, nameof(Entity<TKey>.Id)), parameter);
			return new KeysetColumn<TEntity, TKey>(isDescending, lambda);
		}
	}

	/// <summary>
	///     Convierte los dos hechos que la consulta realmente establecio en los flags de estado.
	/// </summary>
	/// <remarks>
	///     IsFirst e IsLast salen de lo que se encontro, no de lo que pidio el llamador. Sacarlos del
	///     pedido hacia que IsLast se prendiera solo cuando alguien pedia explicitamente la ultima pagina, asi que un cliente paginando hacia
	///     adelante nunca se enteraba de que habia llegado, y una pagina Before vacia volvia marcada como "no es la primera, hay siguiente, no hay
	///     anterior", sin nada que le diga al cliente que vuelva al principio.
	/// </remarks>
	private static KeysetQueryState GetKeysetQueryState(bool hasPrevious, bool hasNext)
	{
		var state = KeysetQueryState.Undefined;

		if (hasPrevious)
		{
			state |= KeysetQueryState.HasPrevious;
		}
		else
		{
			state |= KeysetQueryState.IsFirst;
		}

		if (hasNext)
		{
			state |= KeysetQueryState.HasNext;
		}
		else
		{
			state |= KeysetQueryState.IsLast;
		}

		return state;
	}
}
