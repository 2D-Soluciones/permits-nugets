using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     Contrato del patron specification.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public interface ICollectionSpecification<T> where T : class
{
	/// <summary>
	///     Aplica esta specification a un <see cref="IQueryable{T}" />
	/// </summary>
	/// <param name="queryable">El <see cref="IQueryable{T}" /> de origen.</param>
	/// <returns>Una instancia nueva de <see cref="IQueryable{T}" /> con esta specification aplicada.</returns>
	IQueryable<T> Apply(IQueryable<T> queryable);
}

/// <summary>
///     Contrato del patron specification.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TOut"></typeparam>
[PublicAPI]
public interface ICollectionSpecification<in T, TOut> where T : class
{
	/// <summary>
	///     Aplica esta specification a un <see cref="IQueryable{T}" />
	/// </summary>
	/// <param name="queryable">El <see cref="IQueryable{T}" /> de origen.</param>
	/// <param name="cancellationToken"></param>
	/// <returns>Una instancia nueva de <see cref="IQueryable{TOut}" /> con esta specification aplicada.</returns>
	Task<IReadOnlyCollection<TOut>> Apply(IQueryable<T> queryable, CancellationToken cancellationToken);
}

/// <summary>
///     Contrato del patron specification.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public interface ISingleSpecification<T> where T : class
{
	/// <summary>
	///     Aplica esta specification a un <see cref="IQueryable{T}" />
	/// </summary>
	/// <param name="queryable">El <see cref="IQueryable{T}" /> de origen.</param>
	/// <returns>Una instancia nueva de <see cref="IQueryable{T}" /> con esta specification aplicada.</returns>
	IQueryable<T> Apply(IQueryable<T> queryable);
}

/// <summary>
///     Contrato del patron specification.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TOut"></typeparam>
[PublicAPI]
public interface ISingleSpecification<in T, TOut> where T : class
{
	/// <summary>
	///     Aplica esta specification a un <see cref="IQueryable{T}" />
	/// </summary>
	/// <param name="queryable">El <see cref="IQueryable{T}" /> de origen.</param>
	/// <param name="cancellationToken"></param>
	/// <returns>Una instancia nueva de <see cref="IQueryable{TOut}" /> con esta specification aplicada.</returns>
	Task<TOut?> Apply(IQueryable<T> queryable, CancellationToken cancellationToken);
}
