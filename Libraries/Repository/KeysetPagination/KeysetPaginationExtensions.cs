using System.Linq.Expressions;
using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     Conjunto de metodos de extension para usar paginacion por keyset.
/// </summary>
[PublicAPI]
public static class KeysetPaginationExtensions
{
	/// <summary>
	///     Pagina usando paginacion por keyset.
	/// </summary>
	/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
	/// <param name="keysetQueryDefinition">La definicion de keyset ya armada.</param>
	/// <param name="direction">La direccion a tomar. Por defecto, Forward.</param>
	/// <param name="source">El <see cref="IQueryable{T}" /> a paginar.</param>
	/// <returns>El queryable modificado.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="source" /> es null.
	///     <paramref name="keysetQueryDefinition" /> es null.
	/// </exception>
	/// <exception cref="InvalidOperationException">Si no se registro ninguna propiedad en el builder.</exception>
	/// <remarks>
	///     Ojo: llamar a este metodo pisa cualquier OrderBy que hayas hecho antes.
	/// </remarks>
	public static IQueryable<TEntity> KeysetPaginateQuery<TEntity>(this IQueryable<TEntity> source, KeysetQueryDefinition<TEntity> keysetQueryDefinition, KeysetPaginationDirection direction = KeysetPaginationDirection.Forward)
	{
		return KeysetPaginate(source, keysetQueryDefinition.Columns, direction, null);
	}

	/// <summary>
	///     Pagina usando paginacion por keyset.
	/// </summary>
	/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
	/// <param name="keysetQueryDefinition">La definicion de keyset ya armada.</param>
	/// <param name="direction">La direccion a tomar. Por defecto, Forward.</param>
	/// <param name="reference">
	///     El objeto de referencia. Tiene que tener propiedades con los mismos nombres exactos que las
	///     propiedades configuradas. No hace falta que sea del mismo tipo que T.
	/// </param>
	/// <param name="source">El <see cref="IQueryable{T}" /> a paginar.</param>
	/// <returns>El queryable modificado.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="source" /> es null.
	///     <paramref name="keysetQueryDefinition" /> es null.
	/// </exception>
	/// <exception cref="InvalidOperationException">Si no se registro ninguna propiedad en el builder.</exception>
	/// <remarks>
	///     Ojo: llamar a este metodo pisa cualquier OrderBy que hayas hecho antes.
	/// </remarks>
	public static IQueryable<TEntity> KeysetPaginateQuery<TEntity>(this IQueryable<TEntity> source, KeysetQueryDefinition<TEntity> keysetQueryDefinition, object? reference, KeysetPaginationDirection direction = KeysetPaginationDirection.Forward)
	{
		return KeysetPaginate(source, keysetQueryDefinition.Columns, direction, reference);
	}

	/// <summary>
	///     Pagina usando paginacion por keyset.
	/// </summary>
	/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
	/// <param name="builderAction">
	///     Una accion que recibe un builder y registra las columnas sobre las que va a trabajar la paginacion por
	///     work.
	/// </param>
	/// <param name="direction">La direccion a tomar. Por defecto, Forward.</param>
	/// <param name="source">El <see cref="IQueryable{T}" /> a paginar.</param>
	/// <returns>El queryable modificado.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="source" /> es null.
	///     <paramref name="builderAction" /> es null.
	/// </exception>
	/// <exception cref="InvalidOperationException">Si no se registro ninguna propiedad en el builder.</exception>
	/// <remarks>
	///     Ojo: llamar a este metodo pisa cualquier OrderBy que hayas hecho antes.
	/// </remarks>
	public static IQueryable<TEntity> KeysetPaginateQuery<TEntity>(this IQueryable<TEntity> source, Action<KeysetPaginationBuilder<TEntity>> builderAction, KeysetPaginationDirection direction = KeysetPaginationDirection.Forward)
	{
		return KeysetPaginate(source, KeysetQueryBuilder.BuildColumns(builderAction), direction, null);
	}

	/// <summary>
	///     Pagina usando paginacion por keyset.
	/// </summary>
	/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
	/// <param name="builderAction">
	///     Una accion que recibe un builder y registra las columnas sobre las que va a trabajar la paginacion por
	///     work.
	/// </param>
	/// <param name="direction">La direccion a tomar. Por defecto, Forward.</param>
	/// <param name="reference">
	///     El objeto de referencia. Tiene que tener propiedades con los mismos nombres exactos que las
	///     propiedades configuradas. No hace falta que sea del mismo tipo que T.
	/// </param>
	/// <param name="source">El <see cref="IQueryable{T}" /> a paginar.</param>
	/// <returns>El queryable modificado.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="source" /> es null.
	///     <paramref name="builderAction" /> es null.
	/// </exception>
	/// <exception cref="InvalidOperationException">Si no se registro ninguna propiedad en el builder.</exception>
	/// <remarks>
	///     Ojo: llamar a este metodo pisa cualquier OrderBy que hayas hecho antes.
	/// </remarks>
	public static IQueryable<TEntity> KeysetPaginateQuery<TEntity>(this IQueryable<TEntity> source, Action<KeysetPaginationBuilder<TEntity>> builderAction, object? reference, KeysetPaginationDirection direction = KeysetPaginationDirection.Forward)
	{
		return KeysetPaginate(source, KeysetQueryBuilder.BuildColumns(builderAction), direction, reference);
	}

	private static Expression<Func<TEntity, bool>> BuildKeysetFilterPredicateExpression<TEntity>(IReadOnlyList<KeysetColumn<TEntity>> columns, KeysetPaginationDirection direction, object? reference)
	{
		return KeysetFilterPredicateStrategy.BuildKeysetFilterPredicateExpression(columns, direction, reference);
	}

	private static IQueryable<TEntity> KeysetPaginate<TEntity>(IQueryable<TEntity> source, IReadOnlyList<KeysetColumn<TEntity>> columns, KeysetPaginationDirection direction, object? reference)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (!columns.Any())
		{
			throw new InvalidOperationException("There should be at least one configured column in the keyset.");
		}

		// Order
		var orderedQuery = columns[0].ApplyOrderBy(source, direction);
		for (var i = 1; i < columns.Count; i++)
		{
			orderedQuery = columns[i].ApplyThenOrderBy(orderedQuery, direction);
		}


		// Filter
		var filteredQuery = orderedQuery.AsQueryable();
		if (reference is null)
		{
			return orderedQuery;
		}

		var keysetFilterPredicateLambda = BuildKeysetFilterPredicateExpression(columns, direction, reference);
		return filteredQuery.Where(keysetFilterPredicateLambda);
	}
}
