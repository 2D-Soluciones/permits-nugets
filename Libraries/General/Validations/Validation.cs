using System.Net;
using JetBrains.Annotations;

namespace DDS.General.Validations;
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.

/// <summary>
///     Validation helpers.
/// </summary>
[PublicAPI]
public static class Validation
{
	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> con codigo <see cref="HttpStatusCode.Conflict" /> si el
	///     <paramref name="value" /> que se le pasa no es <c>null</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result Conflict<T>(T? value, string message, IDictionary<string, object>? data = null) where T : class
	{
		return ErrorIf(value is not null, message, HttpStatusCode.Conflict, data);
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si <paramref name="condition" /> es <c>true</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="condition">La condicion a evaluar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result ErrorIf(bool condition, string message, IDictionary<string, object>? data = null)
	{
		return ErrorIf(condition, message, HttpStatusCode.PreconditionFailed, data);
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si <paramref name="condition" /> es <c>true</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="condition"></param>
	/// <param name="message"></param>
	/// <param name="statusCode"></param>
	/// <param name="data"></param>
	/// <returns></returns>
	public static Result ErrorIf(bool condition, string message, HttpStatusCode statusCode, IDictionary<string, object>? data)
	{
		return condition
			? new ErrorResult(message, statusCode) {Data = data}
			: Result.Success;
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si <paramref name="condition" /> es <c>false</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="condition">La condicion a evaluar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result ErrorIfNot(bool condition, string message, IDictionary<string, object>? data = null)
	{
		return ErrorIf(!condition, message, HttpStatusCode.PreconditionFailed, data);
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si el <paramref name="email" /> que se le pasa no es un mail valido,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="email">El valor a validar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result InvalidEmail([System.Diagnostics.CodeAnalysis.NotNull] string? email, string message, IDictionary<string, object>? data = null)
	{
		return ErrorIf(!email.IsValidEmail(), message, HttpStatusCode.PreconditionFailed, data);
	}


	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si el <paramref name="value" /> que se le pasa es <c>null</c> o esta
	///     collection,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="value">El <see cref="IEnumerable{T}" /> de origen.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	/// <typeparam name="T">El <see cref="Type" /> de los items de la coleccion.</typeparam>
	public static Result Missing<T>([System.Diagnostics.CodeAnalysis.NotNull] IEnumerable<T>? value, string message, IDictionary<string, object>? data = null)
	{
		if (value is string str)
		{
			return Missing(str, message, data);
		}

		return ErrorIf(value is null || IsEmpty(value), message, HttpStatusCode.PreconditionFailed, data);
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si el <paramref name="value" /> que se le pasa es <c>null</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result Missing([System.Diagnostics.CodeAnalysis.NotNull] object? value, string message, IDictionary<string, object>? data = null)
	{
		return ErrorIf(value is null, message, HttpStatusCode.PreconditionFailed, data);
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si el <paramref name="value" /> que se le pasa es el valor por defecto
	///     del enum que se le pasa,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	/// <typeparam name="T"></typeparam>
	public static Result Missing<T>(T value, string message, IDictionary<string, object>? data = null) where T : struct, Enum
	{
		return ErrorIf(EqualityComparer<T>.Default.Equals(value, default) || !IsDefinedValue(value), message, HttpStatusCode.PreconditionFailed, data);
	}

	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> si el <paramref name="value" /> que se le pasa es <c>null</c> o vacio,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result Missing([System.Diagnostics.CodeAnalysis.NotNull] string? value, string message, IDictionary<string, object>? data = null)
	{
		return ErrorIf(string.IsNullOrWhiteSpace(value), message, HttpStatusCode.PreconditionFailed, data);
	}



	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> con codigo <see cref="HttpStatusCode.NotFound" /> si el
	///     <paramref name="value" /> que se le pasa es <c>null</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="value">El valor contra el que comparar.</param>
	/// <param name="message">El mensaje de error cuando la validacion falla.</param>
	/// <param name="data"></param>
	public static Result NotFound<T>([System.Diagnostics.CodeAnalysis.NotNull] T? value, string message, IDictionary<string, object>? data = null) where T : class
	{
		return ErrorIf(value is null, message, HttpStatusCode.NotFound, data);
	}


	/// <summary>
	///     Devuelve un <see cref="ErrorResult" /> con codigo <see cref="HttpStatusCode.Unauthorized" /> si
	///     <paramref name="unauthorized" /> es <c>true</c>,
	///     <see cref="Success" /> si no.
	/// </summary>
	/// <param name="unauthorized"></param>
	/// <param name="message"></param>
	/// <param name="data"></param>
	/// <returns></returns>
	public static Result Unauthorized(bool unauthorized, string message, IDictionary<string, object>? data = null)
	{
		return ErrorIf(unauthorized, message, HttpStatusCode.Unauthorized, data);
	}


	/// <summary>
	///     Fabrica de errores
	/// </summary>
	[PublicAPI]
	public static class Errors
	{
		/// <summary>
		///     Equivalente a <see cref="HttpStatusCode.Conflict" />
		/// </summary>
		/// <param name="error"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public static ErrorResult Conflict(string? error = null, IDictionary<string, object>? data = null)
		{
			return new(error ?? "conflict", HttpStatusCode.Conflict) {Data = data};
		}


		/// <summary>
		///     Equivalente a <see cref="HttpStatusCode.NotFound" />
		/// </summary>
		/// <param name="error"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public static ErrorResult NotFound(string? error = null, IDictionary<string, object>? data = null)
		{
			return new(error ?? "not_found", HttpStatusCode.NotFound) {Data = data};
		}


		/// <summary>
		///     Equivalente a <see cref="HttpStatusCode.PreconditionFailed" />
		/// </summary>
		/// <param name="error"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public static ErrorResult PreconditionFailed(string? error = null, IDictionary<string, object>? data = null)
		{
			return new(error ?? "precondition_failed", HttpStatusCode.PreconditionFailed) {Data = data};
		}

		/// <summary>
		///     Equivalente a <see cref="HttpStatusCode.Unauthorized" />
		/// </summary>
		/// <param name="error"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public static ErrorResult Unauthorized(string? error = null, IDictionary<string, object>? data = null)
		{
			return new(error ?? "unauthorized", HttpStatusCode.Unauthorized) {Data = data};
		}
	}

	/// <summary>
	///     Si el valor del enum es uno de los que el tipo realmente declara.
	/// </summary>
	/// <remarks>
	///     Enum.IsDefined da false para cualquier combinacion de un enum [Flags], asi que preguntarle rechazaria valores
	///     values like <c>Read | Write</c>. Un flags se valida enmascarando: cada bit prendido tiene que pertenecer a
	///     algun valor declarado. Saltear la validacion entera para los flags dejaba pasar cualquier cosa - un
	///     <c>(Perm) 64</c> sin ningun nombre detras se daba por bueno.
	/// </remarks>
	private static bool IsDefinedValue<T>(T value) where T : struct, Enum
	{
		return FlagsEnum<T>.IsFlags
			? (FlagsEnum<T>.ToBits(value) & ~FlagsEnum<T>.DeclaredBits) == 0
			: Enum.IsDefined(value);
	}

	/// <summary>
	///     Si la secuencia no tiene elementos.
	/// </summary>
	/// <remarks>
	///     Se cuenta sin enumerar cada vez que la fuente lo puede decir. Una que no puede -un iterador pelado- igual
	///     entrega su primer elemento al Any(), asi que pasa una coleccion materializada si la secuencia es de un solo uso.
	/// </remarks>
	private static bool IsEmpty<T>(IEnumerable<T> value)
	{
		return value.TryGetNonEnumeratedCount(out var count) ? count == 0 : !value.Any();
	}

	private static class FlagsEnum<T> where T : struct, Enum
	{
		public static readonly bool IsFlags = typeof(T).IsDefined(typeof(FlagsAttribute), false);

		/// <summary>
		///     Todos los bits que prende algun miembro declarado de <typeparamref name="T" />.
		/// </summary>
		// ReSharper disable once StaticMemberInGenericType
		public static readonly ulong DeclaredBits = ComputeDeclaredBits();

		/// <summary>
		///     Los bits del valor, extendidos con ceros desde el ancho del tipo subyacente. Solo los bits dentro de ese ancho
		///     significan algo, asi que la mascara y el valor se leen igual.
		/// </summary>
		public static ulong ToBits(T value)
		{
			return Type.GetTypeCode(typeof(T)) switch
			{
				TypeCode.SByte => unchecked((byte) (sbyte) (object) value),
				TypeCode.Byte => (byte) (object) value,
				TypeCode.Int16 => unchecked((ushort) (short) (object) value),
				TypeCode.UInt16 => (ushort) (object) value,
				TypeCode.Int32 => unchecked((uint) (int) (object) value),
				TypeCode.UInt32 => (uint) (object) value,
				TypeCode.Int64 => unchecked((ulong) (long) (object) value),
				_ => (ulong) (object) value
			};
		}

		private static ulong ComputeDeclaredBits()
		{
			var bits = 0UL;

			foreach (var declared in Enum.GetValues<T>())
			{
				bits |= ToBits(declared);
			}

			return bits;
		}
	}
}
