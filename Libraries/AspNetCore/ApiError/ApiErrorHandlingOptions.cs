using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace DDS.AspNetCore;

/// <summary>
///     Opciones de <see cref="ApiErrorHandling" />.
/// </summary>
[PublicAPI]
public sealed class ApiErrorHandlingOptions
{
	internal Dictionary<Type, Func<Exception, ProblemDetails>>? _exceptionHandlers;

	/// <summary>
	///     Si el <see cref="ProblemDetails" /> que se le devuelve al cliente incluye tambien el mensaje de las excepciones
	///     que <b>no</b> mapeamos a un codigo propio, mas los mensajes de las inner exceptions.
	///     Cuando es <c>null</c> (el valor por defecto), eso pasa solo en el entorno de Development.
	/// </summary>
	/// <remarks>
	///     Las excepciones que mapeamos a un 4xx (<see cref="ArgumentException" />, <see cref="UnauthorizedAccessException" />,
	///     etc.) mandan su mensaje siempre, prendas o no esta opcion: ese texto lo escribio quien tiro la excepcion para que
	///     el cliente lo lea. Lo que esta opcion destapa es el resto -el 500 generico y las fallas de
	///     <see cref="HttpRequestException" />-, donde el mensaje lo escribio el runtime y suele llevar connection strings,
	///     rutas absolutas y pedazos del payload. Prendelo fuera de Development solo si la api no es alcanzable publicamente.
	/// </remarks>
	public bool? IncludeExceptionDetails { get; set; }

}
