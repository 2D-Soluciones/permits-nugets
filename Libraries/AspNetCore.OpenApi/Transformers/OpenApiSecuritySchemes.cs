using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DDS.AspNetCore.OpenApi;

/// <summary>
///     Los esquemas de seguridad registrados para un <see cref="OpenApiOptions" />.
/// </summary>
/// <remarks>
///     Un diccionario estatico con el nombre del documento como unica clave es global al proceso: dos hosts que
///     publican un documento llamado igual -lo normal con "v1"- se mezclan los esquemas, y las entradas sobreviven a
///     los dos hosts. Colgar el registro del propio OpenApiOptions lo ata al host que lo configuro y lo deja
///     coleccionable con el.
/// </remarks>
internal sealed class OpenApiSecuritySchemes
{
	private static readonly ConditionalWeakTable<OpenApiOptions, OpenApiSecuritySchemes> _byOptions = [];

	private readonly ConcurrentDictionary<string, OpenApiSecurityScheme> _schemes = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	///     El registro de <paramref name="options" />, creado en el primer uso.
	/// </summary>
	public static OpenApiSecuritySchemes For(OpenApiOptions options)
	{
		return _byOptions.GetValue(options, static _ => new());
	}

	public void AddScheme(string schemeName, OpenApiSecurityScheme scheme)
	{
		_schemes[schemeName] = scheme;
	}

	public bool TryGetScheme(string schemeName, out OpenApiSecurityScheme? scheme)
	{
		return _schemes.TryGetValue(schemeName, out scheme);
	}
}
