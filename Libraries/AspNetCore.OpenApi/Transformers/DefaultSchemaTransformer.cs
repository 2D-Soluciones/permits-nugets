using DDS.General;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

internal sealed class DefaultSchemaTransformer : IOpenApiSchemaTransformer
{
	public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
	{
		var type = context.JsonTypeInfo.Type;
		var nullable = type.IsNullable();
		var realType = nullable ? Nullable.GetUnderlyingType(type) : type;

		if (realType == typeof(long))
		{
			schema.Type = JsonSchemaType.String | (nullable ? JsonSchemaType.Null : 0);
			schema.Format = "int64";
			return Task.CompletedTask;
		}

		if (realType == typeof(TimeSpan))
		{
			schema.Type = JsonSchemaType.String | (nullable ? JsonSchemaType.Null : 0);
			schema.Examples = ["d.hh:mm:ss"];
		}

		return Task.CompletedTask;
	}
}
