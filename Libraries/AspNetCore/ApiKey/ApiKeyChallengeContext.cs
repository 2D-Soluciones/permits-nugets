using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace DDS.AspNetCore.ApiKey;

/// <inheritdoc />
[PublicAPI]
public class ApiKeyChallengeContext : PropertiesContext<ApiKeyOptions>
{
	/// <inheritdoc />
	public ApiKeyChallengeContext(HttpContext context, AuthenticationScheme scheme, ApiKeyOptions options, AuthenticationProperties? properties) : base(context, scheme, options, properties) { }

	/// <summary>
	///     Cualquier falla que aparezca durante el proceso de autenticacion.
	/// </summary>
	public Exception? AuthenticateFailure { get; init; }

	/// <summary>
	///     El resultado <see cref="ApiKeyChallengeError" />.
	/// </summary>
	public ApiKeyChallengeError? Error { get; set; }

	/// <summary>
	///     Si es true, saltea toda la logica por defecto de este challenge.
	/// </summary>
	public bool Handled { get; private set; }

	/// <summary>
	///     Saltea toda la logica por defecto de este challenge.
	/// </summary>
	public void HandleResponse()
	{
		Handled = true;
	}
}
