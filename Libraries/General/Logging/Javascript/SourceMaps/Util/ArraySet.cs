using System.Collections;

namespace DDS.General.Logging.Javascript.SourceMaps.Util;

/// <inheritdoc />
/// <summary>
///     Estructura de datos que combina un arreglo y un set. Agregar un miembro nuevo es O(1), preguntar si algo
///     pertenece es O(1) y encontrar el indice de un elemento es O(1). No se pueden sacar elementos del set.
/// </summary>
/// <remarks>Port de la implementacion de Mozilla.</remarks>
internal sealed class ArraySet : IList<string>
{
	private readonly List<string> _list = [];
	private readonly Dictionary<string, int> _set = new();

	public ArraySet(IEnumerable<string>? items, bool allowDuplicates)
	{
		if (items is null)
		{
			return;
		}

		foreach (var item in items)
		{
			Add(item, allowDuplicates);
		}
	}

	public int Count
	{
		get { return _list.Count; }
	}

	public bool IsReadOnly
	{
		get { return false; }
	}

	public void Add(string item)
	{
		Add(item, false);
	}

	public void Clear()
	{
		_list.Clear();
		_set.Clear();
	}

	public bool Contains(string item)
	{
		return _set.ContainsKey(item);
	}

	public void CopyTo(string[] array, int arrayIndex)
	{
		for (int i = 0, l = _list.Count; i < l; ++i)
		{
			array[i + arrayIndex] = _list[i];
		}
	}

	public IEnumerator<string> GetEnumerator()
	{
		return _list.GetEnumerator();
	}

	public int IndexOf(string item)
	{
		if (_set.TryGetValue(item, out var idx))
		{
			return idx;
		}

		throw new ArgumentException($"The given item ({item}) is not in the set.", nameof(item));
	}

	public void Insert(int index, string item)
	{
		throw new NotImplementedException();
	}

	public bool Remove(string item)
	{
		throw new NotImplementedException();
	}

	public void RemoveAt(int index)
	{
		throw new NotImplementedException();
	}

	private void Add(string item, bool allowDuplicates)
	{
		var isDuplicated = _set.ContainsKey(item);
		var idx = _list.Count;

		if (!isDuplicated || allowDuplicates)
		{
			_list.Add(item);
		}

		if (!isDuplicated)
		{
			_set[item] = idx;
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return _list.GetEnumerator();
	}

	public string this[int index]
	{
		get
		{
			if (index >= 0 && index < _list.Count)
			{
				return _list[index];
			}

			throw new($"No element indexed by {index}");
		}
		set { throw new NotImplementedException(); }
	}
}
