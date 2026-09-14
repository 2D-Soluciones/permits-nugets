using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace DDS.Repository;

internal static class KeysetFilterPredicateStrategy
{
	private static readonly FrozenDictionary<Type, MethodInfo> _typeToCompareToMethod = new Dictionary<Type, MethodInfo>
	{
		{typeof(string), GetCompareToMethod(typeof(string))},
		{typeof(Guid), GetCompareToMethod(typeof(Guid))},
		{typeof(bool), GetCompareToMethod(typeof(bool))}
	}.ToFrozenDictionary();

	private static readonly ConstantExpression _constantExpression0 = Expression.Constant(0);

	public static Expression<Func<TEntity, bool>> BuildKeysetFilterPredicateExpression<TEntity>(IReadOnlyList<KeysetColumn<TEntity>> columns, KeysetPaginationDirection direction, object? reference)
	{
		var referenceValues = GetValues(columns, reference);
		var referenceValueExpressions = new Expression<Func<object>>[referenceValues.Count];

		for (var i = 0; i < referenceValues.Count; i++)
		{
			var referenceValue = referenceValues[i];
			Expression<Func<object>> referenceValueExpression = () => referenceValue;
			referenceValueExpressions[i] = referenceValueExpression;
		}

		// entity =>
		var param = Expression.Parameter(typeof(TEntity), "entity");

		var finalExpression = BuildExpressionCore(columns, direction, referenceValueExpressions, param);

		return Expression.Lambda<Func<TEntity, bool>>(finalExpression, param);
	}

	private static Expression BuildExpressionCore<TEntity>(IReadOnlyList<KeysetColumn<TEntity>> columns, KeysetPaginationDirection direction, IReadOnlyList<Expression<Func<object>>> referenceValueExpressions, ParameterExpression param)
	{
		var firstMemberAccessExpression = default(Expression);
		var firstReferenceValueExpression = default(Expression);

		var orExpression = default(BinaryExpression);
		var innerLimit = 1;

		// Este loop arma las expresiones OR de afuera.
		for (var i = 0; i < columns.Count; i++)
		{
			var andExpression = default(BinaryExpression);

			// Este loop arma las expresiones AND de adentro.
			// innerLimit crece implicitamente de 1 a items.Count en cada iteracion.
			for (var j = 0; j < innerLimit; j++)
			{
				var isInnerLastOperation = j + 1 == innerLimit;
				var column = columns[j];
				var memberAccess = column.MakeAccessExpression(param);
				var referenceValueExpression = referenceValueExpressions[j].Body;

				if (firstMemberAccessExpression == null)
				{
					// Capaz se use mas adelante en alguna optimizacion.
					firstMemberAccessExpression = memberAccess;
					firstReferenceValueExpression = referenceValueExpression;
				}

				BinaryExpression innerExpression;
				if (isInnerLastOperation)
				{
					var compare1 = GetComparisonExpressionToApply(direction, column, false);
					innerExpression = MakeComparisonExpression(column, memberAccess, referenceValueExpression, compare1);
				}
				else
				{
					innerExpression = Expression.Equal(memberAccess, EnsureMatchingType(memberAccess, referenceValueExpression));
				}

				andExpression = andExpression is null ? innerExpression : Expression.And(andExpression, innerExpression);
			}

			orExpression = orExpression is null ? andExpression : Expression.Or(orExpression, andExpression!);
			innerLimit++;
		}

		if (columns.Count <= 1)
		{
			return orExpression!;
		}

		// Implemento la optimizacion que permite un predicado de acceso sobre la primera columna.
		// Esto se logra generando la siguiente expresion:
		//   (x >=|<= a) AND (la expresion generada antes)
		//
		// Esto agrega una clausula redundante sobre la primera columna, pero es una que todas las bases
		// entienden y pueden usar como predicado de acceso (sobre todo cuando la columna esta indexada).

		var firstColumn = columns[0];
		var compare2 = GetComparisonExpressionToApply(direction, firstColumn, true);
		var accessPredicateClause = MakeComparisonExpression(firstColumn, firstMemberAccessExpression!, firstReferenceValueExpression!, compare2);
		return Expression.And(accessPredicateClause, orExpression!);
	}

