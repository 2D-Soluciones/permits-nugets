using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace DDS.General;

/// <summary>
///     Metodos de extension para <see cref="IServiceProvider" />.
/// </summary>
[PublicAPI]
public static class ServiceProviderExtensions
{
	private static readonly ConcurrentDictionary<Type, CachedFactory> _factories = new();

	/// <summary>
	///     Crea un tipo nuevo desde el service provider.
	/// </summary>
	/// <param name="serviceProvider"></param>
	/// <param name="concreteType"></param>
	/// <returns></returns>
	public static object NewFromServices(this IServiceProvider serviceProvider, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type concreteType)
	{
		return NewFromServices(serviceProvider, concreteType, [], []);
	}

	/// <summary>
	///     Crea un tipo nuevo desde el service provider, usando los <paramref name="requiredTypes" /> como tipos
	///     requeridos (en orden) y los <paramref name="requiredArguments" /> como argumentos requeridos.
	/// </summary>
	/// <remarks>
	///     El cache de factories se indexa solo por <paramref name="concreteType" />, asi que se soporta un unico patron de constructor (conjunto de
	///     <paramref name="requiredTypes" />) por tipo. Un segundo lugar que pida el mismo tipo con otra
	///     firma distinta tira excepcion en vez de correr calladito la factory del primero con sus argumentos.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	///     el <paramref name="concreteType" /> ya estaba cacheado con otro conjunto de <paramref name="requiredTypes" />.
	/// </exception>
	/// <param name="serviceProvider"></param>
	/// <param name="concreteType"></param>
	/// <param name="requiredTypes"></param>
	/// <param name="requiredArguments"></param>
	/// <returns></returns>
	public static object NewFromServices(this IServiceProvider serviceProvider, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type concreteType, Type[] requiredTypes, object?[] requiredArguments)
	{
		var cached = _factories.TryGetValue(concreteType, out var existing)
			? existing
			: _factories.GetOrAdd(concreteType, new CachedFactory(ActivatorUtilities.CreateFactory(concreteType, requiredTypes), requiredTypes));

		if (!cached.RequiredTypes.AsSpan().SequenceEqual(requiredTypes))
		{
			throw new InvalidOperationException($"'{concreteType}' was already cached with a different constructor signature. Running this call's arguments through that factory would bind them to the wrong parameters.");
		}

		// La activación corre fuera de cualquier lock: adentro se ejecuta el constructor del usuario, y serializar
		// todas las activaciones del proceso detrás de un único lock hace que un constructor lento frene al resto.
		return cached.Factory(serviceProvider, requiredArguments);
	}

	private sealed record CachedFactory(ObjectFactory Factory, Type[] RequiredTypes);
}
