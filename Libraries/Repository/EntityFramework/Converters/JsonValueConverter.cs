using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DDS.Repository.EntityFramework.Converters;

internal sealed class JsonValueConverter<T> : ValueConverter<T?, string> where T : class
{
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	public JsonValueConverter() : base(value => Serialize(value), json => Deserialize(json)) { }

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static T? Deserialize(string json)
	{
		return JsonSerializer.Deserialize<T>(json, JsonValueConverterOptions.Defaults);
	}

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static string Serialize(T? value)
	{
		return JsonSerializer.Serialize(value, JsonValueConverterOptions.Defaults);
	}
}
