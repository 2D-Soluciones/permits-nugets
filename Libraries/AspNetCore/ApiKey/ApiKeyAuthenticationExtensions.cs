using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;

namespace DDS.AspNetCore.ApiKey;

/// <summary>
///     Metodos de extension para configurar la autenticacion X-Api-Key.
/// </summary>
[PublicAPI]
public static class ApiKeyAuthenticationExtensions
{
	/// <summary>
	///     Habilita la autenticacion X-Api-Key con el esquema indicado.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="key"></param>
	/// <returns></returns>
	public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder, byte[] key)
	{
		return AddApiKeyAuthentication(builder, options => { options.Key = key;});
	}

	/// <summary>
	///     Habilita la autenticacion X-Api-Key con el esquema indicado.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="configureOptions"></param>
	/// <returns></returns>
	public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder, Action<ApiKeyOptions> configureOptions)
	{
		return builder.AddScheme<ApiKeyOptions, ApiKeyHandler>(ApiKeyDefaults.AuthenticationScheme, "X-Api-Key authentication", configureOptions);
	}
}
