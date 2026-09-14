using System.Buffers;
using Corvus.Globbing;
using JetBrains.Annotations;

namespace DDS.AspNetCore.Antiforgery;

/// <summary>
///     Opciones para configurar la proteccion contra ataques cross-origin.
/// </summary>
[PublicAPI]
public sealed class AntiCsrfOptions
{
	/// <summary>
	///     Inicializa una instancia nueva de la clase <see cref="AntiCsrfOptions" />.
	/// </summary>
	public AntiCsrfOptions()
	{
		Enabled = true;
		IgnorePaths = [];
		TrustedOrigins = [];
	}

	/// <summary>
	///     <c>true</c> para habilitar la proteccion anti-forgery (por defecto). <c>false</c> si no.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	///     Ignora los controles anti-forgery en estas rutas.
	/// </summary>
	public IReadOnlyCollection<string> IgnorePaths { get; init; }

	/// <summary>
	/// Lista de origenes en los que se confia para hacer requests cross-origin sin
	/// validacion CSRF. Cada valor tiene que ser un origen completo, con esquema incluido,
	/// como por ejemplo <c>https://example.com</c>.
	/// </summary>
	public IReadOnlyCollection<string> TrustedOrigins { get; init; }

	private SearchValues<string> LazyTrustedOrigins
	{
		get { return field ??= SearchValues.Create(TrustedOrigins.ToArray(), StringComparison.OrdinalIgnoreCase); }
	}

	private ParsedGlobCache[] LazyIgnorePaths
	{
		get { return field ??= GetGlobs(IgnorePaths); }
	}

	internal bool IsTrustedOrigin(string origin)
	{
		return !string.IsNullOrEmpty(origin) && LazyTrustedOrigins.Contains(origin);
	}

	internal bool IsPathIgnored(string? path)
	{
		if (string.IsNullOrWhiteSpace(path) || LazyIgnorePaths.Length == 0)
		{
			return false;
		}

		return IsMatch(path, LazyIgnorePaths);
	}

	private static ParsedGlobCache[] GetGlobs(IReadOnlyCollection<string> globs)
	{
		if (globs.Count == 0)
		{
			return [];
		}

		var list = new List<ParsedGlobCache>(globs.Count);

		foreach (var glob in globs)
		{
			list.Add(new(glob));
		}

		return list.ToArray();
	}

	private static bool IsMatch(string path, ParsedGlobCache[] globs)
	{
		foreach (var glob in globs)
		{
			if (glob.Match(path))
			{
				return true;
			}
		}

		return false;
	}

	private readonly struct ParsedGlobCache
	{
		private readonly string _pattern;
		private readonly GlobToken[] _tokenizedGlob;

		public ParsedGlobCache(string pattern)
		{
			_pattern = pattern;

			var tokenizedGlob = new GlobToken[pattern.Length];
			var tokenCount = GlobTokenizer.Tokenize(pattern, tokenizedGlob);
			_tokenizedGlob = tokenizedGlob[..tokenCount];
		}

		public bool Match(string value)
		{
			return Glob.Match(_pattern, _tokenizedGlob, value, StringComparison.OrdinalIgnoreCase);
		}
	}
}
