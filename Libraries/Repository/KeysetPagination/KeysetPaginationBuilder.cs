using System.Linq.Expressions;
using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     Builder de una definicion de keyset.
/// </summary>
/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
[PublicAPI]
public sealed class KeysetPaginationBuilder<TEntity>
{
	internal List<KeysetColumn<TEntity>> Columns
	{
		get { return field ??= new(2); }
	}


	/// <summary>
	///     Configura una columna descendente como parte del keyset.
	/// </summary>
	public KeysetPaginationBuilder<TEntity> Descending<TColumn>(Expression<Func<TEntity, TColumn>> columnExpression)
	{
		return ConfigureColumn(columnExpression, true);
	}

	private KeysetPaginationBuilder<TEntity> ConfigureColumn<TColumn>(Expression<Func<TEntity, TColumn>> columnExpression, bool isDescending)
	{
		if (Nullable.GetUnderlyingType(typeof(TColumn)) is not null)
		{
			// Toda comparacion que arma el predicado es >, < o =, y las tres dan false contra NULL. Una página cuya
			// fila de cursor tenga un NULL volveria vacia para siempre, y las filas con NULL nunca apareceran después de
			// la primera página. Donde se corta cambia hasta con el proveedor, porque los NULL ordenan primeros en
			// SQLite y SQL Server, y ultimos en PostgreSQL.
			throw new ArgumentException($"'{columnExpression}' is nullable, and a keyset column has to be non-null. Order by a non-nullable column, or project the null away first.", nameof(columnExpression));
		}

		Columns.Add(new KeysetColumn<TEntity, TColumn>(isDescending, columnExpression));
		return this;
	}
}
