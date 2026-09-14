using JetBrains.Annotations;

namespace DDS.Domain;

/// <inheritdoc cref="Entity" />
[PublicAPI]
[NoReorder]
public abstract class Entity<TKey> : Entity where TKey : struct
{
	/// <summary>
	///     La clave de la entidad.
	/// </summary>
	public required TKey Id { get; init; }

	/// <inheritdoc />
	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}

	/// <inheritdoc />
	public override bool Equals(object? obj)
	{
		return obj is Entity<TKey> other && GetType() == obj.GetType() && Id.Equals(other.Id);
	}
}
