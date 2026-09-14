using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace DDS.General;

/// <summary>
///     Una excepcion que lleva adentro un <see cref="ProblemDetails" />
/// </summary>
[PublicAPI]
public sealed class ProblemDetailsException : Exception
{
	/// <summary>
	///     Inicializa una instancia nueva de <see cref="ProblemDetailsException" />.
	/// </summary>
	/// <param name="problemDetails"></param>
	public ProblemDetailsException(ProblemDetails problemDetails) : base(DetailsToMessage(problemDetails))
	{
		ProblemDetails = problemDetails;
	}

	/// <summary>
	///     A <see cref="ProblemDetails" />.
	/// </summary>
	public ProblemDetails ProblemDetails { get; }

	/// <summary>
	///     Metodo helper. Tira un <see cref="ProblemDetailsException" />.
	/// </summary>
	/// <param name="problemDetails"></param>
	/// <exception cref="ProblemDetailsException"></exception>
	[DoesNotReturn]
	public static void Throw(ProblemDetails problemDetails)
	{
		throw new ProblemDetailsException(problemDetails);
	}

	private static string DetailsToMessage(ProblemDetails details)
	{
		var sb = new StringBuilder(64);

		if (details.Status is not null)
		{
			sb.Append('[').Append(details.Status.Value.ToString(CultureInfo.InvariantCulture)).Append(']').Append(' ');
		}

		var hasTitle = false;

		if (!string.IsNullOrWhiteSpace(details.Title))
		{
			hasTitle = true;
			sb.Append(details.Title);
		}

		if (string.IsNullOrWhiteSpace(details.Detail))
		{
			return sb.ToString();
		}

		if (hasTitle)
		{
			sb.Append(':').Append(' ');
		}

		sb.Append(details.Detail);
		return sb.ToString();
	}
}
