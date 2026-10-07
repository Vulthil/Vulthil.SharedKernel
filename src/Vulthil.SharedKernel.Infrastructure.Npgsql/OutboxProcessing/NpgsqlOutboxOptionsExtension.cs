using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// Context options extension that adds <see cref="NpgsqlOutboxConventionSetPlugin"/> to EF Core's internal service
/// provider. <c>UseNpgsql</c> adds it, so the pending-message index filter follows the final column mapping.
/// </summary>
internal sealed class NpgsqlOutboxOptionsExtension : IDbContextOptionsExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NpgsqlOutboxOptionsExtension"/> class.
    /// </summary>
    public NpgsqlOutboxOptionsExtension() => Info = new ExtensionInfo(this);

    /// <inheritdoc />
    public DbContextOptionsExtensionInfo Info { get; }

    /// <summary>
    /// Adds the extension to <paramref name="optionsBuilder"/> unless it is already there.
    /// </summary>
    /// <param name="optionsBuilder">The context options to extend.</param>
    public static void AddTo(DbContextOptionsBuilder optionsBuilder) =>
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
            optionsBuilder.Options.FindExtension<NpgsqlOutboxOptionsExtension>() ?? new NpgsqlOutboxOptionsExtension());

    /// <inheritdoc />
    public void ApplyServices(IServiceCollection services) =>
        new EntityFrameworkServicesBuilder(services).TryAdd<IConventionSetPlugin, NpgsqlOutboxConventionSetPlugin>();

    /// <inheritdoc />
    public void Validate(IDbContextOptions options)
    {
        // The extension carries no settings, so there is nothing to validate.
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "NpgsqlOutboxConventions ";

        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["Vulthil:NpgsqlOutboxConventions"] = "1";
    }
}
