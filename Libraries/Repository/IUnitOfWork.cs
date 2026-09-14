using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     Definicion del patron unit of work.
/// </summary>
[PublicAPI]
public interface IUnitOfWork : IDisposable
{
	/// <summary>
	///     Commitea los cambios a la persistencia subyacente.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns>La cantidad total de entidades modificadas.</returns>
	Task<int> CommitChanges(CancellationToken cancellationToken);

	// void Notify<T>(T domainEvent) where T: IDomainNotification;
	/// <summary>
	///     Trae un repositorio de este <see cref="IUnitOfWork" />.
	/// </summary>
	/// <typeparam name="TRepository">El tipo del repositorio.</typeparam>
	/// <returns></returns>
	TRepository Repository<TRepository>() where TRepository : class, IRepositoryDefinition;
}
