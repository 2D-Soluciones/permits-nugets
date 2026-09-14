using DDS.General.Logging.Javascript.SourceMaps.Consumers;
using DDS.General.Logging.Javascript.SourceMaps.Model;

namespace DDS.General.Logging.Javascript.SourceMaps;

internal sealed class SourceMapConsumer
{
	/// <param name="sourceMap">El mapa ya deserializado.</param>
	/// <param name="sourceMapUrl">
	///     La URL desde la que se bajo el mapa. Es lo que permite resolver las entradas relativas de <c>sources</c>
	///     cuando el mapa no vive al lado del .js al que corresponde.
	/// </param>
	public SourceMapConsumer(SourceMapFile sourceMap, string? sourceMapUrl = null)
	{
		if (sourceMap.Version != 3)
		{
			throw new NotSupportedException($"Unsupported version: {sourceMap.Version}");
		}

		Consumer = sourceMap.Sections?.Length > 0
			? new IndexedSourceMapConsumer(sourceMap, sourceMapUrl)
			: new BasicSourceMapConsumer(sourceMap, sourceMapUrl);
	}

	public ISourceMapConsumer Consumer { get; }
}
