using System.Text.RegularExpressions;

namespace DDS.General.Logging.Javascript.SourceMaps.Util;

internal static class Url
{
	private const char SPLIT = '/';
	private static readonly Regex _backSlash = new(@"\/+$", RegexOptions.CultureInvariant);
	private static readonly Regex _dataUrlRegexp = new(@"^data:.+\,.+$", RegexOptions.CultureInvariant);
	private static readonly Regex _urlRegexp = new(@"^(?:([\w+\-.]+):)?\/\/(?:(\w+:\w+)@)?([\w.]*)(?::(\d+))?(\S*)$", RegexOptions.CultureInvariant);

	public static string Join(string aRoot, string aPath)
	{
		if (string.IsNullOrEmpty(aRoot))
		{
			aRoot = ".";
		}

		if (string.IsNullOrEmpty(aPath))
		{
			aPath = ".";
		}

		var aPathUrl = UrlParse(aPath);
		var aRootUrl = UrlParse(aRoot);

		if (aRootUrl != null)
		{
			aRoot = aRootUrl.Path ?? "/";
		}

		// `join(foo, '//www.example.org')`
		if (aPathUrl != null && string.IsNullOrEmpty(aPathUrl.Scheme))
		{
			if (aRootUrl != null)
			{
				aPathUrl.Scheme = aRootUrl.Scheme;
			}

			return aPathUrl.ToString();
		}

		if (aPathUrl != null || _dataUrlRegexp.Match(aPath).Success)
		{
			return aPath;
		}

		// `join('http://', 'www.example.com')`
		if (aRootUrl != null && string.IsNullOrEmpty(aRootUrl.Host) && string.IsNullOrEmpty(aRootUrl.Path))
		{
			aRootUrl.Host = aPath;
			return aRootUrl.ToString();
		}

		var joined = aPath[0] == '/' ? aPath : Normalize(_backSlash.Replace(aRoot, string.Empty) + "/" + aPath);

		if (aRootUrl == null)
		{
			return joined;
		}

		aRootUrl.Path = joined;
		return aRootUrl.ToString();
	}

	public static string Normalize(string aPath)
	{
		ArgumentNullException.ThrowIfNull(aPath);

		var path = aPath;
		var url = UrlParse(aPath);

		if (url != null)
		{
			if (url.Path == null)
			{
				return aPath;
			}

			path = url.Path;
		}

		if (path.Length == 0)
		{
			//"sources": [""] es un source map perfectamente parseable, y aca reventaba con IndexOutOfRange
			return aPath;
		}

		var isAbsolute = path[0] == '/';

		var parts = new List<string>(path.Split(SPLIT));
		var up = 0;

		for (var i = parts.Count - 1; i >= 0; i--)
		{
			var part = parts[i];

			switch (part)
			{
				case ".":
					parts.RemoveAt(i);
					break;

				case "..":
					up++;
					break;

				default:
					if (up > 0)
					{
						if (string.IsNullOrEmpty(part))
						{
							// La primera parte queda vacia si la ruta es absoluta. Intentar ir
							// mas arriba de la raiz no hace nada. Asi que podemos sacar todos los '..'
							// justo despues de la raiz.
							while (up > 0)
							{
								parts.RemoveAt(i + 1);
								--up;
							}

							up = 0;
						}
						else
						{
							parts.RemoveAt(i);
							parts.RemoveAt(i);

							//parts.splice(i, 2);
							up--;
						}
					}

					break;
			}
		}

		path = string.Join("/", parts);

		if (string.IsNullOrEmpty(path))
		{
			path = isAbsolute ? "/" : ".";
		}

		if (url == null)
		{
			return path;
		}

		url.Path = path;
		return url.ToString();
	}


	/// <summary>
	///     Le saca el nombre de archivo a una URL, dejando el directorio que la contiene.
	/// </summary>
	/// <remarks>Equivale al <c>new URL(".", url)</c> que usa el trimFilename de Mozilla.</remarks>
	public static string TrimFilename(string url)
	{
		var lastSlash = url.LastIndexOf('/');

		return lastSlash < 0
			? string.Empty
			: url[..(lastSlash + 1)];
	}

	/// <summary>
	///     Calcula la URL de una fuente a partir del <paramref name="sourceRoot" /> del mapa, la entrada de
	///     <c>sources</c> y la URL desde la que se bajo el propio mapa.
	/// </summary>
	/// <remarks>
	///     Sin el <paramref name="sourceMapUrl" /> una entrada relativa tipo <c>../src/a.js</c> se devuelve tal cual y
	///     no sirve para nada. Resolverla contra la ubicacion del mapa es lo que la convierte en una URL usable, y es
	///     imprescindible cuando el mapa no vive al lado del .js al que corresponde.
	/// </remarks>
	public static string ComputeSourceUrl(string? sourceRoot, string? sourceUrl, string? sourceMapUrl)
	{
		// El spec dice que sourceRoot y las entradas de sources se "concatenan". Para que
		// sourceRoot="dir" + sources=["/a.js"] se comporte como "dir/a.js", a la entrada absoluta se le saca la
		// barra inicial cuando hay sourceRoot.
		if (!string.IsNullOrEmpty(sourceRoot) && !string.IsNullOrEmpty(sourceUrl) && sourceUrl[0] == '/')
		{
			sourceUrl = sourceUrl[1..];
		}

		var url = Normalize(sourceUrl ?? string.Empty);

		if (!string.IsNullOrEmpty(sourceRoot))
		{
			url = Join(sourceRoot, url);
		}

		if (!string.IsNullOrEmpty(sourceMapUrl))
		{
			url = Join(TrimFilename(sourceMapUrl), url);
		}

		return url;
	}

	public static UrlInfo? UrlParse(string url)
	{
		var m = _urlRegexp.Match(url);

		if (m.Success)
		{
			return new()
			{
				Scheme = m.Groups[1].Value,
				Auth = m.Groups[2].Value,
				Host = m.Groups[3].Value,
				Port = m.Groups[4].Value,
				Path = m.Groups[5].Value
			};
		}

		return null;
	}
}

internal sealed class UrlInfo
{
	public string? Auth;
	public string? Host;
	public string? Path;
	public string? Port;
	public string? Scheme;

	public override string ToString()
	{
		var url = string.Empty;

		if (!string.IsNullOrEmpty(Scheme))
		{
			url += Scheme + ":";
		}

		url += "//";

		if (!string.IsNullOrEmpty(Auth))
		{
			url += Auth + "@";
		}

		if (!string.IsNullOrEmpty(Host))
		{
			url += Host;
		}

		if (!string.IsNullOrEmpty(Port))
		{
			url += ":" + Port;
		}

		if (!string.IsNullOrEmpty(Path))
		{
			url += Path;
		}

		return url;
	}
}
