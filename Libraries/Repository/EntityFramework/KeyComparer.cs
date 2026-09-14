using System.Runtime.CompilerServices;

namespace DDS.Repository.EntityFramework;

internal static class KeyComparer<T>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsDefault(T value)
	{
		return EqualityComparer<T>.Default.Equals(value, default!);
	}
}
