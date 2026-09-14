using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Contrato que define el resultado de una operacion.
/// </summary>
[NoReorder]
[PublicAPI]
public interface IResult
{
	/// <summary>
	///     Devuelve <c>true</c> si la operacion salio bien, <c>false</c> si no.
	/// </summary>
	[MemberNotNullWhen(false, nameof(Error))]
	bool IsSucceeded { get; }

	/// <summary>
	///     Devuelve <c>true</c> si la operacion fallo, <c>false</c> si no.
	/// </summary>
	[MemberNotNullWhen(true, nameof(Error))]
	bool IsFaulted { get; }

	/// <summary>
	///     El <see cref="ErrorResult" /> cuando la operacion esta en estado de error.
	/// </summary>
	ErrorResult? Error { get; }
}

/// <summary>
///     Una extension del contrato <see cref="IResult" /> que ademas tiene un valor asociado.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public interface IResult<out T> : IResult
{
	/// <summary>
	///     El valor asociado a este <see cref="IResult{T}" /> cuando la operacion salio bien.
	/// </summary>
	T? Value { get; }

	/// <summary>
	///     Transforma esta monada en un <typeparamref name="TResult" /> nuevo.
	/// </summary>
	/// <param name="success">La funcion a correr si este <see cref="IResult{T}" /> salio bien.</param>
	/// <param name="error">La funcion a correr si este <see cref="IResult{T}" /> fallo.</param>
	/// <typeparam name="TResult"></typeparam>
	/// <returns></returns>
	TResult Match<TResult>(Func<T, TResult> success, Func<ErrorResult, TResult> error);

	/// <summary>
	///     Ejecuta las funciones que se le pasan segun el estado de la monada.
	/// </summary>
	/// <param name="success">La funcion que corre si este <see cref="IResult{T}" /> salio bien.</param>
	/// <param name="error">La funcion que corre si este <see cref="IResult{T}" /> fallo.</param>
	void Switch(Action<T> success, Action<ErrorResult> error);
}
