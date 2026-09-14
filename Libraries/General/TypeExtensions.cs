using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Metodos de extension que operan sobre <see cref="Type" />.
/// </summary>
[PublicAPI]
public static class TypeExtensions
{



	/// <summary>
	///     Metodo parecido a <see cref="Type.IsAssignableFrom" /> pero que soporta genericos abiertos (por ejemplo ICollection&lt;&gt;)
	/// </summary>
	/// <param name="givenType">El <see cref="Type" /> a evaluar.</param>
	/// <param name="genericType">El <see cref="Type" /> contra el que comparar.</param>
	/// <returns><c>true</c> si <paramref name="givenType" /> es asignable a <paramref name="genericType" />.</returns>
	[PublicAPI]
	public static bool IsAssignableFromOpenGeneric([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type givenType, Type genericType)
	{
		ArgumentNullException.ThrowIfNull(givenType);
		ArgumentNullException.ThrowIfNull(genericType);
		return GetAssignableFromOpenGeneric(givenType, genericType) != null;
	}

	/// <summary>
	///     Verifica si el <paramref name="type" /> que se le pasa es un <see cref="Nullable{T}" />.
	/// </summary>
	/// <param name="type">El <see cref="Type" /> a evaluar.</param>
	/// <returns><c>true</c> si el tipo que se le pasa es un <see cref="Nullable{T}" />, <c>false</c> si no.</returns>
	[PublicAPI]
	public static bool IsNullable(this Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return IsNullableInternal(type);
	}



	private static Type? GetAssignableFromOpenGeneric([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type givenType, Type genericType)
	{
		if (givenType == genericType)
		{
			return givenType;
		}

		//GetGenericTypeDefinition tira si el tipo no es genérico, así que hay que preguntar primero
		if (IsGenericType(givenType, genericType))
		{
			return genericType;
		}

		if (genericType.IsInterface)
		{
			//las interfaces del tipo que se está chequeando, no las del genérico abierto que se busca
			var implemented = givenType.GetInterfaces().FirstOrDefault(iface => IsGenericType(iface, genericType));

			if (implemented is not null)
			{
				return implemented;
			}
		}
		else
		{
			var parentType = givenType;

			//la clase y sus clases base
			while (parentType != null)
			{
				if (IsGenericType(parentType, genericType))
				{
					return parentType;
				}

				parentType = parentType.BaseType;
			}
		}

		//todo lo que acepta IsAssignableFrom - tipos no genéricos y genéricos cerrados - se sigue aceptando
		return genericType.IsAssignableFrom(givenType) ? givenType : null;
	}

	// ReSharper disable once SuggestBaseTypeForParameter
	private static bool IsGenericType(Type type, Type genericType)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == genericType;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsNullableInternal(Type type)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
	}
}
