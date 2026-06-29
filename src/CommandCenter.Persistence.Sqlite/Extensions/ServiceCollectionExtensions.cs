using CommandCenter.Persistence.Sqlite.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommandCenter.Persistence.Sqlite.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Derivation Cache persistence primitives — the connection factory, the derived
    /// snapshot cache, the source fingerprint provider, the repository locator, and the DB path options
    /// — all as singletons. Phase 0: this WIRES NOTHING into existing services; nothing consumes these
    /// yet, so adding the call is behaviour-neutral.
    /// </summary>
    public static IServiceCollection AddSqlitePersistence(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new SqliteDatabaseOptions());
        services.TryAddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.TryAddSingleton<IRepositoryLocator, RepositoryServiceLocator>();
        services.TryAddSingleton<IDerivedSnapshotCache, SqliteDerivedSnapshotCache>();
        services.TryAddSingleton<ISourceFingerprintProvider, DefaultSourceFingerprintProvider>();
        services.TryAddSingleton<IDerivedSnapshotReader, DerivedSnapshotReader>();
        services.TryAddSingleton<IPerRepositoryRecoveryGate, PerRepositoryRecoveryGate>();
        services.TryAddSingleton<IRecoveryLedgerStore, SqliteRecoveryLedgerStore>();
        return services;
    }
}
