using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace DDS.AspNetCore.ApiKey;

/// <inheritdoc />
[PublicAPI]
public sealed class ApiKeyResultContext : ResultContext<ApiKeyOptions>
{
	/// <inheritdoc />
	public ApiKeyResultContext(HttpContext context, AuthenticationScheme scheme, ApiKeyOptions options) : base(context, scheme, options) { }

	/// <summary>
	///     Api Key. Le da a la aplicacion la chance de traer una api-key desde otro lado.
	/// </summary>
	public string ApiKey { get; set; } = string.Empty;
}
