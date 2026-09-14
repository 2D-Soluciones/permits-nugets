using DDS.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DDS.Repository.EntityFramework;

[UsedImplicitly]
internal sealed class ETagValueConverter : ValueConverter<ETag, string>
{
	public ETagValueConverter() : this(null) { }

	public ETagValueConverter(ConverterMappingHints? mappingHints = null)
		: base(vo => vo.Value, value => ETag.From(value), mappingHints) { }
}
