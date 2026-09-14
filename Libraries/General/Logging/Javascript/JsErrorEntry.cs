using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DDS.General.Logging.Javascript;

/// <summary>
///     Entrada normalizada del reporte de errores de JavaScript.
/// </summary>
[PublicAPI]
[NoReorder]
public sealed record JsErrorEntry
{
	/// <summary>
	///     La causa del error (si esta disponible)
	/// </summary>
	[JsonPropertyName("cause")]
	public string? Cause { get; init; }

	/// <summary>
	///     El mensaje de error
	/// </summary>
	[JsonPropertyName("message")]
	public string? Message { get; init; }

	/// <summary>
	///     El nombre del error
	/// </summary>
	[JsonPropertyName("name")]
	public string? Name { get; init; }

	/// <summary>
	///     El stack trace del error (si esta disponible)
	/// </summary>
	[JsonPropertyName("stack")]
	public string? Stack { get; init; }

	/// <summary>
	///     URL de la pagina donde ocurrio el error
	/// </summary>
	[JsonPropertyName("url")]
	public string? Url { get; set; }

	/// <summary>
	///     Full user agent string
	/// </summary>
	[JsonPropertyName("userAgent")]
	public string? UserAgent { get; set; }

	/// <summary>
	///     Ubicacion exacta del error (linea)
	/// </summary>
	[JsonPropertyName("lineNumber")]
	public int? LineNumber { get; set; }

	/// <summary>
	///     Ubicacion exacta del error (columna)
	/// </summary>
	[JsonPropertyName("columnNumber")]
	public int? ColumnNumber { get; set; }

	/// <summary>
	///     Client IP
	/// </summary>
	[JsonPropertyName("ipAddress")]
	public string? IpAddress { get; set; }

	/// <summary>
	///     Screen dimensions
	/// </summary>
	[JsonPropertyName("viewportSize")]
	public string? ViewportSize { get; set; }

	/// <summary>
	///     Si el browser se reporto como online (<c>navigator.onLine</c>) cuando ocurrio el error.
	/// </summary>
	[JsonPropertyName("online")]
	public bool? Online { get; set; }

	/// <summary>
	///     Tipo de conexion efectiva que reporta el browser (por ejemplo <c>4g</c>, <c>3g</c>, <c>slow-2g</c>).
	/// </summary>
	[JsonPropertyName("connectionType")]
	public string? ConnectionType { get; set; }

	/// <summary>
	///     Ancho de banda de bajada efectivo estimado, en megabits por segundo.
	/// </summary>
	[JsonPropertyName("downlink")]
	public float? Downlink { get; set; }

	/// <summary>
	///     Tiempo de ida y vuelta efectivo estimado de la conexion, en milisegundos.
	/// </summary>
	[JsonPropertyName("rtt")]
	public float? Rtt { get; set; }

	/// <summary>
	///     Codigo HTTP del request que fallo, o <c>0</c> si el request nunca llego a completarse (falla de red).
	/// </summary>
	[JsonPropertyName("httpStatus")]
	public int? HttpStatus { get; set; }

	/// <summary>
	///     URL del request que fallo.
	/// </summary>
	[JsonPropertyName("failedUrl")]
	public string? FailedUrl { get; set; }

	/// <summary>
	///     Metodo HTTP del request que fallo (por ejemplo <c>GET</c>, <c>POST</c>).
	/// </summary>
	[JsonPropertyName("failedMethod")]
	public string? FailedMethod { get; set; }

	/// <summary>
	///     Detalles adicionales de la falla (por ejemplo, el cuerpo de la respuesta o la descripcion del error).
	/// </summary>
	[JsonPropertyName("detail")]
	public string? Detail { get; set; }

	/// <inheritdoc />
	public override string ToString()
	{
		return Message ?? Name ?? string.Empty;
	}
}
