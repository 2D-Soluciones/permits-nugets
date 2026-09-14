using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

[UsedImplicitly]
internal sealed class AuthorizationOperationTransformer : IOpenApiOperationTransformer
{
	private readonly OpenApiSecuritySchemes _securitySchemes;

	public AuthorizationOperationTransformer(OpenApiSecuritySchemes securitySchemes)
	{
		_securitySchemes = securitySchemes;
	}

	public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
	{
		var authenticationSchemeProvider = context.ApplicationServices.GetRequiredService<IAuthenticationSchemeProvider>();

		var authorizationMetadata = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().ToList();
		if (authorizationMetadata.Count == 0 || context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
		{
			return;
		}

		operation.Security ??= new List<OpenApiSecurityRequirement>();
		operation.Security.Clear();

		foreach (var authorization in authorizationMetadata)
		{
			var (requestedSchemeNames, namedExplicitly) = await GetRequestedSchemeNames(authorization, authenticationSchemeProvider);

			foreach (var requestedSchemeName in requestedSchemeNames)
			{
				if (!_securitySchemes.TryGetScheme(requestedSchemeName, out _))
				{
					// Un [Authorize] pelado cae en el scheme por defecto de la app, que no tiene por que estar
					// registrado en este documento. Omitirlo es lo correcto; tirar dejaba todo el /openapi/*.json en
					// 500. Solo se reclama cuando el atributo nombro el scheme, que ahi si es un error de config.
					if (namedExplicitly)
					{
						throw new InvalidOperationException($"{context.Description.ActionDescriptor.DisplayName} is decorated with AuthorizeAttribute with an invalid/non-registered an authentication scheme ({requestedSchemeName}).");
					}

					continue;
				}

				var securityInfos = new List<string>(3);

				if (!string.IsNullOrEmpty(authorization.Policy))
				{
					securityInfos.Add($"{nameof(AuthorizeAttribute.Policy)}:{authorization.Policy}");
				}

				if (!string.IsNullOrEmpty(authorization.Roles))
				{
					securityInfos.Add($"{nameof(AuthorizeAttribute.Roles)}:{authorization.Roles}");
				}

				securityInfos.Add($"{nameof(AuthorizeAttribute.AuthenticationSchemes)}:{requestedSchemeName}");
				operation.Security.Add(new()
				{
					[new(requestedSchemeName, context.Document)] = securityInfos
				});
			}
		}
	}

	private static async Task<(IReadOnlyList<string> Names, bool NamedExplicitly)> GetRequestedSchemeNames(IAuthorizeData authorization, IAuthenticationSchemeProvider authenticationSchemeProvider)
	{
		var schemes = authorization.AuthenticationSchemes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (schemes is {Length: > 0})
		{
			return (schemes, true);
		}

		var defaultScheme = await authenticationSchemeProvider.GetDefaultAuthenticateSchemeAsync()
			?? await authenticationSchemeProvider.GetDefaultChallengeSchemeAsync();

		return defaultScheme is null ? ([], false) : ([defaultScheme.Name], false);
	}
}
