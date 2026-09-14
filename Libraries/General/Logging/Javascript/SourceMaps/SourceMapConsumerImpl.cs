using DDS.General.Logging.Javascript.SourceMaps.Model;
using DDS.General.Logging.Javascript.SourceMaps.Util;
using Url = DDS.General.Logging.Javascript.SourceMaps.Util.Url;

namespace DDS.General.Logging.Javascript.SourceMaps;

internal abstract class SourceMapConsumerImpl : ISourceMapConsumer
{
	private readonly string? _mappings;
	protected readonly ArraySet _names;
	protected readonly string? _sourceRoot;
	protected readonly ArraySet _sources;
	protected readonly string[]? _sourcesContent;

	/// <summary>
	///     Las mismas fuentes que <see cref="_sources" /> pero ya resueltas contra el <c>sourceRoot</c> y contra la URL
	///     del propio mapa, en el mismo orden.
	/// </summary>
	protected readonly ArraySet _absoluteSources;

	private readonly string? _sourceMapUrl;
	private readonly Dictionary<string, int> _sourceLookupCache = new(StringComparer.Ordinal);

	private List<Needle>? _generatedMappings;
	private List<Needle>? _originalMappings;

	protected SourceMapConsumerImpl(SourceMapFile sourceMap, string? sourceMapUrl = null)
	{
		_names = new(sourceMap.Names, true);
		_mappings = sourceMap.Mappings;
		_sourceRoot = sourceMap.SourceRoot;
		_sourcesContent = sourceMap.SourcesContent;
		_sourceMapUrl = sourceMapUrl;

		//una entrada nula en "sources" no puede tirar: hay que conservar su posicion porque los mappings referencian
		//las fuentes por indice, asi que se reemplaza por vacio en vez de saltearla.
		_sources = new(sourceMap.Sources?.Select(x => Url.Normalize(x ?? string.Empty)), true);
		_absoluteSources = new(_sources.Select(x => Url.ComputeSourceUrl(_sourceRoot, x, _sourceMapUrl)), true);
	}

	/// <summary>
	///     Devuelve el indice de <paramref name="source" /> dentro de <c>sources</c>, o -1 si no esta.
	/// </summary>
	/// <remarks>
	///     Se prueba primero tomando la fuente como relativa al mapa y despues como relativa al <c>sourceRoot</c>,
	///     porque el llamador puede nombrarla de cualquiera de las dos formas. El resultado se cachea: resolver URLs
	///     es caro y las mismas fuentes se consultan una y otra vez.
	/// </remarks>
	protected int FindSourceIndex(string source)
	{
		if (_sourceLookupCache.TryGetValue(source, out var cached))
		{
			return cached;
		}

		var asMapRelative = Url.ComputeSourceUrl(null, source, _sourceMapUrl);

		if (_absoluteSources.Contains(asMapRelative))
		{
			var index = _absoluteSources.IndexOf(asMapRelative);
			_sourceLookupCache[source] = index;
			return index;
		}

		var asSourceRootRelative = Url.ComputeSourceUrl(_sourceRoot, source, _sourceMapUrl);

		if (_absoluteSources.Contains(asSourceRootRelative))
		{
			var index = _absoluteSources.IndexOf(asSourceRootRelative);
			_sourceLookupCache[source] = index;
			return index;
		}

		//los fallos no se cachean, para que el diccionario no crezca sin limite con nombres que no existen
		return -1;
	}

	public IEnumerable<Position> AllGeneratedPositionsFor(string source, int line, int column = -1)
	{
		var needle = new Needle
		{
			Source = source,
			OriginalLine = line,
			OriginalColumn = column == -1 ? 0 : column
		};

		var sourceIndex = FindSourceIndex(source);

		if (sourceIndex < 0)
		{
			return [];
		}

		needle.Source = _sources[sourceIndex];

		var mappings = new List<Position>(4);

		var index = FindMappingByOriginal(needle, OriginalMappings, SourceMapBias.LeastUpper);

		if (index < 0)
		{
			return mappings;
		}

		var originalCount = OriginalMappings.Count;
		var mapping = OriginalMappings[index];

		if (column == -1)
		{
			var originalLine = mapping.OriginalLine;

			// Itero hasta quedarme sin mappings, o hasta toparme con
			// un mapping de una linea distinta a la que encontramos. Como
			// los mappings estan ordenados, esto garantiza encontrar todos los mappings de
			// la linea que encontramos.
			// El indice se avanza y se chequea arriba: el port original leia OriginalMappings[++index] al final del
			// cuerpo, donde en JS un indice pasado del final da undefined y en C# tira IndexOutOfRange.
			while (index < originalCount)
			{
				mapping = OriginalMappings[index];

				if (mapping.OriginalLine != originalLine)
				{
					break;
				}

				mappings.Add(new()
				{
					Line = mapping.GeneratedLine,
					Column = mapping.GeneratedColumn

					//LastColumn = mapping.LastGeneratedColumn
				});

				++index;
			}
		}
		else
		{
			var originalColumn = mapping.OriginalColumn;

			// Itero hasta quedarme sin mappings, o hasta toparme con
			// un mapping de una linea distinta a la que estabamos buscando.
			// Como los mappings estan ordenados, esto garantiza encontrar todos los mappings de
			// la linea que buscamos.
			while (index < originalCount)
			{
				mapping = OriginalMappings[index];

				if (mapping.OriginalLine != line || mapping.OriginalColumn != originalColumn)
				{
					break;
				}

				mappings.Add(new()
				{
					Line = mapping.GeneratedLine,
					Column = mapping.GeneratedColumn

					//LastColumn = mapping.LastGeneratedColumn
				});

				++index;
			}
		}

		return mappings;
	}

