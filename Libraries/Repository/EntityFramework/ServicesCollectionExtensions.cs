using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DDS.Repository.EntityFramework;

/// <summary>
///     Metodos de extension sobre <see cref="ServiceCollection" />.
/// </summary>
[PublicAPI]
public static class ServicesCollectionExtensions
{
	/// <summary>
	///     Le agrega a los <paramref name="services" /> que se le pasan los servicios de data protection de EntityFramework.
	/// </summary>
	/// <param name="services"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static IServiceCollection AddEntityFrameworkDataProtection<T>(this IServiceCollection services) where T : DbContext, IDataProtectionKeyContext
	{
		services
			.AddDataProtection()
			.PersistKeysToDbContext<T>()
			.UseCryptographicAlgorithms(new()
			{
				EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
				ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
			});

		return services;
	}
}
