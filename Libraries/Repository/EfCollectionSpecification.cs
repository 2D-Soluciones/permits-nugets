using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace DDS.Repository;

/// <summary>
///     Specification base que lleva un <see cref="DbContext" /> para usar con EF Core.
/// </summary>
/// <remarks>
///     Una instancia se queda con el contexto de la operacion que esta corriendo, asi que sirve para exactamente una
///     operacion por vez. Registrarla como singleton, o pasarle la misma instancia a dos llamadas concurrentes, le dejaba un
///     <see cref="DbContext" /> partido sin decir nada; ahora avisa.
/// </remarks>
/// <remarks>
///     El queryable que se le pasa a <c>Apply</c> viene con tracking, como todo lo que devuelve el repositorio, asi que las entidades que
///     materialice se pueden modificar y guardar. Una specification que solo lee le llama <c>AsNoTracking()</c>:
///     esa decision es de quien escribe la consulta, no del repositorio.
/// </remarks>
[PublicAPI]
public abstract class EfSpecification
{
	private DbContext? _dbContext;

	/// <summary>
	///     Current EF <see cref="DbContext" />.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	///     La specification no esta corriendo dentro de una llamada del repositorio.
	/// </exception>
	protected DbContext DbContext
	{
		get { return _dbContext ?? throw new InvalidOperationException($"'{GetType().Name}' has no DbContext. A specification only has one while a repository is running it - do not hold on to it, or call Apply yourself."); }
	}

	internal void Enter(DbContext dbContext)
	{
		if (Interlocked.CompareExchange(ref _dbContext, dbContext, null) is not null)
		{
			throw new InvalidOperationException($"'{GetType().Name}' is already running against a DbContext. Give each call its own specification instance.");
		}
	}

	/// <summary>
	///     Suelta la referencia al contexto, asi la specification nunca le sobrevive con uno ya disposeado.
	/// </summary>
	internal void Exit()
	{
		Interlocked.Exchange(ref _dbContext, null);
	}
}

/// <inheritdoc cref="DDS.Repository.ICollectionSpecification{T,TOut}" />
[PublicAPI]
public abstract class EfCollectionSpecification<T, TOut> : EfSpecification, ICollectionSpecification<T, TOut> where T : class
{
	/// <inheritdoc />
	public abstract Task<IReadOnlyCollection<TOut>> Apply(IQueryable<T> queryable, CancellationToken cancellationToken);
}
