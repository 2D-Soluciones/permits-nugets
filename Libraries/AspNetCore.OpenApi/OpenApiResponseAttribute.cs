using JetBrains.Annotations;

namespace DDS.AspNetCore.OpenApi;

/// <summary>
///     Representa la respuesta de una operacion de OpenAPI. De esta clase no se puede heredar.
/// </summary>
/// <param name="httpStatusCode">El codigo de estado HTTP de la respuesta.</param>
/// <param name="description">La descripcion de la respuesta.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
[PublicAPI]
public sealed class OpenApiResponseAttribute(int httpStatusCode, string description) : Attribute
{
	/// <summary>
	///     La descripcion de la respuesta.
	/// </summary>
	public string Description { get; } = description;

	/// <summary>
	///     El codigo de estado HTTP de la respuesta.
	/// </summary>
	public int HttpStatusCode { get; } = httpStatusCode;
}
