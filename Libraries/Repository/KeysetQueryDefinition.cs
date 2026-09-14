namespace DDS.Repository;

/// <summary>
///     Representa la definicion de una consulta por keyset.
/// </summary>
/// <typeparam name="TEntity">El tipo de la entidad.</typeparam>
public sealed class KeysetQueryDefinition<TEntity>
{
	internal KeysetQueryDefinition(IReadOnlyList<KeysetColumn<TEntity>> columns)
	{
		Columns = columns;
	}

	internal IReadOnlyList<KeysetColumn<TEntity>> Columns { get; }
}
