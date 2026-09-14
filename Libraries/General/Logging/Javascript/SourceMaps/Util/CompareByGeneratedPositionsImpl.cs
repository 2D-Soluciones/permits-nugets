namespace DDS.General.Logging.Javascript.SourceMaps.Util;

internal sealed class CompareByGeneratedPositions : CompareByPosition
{
	public override int Compare(Needle? x, Needle? y)
	{
		return Compare(x, y, false);
	}

	public override int Compare(Needle? x, Needle? y, bool fuzzySame)
	{
		switch (x)
		{
			case null when y is null:
				return 0;

			case null:
				return -1;
		}

		if (y is null)
		{
			return 1;
		}

		var cmp = x.GeneratedLine - y.GeneratedLine;

		if (cmp != 0)
		{
			return cmp;
		}

		cmp = x.GeneratedColumn - y.GeneratedColumn;

		if (cmp != 0 || fuzzySame)
		{
			return cmp;
		}

		cmp = StringCompare(x.Source, y.Source);

		if (cmp != 0)
		{
			return cmp;
		}

		cmp = x.OriginalLine - y.OriginalLine;

		if (cmp != 0)
		{
			return cmp;
		}

		cmp = x.OriginalColumn - y.OriginalColumn;

		return cmp != 0
			? cmp
			: StringCompare(x.Name, y.Name);
	}
}
