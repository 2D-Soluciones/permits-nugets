using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using DDS.AspNetCore.OpenApi.Helpers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

internal sealed class AddSchemaXmlDocumentationTransformer : IOpenApiSchemaTransformer
{
	private readonly Assembly _assembly;
	private readonly XmlDescriptionService _service;

	public AddSchemaXmlDocumentationTransformer(Assembly assembly, XmlDescriptionService service)
	{
		_assembly = assembly;
		_service = service;
	}

	public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
	{
		if (schema.Description is null
			&& GetMemberName(context.JsonTypeInfo, context.JsonPropertyInfo) is {Length: > 0} memberName
			&& _service.GetDescription(memberName) is {Length: > 0} description)
		{
			schema.Description = description;
		}

		return Task.CompletedTask;
	}

	private string? GetMemberName(JsonTypeInfo typeInfo, JsonPropertyInfo? propertyInfo)
	{
		if (typeInfo.Type.Assembly != _assembly && propertyInfo?.DeclaringType?.Assembly != _assembly)
		{
			return null;
		}

		if (propertyInfo is null)
		{
			return $"T:{typeInfo.Type.FullName}";
		}

		var typeName = propertyInfo.DeclaringType?.FullName;
		if (typeName is null) return null;
		var memberName = propertyInfo.AttributeProvider is MemberInfo member ? member.Name : $"{char.ToUpperInvariant(propertyInfo.Name[0])}{propertyInfo.Name[1..]}";
		var memberType = propertyInfo.AttributeProvider is PropertyInfo ? "P" : "F";
		return $"{memberType}:{typeName}{Type.Delimiter}{memberName}";
	}
}
