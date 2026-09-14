using System.Diagnostics.CodeAnalysis;
using DDS.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DDS.Repository.EntityFramework;

/// <summary>
///     El <see cref="DbContext" /> base del que tienen que heredar los que usen esta biblioteca.
/// </summary>
/// <typeparam name="TContext"></typeparam>
[PublicAPI]
[RequiresDynamicCode("EF Core isn't fully compatible with NativeAOT.")]
[RequiresUnreferencedCode("EF Core isn't fully compatible with trimming.")]
public abstract class EfDbContext<TContext> : DbContext where TContext : DbContext
{
	/// <inheritdoc />
	protected EfDbContext(DbContextOptions<TContext> options) : base(options) { }

	/// <inheritdoc />
	public override void Dispose()
	{
		EntityContextInterceptor.Release(this);
		base.Dispose();
	}

	/// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		EntityContextInterceptor.Release(this);
		await base.DisposeAsync();
	}

	/// <summary>
	///     Devuelve la ultima version del historial de migraciones.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns>El ID de la ultima migracion, o una cadena vacia si no hay ninguna.</returns>
	public async Task<string> GetMigrationVersion(CancellationToken cancellationToken = default)
	{
		var migration = await Set<Migrations>().OrderBy(x => x.Id).LastOrDefaultAsync(cancellationToken);
		return migration?.Id ?? string.Empty;
	}

	/// <summary>
	///     Carga una entidad desde el <see cref="DbContext" />.
	/// </summary>
	/// <param name="id"></param>
	/// <param name="cancellationToken"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public ValueTask<T?> Load<T>(object id, CancellationToken cancellationToken) where T : class
	{
		return Set<T>().FindAsync([id], cancellationToken);
	}

	/// <inheritdoc />
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Properties<ETag>().HaveConversion<ETagValueConverter, ETagValueComparer>();
		base.ConfigureConventions(configurationBuilder);
	}

	/// <summary>
	///     Provee una implementacion propia de <see cref="TimeProvider" /> para obtener las fechas de
	///     create/update/delete modifications.
	/// </summary>

	// ReSharper disable once ReturnTypeCanBeNotNullable
	protected virtual TimeProvider? GetTimeProvider()
	{
		return TimeProvider.System;
	}

	/// <inheritdoc />
	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.AddInterceptors(new SaveChangesInterceptor(GetTimeProvider), EntityContextInterceptor.Instance);
		base.OnConfiguring(optionsBuilder);
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		var entities = modelBuilder.GetEntities();

		ApplyGlobal<IETag>(modelBuilder, entities, builder =>
		{
			builder.Property(nameof(IETag.ETag)).IsConcurrencyToken().HasMaxLength(12);
			builder.HasIndex(nameof(IETag.ETag));
		});

		modelBuilder.ApplyConfiguration(new MigrationsConfiguration());
		base.OnModelCreating(modelBuilder);
	}

	private static void ApplyGlobal<TInterface>(ModelBuilder modelBuilder, Type[] entities, Action<EntityTypeBuilder> builder)
	{
		foreach (var entity in entities.FilterByInterface<TInterface>(modelBuilder))
		{
			builder(entity);
		}
	}
}

internal sealed class Migrations
{
	public required string Id { get; init; }
}

internal sealed class MigrationsConfiguration : IEntityTypeConfiguration<Migrations>
{
	public void Configure(EntityTypeBuilder<Migrations> builder)
	{
		builder.ToTable("__EFMigrationsHistory", x => x.ExcludeFromMigrations());
		builder.Property(x => x.Id).HasColumnName("MigrationId");
	}
}
