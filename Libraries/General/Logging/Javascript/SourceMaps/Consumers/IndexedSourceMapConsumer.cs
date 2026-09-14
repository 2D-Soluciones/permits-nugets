using DDS.General.Logging.Javascript.SourceMaps.Model;

namespace DDS.General.Logging.Javascript.SourceMaps.Consumers;

/// <summary>
///     Consumidor de un mapa indexado, o sea uno armado por secciones (<c>sections</c>) en vez de por un unico bloque
///     de mappings. Cada seccion trae su propio mapa y el desplazamiento desde el que arranca dentro del archivo
///     generado.
/// </summary>
/// <remarks>
///     Es lo que emiten varios bundlers al concatenar salidas. Antes esto tiraba <see cref="NotSupportedException" />
///     desde el constructor, que ademas escapaba del logueo entero.
/// </remarks>
internal sealed class IndexedSourceMapConsumer : ISourceMapConsumer
{
	private readonly Section[] _sections;

	public IndexedSourceMapConsumer(SourceMapFile sourceMap, string? sourceMapUrl = null)
	{
		var sections = sourceMap.Sections ?? [];
		var result = new Section[sections.Length];
		var lastLine = -1;
		var lastColumn = 0;

		for (var i = 0; i < sections.Length; i++)
		{
			var section = sections[i];

			if (!string.IsNullOrEmpty(section.Url))
			{
				//bajar el mapa de la seccion pediria otra vuelta de red desde adentro del constructor
				throw new NotSupportedException("Support for url field in sections not implemented.");
			}

			if (section.Map is null)
			{
				throw new InvalidOperationException("\"map\" is a required argument of a section.");
			}

			var offset = section.Offset ?? throw new InvalidOperationException("\"offset\" is a required argument of a section.");

			if (offset.Line < lastLine || (offset.Line == lastLine && offset.Column < lastColumn))
			{
				throw new InvalidOperationException("Section offsets must be ordered and non-overlapping.");
			}

			lastLine = offset.Line;
			lastColumn = offset.Column;

			result[i] = new()
			{
				// Los offsets del archivo vienen en base 0, pero adentro se trabaja en base 1 igual que los mappings.
				GeneratedLine = offset.Line + 1,
				GeneratedColumn = offset.Column + 1,
				Consumer = new BasicSourceMapConsumer(section.Map, sourceMapUrl)
			};
		}

		_sections = result;
	}

	/// <inheritdoc />
	public IEnumerable<Position> AllGeneratedPositionsFor(string source, int line, int column = -1)
	{
		var positions = new List<Position>();

		foreach (var section in _sections)
		{
			if (section.Consumer.SourceContentFor(source) is null && section.Consumer.GeneratedPositionFor(source, line, column == -1 ? 0 : column) is null)
			{
				continue;
			}

			foreach (var position in section.Consumer.AllGeneratedPositionsFor(source, line, column))
			{
				positions.Add(Offset(section, position));
			}
		}

		return positions;
	}

	/// <inheritdoc />
	public Position? GeneratedPositionFor(string source, int line, int column = 0, SourceMapBias bias = SourceMapBias.GreatestLower)
	{
		foreach (var section in _sections)
		{
			var generated = section.Consumer.GeneratedPositionFor(source, line, column, bias);

			if (generated is not null)
			{
				return Offset(section, generated);
			}
		}

		return null;
	}

	/// <inheritdoc />
	public Position? OriginalPositionFor(int line, int column = 0, SourceMapBias bias = SourceMapBias.GreatestLower)
	{
		var section = FindSection(line, column);

		if (section is null)
		{
			return null;
		}

		// La posicion se traduce al sistema de coordenadas de la seccion. La columna solo se corre en la primera
		// linea de la seccion: de ahi para abajo la seccion arranca en la columna 0.
		return section.Consumer.OriginalPositionFor(
			line - (section.GeneratedLine - 1),
			column - (section.GeneratedLine == line ? section.GeneratedColumn - 1 : 0),
			bias);
	}

	/// <inheritdoc />
	public string? SourceContentFor(string? originalSource)
	{
		foreach (var section in _sections)
		{
			if (section.Consumer.SourceContentFor(originalSource) is { } content)
			{
				return content;
			}
		}

		return null;
	}

	/// <summary>
	///     La ultima seccion que arranca en la posicion pedida o antes.
	/// </summary>
	private Section? FindSection(int line, int column)
	{
		Section? found = null;

		foreach (var section in _sections)
		{
			if (section.GeneratedLine > line || (section.GeneratedLine == line && section.GeneratedColumn - 1 > column))
			{
				break;
			}

			found = section;
		}

		return found;
	}

	private static Position Offset(Section section, Position position)
	{
		return new()
		{
			Source = position.Source,
			Name = position.Name,
			Line = position.Line + (section.GeneratedLine - 1),
			Column = position.Column + (position.Line == 1 ? section.GeneratedColumn - 1 : 0)
		};
	}

	private sealed class Section
	{
		public required int GeneratedLine { get; init; }
		public required int GeneratedColumn { get; init; }
		public required ISourceMapConsumer Consumer { get; init; }
	}
}
