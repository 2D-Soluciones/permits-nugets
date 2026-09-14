using System.Reflection;
using System.Runtime.CompilerServices;
using DDS.AspNetCore.ApiKey;
using DDS.AspNetCore.OpenApi.Helpers;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

/// <summary>
///     Conjunto de metodos de extension para usar con <see cref="OpenApiOptions" />.
/// </summary>
[PublicAPI]
public static class OpenApiExtensions
{
	private static readonly ConditionalWeakTable<OpenApiOptions, object> _securityTransformers = [];

	/// <summary>
	///     Agrega la definicion de seguridad para los servicios autenticados por api-key.
	/// </summary>
	/// <param name="options">Una instancia de <see cref="OpenApiOptions" />.</param>
	/// <returns>La instancia actual de <see cref="OpenApiOptions" />.</returns>
	public static OpenApiOptions AddApiKey(this OpenApiOptions options)
	{
		var definition = new OpenApiSecurityScheme
		{
			Name = ApiKeyDefaults.AuthenticationScheme,
			Description = "Please enter api key",
			Type = SecuritySchemeType.ApiKey,
			In = ParameterLocation.Header,
			Scheme = ApiKeyDefaults.AuthenticationScheme
		};

		return AddSecurityScheme(options, ApiKeyDefaults.AuthenticationScheme, definition);
	}


	/// <summary>
	///     Agrega la definicion de seguridad para los servicios autenticados por cookies.
	/// </summary>
	/// <param name="cookieName"></param>
	/// <param name="options">Una instancia de <see cref="OpenApiOptions" />.</param>
	/// <returns>La instancia actual de <see cref="OpenApiOptions" />.</returns>
	public static OpenApiOptions AddCookies(this OpenApiOptions options, string cookieName)
	{
		var definition = new OpenApiSecurityScheme
		{
			Name = cookieName,
			In = ParameterLocation.Cookie,
			Type = SecuritySchemeType.ApiKey,
			Scheme = CookieAuthenticationDefaults.AuthenticationScheme
		};

		return AddSecurityScheme(options, CookieAuthenticationDefaults.AuthenticationScheme, definition);
	}

	/// <summary>
	///     Le agrega al documento OpenAPI un titulo, una version y, opcionalmente, una descripcion.
	/// </summary>
	/// <param name="title"></param>
	/// <param name="version"></param>
	/// <param name="description"></param>
	/// <param name="options">Una instancia de <see cref="OpenApiOptions" />.</param>
	/// <returns>La instancia actual de <see cref="OpenApiOptions" />.</returns>
	public static OpenApiOptions AddDocumentInfo(this OpenApiOptions options, string title, string version, string? description = null)
	{
		return options.AddDocumentTransformer((document, _, _) =>
		{
			document.Info ??= new();
			document.Info.Title = title;
			document.Info.Version = version;
			document.Info.Description = description ?? string.Empty;

			return Task.CompletedTask;
		});
	}

	/// <summary>
	///     Registra un esquema de seguridad para el esquema de autenticacion indicado.
	/// </summary>
	/// <param name="options"></param>
	/// <param name="authenticationSchemeName"></param>
	/// <param name="securityScheme"></param>
	/// <returns></returns>
	public static OpenApiOptions AddSecurityScheme(OpenApiOptions options, string authenticationSchemeName, OpenApiSecurityScheme securityScheme)
	{
		var registry = OpenApiSecuritySchemes.For(options);
		registry.AddScheme(authenticationSchemeName, securityScheme);

		// Los transformers se registran una vez por OpenApiOptions, no una vez por proceso. Con el flag anterior -que
		// avisaba "documento nuevo", no "esquema nuevo"- un segundo host en el mismo proceso no registraba ninguno y
		// publicaba un documento sin securitySchemes, declarando que ningún endpoint pide autenticación.
		if (_securityTransformers.TryGetValue(options, out _))
		{
			return options;
		}

		_securityTransformers.Add(options, new());

		return options
			.AddOperationTransformer(new AuthorizationOperationTransformer(registry))
			.AddDocumentTransformer(new SecurityDocumentTransformer(registry));
	}

	/// <summary>
	///     Agrega soporte para reference types no anulables.
	///     Agrega filtros para arreglar los enums en el documento OpenAPI.
	///     Mapea long a string.
	///     Mapea <see cref="TimeSpan" />.
	/// </summary>
	/// <param name="options">Una instancia de <see cref="OpenApiOptions" />.</param>
	/// <returns>La instancia actual de <see cref="OpenApiOptions" />.</returns>
	public static OpenApiOptions AddSensibleDefaults(this OpenApiOptions options)
	{
		return options
			.AddSchemaTransformer<DefaultSchemaTransformer>()
			.AddSchemaTransformer<VogenSchemaTransformer>()
			.AddOperationTransformer<AddParameterDescriptionsTransformer>()
			.AddOperationTransformer<AddResponseDescriptionsTransformer>();
	}

	/// <summary>
	///     Agrega la documentacion XML de los <paramref name="assemblies" /> que se le pasen.
	/// </summary>
	/// <param name="assemblies"></param>
	/// <param name="options">Una instancia de <see cref="OpenApiOptions" />.</param>
	/// <returns></returns>
	public static OpenApiOptions AddXmlDocumentation(this OpenApiOptions options, params Assembly[] assemblies)
	{
		foreach (var assembly in assemblies)
		{
			var descriptions = new XmlDescriptionService(assembly);
			options.AddSchemaTransformer(new AddSchemaXmlDocumentationTransformer(assembly, descriptions));
			options.AddOperationTransformer(new AddOperationXmlDocumentationTransformer(descriptions));
		}

		return options;
	}
}
