using System.Collections;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Un resultado en estado de error
/// </summary>
[PublicAPI]
[NoReorder]
public sealed record ErrorResult(string Error, HttpStatusCode Status = HttpStatusCode.InternalServerError)
{
	/// <summary>
	///     Cualquier dato adicional asignado a este error.
	/// </summary>
	[JsonExtensionData]
	public IDictionary<string, object>? Data { get; internal set; }

	/// <summary>
	///     Agrega un par clave/valor a la propiedad <see cref="Data" />.
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	public ErrorResult AddData(string key, object value)
	{
		Data ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		Data[key] = value;
		return this;
	}

	/// <inheritdoc />
	public override string ToString()
	{
		if (Data is not { Count: > 0 })
		{
			return $"[{Status:G}]: {Error}";
		}

		var sb = new StringBuilder($"[{Status:G}]: {Error}");
		foreach (var (key, value) in Data)
		{
			sb.Append(Environment.NewLine).Append(' ', 3).Append(key).Append(": ");
			AppendValue(sb, value, 1);
		}

		return sb.ToString();
	}

	private static void AppendValue(StringBuilder sb, object? value, int depth)
	{
		// ponytail: el tope de profundidad cubre los grafos ciclicos sin tener que llevar la cuenta de lo visitado
		if (depth > 8)
		{
			sb.Append('…');
			return;
		}

		switch (value)
		{
			case null:
				sb.Append("<null>");
				break;
			case string s:
				sb.Append(s);
				break;
			case IDictionary dictionary:
				foreach (DictionaryEntry entry in dictionary)
				{
					sb.Append(Environment.NewLine).Append(' ', (depth + 1) * 3).Append(entry.Key).Append(": ");
					AppendValue(sb, entry.Value, depth + 1);
				}

				break;
			case IEnumerable enumerable:
				var items = enumerable.Cast<object?>().ToList();
				if (items.TrueForAll(static i => i is null or string || i.GetType().IsValueType))
				{
					sb.Append('[').Append(string.Join(", ", items)).Append(']');
				}
				else
				{
					foreach (var item in items)
					{
						sb.Append(Environment.NewLine).Append(' ', (depth + 1) * 3).Append("- ");
						AppendValue(sb, item, depth + 1);
					}
				}

				break;
			default:
				sb.Append(value);
				break;
		}
	}

	/// <inheritdoc />
	public bool Equals(ErrorResult? other)
	{
		if (other is null)
		{
			return false;
		}

		return Error.Equals(other.Error, StringComparison.OrdinalIgnoreCase) && Status.Equals(other.Status);
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		//Equals compara Error con OrdinalIgnoreCase: hashear con mayúsculas y minúsculas distintas rompe el contrato
		//y dos errores "iguales" terminan en buckets distintos de cualquier HashSet o Dictionary.
		return HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Error), Status);
	}
}
