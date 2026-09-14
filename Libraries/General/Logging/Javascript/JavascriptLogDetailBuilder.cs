using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using DDS.General.Json;
using DDS.General.Logging.Javascript.SourceMaps;

namespace DDS.General.Logging.Javascript;

internal sealed partial class JavascriptLogDetailBuilder : IDisposable
{
	private const int MAX_RESPONSE_SIZE = 16 * 1024 * 1024;

	private static readonly Regex _sourceMapping = GetSourceMapSource();
	private readonly Uri _baseHost;
	private readonly HttpClient _httpClient;
	private readonly IEnumerable<StackFrame> _stackFrames;

	private Dictionary<Uri, Uri?>? _filesSourceMaps;
	private Dictionary<Uri, SourceMapConsumer?>? _sourceMaps;

	public JavascriptLogDetailBuilder(IEnumerable<StackFrame> stackFrames, Uri baseHost)
	{
		_stackFrames = stackFrames;
		_baseHost = baseHost;
		_httpClient = HttpClientFactory.CreateHttpClient();
		_httpClient.MaxResponseContentBufferSize = MAX_RESPONSE_SIZE;
		_httpClient.Timeout = TimeSpan.FromSeconds(10);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_httpClient.Dispose();
	}

	public async Task<string> GetStackTrace(CancellationToken cancellationToken)
	{
		var builder = new StringBuilder(128);

		foreach (var stackFrame in _stackFrames)
		{
			var transformedFrame = await Transform(stackFrame, cancellationToken);
			builder.AppendLine();
			builder.Append(transformedFrame);

			if (string.IsNullOrEmpty(transformedFrame.Source))
			{
				continue;
			}

			builder.AppendLine();
			builder.Append('\t');
			builder.Append('[');

			var frameString = GetFrameSource(transformedFrame);

			if (frameString is not null)
			{
				builder.Append(frameString);
			}

			builder.Append(']');
		}

		return builder.ToString();
	}

	private async Task<SourceMapConsumer?> GetSourceMapConsumer(Uri sourceMapLocation, CancellationToken cancellationToken)
	{
		_sourceMaps ??= new();

		if (_sourceMaps.TryGetValue(sourceMapLocation, out var consumer))
		{
			return consumer;
		}

		// Bajar, parsear y armar el consumidor entran todos en el mismo try. Antes el consumidor se construia afuera,
		// asi que un mapa que era JSON valido pero raro -una entrada nula en "sources", secciones, una version que no
		// soportamos- tiraba una excepcion que se escapaba del logueo entero. Un mapa que no se puede usar tiene que
		// degradar a "sin source map", igual que una descarga fallida: loguear nunca puede tirar.
		try
		{
			var content = await _httpClient.GetStringAsync(sourceMapLocation, cancellationToken);
			var sourceMap = JsonSerializer.Deserialize(StripXssiPrefix(content), GeneralJsonSerializerContext.Default.SourceMapFile);

			consumer = sourceMap is null ? null : new SourceMapConsumer(sourceMap, sourceMapLocation.ToString());
		}
		catch (Exception ex) when (IsDownloadFailure(ex, cancellationToken))
		{
			consumer = null;
		}

		_sourceMaps.Add(sourceMapLocation, consumer);
		return consumer;
	}

	private async Task<Uri?> GetSourceMapLocation(Uri fileLocation, CancellationToken cancellationToken)
	{
		_filesSourceMaps ??= new();

		if (_filesSourceMaps.TryGetValue(fileLocation, out var sourceMapLocation))
		{
			return sourceMapLocation;
		}

		string minifiedFile;

		try
		{
			minifiedFile = await _httpClient.GetStringAsync(fileLocation, cancellationToken);
		}
		catch (Exception ex) when (IsDownloadFailure(ex, cancellationToken))
		{
			minifiedFile = string.Empty;
		}

		if (string.IsNullOrEmpty(minifiedFile))
		{
			_filesSourceMaps.Add(fileLocation, null);
			return null;
		}

		TryGetFullUri(FindSourceMappingLocation(minifiedFile), fileLocation, out var fullUri);
		_filesSourceMaps.Add(fileLocation, fullUri);
		return fullUri;
	}

	private async Task<StackFrame> Transform(StackFrame frame, CancellationToken cancellationToken)
	{
		if (!TryGetFullUri(frame.FileName, out var fullUri))
		{
			return frame;
		}

		var sourceMapLocation = await GetSourceMapLocation(fullUri, cancellationToken);

		if (sourceMapLocation == null)
		{
			return frame;
		}

		var consumer = (await GetSourceMapConsumer(sourceMapLocation, cancellationToken))?.Consumer;
		var position = consumer?.OriginalPositionFor(frame.LineNumber, frame.ColumnNumber);

		if (position == null)
		{
			return frame;
		}

		return new()
		{
			OriginalFrame = frame.OriginalFrame,
			Source = consumer!.SourceContentFor(position.Source),
			FunctionName = position.Name ?? frame.FunctionName,
			FileName = position.Source ?? frame.FileName,
			LineNumber = position.Line,
			ColumnNumber = position.Column
		};
	}

