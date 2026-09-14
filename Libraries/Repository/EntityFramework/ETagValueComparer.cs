using DDS.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DDS.Repository.EntityFramework;

[UsedImplicitly]
internal sealed class ETagValueComparer : ValueComparer<ETag>
{
	public ETagValueComparer() : base((left, right) => DoCompare(left, right), instance => DoGetHash(instance)) { }

	private static bool DoCompare(ETag left, ETag right)
	{
		if (!left.IsInitialized() && !right.IsInitialized())
		{
			return true;
		}

		return left.IsInitialized() && right.IsInitialized() && left.ValueInternal.Equals(right.ValueInternal);
	}

	private static int DoGetHash(ETag instance)
	{
		return instance.IsInitialized() ? instance.GetHashCode() : 0;
	}
}
