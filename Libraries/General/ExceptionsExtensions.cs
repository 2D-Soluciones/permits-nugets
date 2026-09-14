using System.Reflection;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Extensiones de excepciones
/// </summary>
[PublicAPI]
public static class ExceptionsExtensions
{
	/// <summary>
	///     Lista todas las excepciones que puedan existir dentro de la excepcion que se le pasa.
	/// </summary>
	/// <param name="exception">La excepcion a desenvolver</param>
	/// <returns>Una lista con las excepciones extraidas</returns>
	[PublicAPI]
	public static IReadOnlyCollection<Exception> UnwrapAndCollect(this Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		var exs = new List<Exception>(2);

		// El set lleva identidad, no igualdad: InnerException puede ciclar - basta una excepcion que se envuelva a si
		// misma, o dos que se envuelvan mutuamente - y el recorrido no tenia salida. De paso reemplaza el Contains
		// lineal que hacia el conteo cuadratico.
		CollectExceptions(exception, exs, new(ReferenceEqualityComparer.Instance));
		return exs;
	}

	private static bool CollectAggregateException(Exception exception, List<Exception> exceptions, HashSet<Exception> seen)
	{
		if (exception is not AggregateException aggregateException)
		{
			return false;
		}

		CollectOneOrMany(exception, aggregateException.InnerExceptions, exceptions, seen);
		return true;
	}

	private static void CollectExceptions(Exception exception, List<Exception> exceptions, HashSet<Exception> seen)
	{
		while (true)
		{
			if (!seen.Add(exception))
			{
				return;
			}

			if (!CollectTaskCancelled(exception, exceptions, seen) && !CollectReflectionTypeLoadException(exception, exceptions, seen) && !CollectAggregateException(exception, exceptions, seen))
			{
				exceptions.Add(exception);
			}

			if (exception.InnerException is null)
			{
				return;
			}

			exception = exception.InnerException;
		}
	}

	private static void CollectOneOrMany(Exception upper, IReadOnlyCollection<Exception?>? lower, List<Exception> exceptions, HashSet<Exception> seen)
	{
		if (lower is null || lower.Count == 0)
		{
			exceptions.Add(upper);
			return;
		}

		foreach (var ex in lower)
		{
			if (ex is not null)
			{
				CollectExceptions(ex, exceptions, seen);
			}
		}
	}

	private static bool CollectReflectionTypeLoadException(Exception exception, List<Exception> exceptions, HashSet<Exception> seen)
	{
		if (exception is not ReflectionTypeLoadException reflection)
		{
			return false;
		}

		CollectOneOrMany(exception, reflection.LoaderExceptions, exceptions, seen);
		return true;
	}

	private static bool CollectTaskCancelled(Exception exception, List<Exception> exceptions, HashSet<Exception> seen)
	{
		var task = exception as TaskCanceledException;

		if (task?.Task?.Exception is null)
		{
			return false;
		}

		CollectOneOrMany(task.Task.Exception, task.Task.Exception.InnerExceptions, exceptions, seen);
		return true;
	}
}
