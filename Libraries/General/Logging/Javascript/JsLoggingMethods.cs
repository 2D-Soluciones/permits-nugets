using Microsoft.Extensions.Logging;

namespace DDS.General.Logging.Javascript;

internal static partial class JsLoggingMethods
{
	[LoggerMessage(Level = LogLevel.Error, Message = "JS error: {error}")]
	public static partial void LogJavascriptError(this ILogger logger, [LogProperties(OmitReferenceName = true, SkipNullProperties = true)] JsErrorEntry error);
}
