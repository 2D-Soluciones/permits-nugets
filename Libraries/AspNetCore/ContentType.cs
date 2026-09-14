using JetBrains.Annotations;

namespace DDS.AspNetCore;

/// <summary>
///     Una lista de tipos de medios de internet, que son el identificador estandar que se usa en internet para indicar el tipo de
///     datos que tiene un archivo. Los browsers los usan para decidir como mostrar, emitir o manejar los archivos, y los
///     buscadores los usan para clasificar los archivos de datos de la web.
/// </summary>
[PublicAPI, NoReorder]
public static class ContentType
{
	// /// <summary>Atom feeds.</summary>
	// public const string Atom = "application/atom+xml";

	/// <summary>HTML; definido en RFC 2854.</summary>
	public const string Html = "text/html";

	/// <summary>Form URL Encoded.</summary>
	public const string FormUrlEncoded = "application/x-www-form-urlencoded";

	/// <summary>Imagen GIF; definida en RFC 2045 y RFC 2046.</summary>
	public const string Gif = "image/gif";

	/// <summary>Imagen JPEG JFIF; definida en RFC 2045 y RFC 2046.</summary>
	public const string Jpg = "image/jpeg";

	/// <summary>JavaScript Object Notation JSON; definido en RFC 4627.</summary>
	public const string Json = "application/json";

	/// <summary>JSON Patch; Defined at http://jsonpatch.com/.</summary>
	public const string JsonPatch = "application/json-patch+json";

	/// <summary>Web App Manifest.</summary>
	public const string Manifest = "application/manifest+json";

	/// <summary>Formulario multi-part; definido en RFC 2388.</summary>
	public const string MultipartFormData = "multipart/form-data";

	/// <summary>Portable Network Graphics; registrado,[8] definido en RFC 2083.</summary>
	public const string Png = "image/png";

	/// <summary>Problem Details JavaScript Object Notation (JSON); Defined at https://tools.ietf.org/html/rfc7807.</summary>
	public const string ProblemJson = "application/problem+json";

	// /// <summary>REST'ful JavaScript Object Notation (JSON); Defined at http://restfuljson.org/.</summary>
	// public const string RestfulJson = "application/vnd.restful+json";

	// /// <summary>Rich Site Summary; definido por Harvard Law.</summary>
	// public const string Rss = "application/rss+xml";

	/// <summary>Datos de texto; definidos en RFC 2046 y RFC 3676.</summary>
	public const string Text = "text/plain";

	/// <summary>Extensible Markup Language; definido en RFC 3023.</summary>
	public const string Xml = "application/xml";

	/// <summary>Compressed ZIP.</summary>
	public const string Zip = "application/zip";
}
