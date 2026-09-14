using System.Diagnostics;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace DDS.AspNetCore;

/// <summary>
///     Metodos de extension para requests/responses HTTP.
/// </summary>
[PublicAPI]
public static class HttpExtensions
{
	/// <param name="response"></param>
	extension(HttpResponse response)
	{

	}

	extension(HttpContext context)
	{
		internal Activity? GetActivity()
		{
			return context.Features.Get<IHttpActivityFeature>()?.Activity;
		}

		internal string GetRequestId()
		{
			return context.TraceIdentifier;
		}
	}
}

[UsedImplicitly]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class JsonContext : JsonSerializerContext;
