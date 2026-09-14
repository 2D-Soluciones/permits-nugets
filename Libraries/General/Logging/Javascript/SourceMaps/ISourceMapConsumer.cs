using JetBrains.Annotations;

namespace DDS.General.Logging.Javascript.SourceMaps;

[PublicAPI]
internal interface ISourceMapConsumer
{
	IEnumerable<Position> AllGeneratedPositionsFor(string source, int line, int column = -1);
	Position? GeneratedPositionFor(string source, int line, int column = 0, SourceMapBias bias = SourceMapBias.GreatestLower);
	Position? OriginalPositionFor(int line, int column = 0, SourceMapBias bias = SourceMapBias.GreatestLower);
	string? SourceContentFor(string? originalSource);
}