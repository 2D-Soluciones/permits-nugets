using DDS.Domain;
using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     Generic repository definition
/// </summary>
/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
/// <typeparam name="TKey">El tipo de la clave de la entidad.</typeparam>
[PublicAPI, NoReorder]
public interface IRepository<TEntity, TKey> : IRepositoryDefinition where TEntity : Entity<TKey> where TKey : struct
{
	/// <summary>
	///     Carga una entidad del repositorio.
	/// </summary>
	/// <param name="id"></param>
	/// <param name="token"></param>
	/// <returns>La entidad cargada, o <c>null</c> si no se encuentra.</returns>
	ValueTask<TEntity?> Get(TKey id, CancellationToken token);

	/// <summary>
	///     Carga varias entidades del repositorio.
	/// </summary>
	/// <param name="ids"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	ValueTask<LoadManyResult<TEntity, TKey>> Get(IReadOnlyCollection<TKey> ids, CancellationToken token);

	/// <summary>
	///     Guarda una entidad en el repositorio.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	ValueTask Add(TEntity value, CancellationToken token);

	/// <summary>
	///     Borra del repositorio una entidad por su <paramref name="id" />.
	/// </summary>
	/// <param name="id"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	ValueTask Remove(TKey id, CancellationToken token);

	/// <summary>
	///     Borra una <paramref name="entity"/> del repositorio.
	/// </summary>
	/// <param name="entity"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	ValueTask Remove(TEntity entity, CancellationToken token);

	/// <summary>
	///     Busca entidades segun la <paramref name="specification" /> indicada.
	/// </summary>
	/// <param name="specification"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	Task<IReadOnlyCollection<TEntity>> Find(ICollectionSpecification<TEntity> specification, CancellationToken token);

	/// <summary>
	///     Busca entidades segun la <paramref name="specification" /> indicada.
	/// </summary>
	/// <param name="specification"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	Task<IReadOnlyCollection<TOut>> Find<TOut>(ICollectionSpecification<TEntity, TOut> specification, CancellationToken token);

	/// <summary>
	///     Busca una entidad segun la <paramref name="specification" /> indicada.
	/// </summary>
	/// <param name="specification"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	Task<TEntity?> Find(ISingleSpecification<TEntity> specification, CancellationToken token);

	/// <summary>
	///     Busca una entidad segun la <paramref name="specification" /> indicada.
	/// </summary>
	/// <param name="specification"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	Task<TOut?> Find<TOut>(ISingleSpecification<TEntity, TOut> specification, CancellationToken token);
}
