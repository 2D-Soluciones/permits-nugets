using System.Collections.Concurrent;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;

namespace DDS.AspNetCore.OpenApi.Helpers;

internal sealed class XmlDescriptionService
{
	private readonly ConcurrentDictionary<string, string?> _descriptions = [];
	private readonly Lazy<XPathNavigator?> _navigator;

	public XmlDescriptionService(Assembly assembly)
	{
		// Lazy y no un chequeo a mano: dos threads pidiendo la primera descripción a la vez armaban cada uno su
		// XPathDocument y uno le pisaba el navigator al otro en el medio de una búsqueda.
		_navigator = new(() => CreateNavigator(assembly), LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public string? GetDescription(string memberName, string? parameterName = null, string? section = "summary")
	{
		//el prefijo separa los dos espacios de nombres: sin el, un parametro llamado "summary" pisaba el summary del
		//propio miembro.
		var cacheKey = memberName + (!string.IsNullOrEmpty(parameterName) ? $"/p:{parameterName}" : $"/s:{section}");
		if (_descriptions.TryGetValue(cacheKey, out var description))
		{
			return description;
		}

		// Un XPathNavigator es un cursor y SelectSingleNode lo mueve, así que compartir uno entre requests
		// concurrentes devuelve la descripción de otro miembro o revienta. Clone() da uno propio sobre el mismo
		// documento, que sí es de sólo lectura.
		if (_navigator.Value?.Clone() is not { } navigator)
		{
			return null;
		}

		var xmlPath = !string.IsNullOrEmpty(parameterName)
			? $"/doc/members/member[@name={XPathLiteral(memberName)}]/param[@name={XPathLiteral(parameterName)}]"
			: $"/doc/members/member[@name={XPathLiteral(memberName)}]/{section}";

		if (navigator.SelectSingleNode(xmlPath) is {Value.Length: > 0} node)
		{
			description = node.Value.Trim();
		}

		_descriptions[cacheKey] = description;

		return description;
	}

	/// <summary>
	///     Encomilla <paramref name="value" /> como literal de cadena de XPath.
	/// </summary>
	/// <remarks>
	///     Interpolarlo derecho dentro de <c>[@name='...']</c> significa que una comilla simple - que un nombre de
	///     binder <c>[FromQuery(Name = "...")]</c> puede tener perfectamente - tira XPathException y se lleva puesto
	///     todo el endpoint de OpenAPI con un 500. XPath 1.0 no tiene escapes, de ahi el concat().
	/// </remarks>
	private static string XPathLiteral(string value)
	{
		if (!value.Contains('\''))
		{
			return $"'{value}'";
		}

		if (!value.Contains('"'))
		{
			return $"\"{value}\"";
		}

		return "concat('" + value.Replace("'", "', \"'\", '", StringComparison.Ordinal) + "')";
	}

	private static XPathNavigator? CreateNavigator(Assembly assembly)
	{
		var path = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

		if (!File.Exists(path))
		{
			return null;
		}

		using var reader = XmlReader.Create(path);
		return new XPathDocument(reader).CreateNavigator();
	}
}
