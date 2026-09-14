using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace DDS.Repository.EntityFramework;

[PublicAPI]
internal static class DynamicallyAccessedValue
{
	internal const DynamicallyAccessedMemberTypes DYNAMICALLY_ACCESSED_MEMBER_TYPES =
		DynamicallyAccessedMemberTypes.PublicConstructors
		| DynamicallyAccessedMemberTypes.NonPublicConstructors
		| DynamicallyAccessedMemberTypes.PublicProperties
		| DynamicallyAccessedMemberTypes.PublicFields
		| DynamicallyAccessedMemberTypes.NonPublicProperties
		| DynamicallyAccessedMemberTypes.NonPublicFields
		| DynamicallyAccessedMemberTypes.Interfaces;
}
