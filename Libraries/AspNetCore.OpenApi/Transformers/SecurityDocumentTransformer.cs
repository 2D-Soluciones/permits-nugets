using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

internal sealed class SecurityDocumentTransformer : IOpenApiDocumentTransformer
{
	private readonly OpenApiSecuritySchemes _securitySchemes;

	public SecurityDocumentTransformer(OpenApiSecuritySchemes securitySchemes)
	{
		_securitySchemes = securitySchemes;
	}

	public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		var authenticationSchemeProvider = context.ApplicationServices.GetRequiredService<IAuthenticationSchemeProvider>();
		var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();

		document.Components ??= new();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.OrdinalIgnoreCase);

		foreach (var authenticationScheme in authenticationSchemes)
		{
			if (_securitySchemes.TryGetScheme(authenticationScheme.Name, out var registered) && registered is not null)
			{
				document.Components.SecuritySchemes[authenticationScheme.Name] = registered;
			}
		}
	}
}
