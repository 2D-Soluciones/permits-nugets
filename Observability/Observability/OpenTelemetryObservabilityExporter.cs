using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DDS.Observability;

internal sealed class OpenTelemetryObservabilityExporter : IObservabilityExporter
{
	/// <inheritdoc />
	public string Id
	{
		get { return "otlp"; }
	}

	/// <inheritdoc />
	public void RegisterLoggerExporter(TelemetryConfiguration configuration)
	{
		var loggingSection = configuration.LoggingSection;
		var otlpSection = loggingSection.GetSection("Otlp");

		configuration.Builder.Logging.ClearProviders();

		// Belt-and-suspenders: when OTLP is the chosen logging provider, also emit to stdout so transient OTLP
		// failures don't leave the operator blind. Opt-out via "Telemetry:Logging:ConsoleFallback": false.
		if (loggingSection.GetValue("ConsoleFallback", true))
		{
			configuration.Builder.Logging.AddSimpleConsole(o =>
			{
				o.SingleLine = true;
				o.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
			});
		}

		// Modern API: registers the SDK LoggerProvider in DI (satisfies the hosted service's startup check) while still
		// hooking into ILogger via the underlying OpenTelemetryLoggerProvider. The legacy
		// ILoggingBuilder.AddOpenTelemetry call only registered an ILoggerProvider — leaving the hosted service to warn
		// about the missing LoggerProvider on every startup.
		configuration.TelemetryBuilder.WithLogging(
			loggerProviderBuilder =>
			{
				loggerProviderBuilder.SetResourceBuilder(configuration.ResourceBuilder);
				loggerProviderBuilder.AddOtlpExporter((exporterOptions, processorOptions) =>
				{
					ApplyOtlpOptions(otlpSection, exporterOptions);
					ApplyBatchProcessorOptions(otlpSection, processorOptions.BatchExportProcessorOptions);
				});

				// Diagnostic aid: emit each LogRecord (with its Attributes, incl. ILogEnricher tags) to the
				// console via the OTel exporter. Unlike the AddSimpleConsole fallback, this renders attributes,
				// so it confirms whether enrichment actually reaches the exported record. Opt-in via
				// "Telemetry:Logging:OtelConsole": true. Default off.
				if (loggingSection.GetValue("OtelConsole", false))
				{
					loggerProviderBuilder.AddConsoleExporter();
				}
			},
			loggerOptions =>
			{
				loggerOptions.IncludeFormattedMessage = true;
				loggerOptions.IncludeScopes = true;
				loggerOptions.ParseStateValues = true;
			});

		configuration.Builder.Logging.EnableEnrichment(enrichmentOptions => { enrichmentOptions.CaptureStackTraces = true; });
	}

	/// <inheritdoc />
	public void RegisterMetricsExporter(TelemetryConfiguration configuration, MeterProviderBuilder builder)
	{
		var otlpSection = configuration.MetricsSection.GetSection("Otlp");

		builder.AddOtlpExporter(exporterOptions =>
		{
			// Preserve original 60s scheduled delay as the default; explicit config can override below.
			exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 60 * 1000;

			ApplyOtlpOptions(otlpSection, exporterOptions);
			ApplyBatchProcessorOptions(otlpSection, exporterOptions.BatchExportProcessorOptions);
		});
	}

	/// <inheritdoc />
	public void RegisterTraceExporter(TelemetryConfiguration configuration, TracerProviderBuilder builder)
	{
		var otlpSection = configuration.TracingSection.GetSection("Otlp");

		builder.AddOtlpExporter(exporterOptions =>
		{
			ApplyOtlpOptions(otlpSection, exporterOptions);
			ApplyBatchProcessorOptions(otlpSection, exporterOptions.BatchExportProcessorOptions);
		});
	}

	private static void ApplyBatchProcessorOptions<T>(IConfigurationSection section, BatchExportProcessorOptions<T> options) where T : class
	{
		if (!section.Exists())
		{
			return;
		}

		var queueSize = section.GetValue<int?>("MaxQueueSize");

		if (queueSize.HasValue)
		{
			options.MaxQueueSize = queueSize.Value;
		}

		var batchSize = section.GetValue<int?>("MaxExportBatchSize");

		if (batchSize.HasValue)
		{
			options.MaxExportBatchSize = batchSize.Value;
		}

		var scheduledDelay = section.GetValue<int?>("ScheduledDelayMilliseconds");

		if (scheduledDelay.HasValue)
		{
			options.ScheduledDelayMilliseconds = scheduledDelay.Value;
		}

		var exporterTimeout = section.GetValue<int?>("ExporterTimeoutMilliseconds");

		if (exporterTimeout.HasValue)
		{
			options.ExporterTimeoutMilliseconds = exporterTimeout.Value;
		}
	}

	private static void ApplyOtlpOptions(IConfigurationSection section, OtlpExporterOptions options)
	{
		if (!section.Exists())
		{
			return;
		}

		var endpoint = section["Endpoint"];

		if (!string.IsNullOrWhiteSpace(endpoint))
		{
			options.Endpoint = new(endpoint);
		}

		var protocol = section.TryGetEnum<OtlpExportProtocol>("Protocol");

		if (protocol.HasValue)
		{
			options.Protocol = protocol.Value;
		}

		var headers = section["Headers"];

		if (!string.IsNullOrWhiteSpace(headers))
		{
			options.Headers = headers;
		}

		var timeout = section.GetValue<int?>("TimeoutMilliseconds");

		if (timeout.HasValue)
		{
			options.TimeoutMilliseconds = timeout.Value;
		}
	}
}
