using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Storage;

public sealed class WorkspaceStorageInspector : IWorkspaceStorageInspector
{
    /// <summary>
    /// Test-only observability: how many times <see cref="HashFileAsync"/> has computed a
    /// SHA-256 digest, across every file and every call, on this instance. Instance-scoped
    /// (rather than process-wide) so that concurrent xUnit test classes constructing their own
    /// <see cref="WorkspaceStorageInspector"/> - directly, or indirectly through
    /// <see cref="WorkspaceStorageVerifierAdapter"/> / <c>RepositoryObserver</c>'s
    /// <c>FileSystemStorageVerifier</c> - cannot pollute a count under assertion elsewhere. Used
    /// to prove that <see cref="VerifyAsync"/> reuses the inventory's hash for the database file
    /// instead of hashing it a second time on the deep tier (PERF: hash workspace database once per
    /// verification), and - since Task 3.9 deleted the light tier's full-file hash entirely - that a
    /// <see cref="StorageVerificationDepth.Light"/> verification digests nothing at all, however many
    /// files the persistence tree holds.
    ///
    /// <para>
    /// Exposed as a get-only property backed by <see cref="fileHashInvocations"/> (rather than a
    /// bare mutable field) for shape consistency with the other test-only counters added by this
    /// wave - <c>RotatingJsonlTelemetrySink.RescanCount</c> and
    /// <c>SqliteSessionTelemetrySink.InitializationCount</c> - both of which are properties with
    /// private setters so test code cannot write the counter. This one stays get-only rather than
    /// <c>{ get; private set; }</c> because the increment below still needs
    /// <see cref="Interlocked.Increment(ref int)"/> on a real field for atomicity; a property
    /// setter cannot be passed by <c>ref</c>. Kept instance-scoped (not static) - do not change
    /// that back; it was deliberately converted from static to instance in commit
    /// <c>ac7ce40e</c> to fix a real cross-test flake.
    /// </para>
    /// </summary>
    internal int FileHashInvocations => fileHashInvocations;

    private int fileHashInvocations;

