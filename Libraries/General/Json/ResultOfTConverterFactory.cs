using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDS.General.Json;

internal sealed class ResultOfTConverterFactory : JsonConverterFactory
{
	private const string SERIALIZATION_REQUIRES_DYNAMIC_CODE_MESSAGE = "JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.";

	[RequiresDynamicCode(SERIALIZATION_REQUIRES_DYNAMIC_CODE_MESSAGE)]
	public ResultOfTConverterFactory() { }

	/// <inheritdoc />
	public override bool CanConvert(Type typeToConvert)
	{
		return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
	}

	/// <inheritdoc />
	[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "The constructor has been annotated with RequiredDynamicCodeAttribute.")]
	public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		var valueType = typeToConvert.GetGenericArguments()[0];
		var converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
		return Activator.CreateInstance(converterType) as JsonConverter;
	}
}
