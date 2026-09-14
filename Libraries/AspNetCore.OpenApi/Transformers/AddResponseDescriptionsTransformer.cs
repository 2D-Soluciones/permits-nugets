using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

internal sealed class AddResponseDescriptionsTransformer : IOpenApiOperationTransformer
{
	/// <inheritdoc />
	public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
	{
		foreach (var attribute in context.Description.ActionDescriptor.EndpointMetadata.OfType<OpenApiResponseAttribute>())
		{
			if (operation.Responses?.TryGetValue(attribute.HttpStatusCode.ToString(CultureInfo.InvariantCulture), out var response) == true)
			{
				response.Description = attribute.Description;
			}
		}

		return Task.CompletedTask;
	}
}
