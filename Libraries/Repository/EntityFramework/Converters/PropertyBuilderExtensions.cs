using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DDS.Repository.EntityFramework.Converters;

/// <summary>
///     Un conjunto de metodos de extension para <see cref="PropertyBuilder{TProperty}" />.
/// </summary>
[PublicAPI]
public static class PropertyBuilderExtensions
{
	/// <summary>
	///     Le agrega un value converter JSON a la propiedad definida.
	/// </summary>
	/// <param name="propertyBuilder"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns>La misma instancia del builder, para poder encadenar varias llamadas de configuracion.</returns>
	[RequiresUnreferencedCode("EF Core isn't fully compatible with trimming.")]
	public static PropertyBuilder<T?> HasJsonValueConversion<T>(this PropertyBuilder<T?> propertyBuilder) where T : class
	{
		return propertyBuilder.HasMaxLength(int.MaxValue).HasConversion<JsonValueConverter<T>, JsonValueComparer<T>>();
	}
}
