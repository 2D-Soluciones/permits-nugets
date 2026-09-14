using System.Net;
using JetBrains.Annotations;

namespace DDS.General.Validations;
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.

/// <summary>
///     Extensiones para trabajar con <see cref="Validator" />
/// </summary>
[PublicAPI]
public static class ValidationExtensions
{
	/// <summary>
	///     Le agrega un error <see cref="HttpStatusCode.Conflict" /> al <paramref name="validator" /> si el
	///     <paramref name="value" /> que se le pasa <c>no es null</c>.
	/// </summary>
	/// <param name="validator"></param>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator Conflict<T>(this Validator validator, T? value, string message, IDictionary<string, object>? data = null) where T : class
	{
		return validator.AddRule(() => Validation.Conflict(value, message, data));
	}

	/// <summary>
	///     Le agrega un error <see cref="HttpStatusCode.PreconditionFailed" /> al <paramref name="validator" /> si
	///     se cumple la <paramref name="condition" />.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="condition">La condicion a evaluar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator ErrorIf(this Validator validator, bool condition, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.ErrorIf(condition, message, HttpStatusCode.PreconditionFailed, data));
	}

	/// <summary>
	///     Le agrega un error al <paramref name="validator" /> si no se cumple la <paramref name="condition" />.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="condition">La condicion a evaluar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator ErrorIfNot(this Validator validator, bool condition, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.ErrorIf(!condition, message, HttpStatusCode.PreconditionFailed, data));
	}

	/// <summary>
	///     Si la <paramref name="condition" /> es <c>true</c>, ejecuta el <paramref name="chain" />;
	///     si no, no cambia nada en el <see cref="Validator" />.
	/// </summary>
	/// <param name="validator"></param>
	/// <param name="condition"></param>
	/// <param name="chain"></param>
	/// <returns></returns>
	public static Validator If(this Validator validator, bool condition, Action<Validator> chain)
	{
		if (condition)
		{
			chain(validator);
		}

		return validator;
	}

	/// <summary>
	///     Le agrega un error al <paramref name="validator" /> si el <paramref name="email" /> no es una direccion valida.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="email">El valor a validar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator InvalidEmail(this Validator validator, [System.Diagnostics.CodeAnalysis.NotNull] string? email, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.InvalidEmail(email, message, data));
	}


	/// <summary>
	///     Le agrega un error al <paramref name="validator" /> si el <paramref name="value" /> que se le pasa es <c>null</c> o
	///     empty.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="value">El <see cref="IEnumerable{T}" /> de origen.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	/// <typeparam name="T">El <see cref="Type" /> de los items de la coleccion.</typeparam>
	public static Validator Missing<T>(this Validator validator, [System.Diagnostics.CodeAnalysis.NotNull] IEnumerable<T>? value, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.Missing(value, message, data));
	}

	/// <summary>
	///     Le agrega un error al <paramref name="validator" /> si el <paramref name="value" /> que se le pasa es <c>null</c>.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator Missing(this Validator validator, [System.Diagnostics.CodeAnalysis.NotNull] object? value, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.Missing(value, message, data));
	}

	/// <summary>
	///     Le agrega un error al <paramref name="validator" /> si <paramref name="value" /> es el valor por defecto del
	///     supplied
	///     tipo enum.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	/// <typeparam name="T"></typeparam>
	public static Validator Missing<T>(this Validator validator, T value, string message, IDictionary<string, object>? data = null) where T : struct, Enum
	{
		return validator.AddRule(() => Validation.Missing(value, message, data));
	}

	/// <summary>
	///     Le agrega un error al <paramref name="validator" /> si el <paramref name="value" /> que se le pasa es <c>null</c> o
	///     empty string.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator Missing(this Validator validator, [System.Diagnostics.CodeAnalysis.NotNull] string? value, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.Missing(value, message, data));
	}



	/// <summary>
	///     Le agrega un error <see cref="HttpStatusCode.NotFound" /> al <paramref name="validator" /> si el
	///     <paramref name="value" /> que se le pasa <c>no es null</c>.
	/// </summary>
	/// <param name="validator">El validador de validaciones.</param>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Validator NotFound<T>(this Validator validator, [System.Diagnostics.CodeAnalysis.NotNull] T? value, string message, IDictionary<string, object>? data = null) where T : class
	{
		return validator.AddRule(() => Validation.NotFound(value, message, data));
	}


	/// <summary>
	///     Le agrega un error <see cref="HttpStatusCode.Unauthorized" /> al <paramref name="validator" /> si el
	///     <paramref name="unauthorized" /> es <c>true</c>.
	/// </summary>
	/// <param name="validator"></param>
	/// <param name="unauthorized"></param>
	/// <param name="message"></param>
	/// <param name="data"></param>
	/// <returns></returns>
	public static Validator Unauthorized(this Validator validator, bool unauthorized, string message, IDictionary<string, object>? data = null)
	{
		return validator.AddRule(() => Validation.Unauthorized(unauthorized, message, data));
	}
}
