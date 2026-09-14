using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;

namespace DDS.AspNetCore.ApiKey;

/// <inheritdoc />
[PublicAPI]
public sealed class ApiKeyOptions : AuthenticationSchemeOptions
{
	/// <inheritdoc />
	public ApiKeyOptions()
	{
		Events = new();
	}

	/// <summary>
	///     Tamanio minimo, en bytes, de <see cref="Key" />.
	/// </summary>
	public const int MINIMUM_KEY_SIZE = 16;

	/// <summary>
	/// La clave para encriptar/desencriptar los tokens.
	/// </summary>
	public byte[] Key { get; set; } = [];

	/// <inheritdoc />
	public override void Validate()
	{
		base.Validate();

		// La clave es el unico secreto del esquema: la sal y el nonce viajan dentro del propio token.
		// Una clave corta o sin setear deja adivinable la clave AES derivada, con lo cual cualquiera podria fabricar una api key valida.
		if (Key.Length < MINIMUM_KEY_SIZE)
		{
			throw new InvalidOperationException($"{nameof(ApiKeyOptions)}.{nameof(Key)} must be at least {MINIMUM_KEY_SIZE} bytes long (was {Key.Length}).");
		}
	}

	/// <summary>
	/// </summary>
	public new ApiKeyEvents Events
	{
		get { return (base.Events as ApiKeyEvents)!; }
		set { base.Events = value; }
	}
}
