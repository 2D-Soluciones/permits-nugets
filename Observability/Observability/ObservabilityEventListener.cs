using System;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Threading;

namespace DDS.Observability;

/// <summary>
/// Forwards <c>OpenTelemetry-*</c> EventSource diagnostics to <see cref="Console.Error"/> so failures inside the OTel
/// pipeline (export errors, dropped log records on queue overflow, transport faults) become visible. Without this, the
/// SDK reports those events to an EventSource that has no listener, and they vanish.
/// </summary>
internal sealed class ObservabilityEventListener : EventListener
{
	private static readonly Lock _gate = new();
	private static ObservabilityEventListener? _instance;
	private static EventLevel _minimumLevel = EventLevel.Warning;

	public static void EnsureStarted(EventLevel minimumLevel)
	{
		if (_instance is not null)
		{
			return;
		}

		lock (_gate)
		{
			if (_instance is not null)
			{
				return;
			}

			// Assign before constructing — the base ctor invokes OnEventSourceCreated for already-existing sources.
			_minimumLevel = minimumLevel;
			_instance = new();
		}
	}

	protected override void OnEventSourceCreated(EventSource eventSource)
	{
		if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.Ordinal))
		{
			EnableEvents(eventSource, _minimumLevel);
		}
	}

	protected override void OnEventWritten(EventWrittenEventArgs eventData)
	{
		try
		{
			Console.Error.WriteLine($"[{DateTime.UtcNow:O}] [OTel:{eventData.EventSource.Name}/{eventData.Level}] {FormatMessage(eventData)}");
		}
		catch
		{
			// A diagnostic logger must never throw — cascading failure here would be worse than the original problem.
		}
	}

	private static string FormatMessage(EventWrittenEventArgs eventData)
	{
		var template = eventData.Message;

		if (string.IsNullOrEmpty(template))
		{
			return eventData.EventName ?? "<no-event>";
		}

		if (eventData.Payload is not { Count: > 0 })
		{
			return template;
		}

		try
		{
			return string.Format(CultureInfo.InvariantCulture, template, [..eventData.Payload]);
		}
		catch (FormatException)
		{
			return $"{template} | payload: {string.Join(", ", eventData.Payload)}";
		}
	}
}
