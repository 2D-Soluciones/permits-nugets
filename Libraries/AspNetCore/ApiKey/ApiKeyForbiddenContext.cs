using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace DDS.AspNetCore.ApiKey;

/// <inheritdoc />
[PublicAPI]
public class ApiKeyForbiddenContext : ResultContext<ApiKeyOptions>
{
	/// <inheritdoc />
	public ApiKeyForbiddenContext(HttpContext context, AuthenticationScheme scheme, ApiKeyOptions options) : base(context, scheme, options) { }
}
