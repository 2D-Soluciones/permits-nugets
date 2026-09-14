using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Vogen;

namespace DDS.AspNetCore.OpenApi;

internal sealed class VogenSchemaTransformer : IOpenApiSchemaTransformer
{
	/// <inheritdoc />
	public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
	{
		if (context.JsonTypeInfo.Type.GetCustomAttribute(typeof(ValueObjectAttribute<>)) is not { } attribute)
		{
			return Task.CompletedTask;
		}

		var primitiveType = attribute.GetType();
		var primitiveTypeSchema = primitiveType.GenericTypeArguments[0].MapTypeToOpenApiPrimitiveType();

		schema.Type = primitiveTypeSchema.Type;
		schema.Format = primitiveTypeSchema.Format;
		return Task.CompletedTask;
	}
}
