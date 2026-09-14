using System.Diagnostics;
using System.Linq.Expressions;

namespace DDS.Repository;

internal static class KeysetAdaptingExpressionVisitor
{
	/// <summary>
	///     Toma una lambda y la adapta al parametro nuevo que se le da, devolviendo otra lambda que accede a ese parametro.
	/// </summary>
	public static Expression<Func<T, TColumn>> AdaptParameter<T, TColumn>(Expression<Func<T, TColumn>> expression, ParameterExpression newParameter)
	{
		Debug.Assert(expression.Parameters.Count == 1);

		var visitor = new KeysetParameterAdaptingExpressionVisitor(expression.Parameters[0], newParameter);
		var newBody = visitor.Visit(expression.Body);
		return Expression.Lambda<Func<T, TColumn>>(newBody, newParameter);
	}

	/// <summary>
	///     Toma una lambda y la adapta al tipo nuevo que se le da, devolviendo otra lambda que accede a las propiedades equivalentes
	///     del tipo nuevo, segun las reglas de tipado flexible definidas.
	///     Reemplaza una cadena de accesos a miembros solo si arranca con el primer parametro de la expresion dada.
	/// </summary>
	public static Expression<Func<object, TColumn>> AdaptType<T, TColumn>(Expression<Func<T, TColumn>> expression, Type newType)
	{
		Debug.Assert(expression.Parameters.Count == 1);

		var newParameter = Expression.Parameter(typeof(object), expression.Parameters[0].Name);

		var visitor = new KeysetTypeAdaptingExpressionVisitor(
			expression.Parameters[0],
			newParameter,
			newType);

		var newBody = visitor.Visit(expression.Body);
		return Expression.Lambda<Func<object, TColumn>>(newBody, newParameter);
	}
}

internal class KeysetParameterAdaptingExpressionVisitor : ExpressionVisitor
{
	private readonly ParameterExpression _newParameter;
	protected readonly ParameterExpression _oldParameter;

	public KeysetParameterAdaptingExpressionVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
	{
		_oldParameter = oldParameter;
		_newParameter = newParameter;
	}

	protected override Expression VisitParameter(ParameterExpression node)
	{
		return node == _oldParameter ? _newParameter : node;
	}
}

internal sealed class KeysetTypeAdaptingExpressionVisitor : KeysetParameterAdaptingExpressionVisitor
{
	private readonly Type? _newType;

	public KeysetTypeAdaptingExpressionVisitor(ParameterExpression oldParameter, ParameterExpression newParameter, Type? newType) : base(oldParameter, newParameter)
	{
		_newType = newType;
	}

	protected override Expression VisitMember(MemberExpression node)
	{
		if (_newType == null)
		{
			// No hay nada que reemplazar ni adaptar.
			return base.VisitMember(node);
		}

		var startingExpression = ExpressionHelper.GetStartingExpression(node);

		if (startingExpression != _oldParameter)
		{
			// No hay nada que reemplazar ni adaptar.
			return base.VisitMember(node);
		}

		// Reemplazo la cadena de propiedades por la equivalente del tipo nuevo.
		Expression currentReplacementExpression = Expression.Convert(Visit(startingExpression), _newType);

		var properties = ExpressionHelper.GetPropertyChain(node);

		foreach (var property in properties)
		{
			var accessor = Accessor.Obtain(currentReplacementExpression.Type);

			if (!accessor.TryGetProperty(property.Name, out var newProperty))
			{
				throw CreateIncompatibleObjectException(property.Name);
			}

			currentReplacementExpression = Expression.MakeMemberAccess(currentReplacementExpression, newProperty);
		}

		return currentReplacementExpression;
	}

	private static KeysetPaginationException CreateIncompatibleObjectException(string propertyName)
	{
		return new($"A matching property '{propertyName}' was not found on this object.");
	}
}
