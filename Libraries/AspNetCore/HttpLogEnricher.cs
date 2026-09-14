using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.Enrichment;

namespace DDS.AspNetCore;

[UsedImplicitly]
internal sealed class HttpLogEnricher : ILogEnricher
{
	private const int MAX_SESSION_ID_LENGTH = 128;

	private readonly IHttpContextAccessor _httpContextAccessor;

	public HttpLogEnricher(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	/// <inheritdoc />
	public void Enrich(IEnrichmentTagCollector collector)
	{
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext is null)
		{
			return;
		}

		if (httpContext.Request.Headers.TryGetValue(CustomHeaderNames.SESSION_ID, out var sessionId))
		{
			// Lo manda el cliente y no tiene tope: sin un limite, un header gigante se copia en cada
			// registro de log que produzca el request.
			var value = sessionId.ToString();
			collector.Add("SessionId", value.Length > MAX_SESSION_ID_LENGTH ? value[..MAX_SESSION_ID_LENGTH] : value);
		}
	}
}
