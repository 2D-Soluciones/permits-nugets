using System.Buffers.Binary;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace DDS.AspNetCore.ApiKey;

internal sealed class ApiKeyHandler : AuthenticationHandler<ApiKeyOptions>
{
	private const string HEADER_NAME = "X-Api-Key";

	/// <summary>
	///     Inicializa una instancia nueva de <see cref="ApiKeyHandler" />.
	/// </summary>
	/// <inheritdoc />
	public ApiKeyHandler(IOptionsMonitor<ApiKeyOptions> options, ILoggerFactory logger, UrlEncoder encoder)
		: base(options, logger, encoder) { }

	/// <summary>
	///     El handler llama a metodos de los eventos que le dan control a la aplicacion en ciertos puntos donde el procesamiento
	///     occurring.
	///     Si no se provee, se usa una instancia por defecto que no hace nada cuando se llaman sus metodos.
	/// </summary>
	private new ApiKeyEvents Events
	{
		get { return (ApiKeyEvents) base.Events!; }
		// ReSharper disable once UnusedMember.Local
		set { base.Events = value; }
	}

	/// <inheritdoc />
	protected override Task<object> CreateEventsAsync()
	{
		return Task.FromResult<object>(new ApiKeyEvents());
	}

	/// <summary>
	///     Busca una api key en el header 'X-Api-Key'. Si la encuentra, la valida usando el
	///     metodo <see cref="ApiKeyEvents.OnValidatePrincipal" /> configurado en las opciones.
	/// </summary>
	/// <returns></returns>
	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		try
		{
			// Le doy a la aplicacion la chance de buscarla en otro lado, ajustarla o rechazar la api-key
			var messageReceivedContext = new ApiKeyResultContext(Context, Scheme, Options);

			// el evento puede setear la api-key
			await Events.OnMessageReceived(messageReceivedContext);

			// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (messageReceivedContext.Result != null)
			{
				Logger.LogApiKeyFromContext(messageReceivedContext.ApiKey.Length);
				return messageReceivedContext.Result;
			}

			// Si la aplicacion trajo el token de otro lado, uso ese.

			if (string.IsNullOrEmpty(messageReceivedContext.ApiKey))
			{
				messageReceivedContext.ApiKey = Request.Headers[HEADER_NAME].ToString().Trim();

				// If no api-key found, no further work possible
				if (string.IsNullOrEmpty(messageReceivedContext.ApiKey))
				{
					Logger.LogApiKeyNotFound();
					return AuthenticateResult.NoResult();
				}
			}

			messageReceivedContext.Principal = ReadPrincipal(messageReceivedContext.ApiKey, messageReceivedContext.Options.Key);
			await Events.OnValidatePrincipal(messageReceivedContext);

			if (messageReceivedContext.Principal != null)
			{
				return AuthenticateResult.Success(new(messageReceivedContext.Principal, messageReceivedContext.Properties, Scheme.Name));
			}

			Logger.LogInvalidApiKey(messageReceivedContext.ApiKey.Length);
			return AuthenticateResult.Fail("Invalid authentication.");
		}
		catch (Exception ex)
		{
			Logger.LogErrorRetrievingApiKey(ex);
			var authenticationFailedContext = new ApiKeyAuthenticationFailedContext(Context, Scheme, Options)
			{
				Exception = ex
			};

			await Events.OnAuthenticationFailed(authenticationFailedContext);
			return authenticationFailedContext.Result ?? AuthenticateResult.Fail("Invalid authentication.");
		}
	}

	/// <inheritdoc />
	protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		var authResult = await HandleAuthenticateOnceSafeAsync();

		var eventContext = new ApiKeyChallengeContext(Context, Scheme, Options, properties)
		{
			AuthenticateFailure = authResult.Failure
		};

		await Events.OnChallenge(eventContext);

		if (eventContext.Handled)
		{
			return;
		}

		Response.StatusCode = 401;

		if (eventContext.Error is null)
		{
			Response.Headers.Append(HeaderNames.WWWAuthenticate, ApiKeyDefaults.AuthenticationScheme);
		}
		else
		{
			// https://tools.ietf.org/html/rfc6750#section-3.1
			// WWW-Authenticate: Bearer realm="example", error="invalid_token", error_description="The access token expired"
			var builder = new StringBuilder(ApiKeyDefaults.AuthenticationScheme);

			if (!string.IsNullOrEmpty(eventContext.Error.Error))
			{
				HeaderQuoting.AppendQuoted(builder, " error=", eventContext.Error.Error);
			}

			if (!string.IsNullOrEmpty(eventContext.Error.Description))
			{
				if (!string.IsNullOrEmpty(eventContext.Error.Error))
				{
					builder.Append(',');
				}

				HeaderQuoting.AppendQuoted(builder, " error_description=", eventContext.Error.Description);
			}

			if (!string.IsNullOrEmpty(eventContext.Error.Uri))
			{
				if (!string.IsNullOrEmpty(eventContext.Error.Error) || !string.IsNullOrEmpty(eventContext.Error.Description))
				{
					builder.Append(',');
				}

				HeaderQuoting.AppendQuoted(builder, " error_uri=", eventContext.Error.Uri);
			}

			Response.Headers.Append(HeaderNames.WWWAuthenticate, builder.ToString());
		}
	}

	/// <inheritdoc />
	protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
	{
		var forbiddenContext = new ApiKeyForbiddenContext(Context, Scheme, Options);

		if (Response.StatusCode == 403)
		{
			// No-op
		}
		else if (Response.HasStarted)
		{
			Logger.ForbiddenResponseHasStarted();
		}
		else
		{
			Response.StatusCode = 403;
		}

		return Events.OnForbidden(forbiddenContext);
	}

	private const int PBKDF2_ITERATIONS = 100_000;
	private const byte FORMAT_VERSION = 1;
	private const int NONCE_SIZE = 12;
	private const int SALT_SIZE = 16;
	private const int TAG_SIZE = 16;

	private static byte[] Decrypt(ReadOnlySpan<byte> value, byte[] password)
	{
		const int HEADER_SIZE = 1 + NONCE_SIZE + SALT_SIZE + TAG_SIZE;
		if (value.Length <= HEADER_SIZE || value[0] != FORMAT_VERSION)
		{
			return [];
		}

		var nonce = value.Slice(1, NONCE_SIZE);
		var salt = value.Slice(1 + NONCE_SIZE, SALT_SIZE);
		var tag = value.Slice(1 + NONCE_SIZE + SALT_SIZE, TAG_SIZE);
		var ciphertext = value[HEADER_SIZE..];
		var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, PBKDF2_ITERATIONS, HashAlgorithmName.SHA256, 32);
		var plaintext = new byte[ciphertext.Length];

		try
		{
			using var aes = new AesGcm(key, TAG_SIZE);
			aes.Decrypt(nonce, ciphertext, tag, plaintext);
			return plaintext;
		}
		catch
		{
			CryptographicOperations.ZeroMemory(plaintext);
			return [];
		}
	}

	private static string? ReadString(ReadOnlySpan<byte> span, ref int index)
	{
		if (index + 2 > span.Length) return null;
		var stringLength = BinaryPrimitives.ReadInt16BigEndian(span[index..]);
		if (stringLength < 0 || index + 2 + stringLength > span.Length) return null;
		var start = index += 2;
		index += stringLength;
		return Encoding.UTF8.GetString(span[start..index]);
	}

	private ClaimsPrincipal? ReadPrincipal(string apiKey, byte[] password)
	{
		//TryFromBase64String: una cabecera que no es base64 -o dos X-Api-Key, que llegan unidas por coma- tiraba
		//FormatException, que se atrapaba y se registraba a nivel Error. Un cliente sin autenticar podia asi generar
		//volumen de errores a voluntad.
		var decoded = new byte[(apiKey.Length + 3) / 4 * 3];

		if (!Convert.TryFromBase64String(apiKey, decoded, out var decodedLength))
		{
			Logger.LogMalformedApiKey(apiKey.Length);
			return null;
		}

		var bytes = Decrypt(decoded.AsSpan(0, decodedLength), password).AsSpan();

		// Limito el tamanio maximo de los datos desencriptados para evitar ataques de DoS
		// Una api key razonable con claims no deberia pasar de 64KB
		const int MAX_DECRYPTED_SIZE = 64 * 1024;
		switch (bytes.Length)
		{
			case > MAX_DECRYPTED_SIZE:
				Logger.LogApiKeyDecryptedDataTooLarge(bytes.Length, MAX_DECRYPTED_SIZE);
				return null;

			case < 16:
				Logger.LogApiKeyDataTooSmall();
				return null;
		}

		//Utc explicito: los ticks se escriben en UTC, y sin el Kind la comparacion contra UtcNow es entre peras y
		//manzanas para cualquier host que no este en UTC.
		var expires = new DateTime(BinaryPrimitives.ReadInt64BigEndian(bytes), DateTimeKind.Utc);

		if (expires < DateTime.UtcNow)
		{
			Logger.LogApiKeyExpired(expires, DateTime.UtcNow);
			return null;
		}

		var index = 8;
		var user = ReadString(bytes, ref index);
		if (user is null) return null;

		if (index + 2 > bytes.Length) return null;
		var claims = BinaryPrimitives.ReadInt16BigEndian(bytes[index..]);
		if (claims < 0) return null;

		var identity = new ClaimsIdentity([
			new(ClaimTypes.NameIdentifier, user),
			new(ClaimTypes.Name, user)
		], ApiKeyDefaults.AuthenticationScheme);

		var principal = new ClaimsPrincipal(identity);

		if (claims == 0)
		{
			return principal;
		}

		index += 2;

		for (var i = 0; i < claims; ++i)
		{
			var claimType = ReadString(bytes, ref index);
			var claimValue = ReadString(bytes, ref index);
			if (claimType is null || claimValue is null) return null;
			identity.AddClaim(new(claimType, claimValue));
		}

		return principal;
	}
}
