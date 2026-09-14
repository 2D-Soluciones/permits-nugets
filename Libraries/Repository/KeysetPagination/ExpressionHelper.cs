using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace DDS.Repository;

internal static class ExpressionHelper
{
	/// <summary>
	///     Devuelve la cadena de propiedades que forma la <see cref="MemberExpression" />.
	///     Esta es la lista `[Prop1, Prop2]` de `x.Prop1.Prop2`.
	/// </summary>
	public static List<PropertyInfo> GetPropertyChain(MemberExpression expression)
	{
		ValidateExpressionUnwrapped(expression);

		var properties = new List<PropertyInfo>();

		Expression? current = expression;

		while (current is MemberExpression memberExpression)
		{
			properties.Add(GetPropertyInfoMember(memberExpression));
			current = memberExpression.Expression;
		}

		properties.Reverse();
		return properties;
	}

	/// <summary>
	///     Devuelve la primera expresion de una <see cref="MemberExpression" />.
	///     Este es el `x` de `x.Prop1.Prop2`.
	/// </summary>
	public static Expression GetStartingExpression(MemberExpression expression)
	{
		ValidateExpressionUnwrapped(expression);

		Expression? current = expression;

		while (current is MemberExpression memberExpression)
		{
			current = memberExpression.Expression;
		}

		return current!;
	}

	private static PropertyInfo GetPropertyInfoMember(MemberExpression memberExpression)
	{
		if (memberExpression.Member is PropertyInfo prop)
		{
			return prop;
		}

		throw new InvalidOperationException($"Expected a property access, got '{memberExpression.Member.MemberType}'.");
	}

	[Conditional("DEBUG")]
	private static void ValidateExpressionUnwrapped(Expression expression)
	{
		if (expression.NodeType is ExpressionType.Lambda or ExpressionType.Convert)
		{
			throw new KeysetPaginationException("Expression should have been unwrapped by now.");
		}
	}
}
