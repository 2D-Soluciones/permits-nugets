using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DDS.General.Json;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Una implementacion de <see cref="IResult" /> como monada.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(ResultJsonConverter))]
public readonly struct Result : IResult<Success>, IEquatable<Result>
{
	private Result(ErrorResult? errorResult)
	{
		Error = errorResult;
	}

	/// <inheritdoc />
	[MemberNotNullWhen(false, nameof(Error))]
	public bool IsSucceeded
	{
		get { return Error is null; }
	}

	/// <inheritdoc />
	[MemberNotNullWhen(true, nameof(Error))]
	public bool IsFaulted
	{
		get { return Error is not null; }
	}

	/// <inheritdoc />
	public ErrorResult? Error { get; }

	/// <inheritdoc />
	public Success Value
	{
		get { return new(); }
	}

	/// <inheritdoc />
	public TResult Match<TResult>(Func<Success, TResult> success, Func<ErrorResult, TResult> error)
	{
		return IsSucceeded ? success(new()) : error(Error);
	}

	/// <inheritdoc />
	public void Switch(Action<Success> success, Action<ErrorResult> error)
	{
		if (IsSucceeded)
		{
			success(new());
		}
		else
		{
			error(Error);
		}
	}

	/// <inheritdoc />
	public bool Equals(Result other)
	{
		return IsSucceeded ? other.IsSucceeded : other.IsFaulted && Error.Equals(other.Error);
	}

	/// <inheritdoc />
	public override bool Equals(object? obj)
	{
		return obj is Result other && Equals(other);
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return IsSucceeded ? "Success" : Error.ToString();
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		if (IsSucceeded)
		{
			return 0;
		}

		return (Error.GetHashCode() * 397) ^ 1;
	}

	/// <summary>
	///     Devuelve un <see cref="Result" /> terminado en estado exitoso.
	/// </summary>
	/// <returns></returns>
	public static Result Success
	{
		get { return new(); }
	}

	/// <summary>
	///     Crea un resultado nuevo en estado de error, usando el <paramref name="errorResult" /> que se le pasa.
	/// </summary>
	/// <param name="errorResult"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result FromError(ErrorResult errorResult)
	{
		return new(errorResult);
	}

	/// <summary>
	///     Crea un resultado nuevo en estado exitoso, usando el <paramref name="value" /> que se le pasa.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<T> FromValue<T>(T value)
	{
		return new(value, null);
	}

	/// <summary>
	///     Devuelve el <paramref name="value" /> tal cual. Evita terminar sin querer con un
	///     <c>Result&lt;Result&lt;U&gt;&gt;</c> anidado cuando el llamador ya tiene un <see cref="Result{U}" />.
	/// </summary>
	/// <param name="value"></param>
	/// <typeparam name="U"></typeparam>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<U> FromValue<U>(Result<U> value)
	{
		return value;
	}

	/// <summary>
	///     Devuelve el <paramref name="value" /> tal cual. Evita terminar sin querer con un
	///     <c>Result&lt;Result&gt;</c> anidado cuando el llamador ya tiene un <see cref="Result" />.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result FromValue(Result value)
	{
		return value;
	}

	/// <summary>
	///     Crea un resultado nuevo en estado exitoso, usando el <paramref name="value" /> que se le pasa.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	public Result<T> WithValue<T>(T value)
	{
		return new(value, Error);
	}

	/// <summary>
	///     Combina este <see cref="Result" /> con el <see cref="Result{U}" /> que se le pasa, sin anidar.
	///     Si este resultado esta en error, se conserva su error. Si no, y el <paramref name="value" /> que se le pasa
	///     esta en error, se propaga ese error de adentro. Si no, devuelve el valor exitoso desenvuelto.
	/// </summary>
	/// <param name="value"></param>
	/// <typeparam name="U"></typeparam>
	/// <returns></returns>
	public Result<U> WithValue<U>(Result<U> value)
	{
		if (IsFaulted)
		{
			return Result<U>.FromError(Error);
		}

		return value.IsFaulted ? Result<U>.FromError(value.Error) : new(value.Value, null);
	}

	/// <summary>
	///     Combina este <see cref="Result" /> con el <see cref="Result" /> que se le pasa, sin anidar.
	///     Si este resultado esta en error, se conserva el error actual; si no, devuelve el
	///     <paramref name="value" /> que se le paso.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	public Result WithValue(Result value)
	{
		return IsFaulted ? this : value;
	}

	/// <summary>
	///     Convierte implicitamente un <see cref="Success" /> en un resultado en estado exitoso.
	/// </summary>
	/// <param name="_"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Result(Success _)
	{
		return new(null);
	}

	/// <summary>
	///     Convierte implicitamente un <see cref="ErrorResult" /> en un resultado en estado de error.
	/// </summary>
	/// <param name="error"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Result(ErrorResult error)
	{
		return new(error);
	}

	/// <summary>
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	/// <returns></returns>
	public static bool operator ==(Result left, Result right)
	{
		return left.Equals(right);
	}

	/// <summary>
	/// </summary>
	/// <param name="left"></param>
	/// <param name="right"></param>
	/// <returns></returns>
	public static bool operator !=(Result left, Result right)
	{
		return !(left == right);
	}
}
