using DDS.General;
using JetBrains.Annotations;
using Vogen;

namespace DDS.Domain;

/// <summary>
///     Entity Tag.
/// </summary>
public interface IETag
{
	/// <summary>
	///     Identifica una version puntual de la entidad. La propiedad tiene que tener el setter privado.
	/// </summary>
	ETag ETag { get; }
}

/// <summary>
///     Entity Tag.
/// </summary>
[PublicAPI]
[ValueObject<string>]
[Instance("NotSet", "")]
public readonly partial struct ETag
{
	private const string ERROR = "The supplied etag is empty.";

	/// <summary>
	///     Crea un ETag nuevo.
	/// </summary>
	/// <remarks>El valor es una cadena de 12 caracteres.</remarks>
	/// <returns></returns>
	public static ETag New()
	{
		return From(TimeSortedId.Instance.Next().ToBase36());
	}

	private static Validation Validate(string input)
	{
		return string.IsNullOrWhiteSpace(input)
			? Validation.Invalid(ERROR)
			: Validation.Ok;
	}

	// ReSharper disable once ConvertToAutoProperty
	internal string ValueInternal
	{
		get { return _value ?? string.Empty; }
	}

	private static string NormalizeInput(string input)
	{
		return input.Trim();
	}
}
