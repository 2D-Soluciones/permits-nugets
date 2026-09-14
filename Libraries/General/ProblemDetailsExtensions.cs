using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace DDS.General;

/// <summary>
///     Metodos de extension para requests/responses HTTP.
/// </summary>
[PublicAPI]
public static class ProblemDetailsExtensions
{
	private const string JSON_PROBLEM_CONTENT_TYPE = "application/problem+json";

	/// <summary>
	///     Crea un <see cref="ProblemDetails" /> nuevo a partir de un mensaje <paramref name="response" />.
	/// </summary>
	/// <param name="response"></param>
	/// <returns></returns>
	public static ProblemDetails GetHttpError(HttpResponseMessage response)
	{
		return new()
		{
			Title = response.StatusCode.ToString("G"),
			Detail = $"[{response.RequestMessage?.Method ?? HttpMethod.Parse("Unknown")}] {response.RequestMessage?.RequestUri} => {response.StatusCode:G}.",
			Status = (int) response.StatusCode
		};
	}

	/// <summary>
	///     Devuelve un <see cref="ProblemDetails" /> con la informacion que saca del <paramref name="response" />. Ademas,
	///     lee el cuerpo de la respuesta (si hay) dentro de la propiedad <see cref="ProblemDetails.Extensions" />.
	/// </summary>
	/// <param name="response"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public static async Task<ProblemDetails> GetProblemDetails(this HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		var problemDetails = GetHttpError(response);
		var body = GetJsonBody(response, content);

		if (body is not null)
		{
			problemDetails.Extensions.Add("cause", body);
		}

		return problemDetails;
	}




	private static object? GetJsonBody(HttpResponseMessage response, string? content)
	{
		if (string.IsNullOrEmpty(content))
		{
			return null;
		}

		var contentType = response.Content.Headers.ContentType?.MediaType;

		if (string.IsNullOrEmpty(contentType))
		{
			return content;
		}

		if (JSON_PROBLEM_CONTENT_TYPE.Equals(contentType, StringComparison.OrdinalIgnoreCase))
		{
			var problemDetails = JsonSerializer.Deserialize(content, JsonContext.Default.ProblemDetails);
			return problemDetails is null ? content : problemDetails;
		}

		if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
		{
			return content;
		}

		// JsonNode y no JsonDocument: el documento es dueño de buffers alquilados del pool y acá terminaba dentro de
		// Extensions sin que nadie lo disposeara nunca.
		var jsonNode = JsonNode.Parse(content);
		return jsonNode ?? content;
	}
}

[UsedImplicitly]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class JsonContext : JsonSerializerContext;
