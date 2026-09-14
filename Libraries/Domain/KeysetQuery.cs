using JetBrains.Annotations;

namespace DDS.Domain;

/// <summary>
/// Objeto de consulta para paginar por keyset (con cursor).
/// Es mas eficiente que paginar por offset en conjuntos grandes, porque evita leer y descartar filas.
/// </summary>
/// <typeparam name="T">El tipo de la clave que se usa para paginar (int, long, Guid, DateTime, etc.).</typeparam>
[NoReorder]
[PublicAPI]
public record KeysetQuery<T> where T : struct
{
	/// <summary>
	/// Indica si hay que traer la primera pagina de resultados.
	/// Si es true, la paginacion arranca desde el principio del conjunto.
	/// </summary>
	public bool First { get; set; }

	/// <summary>
	/// Indica si hay que traer la ultima pagina de resultados.
	/// Si es true, la paginacion arranca desde el final del conjunto (en orden inverso).
	/// </summary>
	public bool Last { get; set; }

	/// <summary>
	/// Valor del cursor para traer los items *anteriores* a el.
	/// Se usa para ir hacia atras (paginas previas).
	/// </summary>
	public T? Before { get; set; }

	/// <summary>
	/// Valor del cursor para traer los items *posteriores* a el.
	/// Se usa para ir hacia adelante (paginas siguientes).
	/// </summary>
	public T? After { get; set; }

	/// <summary>
	/// Cantidad maxima de items a devolver en una pagina.
	/// Si es null, el handler suele aplicar un tamanio por defecto.
	/// </summary>
	public int? Size { get; set; }
}

/// <summary>
/// Resultado de una consulta paginada por keyset.
/// Trae la pagina de datos y los metadatos necesarios para armar los links de navegacion (siguiente/anterior).
/// </summary>
/// <typeparam name="TEntity">El tipo de la entidad consultada.</typeparam>
[NoReorder]
[PublicAPI]
public record KeysetQueryResult<TEntity> where TEntity : class
{
	/// <summary>
	/// Las entidades devueltas para la pagina actual.
	/// </summary>
	public required IReadOnlyList<TEntity> Data { get; init; }

	/// <summary>
	/// Cantidad total de items disponibles en todas las paginas.
	/// Ojo: calcularlo pide un COUNT aparte, que en tablas grandes sale caro.
	/// Dejalo en null cuando no haga falta el total, para no pagar ese costo.
	/// </summary>
	public required int? TotalCount { get; init; }

	/// <summary>
	/// Cantidad de items pedidos por pagina.
	/// </summary>
	public required int PageSize { get; init; }

	/// <summary>
	/// Estado del resultado de la paginacion: si es la primera o la ultima pagina, y si hay paginas anteriores o
	/// siguientes.
	/// </summary>
	public required KeysetQueryState State { get; init; }
}

/// <summary>
/// Flags con el estado del resultado de una paginacion por keyset.
/// Sirven para saber que links de navegacion ofrecer y en que posicion esta la pagina actual.
/// </summary>
[PublicAPI]
[Flags]
public enum KeysetQueryState
{
	/// <summary>
	/// El estado no esta definido.
	/// </summary>
	Undefined = 0,

	/// <summary>
	/// La pagina actual es la primera del conjunto.
	/// </summary>
	IsFirst = 1,

	/// <summary>
	/// Hay mas items despues de la pagina actual (se puede ir hacia adelante).
	/// </summary>
	HasNext = 2,

	/// <summary>
	/// Hay items antes de la pagina actual (se puede ir hacia atras).
	/// </summary>
	HasPrevious = 4,

	/// <summary>
	/// La pagina actual es la ultima del conjunto.
	/// </summary>
	IsLast = 8
}
