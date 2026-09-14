using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     Clase helper para armar de antemano un <see cref="KeysetQueryDefinition{TModel}" />.
/// </summary>
[PublicAPI]
public static class KeysetQueryBuilder
{
	/// <summary>
	///     Builds a keyset query definition.
	/// </summary>
	/// <typeparam name="TModel">El tipo de la entidad.</typeparam>
	/// <param name="builderAction">
	///     Una accion que recibe un builder y registra las columnas sobre las que va a trabajar la paginacion por
	///     work.
	/// </param>
	/// <returns>El <see cref="KeysetQueryDefinition{TModel}" /> con la definicion de keyset ya armada.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="builderAction" /> es null.</exception>
	public static KeysetQueryDefinition<TModel> Build<TModel>(Action<KeysetPaginationBuilder<TModel>> builderAction)
	{
		ArgumentNullException.ThrowIfNull(builderAction);
		var columns = BuildColumns(builderAction);
		return new(columns);
	}

	internal static IReadOnlyList<KeysetColumn<T>> BuildColumns<T>(Action<KeysetPaginationBuilder<T>> builderAction)
	{
		var builder = new KeysetPaginationBuilder<T>();
		builderAction(builder);
		return builder.Columns;
	}
}
