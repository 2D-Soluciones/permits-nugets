using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace DDS.AspNetCore.OpenApi.Helpers;

internal static class XmlCommentsHelper
{
	public static string? GetMemberName(MethodInfo method)
	{
		if (method.DeclaringType is null)
		{
			return null;
		}

		var builder = new StringBuilder()
			.Append("M:")
			.Append(QualifiedNameFor(method.DeclaringType))
			.Append('.')
			.Append(method.Name);

		if (method.GetGenericArguments() is {Length: > 0} typeParameters)
		{
			builder.Append("``")
				.Append(typeParameters.Length);
		}

		if (method.GetParameters() is not {Length: > 0} parameters)
		{
			return builder.ToString();
		}

		string?[] names = [.. parameters.Select(GetName)];

		if (names.Any(p => p is null))
		{
			return null;
		}

		builder.Append('(')
			.Append(string.Join(',', names))
			.Append(')');

		return builder.ToString();

		static string? GetName(ParameterInfo parameter)
		{
			return GetNameForType(parameter.ParameterType);
		}
	}

	public static string? GetMemberName(MemberInfo member)
	{
		if (member.DeclaringType is null)
		{
			return null;
		}

		var builder = new StringBuilder()
			.Append((member.MemberType & MemberTypes.Field) != 0 ? 'F' : 'P')
			.Append(':')
			.Append(QualifiedNameFor(member.DeclaringType))
			.Append('.')
			.Append(member.Name);

		return builder.ToString();
	}

	private static string? GetNameForType(Type type)
	{
		if (!type.IsGenericParameter)
		{
			return QualifiedNameFor(type, true);
		}

		//el compilador emite `N para un generico del tipo y ``N para uno del metodo: usar siempre ``N hacia que
		//ningun controller generico encontrara jamas su documentacion.
		var prefix = type.DeclaringMethod is null ? "`" : "``";
		return FormattableString.Invariant($"{prefix}{type.GenericParameterPosition}");
	}

	private static IEnumerable<string> GetNestedTypeNames(Type type)
	{
		if (!type.IsNested || type.DeclaringType is null)
		{
			yield break;
		}

		foreach (var name in GetNestedTypeNames(type.DeclaringType))
		{
			yield return name;
		}

		yield return type.DeclaringType.Name;
	}

	private static string? QualifiedNameFor(Type type, bool expandGenericArguments = false)
	{
		if (type.IsArray)
		{
			var elementType = type.GetElementType();

			if (elementType is null)
			{
				return null;
			}

			return GetNameForType(elementType) + "[]";
		}

		var builder = new StringBuilder();

		if (!string.IsNullOrEmpty(type.Namespace))
		{
			builder.Append(type.Namespace)
				.Append('.');
		}

		if (type.IsNested)
		{
			builder.Append(string.Join('.', GetNestedTypeNames(type)))
				.Append('.');
		}

		if (type.IsConstructedGenericType && expandGenericArguments)
		{
			var index = type.Name.IndexOf('`', StringComparison.Ordinal);
			builder.Append(type.Name.AsSpan(..index));

			var argumentNames = type
				.GetGenericArguments()
				.Select(GetNameForType);

			builder.Append('{')
				.Append(string.Join(',', argumentNames))
				.Append('}');
		}
		else
		{
			builder.Append(type.Name);
		}

		return builder.ToString();
	}
}
