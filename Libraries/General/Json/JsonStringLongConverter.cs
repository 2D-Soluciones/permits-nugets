using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDS.General.Json;

internal sealed class JsonStringLongConverter : JsonConverter<long>
{
	/// <inheritdoc />
	public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			return reader.GetInt64();
		}

		if (reader.TokenType != JsonTokenType.String)
		{
			throw new JsonException();
		}

		var s = reader.GetString();

		if (string.IsNullOrEmpty(s))
		{
			throw new JsonException();
		}

		//JsonException y no Format/Overflow: STJ no envuelve esas dos, asi que un payload con basura salia como 500
		//en vez de 400. Este converter esta en el indice 0 de las opciones por defecto, o sea que gobierna todo long
		//de toda app que las use.
		return !long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? throw new JsonException($"'{s}' is not a valid {nameof(Int64)}.") : value;
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString("D", CultureInfo.InvariantCulture));
	}
}
