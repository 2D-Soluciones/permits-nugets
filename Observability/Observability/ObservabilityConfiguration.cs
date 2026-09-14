using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DDS.Observability;

[PublicAPI]
[NoReorder]
public sealed record ObservabilityConfiguration(string AppVersion, string Tag)
{
	public string? Environment { get; init; }
	public string[]? MetricNames { get; init; }
	public Action<MeterProviderBuilder>? AdditionalMeters { get; init; }
	public Action<TracerProviderBuilder>? AdditionalTracers { get; init; }
	public IEnumerable<IObservabilityExporter>? Exporters { get; init; }
}
