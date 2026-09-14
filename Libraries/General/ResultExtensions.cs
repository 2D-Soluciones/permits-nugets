using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Extensiones de <see cref="Result{T}" />.
/// </summary>
[PublicAPI]
[NoReorder]
public static class ResultExtensions
{




	/// <summary>
	///     Devuelve <c>true</c> si el <paramref name="result" /> es un <see cref="ErrorResult" />, <c>false</c> si no.
	/// </summary>
	/// <param name="result">El valor a evaluar</param>
	/// <param name="error"></param>
	/// <returns></returns>
	public static bool IsFaulted(this Result result, [NotNullWhen(true)] out ErrorResult? error)
	{
		if (result.IsFaulted)
		{
			error = result.Error;
			return true;
		}

		error = null;
		return false;
	}

	/// <summary>
	///     Devuelve <c>true</c> si el <paramref name="result" /> es un <see cref="ErrorResult" />, <c>false</c> si no.
	/// </summary>
	/// <param name="result">El valor a evaluar</param>
	/// <param name="value"></param>
	/// <param name="error"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static bool IsFaulted<T>(this Result<T> result, [NotNullWhen(false)] out T? value, [NotNullWhen(true)] out ErrorResult? error)
	{
		if (result.IsFaulted)
		{
			error = result.Error;
			value = default;
			return true;
		}

		value = result.Value;
		error = null;
		return false;
	}

	/// <summary>
	///     Devuelve <c>true</c> si el <paramref name="result" /> es un resultado exitoso, <c>false</c> si no.
	/// </summary>
	/// <param name="result"></param>
	/// <param name="error"></param>
	/// <returns></returns>
	public static bool IsSucceeded(this Result result, [NotNullWhen(false)] out ErrorResult? error)
	{
		if (result.IsSucceeded)
		{
			error = null;
			return true;
		}

		error = result.Error;
		return false;
	}

	/// <summary>
	///     Devuelve <c>true</c> si el <paramref name="result" /> es un resultado <typeparamref name="T" />, <c>false</c>
	///     si no.
	/// </summary>
	/// <param name="result"></param>
	/// <param name="value"></param>
	/// <param name="error"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static bool IsSucceeded<T>(this Result<T> result, [NotNullWhen(true)] out T? value, [NotNullWhen(false)] out ErrorResult? error)
	{
		if (result.IsSucceeded)
		{
			error = null;
			value = result.Value;
			return true;
		}

		value = default;
		error = result.Error;
		return false;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	[Obsolete("Use the Func<ValueTask<Result>> overload. This one cannot short-circuit: 'next' has already started running by the time 'chain' is evaluated.")]
	public static async ValueTask<Result> Then(this ValueTask<Result> chain, ValueTask<Result> next)
	{
		var result = await chain;

		// Se espera igual aunque 'chain' haya fallado: ya estaba corriendo, y una ValueTask que nunca se espera es
		// una violacion del contrato -si viene de un IValueTaskSource del pool- y deja su excepcion sin observar.
		var nextResult = await next;
		return result.IsSucceeded ? nextResult : result;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	[Obsolete("Use the Func<T, ValueTask<Result<T>>> overload. This one cannot short-circuit: 'next' has already started running by the time 'chain' is evaluated.")]
	public static async ValueTask<Result<T>> Then<T>(this ValueTask<Result<T>> chain, ValueTask<Result<T>> next)
	{
		var result = await chain;
		var nextResult = await next;
		return result.IsSucceeded ? nextResult : result;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	public static async ValueTask<Result> Then(this ValueTask<Result> chain, Func<ValueTask<Result>> next)
	{
		var result = await chain;
		return result.IsSucceeded ? await next() : result;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	public static async ValueTask<Result<T>> Then<T>(this ValueTask<Result<T>> chain, Func<T, ValueTask<Result<T>>> next)
	{
		var result = await chain;
		return result.IsSucceeded ? await next(result.Value) : result;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	public static Result Then(this Result chain, Result next)
	{
		return chain.IsSucceeded ? next : chain;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	public static Result<T> Then<T>(this Result<T> chain, Result<T> next)
	{
		return chain.IsSucceeded ? next : chain;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	public static Result Then(this Result chain, Func<Result> next)
	{
		return chain.IsSucceeded ? next() : chain;
	}

	/// <summary>
	///     Encadena un resultado con otro. Si el <paramref name="chain" /> que se le pasa es invalido, devuelve ese.
	///     Si no, devuelve el <paramref name="next" />.
	/// </summary>
	/// <param name="chain"></param>
	/// <param name="next"></param>
	/// <returns></returns>
	public static Result<T> Then<T>(this Result<T> chain, Func<T, Result<T>> next)
	{
		return chain.IsSucceeded ? next(chain.Value) : chain;
	}
}
