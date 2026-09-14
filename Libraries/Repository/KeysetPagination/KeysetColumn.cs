using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace DDS.Repository;

internal abstract class KeysetColumn<T>
{
	protected KeysetColumn(bool isDescending, LambdaExpression lambdaExpression)
	{
		IsDescending = isDescending;
		LambdaExpression = lambdaExpression;
	}

	public bool IsDescending { get; }

	public Type Type
	{
		get { return LambdaExpression.Body.Type; }
	}

	/// <summary>
	///     Nombre del miembro que lee esta columna, o <see langword="null" /> si la columna no es un acceso simple a un miembro.
	/// </summary>
	public string? MemberName
	{
		get { return (LambdaExpression.Body as MemberExpression)?.Member.Name; }
	}

	/// <summary>
	///     <see langword="true" /> cuando la columna lee un miembro directo del parametro de la lambda (<c>x.Id</c>)
	///     y no a traves de una navegacion (<c>x.Owner.Id</c>).
	/// </summary>
	public bool IsRootMember
	{
		get { return LambdaExpression.Body is MemberExpression {Expression: ParameterExpression}; }
	}

	public abstract IOrderedQueryable<T> ApplyOrderBy(IQueryable<T> query, KeysetPaginationDirection direction);
	public abstract IOrderedQueryable<T> ApplyThenOrderBy(IOrderedQueryable<T> query, KeysetPaginationDirection direction);
	public abstract Expression MakeAccessExpression(ParameterExpression parameter);
	public abstract object ObtainValue<TReference>(TReference reference);

	protected LambdaExpression LambdaExpression { get; }
}

internal sealed class KeysetColumn<T, TColumn> : KeysetColumn<T>
{
	// Cache del tipo de referencia al acceso compilado de la lambda de esta columna.
	private readonly ConcurrentDictionary<Type, Func<object, TColumn>> _referenceTypeToCompiledAccessMap = new();

	public KeysetColumn(
		bool isDescending,
		Expression<Func<T, TColumn>> expression)
		: base(isDescending, expression) { }

	private new Expression<Func<T, TColumn>> LambdaExpression
	{
		get { return (Expression<Func<T, TColumn>>) base.LambdaExpression; }
	}

	public override IOrderedQueryable<T> ApplyOrderBy(IQueryable<T> query, KeysetPaginationDirection direction)
	{
		return ApplyOrderByVariant(
			query, direction,
			Queryable.OrderBy,
			Queryable.OrderByDescending);
	}

	public override IOrderedQueryable<T> ApplyThenOrderBy(IOrderedQueryable<T> query, KeysetPaginationDirection direction)
	{
		return ApplyOrderByVariant(
			query, direction,
			Queryable.ThenBy,
			Queryable.ThenByDescending);
	}

	public override Expression MakeAccessExpression(ParameterExpression parameter)
	{
		return MakeAccessLambdaExpression(parameter).Body;
	}

	public override object ObtainValue<TReference>(TReference reference)
	{
		if (reference == null)
		{
			throw new ArgumentNullException(nameof(reference));
		}

		var compiledAccess = _referenceTypeToCompiledAccessMap.GetOrAdd(reference.GetType(), static (type, context) =>
		{
			var adaptedLambdaExpression = KeysetAdaptingExpressionVisitor.AdaptType(context.LambdaExpression, type);
			return adaptedLambdaExpression.Compile();
		}, this);

		try
		{
			return compiledAccess.Invoke(reference)!;
		}
		catch (NullReferenceException ex)
		{
			// La fila del cursor se lee del mismo queryable que se pagina. Si la columna del keyset entra por una
			// navegacion y esa navegacion no vino cargada, el acceso revienta con un NullReferenceException pelado
			// desde adentro de un lambda compilado, sin decir cual columna ni por que.
			throw new InvalidOperationException(
				$"Keyset column '{LambdaExpression.Body}' could not be read from the cursor row: it goes through a navigation that is not loaded. Include that navigation in the queryable passed to ToKeysetQueryResult, or order by a column of the entity itself.",
				ex);
		}
	}

	private IOrderedQueryable<T> ApplyOrderByVariant<TQueryable>(
		TQueryable query,
		KeysetPaginationDirection direction,
		Func<TQueryable, Expression<Func<T, TColumn>>, IOrderedQueryable<T>> ascendingVariant,
		Func<TQueryable, Expression<Func<T, TColumn>>, IOrderedQueryable<T>> descendingVariant)
		where TQueryable : IQueryable<T>
	{
		var lambdaExpression = MakeAccessLambdaExpression();
		var isDescending = direction == KeysetPaginationDirection.Backward ? !IsDescending : IsDescending;
		return isDescending ? descendingVariant(query, lambdaExpression) : ascendingVariant(query, lambdaExpression);
	}

	/// <summary>
	///     Arma la lambda de acceso de esta columna.
	///     Usa el parametro que se le da si no es null; si no, crea y usa uno nuevo.
	/// </summary>
	private Expression<Func<T, TColumn>> MakeAccessLambdaExpression(ParameterExpression? parameter = null)
	{
		parameter ??= Expression.Parameter(typeof(T), "x");
		return KeysetAdaptingExpressionVisitor.AdaptParameter(LambdaExpression, parameter);
	}
}
