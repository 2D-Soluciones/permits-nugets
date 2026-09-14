using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JetBrains.Annotations;

namespace DDS.General.Json;

/// <summary>
/// </summary>
[PublicAPI]
public static class DefaultJsonSerializerOptions
{
	private static readonly Lazy<JsonSerializerOptions> _options = new(Initializer);

	/// <summary>
	///     La plantilla de la que copian todos los demas miembros. A proposito no lleva <c>MakeReadOnly()</c>: no trae
	///     <see cref="JsonSerializerOptions.TypeInfoResolver" /> para que la copia que entrega <see cref="Get" /> pueda
	///     ninguno, para poder tomar el que provea la aplicacion que la usa; congelarla obligaria a definir uno aca.
	/// </summary>
	private static JsonSerializerOptions Instance
	{
		get { return _options.Value; }
	}

	/// <summary>
	///     Devuelve una instancia nueva de <see cref="JsonSerializerOptions" /> ya cargada con los valores por defecto de DDS.
	/// </summary>
	/// <returns>Una instancia nueva de <see cref="JsonSerializerOptions" />.</returns>
	public static JsonSerializerOptions Get()
	{
		return new(Instance);
	}

	/// <summary>
	///     Mergea las opciones de configuracion por defecto que devuelve <see cref="Get" /> dentro de
	///     <paramref name="target" />.
	/// </summary>
	/// <param name="target">El <see cref="JsonSerializerOptions" /> destino.</param>
	public static void MergeDefaultOptions(this JsonSerializerOptions target)
	{
		MergeOptions(target, Instance);
	}

	/// <summary>
	///     Mergea el <paramref name="source" /> que se le pasa dentro de <paramref name="target" />.
	/// </summary>
	/// <param name="target">El <see cref="JsonSerializerOptions" /> destino.</param>
	/// <param name="source">El <see cref="JsonSerializerOptions" /> de origen.</param>
	public static void MergeOptions(this JsonSerializerOptions target, JsonSerializerOptions source)
	{
		if (target.IsReadOnly)
		{
			throw new InvalidOperationException("The supplied target JsonSerializerOptions is read-only.");
		}

		if (source.TypeInfoResolver is not null)
		{
			AddTypeResolverIfNotExists(target, source.TypeInfoResolver);
		}

		foreach (var resolver in source.TypeInfoResolverChain)
		{
			AddTypeResolverIfNotExists(target, resolver);
		}

		target.AllowTrailingCommas = source.AllowTrailingCommas;
		target.DefaultBufferSize = source.DefaultBufferSize;
		target.Encoder = source.Encoder;
		target.DictionaryKeyPolicy = source.DictionaryKeyPolicy;
		target.DefaultIgnoreCondition = source.DefaultIgnoreCondition;
		target.NumberHandling = source.NumberHandling;
		target.PreferredObjectCreationHandling = source.PreferredObjectCreationHandling;
		target.IgnoreReadOnlyProperties = source.IgnoreReadOnlyProperties;
		target.IgnoreReadOnlyFields = source.IgnoreReadOnlyFields;
		target.IncludeFields = source.IncludeFields;
		target.MaxDepth = source.MaxDepth;
		target.PropertyNamingPolicy = source.PropertyNamingPolicy;
		target.PropertyNameCaseInsensitive = source.PropertyNameCaseInsensitive;
		target.ReadCommentHandling = source.ReadCommentHandling;
		target.UnknownTypeHandling = source.UnknownTypeHandling;
		target.UnmappedMemberHandling = source.UnmappedMemberHandling;
		target.WriteIndented = source.WriteIndented;
		target.ReferenceHandler = source.ReferenceHandler;

		var converters = target.Converters;

		foreach (var converter in source.Converters)
		{
			if (converters.Contains(converter))
			{
				continue;
			}

			converters.Add(converter);
		}
	}

	private static void AddTypeResolverIfNotExists(JsonSerializerOptions target, IJsonTypeInfoResolver resolver)
	{
		if (target.TypeInfoResolverChain.Contains(resolver))
		{
			return;
		}

		target.TypeInfoResolverChain.Add(resolver);
	}

	private static JsonSerializerOptions Initializer()
	{
		var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
		{
			//sin escapado HTML-safe de < > & ' +. Correcto para application/json, y una via de XSS en cuanto esta
			//salida se incruste en una pagina: si eso puede pasar, el consumidor tiene que pisar el Encoder.
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
			RespectNullableAnnotations = true,
			RespectRequiredConstructorParameters = true,
			UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
		};

		options.Converters.Insert(0, new JsonStringLongConverter());
		return options;
	}
}
