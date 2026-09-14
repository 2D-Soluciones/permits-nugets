namespace DDS.General.Logging.Javascript.SourceMaps.Util;

internal abstract class CompareByPosition : ICompareByPosition
{
	public abstract int Compare(Needle? x, Needle? y, bool fuzzySame);
	public abstract int Compare(Needle? x, Needle? y);

	protected static int StringCompare(string? left, string? right)
	{
		return StringComparer.Ordinal.Compare(left ?? string.Empty, right ?? string.Empty);
	}
}

internal interface ICompareByPosition : IComparer<Needle>
{
	int Compare(Needle? x, Needle? y, bool fuzzySame);
}