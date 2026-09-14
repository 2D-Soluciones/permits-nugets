using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DDS.Repository.EntityFramework;

/// <summary>
///     Metodos de extension para ayudar a configurar las entidades de dominio en Entity Framework.
/// </summary>
[PublicAPI]
public static class EfConfigurationBuilderExtensions
{
	/// <summary>
	///     Define las claves por defecto de la entidad que se le pasa.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="keyExpression"></param>
	/// <typeparam name="TEntity"></typeparam>
	public static void HasEntityKey<[DynamicallyAccessedMembers(DynamicallyAccessedValue.DYNAMICALLY_ACCESSED_MEMBER_TYPES)] TEntity>(this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, object?>> keyExpression) where TEntity : class
	{
		builder.HasKey(keyExpression);
		builder.Property(keyExpression).ValueGeneratedNever();
	}

	/// <summary>
	/// Define el prefijo raiz de una entidad owned.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="separator"></param>
	/// <param name="prefix"></param>
	/// <typeparam name="TEntity"></typeparam>
	/// <typeparam name="TRelatedEntity"></typeparam>
	public static void WithPrefix<[DynamicallyAccessedMembers(DynamicallyAccessedValue.DYNAMICALLY_ACCESSED_MEMBER_TYPES)]TEntity, [DynamicallyAccessedMembers(DynamicallyAccessedValue.DYNAMICALLY_ACCESSED_MEMBER_TYPES)]TRelatedEntity>(this OwnedNavigationBuilder<TEntity, TRelatedEntity> builder, string separator, string prefix)
		where TEntity : class
		where TRelatedEntity : class
	{
		PrefixOwned(builder.OwnedEntityType, separator, prefix);
	}

	private static void PrefixOwned(IMutableEntityType entity, string separator, string prefix)
	{
		var properties = entity.GetProperties().Where(x => !x.IsShadowProperty());
		var fullPrefix = string.Concat(prefix, separator);
		foreach (var p in properties)
		{
			p.SetColumnName(string.Concat(fullPrefix, p.Name));
		}

		foreach (var n in entity.GetNavigations())
		{
			var target = n.TargetEntityType;

			if (n.IsCollection || !target.IsOwned())
			{
				continue;
			}

			PrefixOwned(target, separator,  string.Concat(fullPrefix, n.Name));
		}
	}
}
