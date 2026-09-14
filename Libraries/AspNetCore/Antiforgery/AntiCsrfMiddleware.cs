using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DDS.AspNetCore.Antiforgery;

/// <summary>
///     Previene CSRF, XSSI y filtraciones de informacion cross-origin.
/// </summary>
internal sealed class AntiCsrfMiddleware
{
	private static readonly SearchValues<string> _allowedMethods = SearchValues.Create(["GET", "OPTIONS", "HEAD"], StringComparison.OrdinalIgnoreCase);
	private static readonly SearchValues<string> _forbiddenFetchDestinations = SearchValues.Create(["object", "embed"], StringComparison.OrdinalIgnoreCase);

	private static readonly char[] _trimEnd = ['/'];

	private readonly ILogger<AntiCsrfMiddleware> _logger;
	private readonly RequestDelegate _next;
	private readonly AntiCsrfOptions _options;

	public AntiCsrfMiddleware(RequestDelegate next, IOptions<AntiCsrfOptions> options, ILogger<AntiCsrfMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(next);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		_next = next;
		_logger = logger;
		_options = options.Value;
	}

	public Task Invoke(HttpContext context)
	{
		// Cuando esta habilitada, la proteccion CSRF valida los requests asi:
		// 1. Los requests object/embed cross-origin se bloquean (proteccion XSSI).
		// 2. Los requests con "Sec-Fetch-Site: same-origin" o "none" se permiten.
		// 3. Los requests con "Sec-Fetch-Site: same-site" o "cross-site" se rechazan.
		// 4. Los requests sin headers de browser (clientes de API, curl, etc.) se permiten, porque CSRF es un vector exclusivo del browser.
		// 5. Para los browsers que no mandan "Sec-Fetch-Site", se compara el header "Origin" contra el host del request.

		if (!_options.Enabled)
		{
			_logger.LogAntiCsrfDisabled();
			return _next(context);
		}

		var responseHeaders = context.Response.Headers;

		// Le agrego Sec-Fetch-Site al header Vary. Asi el cacheo se comporta bien, porque
		// la respuesta puede variar segun ese header.
		if (responseHeaders.TryGetValue("Vary", out var vary))
		{
			responseHeaders.Vary = StringValues.Concat(vary, "Sec-Fetch-Site");
		}
		else
		{
			responseHeaders.Vary = "Sec-Fetch-Site";
		}

		if (TryValidateCsrf(context, out var errorMessage))
		{
			return _next(context);
		}

		_logger.LogAntiCsrfError(errorMessage);
		context.Response.StatusCode = 403;
		return context.Response.WriteAsync(errorMessage, context.RequestAborted);
	}

	private bool TryValidateCsrf(HttpContext context, [NotNullWhen(false)] out string? errorMessage)
	{
		var request = context.Request;
		var origin = GetHeader(request, "Origin");
		var path = request.Path.Value?.TrimEnd(_trimEnd);
		errorMessage = null;

		// XSSI/Resource isolation: block cross-origin object/embed requests.
		// Con eso se pueden filtrar datos o ejecutar scripts en un contexto cross-origin.
		var secFetchDest = GetHeader(request, "Sec-Fetch-Dest");
		var secFetchSite = GetHeader(request, "Sec-Fetch-Site");

		if (_forbiddenFetchDestinations.Contains(secFetchDest))
		{
			if (string.Equals(secFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase) || string.Equals(secFetchSite, "same-site", StringComparison.OrdinalIgnoreCase))
			{
				errorMessage = "Cross-origin object/embed request blocked";
				return false;
			}
		}

		if (_allowedMethods.Contains(request.Method) || _options.IsPathIgnored(path) || _options.IsTrustedOrigin(origin))
		{
			// Los metodos seguros siempre se permiten.
			// Las rutas ignoradas siempre se permiten.
			// Los origenes confiables se configuran como origenes completos (por ejemplo, https://example.com)
			return true;
		}

		if (string.Equals(secFetchSite, "none", StringComparison.OrdinalIgnoreCase) || string.Equals(secFetchSite, "same-origin", StringComparison.OrdinalIgnoreCase))
		{
			// Permito los requests same-origin y los iniciados por el browser ("same-origin", "none")
			return true;
		}

		if (!string.IsNullOrEmpty(secFetchSite))
		{
			// Rechazo cualquier otro tipo de request ("same-site" o "cross-site")
			errorMessage = "Cross-origin request detected from Sec-Fetch-Site header";
			return false;
		}

		if (string.IsNullOrEmpty(origin))
		{
			// No vino ni el header Sec-Fetch-Site ni el Origin.
			// O el request es same-origin, o no viene de un browser.
			return true;
		}

		if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUrl))
		{
			errorMessage = "Malformed Origin header";
			return false;
		}

		var host = request.Host.Value;
		var originAuthority = originUrl.IsDefaultPort
			? originUrl.Host
			: $"{originUrl.Host}:{originUrl.Port}";

		// El header Origin coincide con el Host. Ojo que el header Host no incluye el esquema,
		// asi que no sabemos si esto puede ser un request cross-origin de HTTP a HTTPS. Fallamos abierto, porque igual
		// los browsers modernos soportan Sec-Fetch-Site desde 2023, y usar uno mas viejo ya implica una
		// concesion de seguridad. Los sitios lo pueden mitigar con HTTP Strict Transport Security (HSTS).
		if (originAuthority.Equals(host, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		errorMessage = "Cross-origin request detected: Sec-Fetch-Site is missing, and Origin does not match Host";
		return false;
	}

	private static string GetHeader(HttpRequest request, string headerName)
	{
		if (request.Headers.TryGetValue(headerName, out var tokens) && tokens.Count > 0)
		{
			return tokens[0] ?? string.Empty;
		}

		return string.Empty;
	}
}
