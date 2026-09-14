using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DDS.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DDS.Repository.EntityFramework;

/// <summary>
///     Implementacion base de <see cref="IRepository{TEntity,TKey}" /> para usar con Entity Framework.
/// </summary>
/// <typeparam name="TContext"></typeparam>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TKey"></typeparam>
[PublicAPI]
[NoReorder]
public abstract class EfRepository<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedValue.DYNAMICALLY_ACCESSED_MEMBER_TYPES)] TEntity, TKey> : IRepository<TEntity, TKey>
	where TContext : EfDbContext<TContext>
	where TEntity : Entity<TKey>
	where TKey : struct
{
	private readonly QuerySplittingBehavior _querySplittingBehavior;

	// Al cargar por clave se saltea solo el filtro de soft delete. El IgnoreQueryFilters() sin parametros
	// tambien tiraria cualquier filtro de tenancy o a nivel de fila que el consumidor haya puesto en esa entidad.
	// ReSharper disable once StaticMemberInGenericType
	private static readonly string[] _softDeleteFilter = [SaveChangesInterceptor.SOFT_DELETE_FILTER];
	private static readonly Lazy<Func<DbContext, IEnumerable<TKey>, IAsyncEnumerable<TEntity>>> _containsAsSingle = new(CompileContainsAsSingle);
	private static readonly Lazy<Func<DbContext, IEnumerable<TKey>, IAsyncEnumerable<TEntity>>> _containsAsSplit = new(CompileContainsAsSplit);

	//https://www.fearofoblivion.com/dont-let-ef-call-the-shots

	/// <summary>
	///     DbSet
	/// </summary>
	protected DbSet<TEntity> DbSet { get; }

	/// <summary>
	///     DbContext
	/// </summary>
	protected TContext DbContext { get; }

	/// <summary>
	///     Inicializa una instancia nueva de <see cref="EfRepository{TContext,TEntity,TKey}" />
	/// </summary>
	/// <param name="dbContext"></param>
	/// <param name="querySplittingBehavior"></param>
	protected EfRepository(TContext dbContext, QuerySplittingBehavior querySplittingBehavior = QuerySplittingBehavior.SplitQuery)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		DbContext = dbContext;
		DbSet = dbContext.Set<TEntity>();
		_querySplittingBehavior = querySplittingBehavior;
	}

	/// <inheritdoc />
	public virtual ValueTask<TEntity?> Get(TKey id, CancellationToken token)
	{
		if (KeyComparer<TKey>.IsDefault(id))
		{
			return ValueTask.FromResult<TEntity?>(null);
		}

		// Una entidad borrada antes en esta unidad de trabajo ya no existe para el llamador, incluso antes del
		// save. Ir a la base tampoco ayudaria: la resolucion de identidad devuelve esta
		// misma entrada, todavia en Deleted.
		return !IsEntityInContext(id, out var entityEntry)
			? GetSlow(id, token)
			: ValueTask.FromResult(entityEntry.State == EntityState.Deleted ? null : entityEntry.Entity);
	}

	/// <inheritdoc />
	public virtual async ValueTask<LoadManyResult<TEntity, TKey>> Get(IReadOnlyCollection<TKey> ids, CancellationToken token)
	{
		// var idArray = ids.ToList();
		var result = await LoadEntities(ids, token);

		if (result.Count == ids.Count)
		{
			return new(result, []);
		}

		if (result.Count == 0)
		{
			return new([], ids);
		}

		var loadedIds = new HashSet<TKey>(result.Count);
		foreach (var entity in result)
		{
			loadedIds.Add(entity.Id);
		}

		var missing = new List<TKey>(ids.Count - result.Count);
		foreach (var id in ids)
		{
			if (!loadedIds.Contains(id))
			{
				missing.Add(id);
			}
		}

		return new(result, missing);
	}

	/// <inheritdoc />
	public virtual ValueTask Add(TEntity value, CancellationToken token)
	{
		if (KeyComparer<TKey>.IsDefault(value.Id))
		{
			return Throw<ValueTask>($"The entity '{value.GetType().Name}' has no Id.");
		}

		if (!IsEntityInContext(value.Id, out var entityEntry))
		{
			DbSet.Add(value);
			return ValueTask.CompletedTask;
		}

		switch (entityEntry.State)
		{
			case EntityState.Detached:
				return Throw<ValueTask>($"The entity '{entityEntry.Entity.GetType().Name}' with Id:{entityEntry.Entity.Id} is in Detached state. This generally happen when you are trying to use an entity obtained from a query. This probably means you have an error in your business code.");

			case EntityState.Deleted:
				return Throw<ValueTask>($"The entity '{entityEntry.Entity.GetType().Name}' with Id:{entityEntry.Entity.Id} is in Deleted state and it is being modified on the same DbContext. This probably means you have an error in your business code.");

			// case EntityState.Unchanged:
			// case EntityState.Modified:
			// case EntityState.Added:
			// default:
			// entityEntry.DetectChanges();
			// return ValueTask.CompletedTask;
		}

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask Remove(TKey id, CancellationToken token)
	{
		if (KeyComparer<TKey>.IsDefault(id))
		{
			return ValueTask.CompletedTask;
		}

		return IsEntityInContext(id, out var entityEntry)
			? Remove(entityEntry.Entity, token)
			: DeleteAsyncSlow(id, token);
	}

	/// <inheritdoc />
	public virtual ValueTask Remove(TEntity entity, CancellationToken token)
	{
		DbSet.Remove(entity);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public virtual async Task<IReadOnlyCollection<TEntity>> Find(ICollectionSpecification<TEntity> specification, CancellationToken token)
	{
		var result = await specification.Apply(DbSet).ToArrayAsync(token);

		if (specification is CollectionSpecification<TEntity> collectionSpecification)
		{
			return collectionSpecification.Filter(result);
		}

		return result;
	}

	/// <inheritdoc />
	/// <remarks>
	///     El queryable va con tracking, como el resto del repositorio. Una proyeccion de verdad no materializa
	///     entidades y no llena el change tracker sola, asi que un AsNoTracking aca no ahorraba nada; lo unico que
	///     hacia era devolver entidades muertas cuando <typeparamref name="TOut" /> es la propia entidad. La
	///     specification que quiera leer sin trackear llama <c>AsNoTracking()</c> sobre el queryable que recibe.
	/// </remarks>
	public async Task<IReadOnlyCollection<TOut>> Find<TOut>(ICollectionSpecification<TEntity, TOut> specification, CancellationToken token)
	{
		if (specification is not EfSpecification efSpecification)
		{
			return await specification.Apply(DbSet, token);
		}

		// El contexto se presta sólo mientras dura la llamada: si queda pegado, la specification sobrevive al scope
		// con un DbContext ya disposeado.
		efSpecification.Enter(DbContext);

		try
		{
			return await specification.Apply(DbSet, token);
		}
		finally
		{
			efSpecification.Exit();
		}
	}

	/// <inheritdoc />
	public virtual async Task<TEntity?> Find(ISingleSpecification<TEntity> specification, CancellationToken token)
	{
		var result = specification.Apply(DbSet);

		if (specification is SingleSpecification<TEntity> singleSpecification)
		{
			return singleSpecification.Filter(await result.ToArrayAsync(token));
		}

		return await result.FirstOrDefaultAsync(token);
	}

	/// <inheritdoc />
	/// <remarks>
	///     Con tracking, igual que la sobrecarga de coleccion. Ver el comentario de esa.
	/// </remarks>
	public async Task<TOut?> Find<TOut>(ISingleSpecification<TEntity, TOut> specification, CancellationToken token)
	{
		if (specification is not EfSpecification efSpecification)
		{
			return await specification.Apply(DbSet, token);
		}

		efSpecification.Enter(DbContext);

		try
		{
			return await specification.Apply(DbSet, token);
		}
		finally
		{
			efSpecification.Exit();
		}
	}

	private async ValueTask<TEntity?> GetSlow(TKey id, CancellationToken token)
	{
		var dbSet = DbSet.IgnoreQueryFilters(_softDeleteFilter);
		dbSet = _querySplittingBehavior == QuerySplittingBehavior.SingleQuery ? dbSet.AsSingleQuery() : dbSet.AsSplitQuery();
		return await dbSet.FirstOrDefaultAsync(EqualityExpressionForId<TKey, TEntity>.Create(id), token);
	}

	private bool IsEntityInContext(TKey id, [NotNullWhen(true)] out EntityEntry<TEntity>? entity)
	{
		entity = DbSet.Local.FindEntry(id);
		return entity is not null;
	}

	private async ValueTask DeleteAsyncSlow(TKey id, CancellationToken cancellationToken)
	{
		TEntity? entity;

		if (IsEntityInContext(id, out var contextEntry))
		{
			entity = contextEntry.Entity;
		}
		else
		{
			entity = await GetSlow(id, cancellationToken);
		}

		if (entity is null)
		{
			return;
		}

		await Remove(entity, cancellationToken);
	}

	private Task<List<TEntity>> LoadEntities(IReadOnlyCollection<TKey> keys, CancellationToken cancellationToken)
	{
		List<TKey>? toLoad = null;
		List<TEntity>? loaded = null;

		foreach (var key in keys)
		{
			if (IsEntityInContext(key, out var entity))
			{
				// Borrada en esta unidad de trabajo: se reporta como ausente y no como viva. Ver Get(TKey).
				if (entity.State != EntityState.Deleted)
				{
					loaded ??= new(keys.Count);
					loaded.Add(entity.Entity);
				}

				continue;
			}

			toLoad ??= new(keys.Count);
			toLoad.Add(key);
		}

		return toLoad is null
			? Task.FromResult(loaded ?? [])
			: LoadManyInternal(toLoad, loaded, cancellationToken);
	}

	private async Task<List<TEntity>> LoadManyInternal(List<TKey> toLoad, List<TEntity>? loaded, CancellationToken cancellationToken)
	{
		var list = new List<TEntity>(toLoad.Count);
		var contains = _querySplittingBehavior == QuerySplittingBehavior.SingleQuery ? _containsAsSingle.Value : _containsAsSplit.Value;

		await foreach (var entity in contains(DbContext, toLoad).WithCancellation(cancellationToken))
		{
			list.Add(entity);
		}

		if (loaded is null)
		{
			return list;
		}

		loaded.AddRange(list);
		return loaded;
	}

	private static Func<DbContext, IEnumerable<TKey>, IAsyncEnumerable<TEntity>> CompileContainsAsSingle()
	{
		return EF.CompileAsyncQuery<DbContext, IEnumerable<TKey>, TEntity>((context, keys) => context
			.Set<TEntity>()
			.AsSingleQuery()
			.IgnoreQueryFilters(_softDeleteFilter)
			.Where(x => keys.Contains(x.Id)));
	}

	private static Func<DbContext, IEnumerable<TKey>, IAsyncEnumerable<TEntity>> CompileContainsAsSplit()
	{
		return EF.CompileAsyncQuery<DbContext, IEnumerable<TKey>, TEntity>((context, keys) => context
			.Set<TEntity>()
			.AsSplitQuery()
			.IgnoreQueryFilters(_softDeleteFilter)
			.Where(x => keys.Contains(x.Id)));
	}

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static T Throw<T>(string message)
	{
		throw new DbUpdateException(message);
	}
}
