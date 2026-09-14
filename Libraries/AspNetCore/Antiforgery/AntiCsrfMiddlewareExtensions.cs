using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace DDS.AspNetCore.Antiforgery;

/// <summary>
///     Metodos de extension para agregar tokens antiforgery
/// </summary>
[PublicAPI]
public static class AntiCsrfMiddlewareExtensions
{
	/// <summary>
	///     Agrega el middleware anti-forgery al <see cref="IApplicationBuilder" />. Conviene ponerlo primero despues de
	///     <code>UseForwardedHeaders</code>.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static IApplicationBuilder UseAntiCsrf(this IApplicationBuilder builder, AntiCsrfOptions? options = null)
	{
		return builder.UseMiddleware<AntiCsrfMiddleware>(Options.Create(options ?? new AntiCsrfOptions()));
	}
}
