using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DDS.General.Logging.Javascript.SourceMaps.Model;

[PublicAPI]
internal sealed class SourceMapSection
{
	[JsonPropertyName("map")]
	public SourceMapFile? Map { get; init; }

	/// <summary>
	///     Desde donde arranca esta seccion dentro del archivo generado.
	/// </summary>
	[JsonPropertyName("offset")]
	public SourceMapSectionOffset? Offset { get; init; }

	[JsonPropertyName("url")]
	public string? Url { get; init; }
}

/// <summary>
///     El desplazamiento de una seccion dentro del archivo generado. Ambos valores son base 0.
/// </summary>
[PublicAPI]
internal sealed class SourceMapSectionOffset
{
	[JsonPropertyName("line")]
	public int Line { get; init; }

	[JsonPropertyName("column")]
	public int Column { get; init; }
}
