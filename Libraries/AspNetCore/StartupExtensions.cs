using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Enrichment;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace DDS.AspNetCore;

/// <summary>
///     Metodos de extension pensados para configurar aplicaciones aspnetcore.
/// </summary>
[PublicAPI]
public static class StartupExtensions
{
	/// <param name="services">El <see cref="IServiceCollection" /> donde agregar el servicio.</param>
	extension(IServiceCollection services)
	{
		/// <summary>
		///     Agrega un <see cref="ILogEnricher" /> que junta informacion de los headers.
		/// </summary>
		/// <returns></returns>
		public IServiceCollection AddHttpLogEnricher()
		{
			return services.AddLogEnricher<HttpLogEnricher>();
		}

		/// <summary>
		///     Configura los forwarded headers del proxy reverso. Hay que combinarlo con <code>app.UseForwardedHeaders()</code>
		/// </summary>
		/// <remarks>
		///     <paramref name="trustedNetworks" /> es obligatorio: los headers <c>X-Forwarded-*</c> se creen solo cuando el
		///     peer inmediato cae dentro de alguna de estas redes. Aceptarlos de cualquier peer deja que un cliente falsifique su
		///     propia direccion, esquema y host, lo que rompe las listas blancas de IP, los rate limits, los logs de auditoria y la
		///     comparacion <c>Origin</c>/<c>Host</c> de <see cref="Antiforgery.AntiCsrfMiddleware" />.
		/// </remarks>
		/// <param name="trustedNetworks">Las redes desde las que se conecta el proxy reverso (por ejemplo <c>10.0.0.0/8</c>).</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"><paramref name="trustedNetworks" /> esta vacio.</exception>
		public IServiceCollection ConfigureForwardedHeaders(IReadOnlyCollection<IPNetwork> trustedNetworks)
		{
			ArgumentNullException.ThrowIfNull(trustedNetworks);

			if (trustedNetworks.Count == 0)
			{
				throw new ArgumentException("At least one trusted proxy network is required; forwarded headers from an untrusted peer are attacker-controlled.", nameof(trustedNetworks));
			}

			return services.Configure<ForwardedHeadersOptions>(options =>
			{
				options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
				options.KnownProxies.Clear();
				options.KnownIPNetworks.Clear();

				foreach (var network in trustedNetworks)
				{
					options.KnownIPNetworks.Add(network);
				}
			});
		}

		/// <summary>
		///     Registra el <typeparamref name="T" /> que se le pasa como objeto de configuracion, y valida todas sus propiedades.
		/// </summary>
		/// <param name="sectionName">El nombre de la seccion. Por defecto, "Application".</param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		/// <remarks>
		///     El valor que devuelve es una foto del arranque, leida de un provider descartable para que la configuracion pueda manejar el
		///     resto del armado. No es la instancia que va a resolver la aplicacion, asi que cambiarla despues no cambia
		///     nothing at runtime.
		/// </remarks>
		[RequiresDynamicCode("")]
		[RequiresUnreferencedCode("")]
		#pragma warning disable ASP0000 // BuildServiceProvider is intentional here to provide early configuration access
		public T GetCustomConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string sectionName = "Application") where T : class
		{
			services
				.AddSingleton(resolver => resolver.GetRequiredService<IOptions<T>>().Value)
				.AddOptions<T>()
				.BindConfiguration(sectionName)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			// Sin using: disposear el provider tambien dispone el T que acabamos de devolver y el llamador se queda con
			// un objeto muerto. Es un provider chico y de una sola vez, al arranque.
			return services.BuildServiceProvider().GetRequiredService<T>();
		}
		#pragma warning restore ASP0000
	}
}

