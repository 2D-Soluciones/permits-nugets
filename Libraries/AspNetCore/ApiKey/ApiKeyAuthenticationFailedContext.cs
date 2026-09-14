using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace DDS.AspNetCore.ApiKey;

/// <inheritdoc />
[PublicAPI]
public sealed class ApiKeyAuthenticationFailedContext : ResultContext<ApiKeyOptions>
{
	/// <inheritdoc />
	public ApiKeyAuthenticationFailedContext(HttpContext context, AuthenticationScheme scheme, ApiKeyOptions options) : base(context, scheme, options) { }

	/// <summary>
	///     La excepcion asociada a la falla de autenticacion.
	/// </summary>
	public Exception Exception { get; set; } = null!;
}
