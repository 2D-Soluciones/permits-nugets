using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DDS.General;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DDS.AspNetCore;

internal static class Utils
{
	private const string INVALID_VALUE_MESSAGE = "The value is not valid.";

	/// <summary>
	///     El detail que lleva una respuesta por un modelo invalido, venga del binding o de una validacion hecha
	///     adentro de la action.
	/// </summary>
	/// <remarks>
	///     Un modelo invalido no siempre es json: un form post falla igual, y una query string mal armada ni siquiera
	///     tiene body. Decir "json" en esos casos manda al cliente a mirar donde no es.
	/// </remarks>
	public static string InvalidModelMessage(HttpRequest request)
	{
		if (request.HasFormContentType)
		{
			return "Invalid form received.";
		}

		return request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
			? "Invalid json received."
			: "Invalid request received.";
	}

	/// <summary>
	///     La clave, adentro de las extensions de un ProblemDetails, donde viaja el detalle campo por campo de un
	///     modelo inválido.
	/// </summary>
	public const string ERRORS_KEY = "errors";

	/// <summary>
	///     Convierte un <see cref="ModelStateDictionary" /> al diccionario que va bajo <see cref="ERRORS_KEY" />: un
	///     array de mensajes por campo.
	/// </summary>
	/// <remarks>
	///     Anidados y no en el root de las extensions, que es donde van los datos de un <see cref="ErrorResult" />.
	///     Estos textos los escribe el framework, no nosotros: no se traducen ni se les da formato, la ui los muestra
	///     tal cual vienen. Tenerlos juntos bajo una clave es lo que le permite recorrerlos sin saber de antemano que
	///     campos son.
	/// </remarks>
	public static Dictionary<string, string[]> ModelStateToErrors(ModelStateDictionary modelState)
	{
		var errors = new Dictionary<string, string[]>(modelState.Count, StringComparer.Ordinal);

		foreach (var kvp in modelState)
		{
			if (kvp.Value is not {Errors.Count: > 0}) continue;
			var messages = new string[kvp.Value.Errors.Count];

			for (var i = 0; i < messages.Length; ++i)
			{
				messages[i] = ClientSafeMessage(kvp.Value.Errors[i]);
			}

			errors[kvp.Key] = messages;
		}

		return errors;
	}

	private static string ClientSafeMessage(ModelError error)
	{
		var message = GetExceptionMessage(error.Exception) ?? error.ErrorMessage;

		// El mensaje de System.Text.Json nombra el tipo .NET contra el que fallo -"...could not be converted to
		// Api.Users.CreateRequest"-, o sea el modelo interno, y en otras ramas cita el pedazo de payload que no pudo
		// leer. La clave ya dice que campo fue; el texto no agrega nada que el cliente pueda usar. Se reconoce por la
		// ubicacion en el buffer que le cuelga al final, porque el InputFormatter copia el texto al ModelState sin
		// dejar la excepcion: no hay tipo que mirar. Un mensaje de validacion propio que hable de LineNumber tambien
		// se cae aca, y esta bien: eso no es para un cliente.
		return message.Contains("| LineNumber:", StringComparison.Ordinal) ? INVALID_VALUE_MESSAGE : message;
	}

	[return: NotNullIfNotNull("exception")]
	public static string? GetExceptionMessage(Exception? exception)
	{
		return exception switch
		{
			null => null,
			JsonException jsonException => JsonExceptionToString(jsonException),
			_ => exception.Message
		};
	}

	private static string JsonExceptionToString(JsonException exception)
	{
		return string.IsNullOrEmpty(exception.Path) ? exception.Message : string.Concat(exception.Message, " @ ", exception.Path);
	}
}
