using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Clase parecida a <c>lock</c>/MonitorEnter/MonitorExit pero asociada a una clave.
/// </summary>
/// <remarks>Las claves se comparan sin distinguir mayusculas, asi que <c>"User"</c> y <c>"user"</c> comparten un lock.</remarks>
[PublicAPI]
public static class NamedLock
{
	private static readonly ConcurrentDictionary<string, Semaphore> _waitLocks = new(StringComparer.OrdinalIgnoreCase);


	/// <summary>
	///     Crea una nueva instancia y bloquea el contexto asincrónicamente hasta adquirir el paso.
	/// </summary>
	/// <remarks>
	///     Es imprescindible disposear el resultado, ya que si no cualquier acceso subsiguiente estará bloqueado
	///     indefinidamente.
	/// </remarks>
	/// <param name="key">El nombre de la key.</param>
	/// <param name="cancellationToken">
	///     Sin esto un solo holder colgado bloquea a todos los que esperan, para siempre.
	/// </param>
	[UsedImplicitly]
	public static async Task<NamedLockSemaphore> EnterAsync(string key, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);
		var semaphore = GetOrAdd(key);

		try
		{
			await semaphore.WaitAsync(cancellationToken);
		}
		catch
		{
			Abandon(key, semaphore);
			throw;
		}

		return new(key, semaphore);
	}

	private static Semaphore GetOrAdd(string key)
	{
		var spin = new SpinWait();

		while (true)
		{
			var lck = _waitLocks.GetOrAdd(key, _ => new());

			if (lck.TryIncrement())
			{
				return lck;
			}

			// El semaforo se esta disposeando: giro hasta que lo saquen y pueda crear uno nuevo
			spin.SpinOnce();
		}
	}

	/// <summary>
	///     Suelta una referencia a <paramref name="semaphore" /> sin liberarlo, y lo disposea cuando era la ultima.
	/// </summary>
	private static void Abandon(string key, Semaphore semaphore)
	{
		if (semaphore.Decrement() > 0)
		{
			return;
		}

		// El contador llego a 0: intento quedarme con la propiedad para disposearlo
		if (!semaphore.TryMarkForDisposal())
		{
			return;
		}

		_waitLocks.TryRemove(new(key, semaphore));
		semaphore.Dispose();
	}

	/// <summary>
	///     Un semaforo de named lock. Se libera exactamente una vez, con <see cref="Dispose" />.
	/// </summary>
	/// <remarks>
	///     Es una clase y no un struct a proposito. Un valor copiable cuyo contrato es "disposear exactamente una vez"
	///     no se puede defender solo: un segundo dispose liberaba el semaforo que para entonces estuviera bajo la
	///     clave, dejandole la cuenta por encima de uno y rompiendo calladito la exclusion mutua que este tipo da.
	/// </remarks>
	public sealed class NamedLockSemaphore : IDisposable
	{
		private readonly string _key;
		private readonly Semaphore _semaphore;
		private int _disposed;

		internal NamedLockSemaphore(string key, Semaphore semaphore)
		{
			_key = key;
			_semaphore = semaphore;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
			{
				return;
			}

			//sin catch: un SemaphoreFullException acá significa que la exclusión mutua se rompió, y eso tiene que
			//verse. El Debug.WriteLine que había se compila afuera en Release, así que no se veía nunca.
			_semaphore.ReleaseLock();
			Abandon(_key, _semaphore);
		}
	}

	internal sealed class Semaphore : IEquatable<Semaphore>, IDisposable
	{
		private readonly SemaphoreSlim _lock = new(1, 1);

		private int _counter;

		/// <inheritdoc />
		public void Dispose()
		{
			_lock.Dispose();
		}

		/// <inheritdoc />
		public bool Equals(Semaphore? other)
		{
			if (other is null)
			{
				return false;
			}

			return ReferenceEquals(this, other) || _lock.Equals(other._lock);
		}

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			return ReferenceEquals(this, obj) || (obj is Semaphore other && Equals(other));
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return _lock.GetHashCode();
		}

		/// <summary>
		///     Incrementa el contador en forma atomica si el semaforo no esta siendo disposeado.
		/// </summary>
		/// <returns><c>true</c> si lo pudo incrementar; <c>false</c> si ya esta marcado para disposear.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryIncrement()
		{
			var spin = new SpinWait();

			while (true)
			{
				var current = Volatile.Read(ref _counter);

				if (current < 0)
				{
					return false;
				}

				if (Interlocked.CompareExchange(ref _counter, current + 1, current) == current)
				{
					return true;
				}

				spin.SpinOnce();
			}
		}

		/// <summary>
		///     Decrementa el contador y devuelve el valor nuevo.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Decrement()
		{
			return Interlocked.Decrement(ref _counter);
		}

		/// <summary>
		///     Intenta marcar este semaforo para disposear, en forma atomica, poniendo el contador en -1.
		///     Solo funciona si el contador actual es exactamente 0.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryMarkForDisposal()
		{
			return Interlocked.CompareExchange(ref _counter, -1, 0) == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ReleaseLock()
		{
			_lock.Release();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Wait()
		{
			_lock.Wait();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Task WaitAsync(CancellationToken cancellationToken)
		{
			return _lock.WaitAsync(cancellationToken);
		}
	}
}
