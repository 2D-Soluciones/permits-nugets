namespace DDS.General.Logging.Javascript.SourceMaps;

internal sealed class Needle
{
	public int GeneratedColumn { get; set; }
	public int GeneratedLine { get; init; }

	public string? Name { get; set; }

	public int OriginalColumn { get; set; }
	public int OriginalLine { get; set; } = -1;
	public string? Source { get; set; }

	/// <summary>
	///     Indice de la fuente dentro de <c>sources</c>, o -1 si el mapping no tiene fuente. Se guarda ademas del
	///     nombre porque la URL absoluta de la fuente se resuelve por indice.
	/// </summary>
	public int SourceIndex { get; set; } = -1;
}
