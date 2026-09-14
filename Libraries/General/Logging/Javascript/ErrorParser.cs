using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace DDS.General.Logging.Javascript;

//https://github.com/stacktracejs/error-stack-parser
internal static class ErrorParser
{
	// Todos estos patrones corren contra stack traces del browser que manda un atacante, asi que cada uno esta acotado.
	private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(250);

	private static readonly Regex _chromeIeStackRegexp = new(@"^\s*at .*(\S+\:\d+|\(native\))", RegexOptions.Multiline | RegexOptions.CultureInvariant, _timeout);

	private static readonly Regex _eval1 = new("eval code", RegexOptions.CultureInvariant, _timeout);
	private static readonly Regex _eval2 = new(@"(\(eval at [^\()]*)|(\,.*$)", RegexOptions.CultureInvariant, _timeout);
	private static readonly Regex _evalCode = new(@"\(eval code", RegexOptions.CultureInvariant, _timeout);

	private static readonly Regex _ffEval1 = new(@" line (\d+)(?: > eval line \d+)* > eval\:\d+\:\d+", RegexOptions.CultureInvariant, _timeout);
	private static readonly Regex _firefoxSafariStackRegexp = new(@"(^|@)\S+\:\d+", RegexOptions.CultureInvariant, _timeout);

	// Matchea el prefijo con el nombre de la funcion de un frame de Firefox/Safari, hasta el '@' que separa. Los
	// tramos entrecomillados se consumen enteros para que un nombre como foo("a@b") no se corte en el '@' de adentro.
	// Las dos alternativas arrancan con clases de caracteres disjuntas, que es lo que lo mantiene lineal: el patron
	// original ((.*".+"[^@]*)?[^@]*)(?:@) backtrackea exponencialmente con una linea larga entrecomillada y sin '@'.
	private static readonly Regex _functionNameRegex = new(@"^((?:[^@""]|""[^""]*"")*)@", RegexOptions.CultureInvariant, _timeout);

	private static readonly Regex _safariNativeCodeRegexp = new(@"^(eval@)?(\[native code\])?$", RegexOptions.CultureInvariant, _timeout);
	private static readonly Regex _tokens1 = new(@"^\s+", RegexOptions.CultureInvariant, _timeout);
	private static readonly Regex _tokens2 = new(@"\s+", RegexOptions.CultureInvariant, _timeout);

	private static readonly Regex _urlExtractor = new(@"(.+?)(?:\:(\d+))?(?:\:(\d+))?$", RegexOptions.CultureInvariant, _timeout);
	private static readonly Regex _urlReplacer = new(@"[\(\)]", RegexOptions.CultureInvariant, _timeout);

	public static IReadOnlyList<StackFrame>? ReadStackFrames(string? error)
	{
		if (string.IsNullOrEmpty(error))
		{
			return null;
		}

		// La entrada es lo que haya posteado un browser. Se enumera aca en vez de devolver una secuencia diferida, asi
		// un frame malformado falla en este metodo y no en el log del llamador; igual se loguea tal cual viene.
		try
		{
			if (_chromeIeStackRegexp.IsMatch(error))
			{
				return ParseV8OrExplorer(error).ToList();
			}

			return _firefoxSafariStackRegexp.IsMatch(error) ? ParseFirefoxOrSafari(error).ToList() : null;
		}
		catch (Exception ex) when (ex is RegexMatchTimeoutException or ArgumentOutOfRangeException or IndexOutOfRangeException)
		{
			return null;
		}
	}

	private static Location ExtractLocation(string urlLike)
	{
		var location = new Location {FileName = urlLike};

		if (!urlLike.Contains(':'))
		{
			return location;
		}

		urlLike = _urlReplacer.Replace(urlLike, string.Empty);
		var match = _urlExtractor.Match(urlLike).Groups;
		var matchCount = match.Count;

		if (matchCount <= 1)
		{
			return location;
		}

		location = location with
		{
			FileName = match[1].Value
		};

		if (matchCount <= 2)
		{
			return location;
		}

		if (int.TryParse(match[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationLine))
		{
			location = location with
			{
				Line = locationLine
			};
		}

		if (matchCount > 3 && int.TryParse(match[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationColumn))
		{
			location = location with
			{
				Column = locationColumn
			};
		}

		return location;
	}

	private static IEnumerable<string> GetLines(string error)
	{
		using var reader = new StringReader(error);

		while (true)
		{
			var line = reader.ReadLine();
			if (line is null)
			{
				yield break;
			}

			yield return line;
		}
	}

	private static IEnumerable<StackFrame> ParseFirefoxOrSafari(string error)
	{
		return GetLines(error).Where(x => !_safariNativeCodeRegexp.IsMatch(x)).Select(line =>
		{
			if (line.IndexOf(" > eval", StringComparison.Ordinal) > -1)
			{
				line = _ffEval1.Replace(line, ":$1");
			}

			if (!line.Contains('@') && !line.Contains(':'))
			{
				// Los frames de eval de Safari solo tienen el nombre de la funcion y nada mas
				return new()
				{
					OriginalFrame = line,
					FunctionName = line
				};
			}

			var matches = _functionNameRegex.Match(line);
			var functionName = matches.Success ? matches.Groups[1].Value : null;
			var locationParts = ExtractLocation(_functionNameRegex.Replace(line, string.Empty, 1));

			return new StackFrame
			{
				FunctionName = functionName,
				FileName = locationParts.FileName,
				LineNumber = locationParts.Line,
				ColumnNumber = locationParts.Column,
				OriginalFrame = line
			};
		});
	}

	private static IEnumerable<StackFrame> ParseV8OrExplorer(string error)
	{
		return GetLines(error).Where(x => _chromeIeStackRegexp.IsMatch(x)).Select(line =>
		{
			if (line.IndexOf("(eval ", StringComparison.Ordinal) > -1)
			{
				line = _eval1.Replace(line, "eval");
				line = _eval2.Replace(line, string.Empty);
			}

			var tokens = _tokens2.Split(_evalCode.Replace(_tokens1.Replace(line, string.Empty), "(")).Skip(1).ToList();
			var location = ExtractLocation(tokens[^1]);
			tokens.RemoveAt(tokens.Count - 1);
			var functionName = string.Join(" ", tokens);

			var fileName = location.FileName;

			if ("eval".Equals(fileName, StringComparison.Ordinal) || "<anonymous>".Equals(fileName, StringComparison.Ordinal))
			{
				fileName = null;
			}

			return new StackFrame
			{
				FunctionName = functionName,
				FileName = fileName,
				LineNumber = location.Line,
				ColumnNumber = location.Column,
				OriginalFrame = line
			};
		});
	}

	[NoReorder]
	private readonly record struct Location
	{
		public string FileName { get; init; }
		public int Line { get; init; }
		public int Column { get; init; }
	}
}
