using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;

namespace DDS.AspNetCore;

/// <summary>
///     Lee y parsea archivos .env, y actualiza las variables de entorno.
/// </summary>
[PublicAPI]
public static class DotEnv
{
	private static readonly char[] _doubleQuotes = ['"'];
	private static readonly char[] _singleQuote = ['\''];

	/// <summary>
	///     Carga un archivo .env y mete sus valores en el entorno. Solo funciona en modo DEBUG.
	/// </summary>
	/// <param name="filePath"></param>
	[Conditional("DEBUG")]
	public static void Load(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return;
		}

		Parse(new(File.ReadAllLines(filePath, Encoding.UTF8)), true);
	}

	private static bool HasNoKey(this ReadOnlySpan<char> row, out int index)
	{
		index = row.IndexOf('=');
		return index <= 0;
	}

	private static bool IsComment(this ReadOnlySpan<char> row)
	{
		return row[0] == '#';
	}

	private static bool IsQuoted(this ReadOnlySpan<char> row)
	{
		return (row.StartsWith(_singleQuote, StringComparison.Ordinal) && row.EndsWith(_singleQuote, StringComparison.Ordinal)) || (row.StartsWith(_doubleQuotes, StringComparison.Ordinal) && row.EndsWith(_doubleQuotes, StringComparison.Ordinal));
	}

	private static string Key(this ReadOnlySpan<char> row, int index)
	{
		return row[..index].Trim().ToString();
	}

	private static void Parse(ReadOnlySpan<string> dotEnvRows, bool trimValue)
	{
		foreach (var dotEnvRow in dotEnvRows)
		{
			var row = dotEnvRow.AsSpan().TrimStart();

			if (row.IsEmpty)
			{
				continue;
			}

			if (row.IsComment())
			{
				continue;
			}

			if (row.HasNoKey(out var index))
			{
				continue;
			}

			var key = row.Key(index);
			var value = row.Value(index, trimValue);
			Environment.SetEnvironmentVariable(key, value);
		}
	}

	private static ReadOnlySpan<char> StripQuotes(this ReadOnlySpan<char> row)
	{
		//un solo par: Trim se lleva todas las comillas de los extremos, asi que '''x''' perdia mas de lo que debia.
		//El llamador ya verifico con IsQuoted que el primero y el ultimo son la misma comilla.
		return row.Length >= 2 ? row[1..^1] : row;
	}

	private static string Value(this ReadOnlySpan<char> row, int index, bool trimValue)
	{
		var value = row[(index + 1)..];

		if (value.IsQuoted())
		{
			value = value.StripQuotes();
		}

		if (trimValue)
		{
			value = value.Trim();
		}

		return value.ToString();
	}
}
