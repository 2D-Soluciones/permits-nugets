using System.Text.RegularExpressions;
using DDS.General.Logging.Javascript.SourceMaps.Model;
using DDS.General.Logging.Javascript.SourceMaps.Util;

namespace DDS.General.Logging.Javascript.SourceMaps.Consumers;

/// <summary>
///     Una instancia de BasicSourceMapConsumer representa un source map ya parseado, al que se le puede
///     consultar informacion de las posiciones en el archivo original pasandole una posicion
///     posicion en el fuente generado.
/// </summary>
internal sealed partial class BasicSourceMapConsumer : SourceMapConsumerImpl
{
	public BasicSourceMapConsumer(SourceMapFile sourceMap, string? sourceMapUrl = null)
		: base(sourceMap, sourceMapUrl) { }

	public override Position? GeneratedPositionFor(string source, int line, int column = 0, SourceMapBias bound = SourceMapBias.GreatestLower)
	{
		var needle = new Needle
		{
			OriginalLine = line,
			OriginalColumn = column,
			Source = source
		};

		var sourceIndex = FindSourceIndex(source);

		if (sourceIndex < 0)
		{
			return null;
		}

		needle.Source = _sources[sourceIndex];

		var index = FindMappingByOriginal(needle, OriginalMappings, bound);
		if (index < 0)
		{
			return null;
		}

		var mapping = OriginalMappings[index];
		if (mapping.Source == needle.Source)
		{
			return new()
			{
				Line = mapping.GeneratedLine,
				Column = mapping.GeneratedColumn
				//LastColumn= mapping.LastGeneratedColumn
			};
		}

		return null;
	}

	public override Position? OriginalPositionFor(int line, int column = 0, SourceMapBias bound = SourceMapBias.GreatestLower)
	{
		var needle = new Needle
		{
			GeneratedLine = line,
			GeneratedColumn = column
		};

		var index = FindMappingByGenerated(needle, GeneratedMappings, bound);

		if (index < 0)
		{
			return null;
		}

		var mapping = GeneratedMappings[index];

		if (mapping.GeneratedLine != needle.GeneratedLine)
		{
			return null;
		}

		//la fuente sale de _absoluteSources, que ya resolvio sourceRoot y la URL del mapa
		var source = mapping.SourceIndex >= 0 ? _absoluteSources[mapping.SourceIndex] : null;

		return new()
		{
			Source = source,
			Line = mapping.OriginalLine,
			Column = mapping.OriginalColumn,
			Name = mapping.Name
		};
	}

	public override string? SourceContentFor(string? originalSource)
	{
		if (_sourcesContent == null || originalSource == null)
		{
			return null;
		}

		var index = FindSourceIndex(originalSource);

		//sourcesContent puede venir mas corto que sources -es habitual que falte el contenido de alguna fuente-, y
		//antes indexarlo derecho tiraba IndexOutOfRange desde adentro del logueo.
		return index >= 0 && index < _sourcesContent.Length
			? _sourcesContent[index]
			: null;
	}

	protected override void ParseMappings(string mappings)
	{
		var generatedLine = 1;
		var previousGeneratedColumn = 0;
		var previousOriginalLine = 0;
		var previousOriginalColumn = 0;
		var previousSource = 0;
		var previousName = 0;
		var length = mappings.Length;
		var index = 0;
		var cachedValues = new Dictionary<string, List<int>>(512, StringComparer.Ordinal);
		while (index < length)
		{
			switch (mappings[index])
			{
				case ';':
					generatedLine++;
					++index;
					previousGeneratedColumn = 0;
					break;

				case ',':
					++index;
					break;

				default:
					var mapping = new Needle { GeneratedLine = generatedLine };

					// Como cada offset se codifica relativo al anterior,
					// muchos segmentos suelen tener la misma codificacion. Se puede aprovechar
					// eso cacheando los campos de largo variable ya parseados de cada segmento,
					// lo que nos evita un segundo parseo si nos volvemos a cruzar el mismo
					// segment again.
					int end;
					for (end = index; end < length; ++end)
					{
						if (NextCharIsMappingSeparator(mappings, end))
						{
							break;
						}
					}

					var str = mappings[index..end];
					if (cachedValues.TryGetValue(str, out var values))
					{
						index += str.Length;
					}
					else
					{
						values = new(16);
						while (index < end)
						{
							values.Add(Base64Vlq.Decode(mappings, ref index));
						}
						cachedValues[str] = values;
					}

					// Generated column.
					previousGeneratedColumn = mapping.GeneratedColumn = previousGeneratedColumn + values[0];

					if (values.Count > 1)
					{
						// Original source.
						previousSource += values[1];
						mapping.SourceIndex = previousSource;
						mapping.Source = _sources[previousSource];

						if (values.Count == 2)
						{
							throw new("Found a source, but no line and column");
						}

						// Linea original.
						previousOriginalLine = mapping.OriginalLine = previousOriginalLine + values[2];

						// Las lineas se guardan en base 0
						mapping.OriginalLine++;
						if (values.Count == 3)
						{
							throw new("Found a source and line, but no column");
						}

						// Original column.
						previousOriginalColumn = mapping.OriginalColumn = previousOriginalColumn + values[3];

						if (values.Count > 4)
						{
							// Nombre original.
							previousName += values[4];
							mapping.Name = _names[previousName];
						}
					}

					GeneratedMappings.Add(mapping);
					if (mapping.OriginalLine != -1)
					{
						OriginalMappings.Add(mapping);
					}
					break;
			}
		}

		GeneratedMappings.Sort(new CompareByGeneratedPositions());
		OriginalMappings.Sort(new CompareByOriginalPositions());
	}

}
