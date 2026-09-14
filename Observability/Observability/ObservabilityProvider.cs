using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace DDS.Observability;

[PublicAPI]
public static class ObservabilityProvider
{
	private static string? _serviceVersion;
	private static ActivitySource? _activitySource;
	private static string _serviceName = null!;

	public static ActivitySource ActivitySource
	{
		get { return _activitySource ??= new(_serviceName, _serviceVersion); }
	}

	public static void ConfigureObservabilityProvider(this WebApplicationBuilder builder, ObservabilityConfiguration configuration)
	{
		var telemetryConfiguration = new TelemetryConfiguration(builder, configuration);
		_serviceName = telemetryConfiguration.AppName;
		_serviceVersion = telemetryConfiguration.AppVersion;
		_activitySource = null;

		telemetryConfiguration.ConfigureTracing();
		telemetryConfiguration.ConfigureMetrics();

		var logging = telemetryConfiguration.GetWriters(telemetryConfiguration.LoggingSection, out var provider);

		if (logging is null)
		{
			throw new ConfigurationException(
				string.IsNullOrWhiteSpace(provider)
					? "A logging provider must be configured in the 'Telemetry:Logging:Provider' configuration section."
					: $"Unknown logging provider: '{provider}'.");
		}

		logging.RegisterLoggerExporter(telemetryConfiguration);
	}
}
