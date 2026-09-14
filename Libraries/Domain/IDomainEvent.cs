using JetBrains.Annotations;
using Mediator;

namespace DDS.Domain;

/// <summary>
///     Interfaz marcadora de los eventos de dominio.
/// </summary>
[PublicAPI]
public interface IDomainEvent : INotification;