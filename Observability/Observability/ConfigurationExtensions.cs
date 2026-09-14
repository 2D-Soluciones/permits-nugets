using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace DDS.Observability;

internal static class ConfigurationExtensions
{
	extension(IConfigurationSection section)
	{
		public string? GetProvider()
		{
			return section["Provider"];
		}

		public bool IsFeatureEnabled(string featureName)
		{
			return section["Enabled"]?
				.Split(',')
				.Any(f => f.Trim().Equals(featureName, StringComparison.OrdinalIgnoreCase)) ?? false;
		}

		public TEnum? TryGetEnum<TEnum>(string key) where TEnum : struct, Enum
		{
			var value = section[key];
			return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : null;
		}
	}
}
