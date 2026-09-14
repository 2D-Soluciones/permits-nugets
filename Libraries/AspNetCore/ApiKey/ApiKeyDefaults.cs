using JetBrains.Annotations;

namespace DDS.AspNetCore.ApiKey;

/// <summary>
///     Valores por defecto para la autenticación de tipo "Basic".
/// </summary>
[PublicAPI]
public static class ApiKeyDefaults
{
	/// <summary>
	///     Valor por defecto de la propiedad AuthenticationScheme en <see cref="ApiKeyOptions" />.
	/// </summary>
	public const string AuthenticationScheme = "X-Api-Key";
}
