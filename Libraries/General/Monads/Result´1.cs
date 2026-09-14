using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DDS.General.Json;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Una implementacion de <see cref="IResult{T}" /> como monada.
/// </summary>
/// <remarks>
///     <c>default(Result&lt;T&gt;)</c> no lleva error, asi que se declara exitoso con un valor <c>default</c>. Esa forma
///     aparece sola -un campo sin inicializar, un arreglo de resultados, un <c>default</c> en codigo generico-, asi que
///     nada de aca puede asumir que <see cref="Value" /> no es null solo porque <see cref="IsSucceeded" /> sea true.
/// </remarks>
[PublicAPI]
[JsonConverter(typeof(ResultOfTConverterFactory))]
public readonly struct Result<T> : IResult<T>, IEquatable<Result<T>>
{
	internal Result(T? value, ErrorResult? errorResult)
	{
		Value = value;
		Error = errorResult;
	}

	/// <inheritdoc />
	[MemberNotNullWhen(false, nameof(Error))]
	[MemberNotNullWhen(true, nameof(Value))]
	public bool IsSucceeded
	{
		get { return Error is null; }
	}

	/// <inheritdoc />
	[MemberNotNullWhen(true, nameof(Error))]
	[MemberNotNullWhen(false, nameof(Value))]
	public bool IsFaulted
	{
		get { return Error is not null; }
	}

	/// <inheritdoc />
	public ErrorResult? Error { get; }

	/// <inheritdoc />
	public T? Value { get; }

	/// <inheritdoc />
	public TResult Match<TResult>(Func<T, TResult> success, Func<ErrorResult, TResult> error)
	{
		return IsSucceeded ? success(Value) : error(Error);
	}

	/// <inheritdoc />
	public void Switch(Action<T> success, Action<ErrorResult> error)
	{
		if (IsSucceeded)
		{
			success(Value);
		}
		else
		{
			error(Error);
		}
	}

	/// <inheritdoc />
	public bool Equals(Result<T> other)
	{
		if (IsSucceeded)
		{
			return other.IsSucceeded && EqualityComparer<T>.Default.Equals(Value, other.Value);
		}

		return other.IsFaulted && Error.Equals(other.Error);
	}

	/// <inheritdoc />
	public override bool Equals(object? obj)
	{
		return obj is Result<T> other && Equals(other);
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return IsSucceeded ? Value?.ToString() ?? string.Empty : Error.ToString();
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var hashCode = IsSucceeded ? Value?.GetHashCode() ?? 0 : Error.GetHashCode();
		return (hashCode * 397) ^ (IsSucceeded ? 0 : 1);
	}

	/// <summary>
	///     Crea un resultado nuevo en estado de error, usando el <paramref name="errorResult" /> que se le pasa.
	/// </summary>
	/// <param name="errorResult"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<T> FromError(ErrorResult errorResult)
	{
		return new(default!, errorResult);
	}

	/// <summary>
	///     Convierte implicitamente un <paramref name="value" /> en un resultado en estado exitoso.
	/// </summary>
	/// <remarks>
	///     No lo llames con <typeparamref name="T" /> siendo un <see cref="Result" /> o un
	///     <see cref="Result{U}" />; el <c>Result&lt;Result&lt;U&gt;&gt;</c> que sale es casi seguro que no es
	///     lo que queriamos. Las restricciones de genericos de C# no lo pueden frenar en compilacion. Usa
	///     <see cref="Result.FromValue{U}(Result{U})" /> o <see cref="Result.FromValue(Result)" />,
	///     que desenvuelven los resultados anidados.
	/// </remarks>
	/// <param name="value">El valor a usar como estado exitoso.</param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Result<T>(T value)
	{
		return new(value, null);
	}

	/// <summary>
	///     Convierte implicitamente un <see cref="ErrorResult" /> en un resultado en estado de error.
	/// </summary>
	/// <param name="error"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Result<T>(ErrorResult error)
	{
		return new(default, error);
	}

	/// <summary>
	///     Convierte implicitamente un <see cref="Result{T}" /> en un resultado en estado de error.
	/// </summary>
	/// <param name="result"></param>
	/// <returns></returns>
	public static implicit operator Result(Result<T> result)
	{
		return result.IsSucceeded ? Result.Success : Result.FromError(result.Error);
	}

	/// <summary>
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	/// <returns></returns>
	public static bool operator ==(Result<T> left, Result<T> right)
	{
		return left.Equals(right);
	}

	/// <summary>
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	/// <returns></returns>
	public static bool operator !=(Result<T> left, Result<T> right)
	{
		return !(left == right);
	}
}
