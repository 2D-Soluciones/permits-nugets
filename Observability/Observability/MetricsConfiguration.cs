using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace DDS.Observability;

internal static class MetricsConfiguration
{
	public static void ConfigureMetrics(this TelemetryConfiguration telemetry)
	{
		var metricsSection = telemetry.MetricsSection;
		var writers = telemetry.GetWriters(metricsSection, out var provider);

		if (string.IsNullOrWhiteSpace(provider))
		{
			return;
		}

		if (writers is null)
		{
			throw new ConfigurationException($"Unknown metrics exporter: '{provider}'.");
		}

		telemetry.TelemetryBuilder.WithMetrics(metrics =>
			{
				metrics.SetResourceBuilder(telemetry.ResourceBuilder);

				if (metricsSection.IsFeatureEnabled("AspNetCore"))
				{
					metrics.AddAspNetCoreInstrumentation();
				}

				if (metricsSection.IsFeatureEnabled("HttpClient"))
				{
					metrics.AddHttpClientInstrumentation();
				}

				if (metricsSection.IsFeatureEnabled("Runtime"))
				{
					metrics.AddRuntimeInstrumentation();
				}

				if (metricsSection.IsFeatureEnabled("Process"))
				{
					metrics.AddProcessInstrumentation();
				}

				if (metricsSection.IsFeatureEnabled("Application"))
				{
					metrics.AddApplicationInstrumentation(telemetry);
				}

				writers.RegisterMetricsExporter(telemetry, metrics);
			}
		);
	}

	private static void AddApplicationInstrumentation(this MeterProviderBuilder builder, TelemetryConfiguration telemetry)
	{
		telemetry.UserConfiguration.AdditionalMeters?.Invoke(builder);

		if (telemetry.UserConfiguration.MetricNames is null || telemetry.UserConfiguration.MetricNames.Length == 0)
		{
			return;
		}

		//https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-metrics-and-dotnet-part-2/
		builder.AddMeter(telemetry.UserConfiguration.MetricNames);
	}
}