	internal bool TryGetFullUri(string? possibleUri, [NotNullWhen(true)] out Uri? fullUri)
	{
		return TryGetFullUri(possibleUri, _baseHost, out fullUri);
	}

	/// <summary>
	///     Resuelve <paramref name="possibleUri" /> contra <paramref name="relativeTo" /> y exige que el resultado
	///     caiga en el origen configurado.
	/// </summary>
	/// <remarks>
	///     La base y el origen permitido son dos cosas distintas. Un <c>sourceMappingURL</c> relativo se resuelve
	///     contra el .js que lo declara -asi lo dice el spec-, no contra la raiz del sitio: con el mapa en un
	///     subdirectorio, <c>maps/app.js.map</c> dentro de <c>/js/app.js</c> es <c>/js/maps/app.js.map</c> y no
	///     <c>/maps/app.js.map</c>, que era lo que se pedia antes y daba 404 siempre.
	/// </remarks>
	private bool TryGetFullUri(string? possibleUri, Uri relativeTo, [NotNullWhen(true)] out Uri? fullUri)
	{
		fullUri = null;

		if (string.IsNullOrWhiteSpace(possibleUri))
		{
			return false;
		}

		if (!Uri.TryCreate(possibleUri, UriKind.Absolute, out var candidate)
			&& (!Uri.TryCreate(possibleUri, UriKind.Relative, out var relativeUri)
				|| !Uri.TryCreate(relativeTo, relativeUri, out candidate)))
		{
			return false;
		}

		// Estas URIs vienen de stack traces que manda el browser y del sourceMappingURL de lo que acabamos de
		// bajar. Bajar una URI arbitraria seria un SSRF, y la respuesta termina en el log, asi que la URI
		// *resuelta* tiene que caer en el origen de source maps configurado. Las relativas necesitan
		// el mismo control, no solo las absolutas: una network-path reference tipo "//host/x" no es
		// una URI absoluta, pero igual resuelve a otra autoridad.
		if (!IsSameOrigin(_baseHost, candidate))
		{
			return false;
		}

		fullUri = candidate;
		return true;
	}

	/// <summary>
	///     Si <paramref name="ex" /> significa "no se pudo bajar el source map" -que degrada a un stack trace sin
	///     mapear- y no que el llamador haya cancelado, que tiene que propagarse.
	/// </summary>
	/// <remarks>
	///     El timeout del HttpClient llega como TaskCanceledException aunque el token del llamador siga vivo, asi que
	///     filtrar por OperationCanceledException a secas dejaba que un origen lento tirara la request entera en vez
	///     de degradar a "no hay source map".
	/// </remarks>
	private static bool IsDownloadFailure(Exception ex, CancellationToken cancellationToken)
	{
		return ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
	}

	internal static bool IsSameOrigin(Uri baseHost, Uri uri)
	{
		return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
			&& uri.Scheme == baseHost.Scheme
			&& uri.Port == baseHost.Port
			&& string.Equals(uri.Host, baseHost.Host, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	///     Le saca el prefijo anti-XSSI (<c>)]}'</c> y lo que siga hasta el fin de linea) que el spec de source maps
	///     permite al principio del archivo.
	/// </summary>
	/// <remarks>
	///     Varias herramientas lo emiten. Sin sacarlo, el mapa no es JSON valido y el source map directamente no se
	///     podia usar nunca.
	/// </remarks>
	internal static string StripXssiPrefix(string content)
	{
		if (!content.StartsWith(")]}", StringComparison.Ordinal))
		{
			return content;
		}

		var newLine = content.IndexOf('\n');

		return newLine < 0 ? string.Empty : content[(newLine + 1)..];
	}

	private static string? FindSourceMappingLocation(string source)
	{
		var m = _sourceMapping.Match(source);

		return m.Success
			? m.Groups[1].Value
			: null;
	}

	private static string? GetFrameSource(StackFrame frame)
	{
		var lineNumber = frame.LineNumber;

		if (lineNumber == 0 || frame.Source == null)
		{
			return null;
		}

		using var reader = new StringReader(frame.Source);
		string? line = null;

		while (lineNumber > 0)
		{
			--lineNumber;
			line = reader.ReadLine();

			if (line is null)
			{
				break;
			}
		}

		return line;
	}

	[GeneratedRegex("\\/\\/[#@] ?sourceMappingURL=([^\\s'\"]+)", RegexOptions.CultureInvariant)]
	private static partial Regex GetSourceMapSource();
}
