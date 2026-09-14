namespace DDS.Repository;

/// <inheritdoc />
public sealed class KeysetPaginationException : Exception
{
	/// <inheritdoc />
	public KeysetPaginationException(string message) : base(message) { }
}