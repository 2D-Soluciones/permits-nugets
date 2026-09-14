using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DDS.Observability;

public interface IObservabilityExporter
{
	string Id { get; }
	void RegisterLoggerExporter(TelemetryConfiguration configuration);
	void RegisterMetricsExporter(TelemetryConfiguration configuration, MeterProviderBuilder builder);
	void RegisterTraceExporter(TelemetryConfiguration configuration, TracerProviderBuilder builder);
}
