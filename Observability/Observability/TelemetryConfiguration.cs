using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace DDS.Observability;

public sealed class TelemetryConfiguration
{
	private static readonly IObservabilityExporter[] _internalExporters =
	[
		new ConsoleObservabilityExporter(),
		new OpenTelemetryObservabilityExporter()
	];

	private readonly IConfigurationSection _telemetrySection;
	private readonly IObservabilityExporter[] _allExporters;

	internal TelemetryConfiguration(WebApplicationBuilder builder, ObservabilityConfiguration configuration)
	{
		Builder = builder;
		UserConfiguration = configuration;
		_telemetrySection = builder.Configuration.GetSection("Telemetry");
		_allExporters = configuration.Exporters is null
			? _internalExporters
			: [.._internalExporters, ..configuration.Exporters];

		ConfigureSdkDiagnostics(DiagnosticsSection);
	}

	public string AppName
	{
		get { return _telemetrySection["AppName"] ?? Builder.Environment.ApplicationName; }
	}

	public string AppVersion
	{
		get { return UserConfiguration.AppVersion; }
	}

	public WebApplicationBuilder Builder { get; }

	public IConfigurationSection DiagnosticsSection
	{
		get { return _telemetrySection.GetSection("Diagnostics"); }
	}

	public IConfigurationSection LoggingSection
	{
		get { return _telemetrySection.GetSection("Logging"); }
	}

	public IConfigurationSection MetricsSection
	{
		get { return _telemetrySection.GetSection("Metrics"); }
	}

	public ResourceBuilder ResourceBuilder
	{
		get
		{
			if (field is not null)
			{
				return field;
			}

			field = ResourceBuilder.CreateDefault().AddService(AppName, serviceVersion: AppVersion);

			field.AddAttributes(new Dictionary<string, object>
			{
				["service.environment"] = Environment,
				["deployment.tag"] = Tag
			});

			return field;
		}
	}

	/// <summary>
	/// Lazily registers the OpenTelemetry hosted service on first access. Kept lazy so apps that only use the Console
	/// logging exporter (no tracing, no metrics) never register the hosted service — otherwise it emits startup warnings
	/// about missing <c>MeterProvider</c>/<c>LoggerProvider</c> even though those signals are intentionally disabled.
	/// </summary>
	public IOpenTelemetryBuilder TelemetryBuilder
	{
		get { return field ??= Builder.Services.AddOpenTelemetry(); }
	}

	public IConfigurationSection TracingSection
	{
		get { return _telemetrySection.GetSection("Tracing"); }
	}

	public ObservabilityConfiguration UserConfiguration { get; }

	public IObservabilityExporter? GetWriters(IConfigurationSection section, out string? provider)
	{
		provider = section.GetProvider();

		if (string.IsNullOrWhiteSpace(provider))
		{
			return null;
		}

		var id = provider;
		return _allExporters.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
	}

	private string Environment
	{
		get { return string.IsNullOrWhiteSpace(UserConfiguration.Environment) ? _telemetrySection["Environment"] ?? "not-set" : UserConfiguration.Environment; }
	}

	private string Tag
	{
		get { return UserConfiguration.Tag; }
	}

	private static void ConfigureSdkDiagnostics(IConfigurationSection diagnostics)
	{
		// EventListener defaults on so OTel SDK warnings/errors surface to stderr — opt-out via "Enabled": false.
		if (diagnostics.GetValue("Enabled", true))
		{
			var level = diagnostics.TryGetEnum<EventLevel>("Level") ?? EventLevel.Warning;
			ObservabilityEventListener.EnsureStarted(level);
		}

		// OTel SDK self-diagnostics: file-based circular log written by the SDK itself. Survives stdout being lost.
		// Triggered by the presence of OTEL_DIAGNOSTICS.json in AppContext.BaseDirectory.
		var selfDiag = diagnostics.GetSection("SelfDiagnostics");
		if (!selfDiag.GetValue("Enabled", false))
		{
			return;
		}

		try
		{
			var path = Path.Combine(AppContext.BaseDirectory, "OTEL_DIAGNOSTICS.json");
			var payload = JsonSerializer.Serialize(new
			{
				LogDirectory = selfDiag["LogDirectory"] ?? ".",
				FileSize = selfDiag.GetValue("FileSize", 32768),
				LogLevel = selfDiag["LogLevel"] ?? "Warning"
			});
			File.WriteAllText(path, payload);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[Observability] Failed to write OTEL_DIAGNOSTICS.json: {ex.Message}");
		}
	}
}
