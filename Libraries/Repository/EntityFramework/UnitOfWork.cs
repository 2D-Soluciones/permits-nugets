using System.Runtime.CompilerServices;
using DDS.Domain;
using JetBrains.Annotations;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DDS.Repository.EntityFramework;

/// <summary>
///     Implementacion por defecto de <see cref="IUnitOfWork" /> para Entity Framework.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public sealed class UnitOfWork<T> : IUnitOfWork where T : EfDbContext<T>
{
	private readonly T _dbContext;
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	///     Inicializa una instancia nueva de <see cref="UnitOfWork{T}" />.
	/// </summary>
	/// <param name="serviceProvider">El service provider que se usa para obtener los repositorios.</param>
	/// <param name="dbContext"></param>
	public UnitOfWork(IServiceProvider serviceProvider, T dbContext)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(dbContext);
		_serviceProvider = serviceProvider;
		_dbContext = dbContext;
		// dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
	}

	/// <inheritdoc />
	/// <remarks>
	///     Los eventos de dominio se publican recien después de commitear la escritura a la que pertenecen, asi que un handler puede
	///     recargar sin riesgo las entidades de las que le avisaron. Un save que tira excepcion deja los eventos encolados en sus
	///     entidades, asi que un reintento igual los publica.
	///     <para>
	///         El <see cref="IPublisher" /> es opcional: sin publisher registrado esto es un save comun y los eventos
	///         encolados nunca se drenan.
	///     </para>
	///     <para>
	///         Un handler puede dejar mas cambios pendientes, asi que esto puede terminar haciendo varios saves. Aca no se abre
	///         ninguna transaccion: cada save commitea por su cuenta. El que necesite todo-o-nada abre la suya -a traves de
	///         <c>Database.CreateExecutionStrategy()</c>, porque si no la execution strategy de EF tira excepcion- antes de
	///         llamar a esto. Una transaccion ya abierta es del llamador y aca no se toca.
	///     </para>
	/// </remarks>
	public async Task<int> CommitChanges(CancellationToken cancellationToken)
	{
		var eventPublisher = _serviceProvider.GetService<IPublisher>();

		if (eventPublisher is null)
		{
			return await _dbContext.SaveChangesAsync(cancellationToken);
		}

		const int MAX_ITERATIONS = 100; // Prevent infinite loops from cascading domain events
		var total = 0;
		var iterations = 0;

		while (true)
		{
			if (++iterations > MAX_ITERATIONS)
			{
				throw new InvalidOperationException(
					$"Domain event processing exceeded maximum iteration limit ({MAX_ITERATIONS}). " +
					"This may indicate a circular dependency in your domain events.");
			}

			// Las entidades se juntan antes del save porque una borrada deja de estar trackeada apenas el
			// borrado sale bien; los eventos se drenan después, asi que un save que tira excepcion no pierde nada.
			var sources = _dbContext.CollectEventSources();
			total += await _dbContext.SaveChangesAsync(cancellationToken);
			var notifications = sources?.DrainDomainEvents();

			if (notifications is null)
			{
				return total;
			}

			foreach (var notification in notifications)
			{
				await eventPublisher.Publish(notification, cancellationToken);
			}
		}
	}

	/// <inheritdoc />
	/// <remarks>
	///     Vacio a proposito. El <see cref="DbContext" /> es del contenedor, que le da la misma instancia scopeada
	///     a todos los repositorios del request y lo disposea al final del scope. Disposearlo aca convertiria
	///     el <c>using var uow = provider.GetRequiredService&lt;IUnitOfWork&gt;()</c> de siempre en un
	///     <see cref="ObjectDisposedException" /> para todo lo demas de ese scope.
	/// </remarks>
	public void Dispose() { }

	/// <inheritdoc />
	public TRepository Repository<TRepository>() where TRepository : class, IRepositoryDefinition
	{
		return _serviceProvider.GetRequiredService<TRepository>();
	}
}

file static class DomainEventHandler
{
	public static List<Entity>? CollectEventSources(this DbContext dbContext)
	{
		List<Entity>? sources = null;

		foreach (var entry in dbContext.ChangeTracker.Entries().Where(e => e.State is not EntityState.Detached))
		{
			if (entry.Entity is not Entity entity || GetEntityNotifications(entity) is not { Count: > 0 })
			{
				continue;
			}

			sources ??= new(1);
			sources.Add(entity);
		}

		return sources;
	}

	public static List<IDomainEvent>? DrainDomainEvents(this List<Entity> sources)
	{
		List<IDomainEvent>? allEvents = null;

		foreach (var entity in sources)
		{
			var events = GetEntityNotifications(entity);

			if (events is null || events.Count == 0)
			{
				continue;
			}

			allEvents ??= new(events.Count);
			allEvents.AddRange(events);
			events.Clear();
		}

		return allEvents;
	}

	[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_queuedNotifications")]
	private static extern ref List<IDomainEvent>? GetEntityNotifications(Entity @this);
}
