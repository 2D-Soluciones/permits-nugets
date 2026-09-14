using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

internal sealed class AddParameterDescriptionsTransformer : IOpenApiOperationTransformer
{
	private readonly ConcurrentDictionary<MethodInfo, ParameterDescription[]> _methodParametersDescriptions = [];

	/// <inheritdoc />
	public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
	{
		if (operation.Parameters is {Count: > 0})
		{
			TryAddParameterDescriptions(operation.Parameters, context.Description);
		}

		return Task.CompletedTask;
	}

	private ParameterDescription[] GetMethodParameterDescriptions(ApiDescription description)
	{
		var method = description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().FirstOrDefault();
		return method is null ? [] : _methodParametersDescriptions.GetOrAdd(method, ValueFactory);
	}

	private void TryAddParameterDescriptions(IList<IOpenApiParameter> parameters, ApiDescription apiDescription)
	{
		var descriptions = GetMethodParameterDescriptions(apiDescription);

		if (descriptions is not {Length: > 0})
		{
			return;
		}

		foreach (var (argument, description) in descriptions)
		{
			if (description is null)
			{
				continue;
			}

			var parameter = parameters.FirstOrDefault(p => p.Name == argument.Name);
			parameter?.Description ??= description;
		}
	}

	private static ParameterDescription[] ValueFactory(MethodInfo p)
	{
		var parameters = p.GetParameters();
		var descriptions = new ParameterDescription[parameters.Length];

		for (var i = 0; i < parameters.Length; i++)
		{
			var parameter = parameters[i];
			var description = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;

			descriptions[i] = new(parameter, description);
		}

		return descriptions;
	}

	private sealed record ParameterDescription(ParameterInfo Parameter, string? Description);
}
