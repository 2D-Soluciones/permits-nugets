using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DDS.General.Logging.Javascript.SourceMaps.Model;

[PublicAPI]
internal sealed class SourceMapFile
{
	/// <summary>
	///     El nombre del archivo al que corresponde el mapa.
	/// </summary>
	[JsonPropertyName("file")]
	public string? File { get; set; }

	/// <summary>
	///     Los mappings codificados, que traducen la informacion del fuente generado a
	///     referencias a los archivos fuente originales.
	/// </summary>
	[JsonPropertyName("mappings")]
	public string? Mappings { get; init; }

	/// <summary>
	///     La coleccion de nombres de simbolos que usa Mappings.
	/// </summary>
	[JsonPropertyName("names")]
	public string[]? Names { get; init; }

	[JsonPropertyName("sections")]
	public SourceMapSection[]? Sections { get; init; }

	/// <summary>
	///     La ruta raiz de las fuentes listadas.
	/// </summary>
	[JsonPropertyName("sourceRoot")]
	public string? SourceRoot { get; init; }

	/// <summary>
	///     La coleccion de archivos fuente originales que participaron en la creacion del
	///     fuente generado al que corresponde el mapa.
	/// </summary>
	[JsonPropertyName("sources")]
	public string[]? Sources { get; init; }

	/// <summary>
	///     La coleccion de fuentes originales (cuando esas fuentes no estan publicadas).
	/// </summary>
	[JsonPropertyName("sourcesContent")]
	public string[]? SourcesContent { get; init; }

	/// <summary>
	///     Indices de <c>sources</c> que la herramienta marco como codigo de terceros (frameworks, polyfills). Sirve
	///     para no mostrar esos frames como si fueran del usuario.
	/// </summary>
	[JsonPropertyName("x_google_ignoreList")]
	public int[]? IgnoreList { get; init; }

	/// <summary>
	///     La version del formato de source map.
	/// </summary>
	[JsonPropertyName("version")]
	public int Version { get; init; }
}
