namespace DDS.General.Logging.Javascript.SourceMaps;

internal sealed class Position
{
	public int Column { get; init; }
	public int Line { get; init; }
	public string? Name { get; init; }
	public string? Source { get; init; }
}
