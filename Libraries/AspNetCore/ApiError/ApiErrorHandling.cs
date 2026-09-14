using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Security;
using DDS.General;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Vogen;

namespace DDS.AspNetCore;

/// <summary>
///     Manejo de errores propio para webapis.
/// </summary>
[PublicAPI]
public static class ApiErrorHandling
{
	private const string IS_EXCEPTION = "is-exception";
	private const string REQUEST_ID = "requestId";
	private const string TRACE_ID = "traceId";

	/// <summary>
	///     Agrega los servicios necesarios para crear <see cref="ProblemDetails" /> en los requests fallidos. Personaliza el
	///     <see cref="ProblemDetails" />
	///     con informacion adicional, como el request-id, el service-name y el trace-id.
	/// </summary>
	/// <param name="serviceCollection"></param>
	/// <param name="serviceName"></param>
	/// <returns></returns>
	public static IServiceCollection AddCustomProblemDetails(this IServiceCollection serviceCollection, string serviceName)
	{
		// El filtro de [ApiController] contesta un modelo invalido antes de que la action corra, asi que
		// ModelHasErrors no llega a verlo nunca: un json que no bindea salia como ValidationProblemDetails, sin
		// detail y con el texto en el title, o sea una tercera forma de error para el mismo cliente. Aca lo mandamos
		// por el mismo molde que usa el resto, con el detalle campo por campo donde ya estaba: bajo "errors".
		// PostConfigure y no Configure: ApiBehaviorOptionsSetup pisa el factory sin mirar si ya habia uno, asi que con
		// Configure el que ganaba dependia de si AddControllers se llamo antes o despues de este metodo.
		serviceCollection.PostConfigure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = InvalidModelStateResponse);

