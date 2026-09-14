using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DDS.Observability;

internal sealed class ConsoleObservabilityExporter : IObservabilityExporter
{
	/// <inheritdoc />
	public string Id
	{
		get { return "console"; }
	}

	/// <inheritdoc />
	public void RegisterLoggerExporter(TelemetryConfiguration configuration)
	{
		configuration.Builder.Logging.ClearProviders();
		configuration.Builder.Logging.AddConsole();
	}

	/// <inheritdoc />
	public void RegisterMetricsExporter(TelemetryConfiguration configuration, MeterProviderBuilder builder)
	{
		builder.AddConsoleExporter();
	}

	/// <inheritdoc />
	public void RegisterTraceExporter(TelemetryConfiguration configuration, TracerProviderBuilder builder)
	{
		builder.AddConsoleExporter();
	}
}
