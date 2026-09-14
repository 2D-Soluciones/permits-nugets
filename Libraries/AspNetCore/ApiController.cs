using System.Diagnostics.CodeAnalysis;
using System.Net;
using DDS.General;
using JetBrains.Annotations;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DDS.AspNetCore;

/// <summary>
///     Controller base con metodos helper.
/// </summary>
[PublicAPI]
[ApiController]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
	private readonly ISender _sender;

	/// <summary>
	///     Inicializa una instancia nueva de <see cref="ApiController" />.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="logger"></param>
	protected ApiController(ISender sender, ILogger logger)
	{
		_sender = sender;
		Logger = logger;
	}

	/// <summary>
	///     La instancia de <see cref="ILogger" /> asociada a este controller.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	///     Convierte un <see cref="ErrorResult" /> en un <see cref="ProblemDetails" /> y devuelve el valor por el
	///     metodo Problem de este controller.
	/// </summary>
	/// <param name="result"></param>
	/// <param name="additionalData"></param>
	/// <returns></returns>
	protected IActionResult GetProblemDetails(ErrorResult result, params IEnumerable<KeyValuePair<string, object>> additionalData)
	{
		// Una copia, no el diccionario del ErrorResult: un ErrorResult static readonly -el patron habitual para un
		// "no encontrado" compartido- iba acumulando los datos de cada request y filtrandolos al siguiente.
		var extensions = new Dictionary<string, object>(result.Data?.Count ?? 0, StringComparer.OrdinalIgnoreCase);

		// Copiado clave por clave y no con el constructor de copia: el Data de un ErrorResult deserializado lo arma
		// ResultJsonConverter con el comparador por defecto, asi que puede traer "id" y "ID" a la vez y el
		// constructor de copia tira ArgumentException justo adentro del camino de error, devolviendo un 500.
		if (result.Data is not null)
		{
			foreach (var kvp in result.Data)
			{
				extensions[kvp.Key] = kvp.Value;
			}
		}

		foreach (var kvp in additionalData)
		{
			extensions[kvp.Key] = kvp.Value;
		}

		return Problem(result.Error, null, (int) result.Status, result.Status.ToString("G"), null, extensions!);
	}

	/// <summary>
	///     Determina si el ModelState tiene errores y, si los tiene, devuelve un <see cref="IActionResult" /> que sirve como
	///     resultado de una action.
	/// </summary>
	/// <param name="result"></param>
	/// <returns></returns>
	protected bool ModelHasErrors([NotNullWhen(true)] out IActionResult? result)
	{
		if (ModelState.IsValid)
		{
			result = null;
			return false;
		}

		result = GetProblemDetails(
			new(Utils.InvalidModelMessage(Request), HttpStatusCode.UnprocessableEntity),
			new KeyValuePair<string, object>(Utils.ERRORS_KEY, Utils.ModelStateToErrors(ModelState)));
		return true;
	}

	/// <summary>
	///     Igual que <see cref="ModelHasErrors" />, pero optimizado para metodos async (para no armar una maquina de estados).
	/// </summary>
	/// <param name="result"></param>
	/// <returns></returns>
	protected bool ModelHasErrorsTask([NotNullWhen(true)] out Task<IActionResult>? result)
	{
		if (!ModelHasErrors(out var r))
		{
			result = null;
			return false;
		}

		result = Task.FromResult(r);
		return true;
	}

	/// <summary>
	///     Corre un comando y, segun el estado del <see cref="Result" />, devuelve Ok o Problem.
	/// </summary>
	/// <param name="command"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunCommand<TCommand, TResponse>(TCommand command) where TCommand : class, ICommand<Result<TResponse>>
	{
		var result = await SendCommand<TCommand, TResponse>(command);
		return result.Match<IActionResult>(response => Ok(response), validationResult => GetProblemDetails(validationResult));
	}

	/// <summary>
	///     Corre un comando y, segun el estado del <see cref="Result" />, devuelve NoContent o Problem.
	/// </summary>
	/// <param name="command"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunCommand<TCommand>(TCommand command) where TCommand : class, ICommand<Result>
	{
		var result = await SendCommand(command).AsTask();
		return result.Match<IActionResult>(_ => NoContent(), validationResult => GetProblemDetails(validationResult));
	}

	/// <summary>
	///     Corre un comando que devuelve una cadena y, segun el estado del <see cref="Result" />, devuelve Content o
	///     Problem.
	/// </summary>
	/// <param name="command"></param>
	/// <param name="contentType"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunContentCommand<TCommand>(TCommand command, string? contentType = null) where TCommand : class, ICommand<Result<string>>
	{
		var result = await SendCommand<TCommand, string>(command);
		return result.Match<IActionResult>(x => Content(x, contentType ?? ContentType.Text), validationResult => GetProblemDetails(validationResult));
	}

	/// <summary>
	///     Corre una consulta y, segun el estado del <see cref="Result" />, devuelve Ok o Problem.
	/// </summary>
	/// <param name="query"></param>
	/// <typeparam name="TQuery"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunQuery<TQuery, TResponse>(TQuery query) where TQuery : class, IQuery<Result<TResponse>>
	{
		var result = await _sender.Send(query, HttpContext.RequestAborted);
		return result.Match<IActionResult>(response => Ok(response), validationResult => GetProblemDetails(validationResult));
	}

	/// <summary>
	///     Corre una consulta y, segun el estado del <see cref="Result" />, devuelve Ok o Problem.
	/// </summary>
	/// <param name="query"></param>
	/// <param name="contentType"></param>
	/// <typeparam name="TQuery"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunContentQuery<TQuery>(TQuery query, string? contentType = null) where TQuery : class, IQuery<Result<string>>
	{
		var result = await _sender.Send(query, HttpContext.RequestAborted);
		return result.Match<IActionResult>(response => Content(response, contentType ?? ContentType.Text), validationResult => GetProblemDetails(validationResult));
	}

	/// <summary>
	///     Corre un comando <see cref="IStreamCommand{T}" />. Siempre devuelve Ok, porque un stream command no se puede validar.
	/// </summary>
	/// <param name="command"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected IActionResult RunStreamCommand<TCommand, TResponse>(TCommand command) where TCommand : class, IStreamCommand<TResponse>
	{
		return Ok(SendStreamCommand<TCommand, TResponse>(command));
	}

	/// <summary>
	///     Corre un comando sin validar; siempre devuelve Ok. Como todo comando corre con
	///     <see cref="CancellationToken.None" />; ver <see cref="SendCommand{TCommand}" />.
	/// </summary>
	/// <param name="command"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunUnvalidatedCommand<TCommand, TResponse>(TCommand command) where TCommand : class, ICommand<TResponse>
	{
		return Ok(await _sender.Send(command, CancellationToken.None));
	}

	/// <summary>
	///     Corre una consulta sin validar; siempre devuelve Ok.
	/// </summary>
	/// <param name="query"></param>
	/// <typeparam name="TQuery"></typeparam>
	/// <typeparam name="TResult"></typeparam>
	/// <returns></returns>
	protected async Task<IActionResult> RunUnvalidatedQuery<TQuery, TResult>(TQuery query) where TQuery : class, IQuery<TResult> where TResult : class
	{
		return Ok(await _sender.Send(query, HttpContext.RequestAborted));
	}

	/// <summary>
	///     Manda un comando al <see cref="ISender" />. A diferencia de una consulta, un comando NO usa
	///     <see cref="HttpContext.RequestAborted" />: para cuando el cliente se va -pestana recargada,
	///     navegacion, red caida- el comando ya puede haber hecho un efecto externo irreversible, como una
	///     operacion confirmada y cobrada en un tercero. Cortarlo ahi deja ese cobro vivo afuera y nada escrito
	///     de este lado, ademas en silencio: ExceptionHandlerMiddleware se come el OperationCanceledException a
	///     nivel Debug antes de llegar a ningun handler propio. El comando termina siempre; lo unico que lo
	///     acota es el shutdown del host.
	/// </summary>
	/// <param name="command"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <returns></returns>
	protected ValueTask<Result> SendCommand<TCommand>(TCommand command) where TCommand : class, ICommand<Result>
	{
		return _sender.Send(command, CancellationToken.None);
	}

	/// <summary>
	///     Manda un comando al <see cref="ISender" /> con <see cref="CancellationToken.None" />; ver
	///     <see cref="SendCommand{TCommand}" />.
	/// </summary>
	/// <param name="command"></param>
	/// <typeparam name="TCommand"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected ValueTask<Result<TResponse>> SendCommand<TCommand, TResponse>(TCommand command) where TCommand : class, ICommand<Result<TResponse>>
	{
		return _sender.Send(command, CancellationToken.None);
	}

	/// <summary>
	///     Manda una consulta al <see cref="ISender" />, usando <see cref="HttpContext.RequestAborted" /> como
	///     <see cref="CancellationToken" />.
	/// </summary>
	/// <param name="query"></param>
	/// <typeparam name="TQuery"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected ValueTask<Result<TResponse>> SendQuery<TQuery, TResponse>(TQuery query) where TQuery : class, IQuery<Result<TResponse>>
	{
		return _sender.Send(query, HttpContext.RequestAborted);
	}

	/// <summary>
	///     Manda una consulta sin validar al <see cref="ISender" />, usando <see cref="HttpContext.RequestAborted" /> como
	///     <see cref="CancellationToken" />.
	/// </summary>
	/// <param name="query"></param>
	/// <typeparam name="TQuery"></typeparam>
	/// <typeparam name="TResponse"></typeparam>
	/// <returns></returns>
	protected ValueTask<TResponse> SendUnvalidatedQuery<TQuery, TResponse>(TQuery query) where TQuery : class, IQuery<TResponse>
	{
		return _sender.Send(query, HttpContext.RequestAborted);
	}

	private IAsyncEnumerable<TResponse> SendStreamCommand<TCommand, TResponse>(TCommand command) where TCommand : class, IStreamCommand<TResponse>
	{
		HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
		return _sender.CreateStream(command, HttpContext.RequestAborted);
	}
}