	public abstract Position? GeneratedPositionFor(string source, int line, int column = 0, SourceMapBias bound = SourceMapBias.GreatestLower);
	public abstract Position? OriginalPositionFor(int line, int column = 0, SourceMapBias bound = SourceMapBias.GreatestLower);
	public abstract string? SourceContentFor(string? originalSource);

	protected List<Needle> GeneratedMappings
	{
		get
		{
			InitializeMappings();
			return _generatedMappings!;
		}
	}

	protected List<Needle> OriginalMappings
	{
		get
		{
			InitializeMappings();
			return _originalMappings!;
		}
	}

	private void InitializeMappings()
	{
		if (_generatedMappings != null)
		{
			return;
		}

		//las listas se crean igual: un mapa sin "mappings" es un mapa vacio, no un NRE en la primera busqueda.
		_generatedMappings = new(8);
		_originalMappings = new(8);

		if (_mappings is not null)
		{
			ParseMappings(_mappings);
		}
	}

	protected abstract void ParseMappings(string mappings);

	private static int BinarySearch(List<Needle> haystack, Needle needle, CompareByPosition comparer, SourceMapBias bound)
	{
		if (haystack.Count == 0)
		{
			return -1;
		}

		var index = RecursiveSearch(-1, haystack.Count, needle, haystack, comparer, bound);

		if (index < 0)
		{
			return -1;
		}

		// Encontramos el elemento exacto, o el mas cercano al
		// que buscamos. Igual, puede haber mas de uno asi
		// elemento. Hay que asegurarse de devolver siempre el menor de todos.
		while (index - 1 >= 0)
		{
			if (comparer.Compare(haystack[index], haystack[index - 1], true) != 0)
			{
				break;
			}

			--index;
		}

		return index;
	}

	protected static int FindMappingByGenerated(Needle needle, List<Needle> mappings, SourceMapBias bound)
	{
		if (needle.GeneratedLine <= 0)
		{
			throw new ArgumentException($"Line must be greater than or equal to 1, got {needle.GeneratedLine}");
		}

		if (needle.GeneratedColumn < 0)
		{
			throw new ArgumentException($"Column must be greater than or equal to 0, got {needle.GeneratedColumn}");
		}

		return BinarySearch(mappings, needle, new CompareByGeneratedPositions(), bound);
	}

	protected static int FindMappingByOriginal(Needle needle, List<Needle> mappings, SourceMapBias bound)
	{
		// Para devolver la posicion que buscamos, primero hay que encontrar el
		// mapping de la posicion dada y despues devolver la posicion opuesta a la que
		// apunta. Como los mappings estan ordenados, se puede usar busqueda binaria para
		// encontrar el mejor mapping.
		if (needle.OriginalLine <= 0)
		{
			throw new ArgumentException($"Line must be greater than or equal to 1, got {needle.OriginalLine}");
		}

		if (needle.OriginalColumn < 0)
		{
			throw new ArgumentException($"Column must be greater than or equal to 0, got {needle.OriginalColumn}");
		}

		return BinarySearch(mappings, needle, new CompareByOriginalPositions(), bound);
	}

	protected static bool NextCharIsMappingSeparator(string str, int index)
	{
		var c = str[index];
		return c == ';' || c == ',';
	}

	// ReSharper disable SuggestBaseTypeForParameter
	private static int RecursiveSearch(int aLow, int aHigh, Needle needle, List<Needle> haystack, CompareByPosition comparer, SourceMapBias bound)

		// ReSharper restore SuggestBaseTypeForParameter
	{
		while (true)
		{
			var mid = (int)(Math.Floor((double)(aHigh - aLow) / 2) + aLow);
			var cmp = comparer.Compare(needle, haystack[mid], true);

			if (cmp == 0)
			{
				// Encontre el elemento que buscaba.
				return mid;
			}

			if (cmp > 0)
			{
				// La aguja es mayor que haystack[mid].
				if (aHigh - mid > 1)
				{
					// El elemento esta en la mitad de arriba.
					aLow = mid;
					continue;
				}

				// La aguja exacta no aparecio en este pajar. Determino si
				// estamos en el caso de corte (3) o (2), y devuelvo lo que corresponda.
				if (bound == SourceMapBias.LeastUpper)
				{
					return aHigh < haystack.Count
						? aHigh
						: -1;
				}

				return mid;
			}

			// La aguja es menor que haystack[mid].
			if (mid - aLow > 1)
			{
				// El elemento esta en la mitad de abajo.
				aHigh = mid;
				continue;
			}

			// estamos en el caso de corte (3) o (2), y devuelvo lo que corresponda.
			if (bound == SourceMapBias.LeastUpper)
			{
				return mid;
			}

			return aLow < 0
				? -1
				: aLow;
		}
	}
}
