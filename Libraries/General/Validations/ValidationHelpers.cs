using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace DDS.General.Validations;

/// <summary>
///     Helpers para validar cosas.
/// </summary>
[PublicAPI]
public static partial class ValidationHelpers
{




	/// <summary>
	///     Determina si el <paramref name="value" /> que se le pasa es una direccion de mail valida.
	/// </summary>
	/// <param name="value"></param>
	/// <returns><c>true</c> si el mail es valido, <c>false</c> si no.</returns>
	public static bool IsValidEmail([NotNullWhen(true)] this string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		try
		{
			value = EmailDomainRegex().Replace(value, DomainMapper);
		}
		catch (RegexMatchTimeoutException)
		{
			return false;
		}
		catch (ArgumentException)
		{
			return false;
		}

		try
		{
			return EmailValidationRegex().IsMatch(value);
		}
		catch (RegexMatchTimeoutException)
		{
			return false;
		}
	}



	/// <summary>
	///     Verifica que un valor este en el rango valido de una latitud (-90...90).
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	public static bool IsValidLatitude(double value)
	{
		return value is >= -90 and <= 90;
	}

	/// <summary>
	///     Verifica que un valor este en el rango valido de una longitud (-180...180).
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	public static bool IsValidLongitude(double value)
	{
		return value is >= -180 and <= 180;
	}



	[GeneratedRegex("(@)(.+)$", RegexOptions.None, 200)]
	private static partial Regex EmailDomainRegex();

	[GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+\z", RegexOptions.IgnoreCase, 250)]
	private static partial Regex EmailValidationRegex();

	private static string DomainMapper(Match match)
	{
		// Uso IdnMapping para convertir los nombres de dominio Unicode.
		var idn = new IdnMapping();

		// Extraigo y proceso el nombre de dominio (tira ArgumentException si es invalido)
		var val = match.Groups[2].Value;
		var domainName = idn.GetAscii(val);

		return match.Groups[1].Value + domainName;
	}

}
