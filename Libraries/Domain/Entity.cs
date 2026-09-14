using JetBrains.Annotations;

namespace DDS.Domain;

/// <summary>
///     Clase base de todas las entidades de un dominio.
/// </summary>
[PublicAPI, NoReorder]
public abstract class Entity
{
	// ReSharper disable once CollectionNeverQueried.Local
	private List<IDomainEvent>? _queuedNotifications;

	/// <summary>
	///     Publica una notificacion de dominio.
	/// </summary>
	/// <param name="domainEvent"></param>
	protected void PublishEvent(IDomainEvent domainEvent)
	{
		_queuedNotifications ??= new(1);
		_queuedNotifications.Add(domainEvent);
	}
}
