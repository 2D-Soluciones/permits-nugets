namespace DDS.Repository;

/// <summary>
///     Direccion a usar al buscar items.
/// </summary>
public enum KeysetPaginationDirection
{
	/// <summary>
	///     Busca los items posteriores al que se le pasa.
	/// </summary>
	Forward,

	/// <summary>
	///     Busca los items anteriores al que se le pasa.
	/// </summary>
	Backward
}