using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDS.General.Json;

[SuppressMessage("AOT", "IL3050:Calling members annotated with \'RequiresDynamicCodeAttribute\' may break functionality when AOT compiling.")]
[SuppressMessage("Trimming", "IL2026:Members annotated with \'RequiresUnreferencedCodeAttribute\' require dynamic access otherwise can break functionality when trimming application code")]
internal sealed class ResultJsonConverter : JsonConverter<Result>
{
	private const string DATA = "Data";
	private const string ERROR = "Error";
	private const string STATUS = "Status";
	private const string TYPE = "$type";

	/// <inheritdoc />
	public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException();
		}

		string? error = null;
		IDictionary<string, object>? data = null;
		var status = HttpStatusCode.InternalServerError;

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				break;
			}

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException();
			}

			var propName = reader.GetString()?.ToUpperInvariant();
			reader.Read();

			switch (propName)
			{
				case null:
					throw new JsonException();

				case "ERROR":
					error = reader.GetString();
					break;

				case "STATUS":
					status = (HttpStatusCode) reader.GetInt32();
					break;

				case "DATA":
					data = (IDictionary<string, object>?) JsonSerializer.Deserialize(ref reader, typeof(IDictionary<string, object>), options);
					break;

				default:
					//sin esto, el próximo Read() entra en el objeto/array desconocido y su EndObject corta el loop
					reader.Skip();
					break;
			}
		}

		return error is null ? Result.Success : Result.FromError(new(error, status) {Data = data});
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		if (value.IsSucceeded)
		{
			writer.WriteString(TYPE, "succeeded");
			writer.WriteEndObject();
			return;
		}

		writer.WriteString(TYPE, "faulted");
		writer.WriteString(GetPropertyName(options, ERROR), value.Error.Error);
		writer.WriteNumber(GetPropertyName(options, STATUS), (int) value.Error.Status);

		if (value.Error.Data is not null)
		{
			writer.WritePropertyName(GetPropertyName(options, DATA));
			JsonSerializer.Serialize(writer, value.Error.Data, typeof(IDictionary<string, object>), options);
		}

		writer.WriteEndObject();
	}

	private static string GetPropertyName(JsonSerializerOptions options, string propertyName)
	{
		return options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
	}
}
