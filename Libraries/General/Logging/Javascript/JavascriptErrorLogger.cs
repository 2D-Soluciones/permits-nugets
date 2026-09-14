using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DDS.General.Logging.Javascript;

/// <summary>
///     Conjunto de metodos de extension que parsean
/// </summary>
[PublicAPI]
public static class JavascriptErrorLoggerExtensions
{
	/// <summary>
	///     Intenta traer los sourcemaps de JavaScript desde el <paramref name="sourceMapsSource" /> que se le pasa y usarlos para dar
	///     better stack traces.
	/// </summary>
	/// <param name="logger"></param>
	/// <param name="error"></param>
	/// <param name="sourceMapsSource"></param>
	/// <param name="cancellationToken"></param>
	public static async Task LogJavascriptError(this ILogger logger, JsErrorEntry error, Uri? sourceMapsSource, CancellationToken cancellationToken)
	{
		var stackFrames = ErrorParser.ReadStackFrames(error.Stack ?? error.Message);

		if (stackFrames is null || sourceMapsSource is null)
		{
			//no se pudieron parsear los frames: lo logueo tal cual viene
			logger.LogJavascriptError(error);
			return;
		}

		using var builder = new JavascriptLogDetailBuilder(stackFrames, sourceMapsSource);
		var stackTrace = await builder.GetStackTrace(cancellationToken);

		if (!string.IsNullOrWhiteSpace(stackTrace))
		{
			error = error with {Stack = stackTrace};
		}

		logger.LogJavascriptError(error);
	}
}
