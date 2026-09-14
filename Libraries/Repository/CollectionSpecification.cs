using JetBrains.Annotations;

namespace DDS.Repository;

/// <inheritdoc />
[PublicAPI]
public abstract class CollectionSpecification<T> : ICollectionSpecification<T> where T : class
{
	/// <inheritdoc />
	public abstract IQueryable<T> Apply(IQueryable<T> queryable);

	/// <summary>
	///     Filtra el resultado
	/// </summary>
	/// <param name="result"></param>
	/// <returns></returns>
	public virtual IReadOnlyCollection<T> Filter(T[] result)
	{
		return result;
	}
}

/// <inheritdoc />
[PublicAPI]
public abstract class SingleSpecification<T> : ISingleSpecification<T> where T : class
{
	/// <inheritdoc />
	public abstract IQueryable<T> Apply(IQueryable<T> queryable);

	/// <summary>
	///     Filtra el resultado
	/// </summary>
	/// <param name="result"></param>
	/// <returns></returns>
	public virtual T? Filter(T[] result)
	{
		return result.FirstOrDefault();
	}
}