		return serviceCollection.AddProblemDetails(options =>
			options.CustomizeProblemDetails = ctx =>
			{
				ctx.ProblemDetails.Extensions.TryAdd(REQUEST_ID, ctx.HttpContext.GetRequestId());
				ctx.ProblemDetails.Instance = serviceName;
				var activity = ctx.HttpContext.GetActivity();

				if (activity is null)
				{
					return;
				}

				ctx.ProblemDetails.Extensions[TRACE_ID] = activity.SpanId.ToString();
			});
	}

	/// <summary>
	///     Registra un manejo de errores propio. Hay que combinarlo con <code>services.AddProblemDetails()</code>.
	/// </summary>
	/// <param name="app"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static IApplicationBuilder UseCustomErrorHandling(this IApplicationBuilder app, Action<ApiErrorHandlingOptions>? options = null)
	{
		app.Use((context, next) =>
		{
			if (context.Response.HasStarted)
			{
				return next();
			}

			WriteCustomHeaders(context, context.GetActivity());
			return next();
		});

		// Capturadas por closure y no en una estatica: dos hosts en el mismo proceso -o dos llamadas- se pisaban las
		// opciones entre si, y la ultima ganaba para todos.
		var handlingOptions = new ApiErrorHandlingOptions();
		options?.Invoke(handlingOptions);

		return app.UseExceptionHandler(new ExceptionHandlerOptions
		{
			ExceptionHandler = context => HandleExceptions(context, handlingOptions),
			// Un 404 nuestro es una respuesta elegida, no un handler que se perdio. Sin esto el middleware lo lee como
			// fracaso y re-tira la excepcion original -500 y log de unhandled- pero solo cuando el body todavia no
			// empezo, o sea cuando el cliente no acepta problem+json. Un bug que aparecia segun el header Accept.
			AllowStatusCode404Response = true,
			// ExceptionHandlerMiddleware loguea "An unhandled exception has occurred..." a Error por su cuenta,
			// antes de llamarnos: duplicaba cada error y ademas escribía justo los que LogLevelFor queria bajar de
			// tono. Aca somos la unica voz. Lo que se tira despues de empezar la respuesta lo sigue logueando el.
			SuppressDiagnosticsCallback = _ => true
		});
	}

	private static ProblemDetails ExceptionToProblemDetails(ApiErrorHandlingOptions options, Exception exception, Span<Exception> innerExceptions, bool includeDetails)
	{
		if (TryGetCustomDetails(options, exception, out var customDetails))
		{
			return customDetails;
		}

		// Las que mapeamos a un 4xx llevan su mensaje siempre: ese texto lo eligio quien escribio el throw para que el
		// cliente lo lea -es el mismo que sale cuando el error viaja como ErrorResult por GetProblemDetails, en vez de
		// tirarse-. Redactarlo dejaba al cliente con un 400 vacio y sin forma de saber que pidio mal.
		var message = Utils.GetExceptionMessage(exception);

		// Los inner los elige el runtime, no quien escribio el throw, asi que esos siguen siendo solo de Development.
		if (!includeDetails)
		{
			innerExceptions = [];
		}

		return exception switch
		{
			ProblemDetailsException pde => pde.ProblemDetails,
			//400 y no 412: un argumento invalido es un request mal armado, no una precondicion que fallo
			ArgumentException {ParamName: { } paramName} => FromException(message, innerExceptions, HttpStatusCode.BadRequest, new KeyValuePair<string, object?>("paramName", paramName)),
			ArgumentException => FromException(message, innerExceptions),
			UnauthorizedAccessException => FromException(message, innerExceptions, HttpStatusCode.Unauthorized),
			SecurityException => FromException(message, innerExceptions, HttpStatusCode.Forbidden),
			NotImplementedException => FromException(message, innerExceptions, HttpStatusCode.NotImplemented),
			OperationCanceledException => FromException(message, innerExceptions, (HttpStatusCode) 499),
			// El mensaje de una HttpRequestException nombra el host y el puerto del backend que fallo: eso es topologia
			// interna, no algo que el cliente haya hecho mal.
			HttpRequestException rx => FromException(includeDetails ? message : null, innerExceptions, rx.StatusCode ?? HttpStatusCode.InternalServerError),
			ValueObjectValidationException => FromException(message, innerExceptions, HttpStatusCode.PreconditionFailed),
			// La rama que no reconocemos. Ese mensaje lo escribio el runtime o una libreria de terceros, y ahi si
			// aparecen connection strings, rutas absolutas y pedazos del payload. Fuera de Development, status y nada mas.
			_ => FromException(includeDetails ? message : null, innerExceptions, HttpStatusCode.InternalServerError)
		};
	}

	private static bool ShouldIncludeExceptionDetails(ApiErrorHandlingOptions options, HttpContext context)
	{
		return options.IncludeExceptionDetails
			?? context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment()
			?? false;
	}

	private static ProblemDetails FromException(string? message, Span<Exception> innerExceptions, HttpStatusCode status = HttpStatusCode.BadRequest, KeyValuePair<string, object?>? errorDetail = null)
	{
		var details = new ProblemDetails
		{
			Title = status.ToString("G"),
			Status = (int) status,
			Detail = message
		};

		// Lo llevan todas, y el status no alcanza para deducirlo. En este repo un error de negocio viaja como
		// ErrorResult adentro de un Result: si algo se tiro, es porque no era un error de negocio. Eso es lo que la ui
		// lee aca -no reintentar, no es algo que el usuario pueda arreglar-, incluso en un 400: Permits2 convierte los
		// codigos que Integra no sabe explicar en un ArgumentException justamente para que salgan marcados.
		// La salida deliberada es ProblemDetailsException -o un handler propio-, que trae su ProblemDetails ya armado
		// y no pasa por aca: eso es un error elegido, y llega sin marcador.
		details.Extensions.Add(IS_EXCEPTION, true);

		if (errorDetail is not null)
		{
			details.Extensions.Add(errorDetail.Value);
		}

		if (innerExceptions.Length == 0)
		{
			return details;
		}

		var count = 0;

		foreach (var inner in innerExceptions)
		{
			details.Extensions.TryAdd($"cause--{++count}", Utils.GetExceptionMessage(inner));
		}

		return details;
	}

	private static IActionResult InvalidModelStateResponse(ActionContext context)
	{
		// Por el ProblemDetailsFactory y no armando el ProblemDetails a mano: es el mismo que usa el Problem() de
		// ApiController, asi que el type, el instance, el requestId y el traceId salen igual sin repetir nada.
		var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();

		var details = factory.CreateProblemDetails(
			context.HttpContext,
			StatusCodes.Status422UnprocessableEntity,
			HttpStatusCode.UnprocessableEntity.ToString("G"),
			detail: Utils.InvalidModelMessage(context.HttpContext.Request));

		details.Extensions[Utils.ERRORS_KEY] = Utils.ModelStateToErrors(context.ModelState);

		return new ObjectResult(details)
		{
			StatusCode = details.Status,
			ContentTypes = {MediaTypeNames.Application.ProblemJson}
		};
	}

	private static async Task HandleExceptions(HttpContext context, ApiErrorHandlingOptions options)
	{
		var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiError");
		var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
		var activity = context.GetActivity();
		var exception = exceptionHandlerFeature?.Error;

		if (context.RequestAborted.IsCancellationRequested)
		{
			logger.LogRequestCancelled(context.Request.Path);
			context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
			context.Response.ContentType = MediaTypeNames.Text.Plain;
			activity?.AddEvent(new("Client closed the request."));
			return;
		}

		if (exception is null)
		{
			logger.LogUnknownError();
			context.Response.StatusCode = StatusCodes.Status500InternalServerError;
			context.Response.ContentType = MediaTypeNames.Text.Plain;
			return;
		}

		var sessionId = context.Request.Headers[CustomHeaderNames.SESSION_ID];
		var exceptions = exception.UnwrapAndCollect().ToArray();
		var innerExceptions = exceptions.AsSpan()[1..];
		var parent = exceptions[0];
		var level = LogLevelFor(parent);
		var parentMessage = Utils.GetExceptionMessage(parent);

		if (StringValues.IsNullOrEmpty(sessionId))
		{
			logger.LogApiCallError(level, parent, parentMessage);
		}
		else
		{
			logger.LogApiCallWithSessionError(level, parent, parentMessage, sessionId!);
		}

		activity?.AddException(parent);
		activity?.SetTag("error.type", parent.GetType().FullName);

		// Solo lo que realmente es un error del servidor pinta el span de rojo: un 401 o un cancel del cliente no.
		if (level == LogLevel.Error)
		{
			activity?.SetStatus(ActivityStatusCode.Error);
		}

		foreach (var inner in innerExceptions)
		{
			logger.LogApiCallInnerError(level, inner, parentMessage, Utils.GetExceptionMessage(inner));
			activity?.AddException(inner);
		}

		if (context.RequestServices.GetService<IProblemDetailsService>() is not { } problemDetailsService)
		{
			context.Response.StatusCode = StatusCodes.Status500InternalServerError;
			context.Response.ContentType = MediaTypeNames.Text.Plain;
			return;
		}

		var problemDetail = ExceptionToProblemDetails(options, exception, innerExceptions, ShouldIncludeExceptionDetails(options, context));

		WriteCustomHeaders(context, activity);

		context.Response.StatusCode = problemDetail.Status ?? StatusCodes.Status500InternalServerError;

		if (await problemDetailsService.TryWriteAsync(new()
			{
				HttpContext = context,
				ProblemDetails = problemDetail,
				Exception = exception
			}))
		{
			return;
		}

		// Ningun IProblemDetailsWriter acepta el Accept del cliente -un browser navegando pide text/html-. WriteAsync
		// tiraba InvalidOperationException aca adentro: ExceptionHandlerMiddleware la cazaba, logueaba el unhandled
		// que justamente queremos callar y re-tiraba la original, asi que el cliente terminaba con un 500 en vez del
		// status que elegimos. El status code solo ya es una respuesta valida.
		context.Response.ContentType = MediaTypeNames.Text.Plain;
	}

	private static LogLevel LogLevelFor(Exception exception)
	{
		// Estos tres los provoca el cliente, no el servidor. Antes no se logueaban, pero el middleware de asp.net los
		// publicaba igual como Error: invisibles para nosotros y ruido para todos. Ahora se ven, en su nivel.
		return exception switch
		{
			UnauthorizedAccessException => LogLevel.Warning,
			ValueObjectValidationException => LogLevel.Warning,
			OperationCanceledException => LogLevel.Information,
			_ => LogLevel.Error
		};
	}

	private static bool TryGetCustomDetails(ApiErrorHandlingOptions options, Exception exception, [NotNullWhen(true)] out ProblemDetails? details)
	{
		if (options._exceptionHandlers is null)
		{
			details = null;
			return false;
		}

		if (options._exceptionHandlers.TryGetValue(exception.GetType(), out var handler))
		{
			details = handler(exception);
			return true;
		}

		details = null;
		return false;
	}

	private static void WriteCustomHeaders(HttpContext context, Activity? activity)
	{
		context.Response.Headers[CustomHeaderNames.REQUEST_ID] = context.GetRequestId();

		if (activity is not null)
		{
			context.Response.Headers[CustomHeaderNames.TRACE_ID] = activity.SpanId.ToString();
		}
	}
}
