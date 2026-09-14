using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DDS.Repository;

internal sealed class Accessor
{
	private static readonly ConcurrentDictionary<Type, Accessor> _typeToAccessorMap = new();

	private readonly IReadOnlyDictionary<string, PropertyInfo> _propertyInfoMap;

	private Accessor(IReadOnlyDictionary<string, PropertyInfo> propertyInfoLookup)
	{
		_propertyInfoMap = propertyInfoLookup;
	}

	public bool TryGetProperty(string key, [MaybeNullWhen(false)] out PropertyInfo value)
	{
		return _propertyInfoMap.TryGetValue(key, out value);
	}

	public static Accessor Obtain(Type type)
	{
		return _typeToAccessorMap.GetOrAdd(type, CreateNew);
	}

	private static Accessor CreateNew([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
	{
		var properties = type.GetProperties();

		var propertyInfoLookup = new Dictionary<string, PropertyInfo>(properties.Length);

		foreach (var p in properties)
		{
			propertyInfoLookup[p.Name] = p;
		}

		return new(propertyInfoLookup);
	}
}