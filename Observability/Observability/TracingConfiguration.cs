using OpenTelemetry;
using OpenTelemetry.Trace;

namespace DDS.Observability;

internal static class TracingConfiguration
{
	public static void ConfigureTracing(this TelemetryConfiguration telemetry)
	{
		var tracingConfiguration = telemetry.TracingSection;
		var writers = telemetry.GetWriters(tracingConfiguration, out var provider);

		if (string.IsNullOrWhiteSpace(provider))
		{
			return;
		}

		if (writers is null)
		{
			throw new ConfigurationException($"Unknown tracing exporter: '{provider}'.");
		}

		telemetry.TelemetryBuilder.WithTracing(tracer =>
		{
			tracer
				.SetResourceBuilder(telemetry.ResourceBuilder)
				.AddSource(telemetry.AppName);

			if (tracingConfiguration.IsFeatureEnabled("AspNetCore"))
			{
				tracer.AddAspNetCoreInstrumentation();
			}

			if (tracingConfiguration.IsFeatureEnabled("HttpClient"))
			{
				tracer.AddHttpClientInstrumentation();
			}

			if (tracingConfiguration.IsFeatureEnabled("EntityFrameworkCore"))
			{
				tracer.AddEntityFrameworkCoreInstrumentation();
			}

			if (tracingConfiguration.IsFeatureEnabled("Application"))
			{
				telemetry.UserConfiguration.AdditionalTracers?.Invoke(tracer);
			}

			writers.RegisterTraceExporter(telemetry, tracer);
		});
	}
}
