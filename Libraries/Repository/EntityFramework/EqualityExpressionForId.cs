using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DDS.Domain;

namespace DDS.Repository.EntityFramework;

internal static class EqualityExpressionForId<TKey, TEntity> where TEntity : notnull where TKey : struct
{
	private static readonly ParameterExpression _lambdaParam = Expression.Parameter(typeof(TEntity));

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    private static readonly MemberExpression _memberExpression = Expression.PropertyOrField(_lambdaParam, nameof(Entity<TKey>.Id));

	/// <remarks>
	///     La clave se captura en un closure y no como ConstantExpression: EF traduce una constante a un literal SQL,
	///     asi que cada id distinto generaba un texto de consulta distinto y se perdian tanto la cache de consultas de
	///     EF como la de planes del motor. Esto esta en el camino caliente de Get(id) y de la paginacion por keyset.
	/// </remarks>
	public static Expression<Func<TEntity, bool>> Create(TKey id)
	{
		var box = new KeyBox(id);
		var value = Expression.Field(Expression.Constant(box), nameof(KeyBox.Value));
		return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(_memberExpression, value), _lambdaParam);
	}

	private sealed class KeyBox
	{
		public readonly TKey Value;

		public KeyBox(TKey value)
		{
			Value = value;
		}
	}
}
