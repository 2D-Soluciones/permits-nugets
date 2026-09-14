using System;
using JetBrains.Annotations;

namespace DDS.Observability;

[PublicAPI]
public class ConfigurationException : Exception
{
	public ConfigurationException() { }
	public ConfigurationException(string message) : base(message) { }
	public ConfigurationException(string message, Exception inner) : base(message, inner) { }
}
