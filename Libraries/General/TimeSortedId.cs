using System.Buffers.Binary;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Clase para generar identificadores unicos ordenados por tiempo (TSID).
/// </summary>
[PublicAPI]
public sealed class TimeSortedId
{
	private const int RANDOM_BITS = 22;
	private const int RANDOM_MASK = 0x003fffff;
	private static readonly long _customEpoch = (long) (new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

	private readonly int _counterBits;
	private readonly int _counterMask;
	private readonly long _node;

	private readonly Lock _timeLock = new();
	private long _counter;
	private long _lastTime;

	/// <summary>
	///     Inicializa una instancia nueva de <see cref="TimeSortedId" />
	/// </summary>
	/// <param name="nodeCount">Cantidad de nodos.</param>
	/// <param name="node">Current node Id.</param>
	/// <exception cref="ArgumentException"></exception>
	public TimeSortedId(int nodeCount, int node)
	{
		//nodeCount <= 0 hace que Math.Log de -infinito y que nodeBits termine siendo int.MinValue, que despues se usa
		//como cantidad de bits a desplazar; node negativo pasaba derecho.
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodeCount);
		ArgumentOutOfRangeException.ThrowIfNegative(node);

		if (node >= nodeCount)
		{
			throw new ArgumentOutOfRangeException(nameof(node), node, $"Node ID out of range [0, {nodeCount - 1}].");
		}

		var nodeBits = (int) Math.Ceiling(Math.Log(nodeCount) / Math.Log(2));

		_counterBits = RANDOM_BITS - nodeBits;
		_counterMask = RANDOM_MASK >>> nodeBits;
		_node = node & (RANDOM_MASK >>> _counterBits);
	}

	/// <summary>
	///     Crea un Time-Sorted Id nuevo.
	/// </summary>
	/// <returns></returns>
	public long Next()
	{
		var (time, counter) = GetTime();
		var t = time << RANDOM_BITS;
		var node = _node << _counterBits;
		return t | node | (counter & _counterMask);
	}

	/// <summary>
	///     Devuelve una instancia por defecto de <see cref="TimeSortedId" /> con 8 nodos y node=1.
	/// </summary>
	/// <remarks>
	///     El id de nodo esta fijo en el codigo, asi que dos procesos que usen esta instancia lo comparten y los bits de nodo dejan de hacer
	///     lo unico para lo que existen. Arma el tuyo con el id del proceso o del pod cuando haya mas de un generador
	///     pueda correr a la vez.
	/// </remarks>
	public static TimeSortedId Instance { get; } = new(8, 1);

	private int GetRandomCounter()
	{
		return (int) NextInt() & _counterMask;
	}

	private (long time, long counter) GetTime()
	{
		var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		long counter;

		using (_timeLock.EnterScope())
		{
			if (time <= _lastTime)
			{
				++_counter;

				// El carry es 1 si hay overflow despues del ++.
				var carry = _counter >>> _counterBits;
				_counter &= _counterMask;
				time = _lastTime + carry; // increment time
			}
			else
			{
				// Si el reloj del sistema avanzo como se esperaba,
				// simplemente reinicio el contador con un valor aleatorio nuevo.
				_counter = GetRandomCounter();
			}

			// guardo la hora actual
			_lastTime = time;
			counter = _counter;
		}

		// ajusto al epoch propio
		return (time - _customEpoch, counter);
	}

	private static uint NextInt()
	{
		Span<byte> buffer = stackalloc byte[4];
		RandomNumberGenerator.Fill(buffer);
		return BinaryPrimitives.ReadUInt32BigEndian(buffer);
	}
}
