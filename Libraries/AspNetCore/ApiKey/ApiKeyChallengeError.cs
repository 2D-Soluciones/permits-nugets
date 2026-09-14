using JetBrains.Annotations;

namespace DDS.AspNetCore.ApiKey;

/// <summary>
/// </summary>
/// <param name="Error">El valor "error" que se le devuelve al llamador dentro del header WWW-Authenticate.</param>
/// <param name="Description">El valor "error_description" que se le devuelve al llamador dentro del header WWW-Authenticate.</param>
/// <param name="Uri">El valor "error_uri" que se le devuelve al llamador dentro del header WWW-Authenticate.</param>
[PublicAPI]
public sealed record ApiKeyChallengeError(string Error, string Description, string? Uri);