	private static Expression EnsureEnumIsConvertedToUnderlyingType(Expression memberAccess)
	{
		if (!memberAccess.Type.IsEnum)
		{
			return memberAccess;
		}

		var enumUnderlyingType = Enum.GetUnderlyingType(memberAccess.Type);
		return Expression.Convert(memberAccess, enumUnderlyingType);
	}

	private static Expression EnsureMatchingType(Expression memberExpression, Expression targetExpression)
	{
		// Si el destino es de otro tipo, hay que convertirlo.
		// Al principio esto pasaba solo con los nullables, pero ahora que usamos expresiones
		// para el acceso al destino en vez de constantes, esto hace falta o la comparacion no anda
		// entre tipos que no coinciden (por ejemplo, int (miembro) comparado con object (destino)).
		return memberExpression.Type != targetExpression.Type ? Expression.Convert(targetExpression, memberExpression.Type) : targetExpression;
	}

	private static MethodInfo GetCompareToMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
	{
		var methodInfo = type.GetTypeInfo().GetMethod(nameof(string.CompareTo), [type]);
		return methodInfo ?? throw new InvalidOperationException($"Didn't find a CompareTo method on type {type.Name}.");
	}

	private static Func<Expression, Expression, BinaryExpression> GetComparisonExpressionToApply<T>(KeysetPaginationDirection direction, KeysetColumn<T> column, bool orEqual)
	{
		var greaterThan = direction switch
		{
			KeysetPaginationDirection.Forward when !column.IsDescending => true,
			KeysetPaginationDirection.Forward when column.IsDescending => false,
			KeysetPaginationDirection.Backward when !column.IsDescending => false,
			KeysetPaginationDirection.Backward when column.IsDescending => true,
			_ => throw new NotImplementedException()
		};

		return orEqual ? greaterThan ? Expression.GreaterThanOrEqual : Expression.LessThanOrEqual :
			greaterThan ? Expression.GreaterThan : Expression.LessThan;
	}

	private static List<object> GetValues<T>(IReadOnlyList<KeysetColumn<T>> columns, object? reference)
	{
		if (reference is null)
		{
			return [];
		}

		var referenceValues = new List<object>(columns.Count);

		foreach (var column in columns)
		{
			var value = column.ObtainValue(reference);

			if (value is null)
			{
				// Ver KeysetPaginationBuilder.ConfigureColumn: un null aca deja calladito sin alcanzar todas las filas que quedan.
				throw new InvalidOperationException($"Keyset column '{column.MemberName ?? column.Type.Name}' is null on the cursor row, and a keyset column has to be non-null.");
			}

			referenceValues.Add(value);
		}

		return referenceValues;
	}

	private static BinaryExpression MakeComparisonExpression<TEntity>(KeysetColumn<TEntity> column, Expression memberAccess, Expression referenceValue, Func<Expression, Expression, BinaryExpression> compare)
	{
		if (!_typeToCompareToMethod.TryGetValue(column.Type, out var compareToMethod))
		{
			return compare(
				EnsureEnumIsConvertedToUnderlyingType(memberAccess),
				EnsureEnumIsConvertedToUnderlyingType(
					EnsureMatchingType(memberAccess,
						referenceValue)
				)
			);
		}

		// Los operadores LessThan/GreaterThan no valen para algunos tipos, como strings y guids.
		// Para esos tipos usamos el metodo CompareTo.

		// entity.Property.CompareTo(referenceValue) >|< 0
		// -----------------------------------------

		// entity.Property.CompareTo(referenceValue)
		var methodCallExpression = Expression.Call(memberAccess, compareToMethod, EnsureMatchingType(memberAccess, referenceValue));

		// >|< 0
		return compare(methodCallExpression, _constantExpression0);
	}
}
