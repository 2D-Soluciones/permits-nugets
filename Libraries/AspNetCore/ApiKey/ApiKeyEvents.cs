using JetBrains.Annotations;

namespace DDS.AspNetCore.ApiKey;

/// <summary>
///     Define los eventos que invoca el <see cref="ApiKeyHandler" /> para que el desarrollador pueda controlar la
///     process.
/// </summary>
[PublicAPI]
public class ApiKeyEvents
{
	/// <summary>
	///     Se invoca si la autenticacion falla procesando el request. Las excepciones se vuelven a tirar despues de este evento, salvo
	///     suppressed.
	/// </summary>
	public Func<ApiKeyAuthenticationFailedContext, Task> OnAuthenticationFailed { get; set; } = _ => Task.CompletedTask;

	/// <summary>
	///     Se invoca antes de mandarle un challenge al llamador.
	/// </summary>
	public Func<ApiKeyChallengeContext, Task> OnChallenge { get; set; } = _ => Task.CompletedTask;

	/// <summary>
	///     Se invoca si la autorizacion falla y termina en una respuesta Forbidden.
	/// </summary>
	public Func<ApiKeyForbiddenContext, Task> OnForbidden { get; set; } = _ => Task.CompletedTask;

	/// <summary>
	///     Se invoca cuando llega por primera vez un mensaje del protocolo.
	/// </summary>
	public Func<ApiKeyResultContext, Task> OnMessageReceived { get; set; } = _ => Task.CompletedTask;

	/// <summary>
	///     El método asignado a esta propiedad será invocado cuando sea necesario validar un usuario.
	/// </summary>
	// ReSharper disable once PropertyCanBeMadeInitOnly.Global
	public Func<ApiKeyResultContext, Task> OnValidatePrincipal { get; set; } = _ => Task.CompletedTask;
}
