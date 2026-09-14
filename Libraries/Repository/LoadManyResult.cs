using JetBrains.Annotations;

namespace DDS.Repository;

/// <summary>
///     El resultado de una carga de varias entidades.
/// </summary>
/// <param name="Loaded">Una coleccion con las entidades cargadas.</param>
/// <param name="NotFound">Una coleccion con las claves de las entidades que no se encontraron.</param>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TEntity"></typeparam>
[PublicAPI]
public sealed record LoadManyResult<TEntity, TKey>(IReadOnlyCollection<TEntity> Loaded, IReadOnlyCollection<TKey> NotFound)
{
	/// <summary>
	///     Devuelve <c>true</c> si hay algun item cargado, <c>false</c> si no.
	/// </summary>
	public bool HasLoaded
	{
		get { return Loaded.Count > 0; }
	}

	/// <summary>
	///     Devuelve <c>true</c> si al menos un item no se encontro, <c>false</c> si se cargo todo.
	/// </summary>
	public bool HasNotFound
	{
		get { return NotFound.Count > 0; }
	}
}
