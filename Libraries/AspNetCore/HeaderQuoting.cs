using System.Text;

namespace DDS.AspNetCore;

internal static class HeaderQuoting
{
	/// <summary>
	///     Agrega <c>&lt;name&gt;"&lt;value&gt;"</c> como una quoted-string bien formada segun RFC 9110.
	/// </summary>
	/// <remarks>
	///     Concatenar el valor crudo permite que cualquier cosa que exponga entrada del usuario -una descripcion de error, un realm-
	///     cierre la comilla y agregue sus propios parametros. Kestrel bloquea CR/LF, asi que esto no es response splitting,
	///     pero igual queda un header que el llamador no escribio.
	/// </remarks>
	public static void AppendQuoted(StringBuilder builder, string name, string? value)
	{
		builder.Append(name).Append('"');

		foreach (var c in value ?? string.Empty)
		{
			//los controles no son representables dentro de una quoted-string
			if (char.IsControl(c))
			{
				continue;
			}

			if (c is '"' or '\\')
			{
				builder.Append('\\');
			}

			builder.Append(c);
		}

		builder.Append('"');
	}
}
