using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DDS.General.Json;

/// <summary>
///     Converter JSON para tipos polimorficos. El discriminador se escribe como propiedad, y su valor es el nombre del enum o su valor entero subyacente.
/// </summary>
/// <typeparam name="TBase"></typeparam>
/// <typeparam name="TDiscriminator"></typeparam>
[PublicAPI]
[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.")]
[RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.")]
public abstract class JsonPolymorphicConverter<TBase, TDiscriminator> : JsonConverter<TBase>
	where TBase : class
	where TDiscriminator : struct, Enum
{
	/// <inheritdoc />
	public override bool CanConvert(Type typeToConvert)
	{
		return typeToConvert == typeof(TBase);
	}

	/// <inheritdoc />
	public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}

		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException($"Expected StartObject, got {reader.TokenType}.");
		}

		using var doc = JsonDocument.ParseValue(ref reader);
		var root = doc.RootElement;

		var discriminatorName = GetPropertyName(options, DiscriminatorPropertyName);

		if (!root.TryGetProperty(discriminatorName, out var discEl))
		{
			throw new JsonException($"Missing discriminator property '{discriminatorName}'.");
		}

		TDiscriminator discriminator;

		if (UseStringDiscriminator)
		{
			if (discEl.ValueKind != JsonValueKind.String)
			{
				throw new JsonException($"Discriminator '{discriminatorName}' must be a string.");
			}

			var name = discEl.GetString()!;

			//TryParse también acepta "3", así que sin IsDefined un número disfrazado de nombre pasa como válido
			if (!Enum.TryParse(name, true, out discriminator) || !Enum.IsDefined(discriminator))
			{
				throw new JsonException($"Unknown discriminator value '{name}' for {typeof(TDiscriminator).Name}.");
			}
		}
		else
		{
			if (discEl.ValueKind != JsonValueKind.Number || !discEl.TryGetInt32(out var discInt))
			{
				throw new JsonException($"Discriminator '{discriminatorName}' must be an integer.");
			}

			discriminator = (TDiscriminator) Enum.ToObject(typeof(TDiscriminator), discInt);
		}

		var concreteType = GetTypeForDiscriminator(discriminator);

		if (!typeof(TBase).IsAssignableFrom(concreteType))
		{
			throw new JsonException($"Type {concreteType} is not assignable to {typeof(TBase)}.");
		}

		var typeInfo = options.GetTypeInfo(concreteType);
		return (TBase?) root.Deserialize(typeInfo);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
	{
		var concreteType = value.GetType();
		var discriminator = GetDiscriminatorForInstance(value);
		var discriminatorName = GetPropertyName(options, DiscriminatorPropertyName);

		writer.WriteStartObject();

		if (UseStringDiscriminator)
		{
			writer.WriteString(discriminatorName, discriminator.ToString());
		}
		else
		{
			//unboxear a int directo revienta con un enum de byte, short o long
			writer.WriteNumber(discriminatorName, Convert.ToInt32(discriminator, CultureInfo.InvariantCulture));
		}

		using var doc = JsonSerializer.SerializeToDocument(value, concreteType, options);

		foreach (var prop in doc.RootElement.EnumerateObject())
		{
			if (prop.NameEquals(discriminatorName))
			{
				continue;
			}

			prop.WriteTo(writer);
		}

		writer.WriteEndObject();
	}

	/// <summary>
	///     Nombre de la propiedad discriminadora.
	/// </summary>
	protected abstract string DiscriminatorPropertyName { get; }

	/// <summary>
	///     Cuando es true, el discriminador se escribe/lee como el nombre del enum.
	///     Cuando es false (el valor por defecto), como su valor entero subyacente.
	/// </summary>
	protected virtual bool UseStringDiscriminator
	{
		get { return false; }
	}

	/// <summary>
	///     Devuelve el valor del discriminador para el <paramref name="value" /> que se le pasa.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	protected abstract TDiscriminator GetDiscriminatorForInstance(TBase value);

	/// <summary>
	///     Devuelve el tipo concreto para el <paramref name="discriminator" /> que se le pasa.
	/// </summary>
	/// <param name="discriminator"></param>
	/// <returns></returns>
	protected abstract Type GetTypeForDiscriminator(TDiscriminator discriminator);

	private static string GetPropertyName(JsonSerializerOptions options, string propertyName)
	{
		return options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
	}
}