    public async Task<StorageInspection> VerifyAsync(
        StorageVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        bool deep = request.Depth == StorageVerificationDepth.Deep;
        string root = Path.GetFullPath(request.RepositoryPath);
        string database = Path.Combine(root,
            LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
        string persistence = Path.GetDirectoryName(database)!;
        IReadOnlyList<StorageTreeEntry> inventory = await InventoryAsync(root, persistence, deep, cancellationToken);
        if (!File.Exists(database))
        {
            return new StorageInspection(
                StorageHealth.ActionRequired, false, null, null, null, inventory, [],
                Interrupted(inventory), ["Run `storage init` to create a new canonical authority."],
                inventory.Select(item => item.RelativePath).ToArray());
        }

        // Sampling-order note: on the deep tier byteHash below is satisfied from the inventory
        // entry computed inside InventoryAsync, which ran before this Length read - so under a
        // concurrent writer, length and hash are now sampled in the opposite order relative to
        // each other compared to when the hash used to be computed here, after the length read.
        // Acceptable: VerifyAsync is read-only verification, not a consistency-guaranteeing
        // snapshot, so a stale-relative-to-each-other length/hash pair here is cosmetic, not a
        // defect.
        long length = new FileInfo(database).Length;
        string databaseRelativePath = Path.GetRelativePath(root, database).Replace('\\', '/');
        // OrdinalIgnoreCase: entry.RelativePath carries the on-disk casing reported by
        // Directory.GetFiles in InventoryAsync, which need not match the constant casing of
        // LoopRelayWorkspaceDatabase.RelativeDatabasePath baked into databaseRelativePath (e.g.
        // case-insensitive filesystems, or a file renamed only in case). Comparing ordinally
        // here would silently miss the inventory entry and force a redundant rehash on the deep
        // tier, defeating the single-hash optimization without failing anything.
        //
        // Task 3.9: the database digest is deep-tier only now. It used to be computed
        // unconditionally - including on the light tier, which routine observation pays for on
        // every kernel cycle - solely to feed a `bytes-sha256:` evidence line. That line does
        // reach durable state (OrchestrationKernel.Snapshot hashes it into
        // canonical_kernel_decisions.snapshot_identity, and it is rendered in CLI/JSON evidence
        // output), but tracing every consumer of it found none that ever reads it back to
        // compare, detect drift, or gate a decision: it was write-only provenance. So the light
        // tier no longer computes it at all - byteHash is null unless deep, and the evidence line
        // below is omitted whenever it is. Deep tier is untouched: the `storage` commands and
        // certification still hash the database (and every other persistence file) because that
        // is a real, explicit verification surface with a real consumer
        // (WorkspaceStorageApplicationService.MigrateAsync's source-fingerprint fallback, among
        // others).
        string? byteHash = deep
            ? inventory.FirstOrDefault(entry =>
                    string.Equals(entry.RelativePath, databaseRelativePath, StringComparison.OrdinalIgnoreCase))
                ?.Sha256 ?? await HashFileAsync(database, cancellationToken)
            : null;
        WorkspaceSchemaInspection schema;
        IReadOnlyList<string> unresolved;
        try
        {
            // Deep is the authority on physical shape and runs the ~190-probe classification. Light
            // answers from the stamp, falling back to that same classification whenever the stamp is
            // absent, malformed, or internally inconsistent.
            //
            // Residual gap, stated precisely: a well-formed stamp over a physical shape that has
            // since been mutated out-of-band. Do NOT read the mutation path as an independent second
            // layer that re-checks physical shape here. LoopRelayWorkspaceDatabase.EnsureSchemaAsync
            // runs the full InspectSchemaAsync classification only on FIRST CONTACT PER PROCESS for
            // a given database path; thereafter its per-process memo short-circuits on
            // MatchesCachedStampAsync, which re-reads only `schema_version` and the shape
            // fingerprint back out of `schema_metadata` - the stamp, not the shape. So after first
            // contact the mutation guard trusts the stamp on exactly the same terms the light tier
            // does; that is Core commit fa8a5286's documented, tested trade-off (see the doc comment
            // on MatchesCachedStampAsync, which says so in as many words), not an extra guarantee
            // available to this method.
            //
            // What EnsureSchemaAsync does still contribute unconditionally is `PRAGMA
            // foreign_keys = ON`, executed on every call before the memo is consulted, because
            // PRAGMA state is per-connection and cannot be memoized. That part holds for every
            // write connection, first contact or not.
            schema = deep
                ? await new WorkspaceSchemaReadOnlyInspector().InspectAsync(database, cancellationToken)
                : await StampedSchemaAsync(database, cancellationToken);
            unresolved = deep ? await ForeignKeyViolationsAsync(database, cancellationToken) : [];
        }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException)
        {
            return new StorageInspection(
                StorageHealth.Corrupt, true, length, byteHash, null, inventory, [], Interrupted(inventory),
                ["Restore or explicitly repair the corrupt workspace authority."],
                [exception.GetType().Name, "SQLite authority is unreadable."]);
        }

        string[] interrupted = Interrupted(inventory)
            .Concat(await InterruptedRowsAsync(database, cancellationToken))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        StorageHealth health;
        var actions = new List<string>();
        if (schema.Version is > LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
        {
            health = StorageHealth.Unsupported;
            actions.Add($"Use a LoopRelay version supporting schema v{schema.Version}.");
        }
        else if (schema.Shape == WorkspaceSchemaShape.CanonicalV16Complete &&
                 unresolved.Count == 0 && interrupted.Length == 0)
        {
            health = StorageHealth.Healthy;
        }
        else if (schema.Family == WorkspaceSchemaFamily.CanonicalWorkspace &&
                 schema.Shape is not (WorkspaceSchemaShape.Unknown or WorkspaceSchemaShape.UnknownV9Shape or
                     WorkspaceSchemaShape.CorruptCanonicalV9 or WorkspaceSchemaShape.CorruptCanonicalV10 or
                     WorkspaceSchemaShape.CorruptCanonicalV11 or WorkspaceSchemaShape.CorruptCanonicalV12 or
                     WorkspaceSchemaShape.CorruptCanonicalV13 or WorkspaceSchemaShape.CorruptCanonicalV14 or
                     WorkspaceSchemaShape.CorruptCanonicalV15 or WorkspaceSchemaShape.CorruptCanonicalV16))
        {
            health = StorageHealth.ActionRequired;
            string chain = string.Join(" -> ", WorkspaceSchemaMigrationCatalog.Plan(schema.Version));
            actions.Add($"Run `storage migrate`; planned target versions: {chain}.");
        }
        else
        {
            health = StorageHealth.Corrupt;
            actions.Add("Use explicit compatibility import or repair; verification will not mutate this authority.");
        }
        if (unresolved.Count > 0) actions.Add("Resolve canonical foreign-key references before mutation.");
        if (interrupted.Length > 0) actions.Add("Recover the interrupted storage operation before starting another.");

        // `bytes-sha256:` only appears when a digest was actually computed - i.e. on the deep
        // tier. The light tier's evidence is therefore one line shorter than the deep tier's for
        // the same authority; see the Task 3.9 note above VerifyAsync's byteHash computation for
        // why that evidence-contract change is safe.
        var evidence = new List<string>
        {
            $"schema:{schema.SchemaIdentity ?? "unknown"}",
            $"family:{schema.Family}",
            $"version:{schema.Version?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}",
            $"shape:{schema.Shape}",
            $"shape-fingerprint:{schema.ShapeFingerprint ?? "unknown"}",
        };
        if (byteHash is not null) evidence.Add($"bytes-sha256:{byteHash}");

        return new StorageInspection(
            health, true, length, byteHash, schema, inventory, unresolved, interrupted, actions, evidence);
    }

    /// <summary>
    /// Lists the persistence tree. <paramref name="hashContents"/> is the whole cost difference
    /// between the two tiers: every consumer of the tree's <em>digests</em> is a deep consumer, and
    /// the routine-observation path needs only the relative paths, because
    /// <see cref="Interrupted"/> looks at nothing else. Lengths are read at both depths so the
    /// entries stay honest about the one field a light listing can still answer cheaply.
    /// </summary>
    private async Task<IReadOnlyList<StorageTreeEntry>> InventoryAsync(
        string root,
        string persistence,
        bool hashContents,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(persistence)) return [];
        var result = new List<StorageTreeEntry>();
        foreach (string file in Directory.GetFiles(persistence, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            result.Add(new StorageTreeEntry(
                Path.GetRelativePath(root, file).Replace('\\', '/'),
                new FileInfo(file).Length,
                hashContents ? await HashFileAsync(file, cancellationToken) : null));
        }
        return result;
    }

    /// <summary>
    /// The light tier's schema read. Deliberately reaches
    /// <see cref="LoopRelayWorkspaceDatabase.InspectStampedAsync"/> directly rather than going
    /// through <see cref="WorkspaceSchemaReadOnlyInspector"/>: that inspector stays on the full
    /// classification on purpose, because it is also the thing that inspects untrusted files on
    /// demand. The <c>File.Exists</c> guard it applies is already satisfied by the caller.
    /// </summary>
    private static async Task<WorkspaceSchemaInspection> StampedSchemaAsync(
        string database,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        return await LoopRelayWorkspaceDatabase.InspectStampedAsync(connection, cancellationToken);
    }

    private static string[] Interrupted(IReadOnlyList<StorageTreeEntry> inventory) => inventory
        .Where(item => item.RelativePath.EndsWith("-journal", StringComparison.OrdinalIgnoreCase) ||
                       item.RelativePath.EndsWith(".storage-stage", StringComparison.OrdinalIgnoreCase))
        .Select(item => item.RelativePath)
        .ToArray();

    private static async Task<IReadOnlyList<string>> InterruptedRowsAsync(
        string database,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        var result = new List<string>();
        if (await TableExistsAsync(connection, "workflow_transactions", cancellationToken))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT transaction_id, status FROM workflow_transactions
                WHERE status <> 'Completed' OR completed_at IS NULL ORDER BY started_at, transaction_id;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add($"workflow_transactions:{reader.GetString(0)}:{reader.GetString(1)}");
        }
        if (await TableExistsAsync(connection, "canonical_storage_operation_plans", cancellationToken))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT operation_id, current_lifecycle FROM canonical_storage_operation_plans
                WHERE current_lifecycle NOT IN ('Completed','Refused') ORDER BY planned_at, operation_id;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add($"storage-operation:{reader.GetString(0)}:{reader.GetString(1)}");
        }
        return result;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$table;";
        command.Parameters.AddWithValue("$table", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>
    /// Runs <c>PRAGMA foreign_key_check</c> against an arbitrary database file. Unlike
    /// <see cref="VerifyAsync"/>, this does not resolve its target from a repository root via
    /// <see cref="LoopRelayWorkspaceDatabase.RelativeDatabasePath"/> - it is exposed publicly for
    /// callers (<c>CanonicalImportGateway.ExecuteAsync</c>) that must run the same deep, fail-closed
    /// FK check against a staged working database that has not - and, if the check fails, must
    /// never - been promoted to a repository's canonical location, and so has no repository path to
    /// resolve one from.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ForeignKeyViolationsAsync(
        string database,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(database);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        var result = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add($"{reader.GetString(0)}:{reader.GetInt64(1)}:{(reader.IsDBNull(2) ? "unknown" : reader.GetString(2))}");
        return result;
    }

    private async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref fileHashInvocations);
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
