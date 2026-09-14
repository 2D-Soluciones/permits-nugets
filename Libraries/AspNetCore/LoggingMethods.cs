using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DDS.AspNetCore;

[NoReorder]
internal static partial class LoggingMethods
{
	[LoggerMessage(Level = LogLevel.Warning, Message = "Request was cancelled @ {path}")]
	public static partial void LogRequestCancelled(this ILogger logger, string path);

	[LoggerMessage(Level = LogLevel.Error, Message = "Unknown error in api call. No exception was provided")]
	public static partial void LogUnknownError(this ILogger logger);

	// El nivel lo elige el call site: no todo error de api merece Error (un 401 no es una falla del servidor).
	// El mensaje va en el texto y no solo en la excepcion: sino el visor de logs muestra N lineas identicas.
	[LoggerMessage(Message = "API error: {errorMessage}")]
	public static partial void LogApiCallError(this ILogger logger, LogLevel level, Exception exception, string errorMessage);

	[LoggerMessage(Message = "API error: {errorMessage} (session {sessionId})")]
	public static partial void LogApiCallWithSessionError(this ILogger logger, LogLevel level, Exception exception, string errorMessage, string sessionId);

	[LoggerMessage(Message = "Inner error of {parent}: {errorMessage}")]
	public static partial void LogApiCallInnerError(this ILogger logger, LogLevel level, Exception exception, string parent, string errorMessage);

	[LoggerMessage(Level = LogLevel.Debug, Message = "AntiCsrf validation disabled")]
	public static partial void LogAntiCsrfDisabled(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Information, Message = "AntiCsrf validation failed, error: {error}")]
	public static partial void LogAntiCsrfError(this ILogger logger, string error);

	// Nunca loguear la clave: es una credencial viva y el logging en Debug es habitual en staging.
	[LoggerMessage(Level = LogLevel.Debug, Message = "Received an api key from ApiKeyResultContext (length: {apiKeyLength})")]
	public static partial void LogApiKeyFromContext(this ILogger logger, int apiKeyLength);

	[LoggerMessage(Level = LogLevel.Debug, Message = "No Api Key found in the request")]
	public static partial void LogApiKeyNotFound(this ILogger logger);

	// Debug y no Error: la manda un cliente sin autenticar, y a nivel Error es un grifo de log abierto para cualquiera.
	[LoggerMessage(Level = LogLevel.Debug, Message = "Malformed API Key provided: not valid base64 (length: {apiKeyLength})")]
	public static partial void LogMalformedApiKey(this ILogger logger, int apiKeyLength);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Invalid API Key provided (length: {apiKeyLength})")]
	public static partial void LogInvalidApiKey(this ILogger logger, int apiKeyLength);

	[LoggerMessage(Level = LogLevel.Error, Message = "Error retrieving api key")]
	public static partial void LogErrorRetrievingApiKey(this ILogger logger, Exception exception);

	[LoggerMessage(Level = LogLevel.Information, Message = "Forbidden response has started")]
	public static partial void ForbiddenResponseHasStarted(this ILogger logger);









	[LoggerMessage(Level = LogLevel.Warning, Message = "[Auth] Decrypted data size {size} exceeds maximum allowed {maxSize}")]
	public static partial void LogApiKeyDecryptedDataTooLarge(this ILogger logger, int size, int maxSize);

	[LoggerMessage(Level = LogLevel.Debug, Message = "[Auth] Info.Length < 16")]
	public static partial void LogApiKeyDataTooSmall(this ILogger logger);

	[LoggerMessage(Level = LogLevel.Debug, Message = "[Auth] ApiKey not valid because {expires} < {now}")]
	public static partial void LogApiKeyExpired(this ILogger logger, DateTime expires, DateTime now);


	// Information y no Error: un batch mal armado lo manda el cliente, igual que un api key malformada mas arriba.
}
