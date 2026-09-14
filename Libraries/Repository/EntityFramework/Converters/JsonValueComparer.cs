using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DDS.Repository.EntityFramework.Converters;

internal sealed class JsonValueComparer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] T> : ValueComparer<T?> where T : class
{
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	public JsonValueComparer() : base((t1, t2) => DoEquals(t1, t2), t => DoGetHashCode(t), t => DoGetSnapshot(t)) { }

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static bool DoEquals(T? left, T? right)
	{
		return left switch
		{
			null => right is null,
			IEquatable<T> equatable => equatable.Equals(right),
			_ => JsonSerializer.Serialize(left, JsonValueConverterOptions.Defaults).Equals(JsonSerializer.Serialize(right, JsonValueConverterOptions.Defaults), StringComparison.Ordinal)
		};
	}

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static int DoGetHashCode(T? instance)
	{
		if (instance is null)
		{
			return 0;
		}

		return instance is IEquatable<T>
			? instance.GetHashCode()
			: JsonSerializer.Serialize(instance, JsonValueConverterOptions.Defaults).GetHashCode();
	}

	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)"), RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static T? DoGetSnapshot(T? instance)
	{
		// A proposito no corta por ICloneable: esa interfaz no dice nada sobre la profundidad, y la implementacion
		// habitual es MemberwiseClone. Un snapshot superficial comparte sus objetos anidados con el valor vivo, asi que
		// mutar uno muta el otro, DoEquals los ve identicos y el cambio nunca se escribe.
		return instance is null
			? null
			: JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(instance, JsonValueConverterOptions.Defaults), JsonValueConverterOptions.Defaults)!;
	}
}
