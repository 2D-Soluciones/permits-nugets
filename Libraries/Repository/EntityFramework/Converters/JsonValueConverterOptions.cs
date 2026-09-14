using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DDS.Repository.EntityFramework.Converters;

/// <summary>
///     Clase que guarda las <see cref="JsonSerializerOptions">opciones</see> por defecto que usa el value converter JSON de
///     Value converter JSON.
/// </summary>
[PublicAPI]
public static class JsonValueConverterOptions
{
	private static JsonSerializerOptions? _defaults;

	/// <summary>
	///     Guarda una unica instancia de <see cref="JsonSerializerOptions" /> para que la use el value converter JSON de Entity
	///     converter.
	/// </summary>
	public static JsonSerializerOptions Defaults
	{
		get
		{
			var current = Volatile.Read(ref _defaults);
			if (current != null)
			{
				return current;
			}

			var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
			{
				DefaultIgnoreCondition = JsonIgnoreCondition.Never,
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				IgnoreReadOnlyProperties = false,
				PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
			};

			return Interlocked.CompareExchange(ref _defaults, options, null) ?? options;
		}
	}

}
