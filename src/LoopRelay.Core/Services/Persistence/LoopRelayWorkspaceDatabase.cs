using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

public enum WorkspaceSchemaFamily
{
    Empty,
    CanonicalWorkspace,
    LegacyContinuity,
    Unknown,
}

public enum WorkspaceSchemaShape
{
    Empty,
    PreLineageCanonical,
    LegacyContinuityV3,
    Merge4V9Partial,
    ArchitectureConvergenceV9Partial,
    RecognizedMixedV9Partial,
    CanonicalV9Complete,
    CanonicalV10Complete,
    CanonicalV11Complete,
    CanonicalV12Complete,
    CanonicalV13Complete,
    CanonicalV14Complete,
    CanonicalV15Complete,
    UnknownV9Shape,
    CorruptCanonicalV9,
    CorruptCanonicalV10,
    CorruptCanonicalV11,
    CorruptCanonicalV12,
    CorruptCanonicalV13,
    CorruptCanonicalV14,
    CorruptCanonicalV15,
    Unknown,
}

public sealed record WorkspaceSchemaInspection(
    string? SchemaIdentity,
    WorkspaceSchemaFamily Family,
    int? Version,
    bool HasExplicitLineage,
    WorkspaceSchemaShape Shape,
    string? ShapeFingerprint,
    string Diagnostic);

public sealed class WorkspaceCompatibilityImportRequiredException(
    WorkspaceSchemaInspection inspection)
    : InvalidOperationException(
        $"Workspace database requires explicit compatibility import: {inspection.Diagnostic}")
{
    public WorkspaceSchemaInspection Inspection { get; } = inspection;
}

/// <summary>
/// Shared owner for the local `.LoopRelay` SQLite workspace database contract.
/// </summary>
public static class LoopRelayWorkspaceDatabase
{
    public const string SchemaIdentity = "looprelay.workspace-state";
    public const string SchemaFamily = "CanonicalWorkspace";
    public const string SchemaShapeMetadataKey = "schema_shape";
    public const int CurrentSchemaVersion = 15;
    public const string RelativeDatabasePath = ".LoopRelay/persistence/looprelay.sqlite3";

    public static string CanonicalV9ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV9Requirements.Select(requirement => requirement.Token));

    public static string CanonicalV10ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV10Requirements.Select(requirement => requirement.Token));

    public static string CanonicalV11ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV11Requirements.Select(requirement => requirement.Token));

    public static string CanonicalV12ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV12Requirements.Select(requirement => requirement.Token));

    public static string CanonicalV13ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV13Requirements.Select(requirement => requirement.Token));

    public static string CanonicalV14ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV14Requirements.Select(requirement => requirement.Token));

    public static string CanonicalV15ShapeFingerprint =>
        ComputeShapeFingerprint(CanonicalV15Requirements.Select(requirement => requirement.Token));

    public static string Resolve(Repository repository)
    {
        string workspaceRoot = Path.GetFullPath(repository.Path);
        string databasePath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(workspaceRoot, databasePath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Resolved workspace database path escaped the repository root.");
        }

        return databasePath;
    }

    public static async Task EnsureSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
        WorkspaceSchemaInspection inspection = await InspectSchemaAsync(connection, cancellationToken);
        if (inspection.Family == WorkspaceSchemaFamily.LegacyContinuity)
        {
            throw new WorkspaceCompatibilityImportRequiredException(inspection);
        }

        if (inspection.Family == WorkspaceSchemaFamily.Unknown)
        {
            throw new InvalidOperationException($"Unsupported workspace schema: {inspection.Diagnostic}");
        }

        if (inspection.Version is > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite schema version `{inspection.Version}`; expected `1..{CurrentSchemaVersion}`.");
        }

        string? preservedWorkspaceId = await ValidateExistingWorkspaceIdentityAsync(
            connection,
            cancellationToken);
        bool structurallyComplete = inspection.Shape == WorkspaceSchemaShape.CanonicalV15Complete;
        if (structurallyComplete && preservedWorkspaceId is null)
        {
            throw new InvalidOperationException(
                "Canonical v15 is stamped complete but has no immutable workspace identity.");
        }

        LegacyResumeImport? legacyResume = await ReadLegacyResumeAsync(connection, cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            if (structurallyComplete)
            {
                if (legacyResume is not null)
                {
                    await ImportLegacyResumeAsync(connection, transaction, legacyResume, cancellationToken);
                }

                await ExecuteAsync(connection, transaction, CanonicalDataRepairSql, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            await EnsureCanonicalV8ShapeAsync(connection, transaction, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV9Sql, cancellationToken);
            await EnsureV9ColumnsAsync(connection, transaction, cancellationToken);
            await EnsureV10ColumnsAsync(connection, transaction, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV10Sql, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV11Sql, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV12Sql, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV13Sql, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV14Sql, cancellationToken);
            await EnsureV14ColumnsAsync(connection, transaction, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV15Sql, cancellationToken);
            if (legacyResume is not null)
            {
                await ImportLegacyResumeAsync(connection, transaction, legacyResume, cancellationToken);
            }
            await ExecuteAsync(connection, transaction, CanonicalDataRepairSql, cancellationToken);
            string workspaceId = await EnsureImmutableWorkspaceIdentityAsync(
                connection,
                transaction,
                inspection,
                preservedWorkspaceId,
                cancellationToken);
            await VerifyCanonicalV15ShapeAsync(connection, transaction, cancellationToken);
            await RecordSchemaConvergenceAsync(
                connection,
                transaction,
                inspection,
                workspaceId,
                cancellationToken);
            await StampCanonicalSchemaAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public static async Task<WorkspaceSchemaInspection> InspectSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(connection, "schema_metadata", cancellationToken))
        {
            long tableCount = await CountUserTablesAsync(connection, cancellationToken);
            return tableCount == 0
                ? new WorkspaceSchemaInspection(
                    null, WorkspaceSchemaFamily.Empty, null, false,
                    WorkspaceSchemaShape.Empty, null, "Empty database.")
                : new WorkspaceSchemaInspection(
                    null, WorkspaceSchemaFamily.Unknown, null, false,
                    WorkspaceSchemaShape.Unknown, null,
                    "Database contains tables but no schema metadata.");
        }

        string? identity = await ReadMetadataValueAsync(connection, "schema_identity", cancellationToken);
        string? family = await ReadMetadataValueAsync(connection, "schema_family", cancellationToken);
        string? stampedShape = await ReadMetadataValueAsync(connection, SchemaShapeMetadataKey, cancellationToken);
        int? version = await ReadExistingSchemaVersionAsync(connection, cancellationToken);
        if (identity is not null || family is not null)
        {
            bool canonical = string.Equals(identity, SchemaIdentity, StringComparison.Ordinal) &&
                string.Equals(family, SchemaFamily, StringComparison.Ordinal);
            if (!canonical)
            {
                return new WorkspaceSchemaInspection(
                    identity,
                    WorkspaceSchemaFamily.Unknown,
                    version,
                    true,
                    WorkspaceSchemaShape.Unknown,
                    stampedShape,
                    $"Unrecognized schema lineage identity='{identity}', family='{family}'.");
            }

            if (version == CurrentSchemaVersion)
            {
                return await ClassifyV15ShapeAsync(
                    connection,
                    identity,
                    hasExplicitLineage: true,
                    stampedShape,
                    cancellationToken);
            }

            if (version == 14)
            {
                return await ClassifyV14ShapeAsync(
                    connection, identity, hasExplicitLineage: true, stampedShape, cancellationToken);
            }

            if (version == 13)
            {
                return await ClassifyV13ShapeAsync(
                    connection, identity, hasExplicitLineage: true, stampedShape, cancellationToken);
            }

            if (version == 12)
            {
                return await ClassifyV12ShapeAsync(
                    connection,
                    identity,
                    hasExplicitLineage: true,
                    stampedShape,
                    cancellationToken);
            }

            if (version == 11)
            {
                return await ClassifyV11ShapeAsync(
                    connection,
                    identity,
                    hasExplicitLineage: true,
                    stampedShape,
                    cancellationToken);
            }

            if (version == 10)
            {
                return await ClassifyV10ShapeAsync(
                    connection,
                    identity,
                    hasExplicitLineage: true,
                    stampedShape,
                    cancellationToken);
            }

            if (version == 9)
            {
                return await ClassifyV9ShapeAsync(
                    connection,
                    identity,
                    hasExplicitLineage: true,
                    stampedShape,
                    cancellationToken);
            }

            return new WorkspaceSchemaInspection(
                identity,
                WorkspaceSchemaFamily.CanonicalWorkspace,
                version,
                true,
                WorkspaceSchemaShape.PreLineageCanonical,
                stampedShape,
                $"Canonical workspace schema v{version} requires migration to v{CurrentSchemaVersion}.");
        }

        bool legacyContinuity = version == 3 &&
            await TableExistsAsync(connection, "session_continuity_profiles", cancellationToken) &&
            await TableExistsAsync(connection, "decision_session_scopes", cancellationToken) &&
            !await TableExistsAsync(connection, "workspace_identity", cancellationToken);
        if (legacyContinuity)
        {
            return new WorkspaceSchemaInspection(
                "looprelay.legacy-continuity",
                WorkspaceSchemaFamily.LegacyContinuity,
                version,
                false,
                WorkspaceSchemaShape.LegacyContinuityV3,
                null,
                "Detected branch-local LegacyContinuity v3 by structural fingerprint.");
        }

        if (version == 9)
        {
            return await ClassifyV9ShapeAsync(
                connection,
                schemaIdentity: null,
                hasExplicitLineage: false,
                stampedShape,
                cancellationToken);
        }


        if (version == CurrentSchemaVersion)
        {
            return await ClassifyV15ShapeAsync(
                connection,
                schemaIdentity: null,
                hasExplicitLineage: false,
                stampedShape,
                cancellationToken);
        }

        if (version == 14)
        {
            return await ClassifyV14ShapeAsync(
                connection, schemaIdentity: null, hasExplicitLineage: false, stampedShape, cancellationToken);
        }

        if (version == 13)
        {
            return await ClassifyV13ShapeAsync(
                connection, schemaIdentity: null, hasExplicitLineage: false, stampedShape, cancellationToken);
        }

        if (version == 12)
        {
            return await ClassifyV12ShapeAsync(
                connection,
                schemaIdentity: null,
                hasExplicitLineage: false,
                stampedShape,
                cancellationToken);
        }

        if (version == 11)
        {
            return await ClassifyV11ShapeAsync(
                connection,
                schemaIdentity: null,
                hasExplicitLineage: false,
                stampedShape,
                cancellationToken);
        }

        if (version == 10)
        {
            return await ClassifyV10ShapeAsync(
                connection,
                schemaIdentity: null,
                hasExplicitLineage: false,
                stampedShape,
                cancellationToken);
        }

        // Numeric pre-lineage versions 1..8 belong to the historical canonical family unless
        // the v3 continuity fingerprint above proves otherwise. Older test/production databases
        // may contain only the tables introduced by their version, so table completeness cannot
        // be required before the migration itself reconstructs the canonical shape.
        bool recognizableCanonical = version is >= 1 and <= 8;
        return recognizableCanonical
            ? new WorkspaceSchemaInspection(
                SchemaIdentity,
                WorkspaceSchemaFamily.CanonicalWorkspace,
                version,
                false,
                WorkspaceSchemaShape.PreLineageCanonical,
                null,
                $"Recognized pre-lineage canonical workspace schema v{version}.")
            : new WorkspaceSchemaInspection(
                null,
                WorkspaceSchemaFamily.Unknown,
                version,
                false,
                WorkspaceSchemaShape.Unknown,
                null,
                $"Schema version {version?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"} has an unknown structural fingerprint.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV9ShapeAsync(
        SqliteConnection connection,
        string? schemaIdentity,
        bool hasExplicitLineage,
        string? stampedShape,
        CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection,
            CanonicalV9Requirements,
            transaction: null,
            cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool complete = HasAll(satisfied, CanonicalV9Requirements);
        bool coreCanonical = HasAll(satisfied, CoreCanonicalSignatureRequirements);
        bool merge4Complete = HasAll(satisfied, Merge4V9Requirements);
        bool incomingComplete = HasAll(satisfied, ArchitectureConvergenceV9Requirements);
        bool anyMerge4 = HasAny(satisfied, Merge4V9Requirements);
        bool anyIncoming = HasAny(satisfied, ArchitectureConvergenceV9Requirements);

        if (stampedShape is not null)
        {
            bool validStamp = hasExplicitLineage &&
                string.Equals(stampedShape, CanonicalV9ShapeFingerprint, StringComparison.Ordinal) &&
                complete;
            if (!validStamp)
            {
                return new WorkspaceSchemaInspection(
                    schemaIdentity,
                    WorkspaceSchemaFamily.Unknown,
                    9,
                    hasExplicitLineage,
                    WorkspaceSchemaShape.CorruptCanonicalV9,
                    observedFingerprint,
                    "Canonical-v9 shape stamp is missing its declared physical contract or lineage; mutation is blocked.");
            }

            return new WorkspaceSchemaInspection(
                SchemaIdentity,
                WorkspaceSchemaFamily.CanonicalWorkspace,
                9,
                true,
                WorkspaceSchemaShape.CanonicalV9Complete,
                observedFingerprint,
                "Canonical workspace v9 lineage and complete physical-shape fingerprint verified.");
        }

        if (!coreCanonical)
        {
            return new WorkspaceSchemaInspection(
                schemaIdentity,
                WorkspaceSchemaFamily.Unknown,
                9,
                hasExplicitLineage,
                hasExplicitLineage
                    ? WorkspaceSchemaShape.CorruptCanonicalV9
                    : WorkspaceSchemaShape.UnknownV9Shape,
                observedFingerprint,
                "Schema version 9 does not match the canonical workspace spine; mutation is blocked.");
        }

        WorkspaceSchemaShape shape;
        if (merge4Complete && !anyIncoming)
        {
            shape = WorkspaceSchemaShape.Merge4V9Partial;
        }
        else if (incomingComplete && !anyMerge4)
        {
            shape = WorkspaceSchemaShape.ArchitectureConvergenceV9Partial;
        }
        else if ((merge4Complete && anyIncoming) || (incomingComplete && anyMerge4) || complete)
        {
            shape = WorkspaceSchemaShape.RecognizedMixedV9Partial;
        }
        else
        {
            return new WorkspaceSchemaInspection(
                schemaIdentity,
                WorkspaceSchemaFamily.Unknown,
                9,
                hasExplicitLineage,
                hasExplicitLineage
                    ? WorkspaceSchemaShape.CorruptCanonicalV9
                    : WorkspaceSchemaShape.UnknownV9Shape,
                observedFingerprint,
                "Schema version 9 has an incomplete or contradictory physical shape that matches no supported provisional v9 contract.");
        }

        return new WorkspaceSchemaInspection(
            schemaIdentity ?? SchemaIdentity,
            WorkspaceSchemaFamily.CanonicalWorkspace,
            9,
            hasExplicitLineage,
            shape,
            observedFingerprint,
            $"Recognized {shape} by strict canonical-v9 structural fingerprint; convergence is required.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV10ShapeAsync(
        SqliteConnection connection,
        string? schemaIdentity,
        bool hasExplicitLineage,
        string? stampedShape,
        CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection,
            CanonicalV10Requirements,
            transaction: null,
            cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool complete = HasAll(satisfied, CanonicalV10Requirements);
        bool validStamp = hasExplicitLineage &&
            string.Equals(stampedShape, CanonicalV10ShapeFingerprint, StringComparison.Ordinal) &&
            complete;
        if (!validStamp)
        {
            return new WorkspaceSchemaInspection(
                schemaIdentity,
                WorkspaceSchemaFamily.Unknown,
                10,
                hasExplicitLineage,
                WorkspaceSchemaShape.CorruptCanonicalV10,
                observedFingerprint,
                "Canonical-v10 shape stamp is missing its declared effect-work contract or lineage; mutation is blocked.");
        }

        return new WorkspaceSchemaInspection(
            SchemaIdentity,
            WorkspaceSchemaFamily.CanonicalWorkspace,
            10,
            true,
            WorkspaceSchemaShape.CanonicalV10Complete,
            observedFingerprint,
            "Canonical workspace v10 lineage and complete physical-shape fingerprint verified.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV11ShapeAsync(
        SqliteConnection connection,
        string? schemaIdentity,
        bool hasExplicitLineage,
        string? stampedShape,
        CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection,
            CanonicalV11Requirements,
            transaction: null,
            cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool complete = HasAll(satisfied, CanonicalV11Requirements);
        bool validStamp = hasExplicitLineage &&
            string.Equals(stampedShape, CanonicalV11ShapeFingerprint, StringComparison.Ordinal) &&
            complete;
        if (!validStamp)
        {
            return new WorkspaceSchemaInspection(
                schemaIdentity,
                WorkspaceSchemaFamily.Unknown,
                11,
                hasExplicitLineage,
                WorkspaceSchemaShape.CorruptCanonicalV11,
                observedFingerprint,
                "Canonical-v11 shape stamp is missing its declared recovery-authority contract or lineage; mutation is blocked.");
        }

        return new WorkspaceSchemaInspection(
            SchemaIdentity,
            WorkspaceSchemaFamily.CanonicalWorkspace,
            11,
            true,
            WorkspaceSchemaShape.CanonicalV11Complete,
            observedFingerprint,
            "Canonical workspace v11 lineage and complete physical-shape fingerprint verified.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV12ShapeAsync(
        SqliteConnection connection,
        string? schemaIdentity,
        bool hasExplicitLineage,
        string? stampedShape,
        CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection, CanonicalV12Requirements, transaction: null, cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool validStamp = hasExplicitLineage &&
            string.Equals(stampedShape, CanonicalV12ShapeFingerprint, StringComparison.Ordinal) &&
            HasAll(satisfied, CanonicalV12Requirements);
        if (!validStamp)
        {
            return new WorkspaceSchemaInspection(
                schemaIdentity, WorkspaceSchemaFamily.Unknown, 12, hasExplicitLineage,
                WorkspaceSchemaShape.CorruptCanonicalV12, observedFingerprint,
                "Canonical-v12 shape stamp is missing its declared interaction-broker contract or lineage; mutation is blocked.");
        }
        return new WorkspaceSchemaInspection(
            SchemaIdentity, WorkspaceSchemaFamily.CanonicalWorkspace, 12, true,
            WorkspaceSchemaShape.CanonicalV12Complete, observedFingerprint,
            "Canonical workspace v12 lineage and complete physical-shape fingerprint verified.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV13ShapeAsync(
        SqliteConnection connection,
        string? schemaIdentity,
        bool hasExplicitLineage,
        string? stampedShape,
        CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection, CanonicalV13Requirements, transaction: null, cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool validStamp = hasExplicitLineage &&
            string.Equals(stampedShape, CanonicalV13ShapeFingerprint, StringComparison.Ordinal) &&
            HasAll(satisfied, CanonicalV13Requirements);
        if (!validStamp)
        {
            return new WorkspaceSchemaInspection(
                schemaIdentity, WorkspaceSchemaFamily.Unknown, 13, hasExplicitLineage,
                WorkspaceSchemaShape.CorruptCanonicalV13, observedFingerprint,
                "Canonical-v13 shape stamp is missing its declared storage-authority contract or lineage; mutation is blocked.");
        }
        return new WorkspaceSchemaInspection(
            SchemaIdentity, WorkspaceSchemaFamily.CanonicalWorkspace, 13, true,
            WorkspaceSchemaShape.CanonicalV13Complete, observedFingerprint,
            "Canonical workspace v13 lineage and complete physical-shape fingerprint verified.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV14ShapeAsync(
        SqliteConnection connection, string? schemaIdentity, bool hasExplicitLineage,
        string? stampedShape, CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection, CanonicalV14Requirements, transaction: null, cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool validStamp = hasExplicitLineage &&
            string.Equals(stampedShape, CanonicalV14ShapeFingerprint, StringComparison.Ordinal) &&
            HasAll(satisfied, CanonicalV14Requirements);
        if (!validStamp)
            return new WorkspaceSchemaInspection(schemaIdentity, WorkspaceSchemaFamily.Unknown, 14,
                hasExplicitLineage, WorkspaceSchemaShape.CorruptCanonicalV14, observedFingerprint,
                "Canonical-v14 shape stamp is missing its declared import-gateway contract or lineage; mutation is blocked.");
        return new WorkspaceSchemaInspection(SchemaIdentity, WorkspaceSchemaFamily.CanonicalWorkspace, 14, true,
            WorkspaceSchemaShape.CanonicalV14Complete, observedFingerprint,
            "Canonical workspace v14 lineage and complete physical-shape fingerprint verified.");
    }

    private static async Task<WorkspaceSchemaInspection> ClassifyV15ShapeAsync(
        SqliteConnection connection, string? schemaIdentity, bool hasExplicitLineage,
        string? stampedShape, CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection, CanonicalV15Requirements, transaction: null, cancellationToken);
        string observedFingerprint = ComputeShapeFingerprint(satisfied);
        bool validStamp = hasExplicitLineage &&
            string.Equals(stampedShape, CanonicalV15ShapeFingerprint, StringComparison.Ordinal) &&
            HasAll(satisfied, CanonicalV15Requirements);
        if (!validStamp)
            return new WorkspaceSchemaInspection(schemaIdentity, WorkspaceSchemaFamily.Unknown, 15,
                hasExplicitLineage, WorkspaceSchemaShape.CorruptCanonicalV15, observedFingerprint,
                "Canonical-v15 shape stamp is missing its declared completion-authority contract or lineage; mutation is blocked.");
        return new WorkspaceSchemaInspection(SchemaIdentity, WorkspaceSchemaFamily.CanonicalWorkspace, 15, true,
            WorkspaceSchemaShape.CanonicalV15Complete, observedFingerprint,
            "Canonical workspace v15 lineage and complete physical-shape fingerprint verified.");
    }

    private static async Task<string?> ValidateExistingWorkspaceIdentityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        string? canonical = await ReadOptionalWorkspaceIdentityAsync(
            connection,
            "workspace_identity",
            "SELECT workspace_id FROM workspace_identity WHERE id = 1;",
            cancellationToken);
        string? legacy = await ReadOptionalWorkspaceIdentityAsync(
            connection,
            "workspace_metadata",
            "SELECT value FROM workspace_metadata WHERE key = 'workspace_id';",
            cancellationToken);
        if ((canonical is not null && string.IsNullOrWhiteSpace(canonical)) ||
            (legacy is not null && string.IsNullOrWhiteSpace(legacy)))
        {
            throw new InvalidOperationException("Workspace identity must not be empty.");
        }

        if (canonical is not null && legacy is not null &&
            !string.Equals(canonical, legacy, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Workspace identity is immutable, but canonical and legacy identity records disagree.");
        }

        return canonical ?? legacy;
    }

    private static async Task<string?> ReadOptionalWorkspaceIdentityAsync(
        SqliteConnection connection,
        string table,
        string query,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = query;
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task VerifyCanonicalV15ShapeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        HashSet<string> satisfied = await ReadSatisfiedRequirementsAsync(
            connection,
            CanonicalV15Requirements,
            transaction,
            cancellationToken);
        string[] missing = CanonicalV15Requirements
            .Where(requirement => !satisfied.Contains(requirement.Token))
            .Select(requirement => requirement.Token)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Canonical-v15 migration verification failed. Missing contract requirements: " +
                string.Join(", ", missing.Take(12)) +
                (missing.Length > 12 ? $" (+{missing.Length - 12} more)." : "."));
        }

        string fingerprint = ComputeShapeFingerprint(satisfied);
        if (!string.Equals(fingerprint, CanonicalV15ShapeFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Canonical-v15 migration produced fingerprint '{fingerprint}', expected '{CanonicalV15ShapeFingerprint}'.");
        }
    }

    private static async Task EnsureCanonicalV8ShapeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, SchemaSql, cancellationToken);
        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "canonical_product_records",
            "schema_version",
            "text not null default '1'",
            cancellationToken);
        foreach ((string table, string column) in V6LineageColumns)
        {
            await AddColumnIfMissingAsync(
                connection, transaction, table, column, "text", cancellationToken);
        }

        await AddColumnIfMissingAsync(
            connection, transaction, "attempts", "policy_id", "text", cancellationToken);
        foreach (string column in (string[])["effort", "sandbox"])
        {
            await AddColumnIfMissingAsync(
                connection, transaction, "agent_sessions", column, "text", cancellationToken);
        }
    }

    private static async Task EnsureV9ColumnsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach ((string table, string column) in V9CausalColumns)
        {
            await AddColumnIfMissingAsync(
                connection, transaction, table, column, "text", cancellationToken);
        }

        foreach ((string column, string type) in V9TurnEvidenceColumns)
        {
            await AddColumnIfMissingAsync(
                connection, transaction, "agent_turns", column, type, cancellationToken);
        }
    }

    private static async Task EnsureV10ColumnsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach ((string column, string declaration) in V10EffectIntentColumns)
        {
            await AddColumnIfMissingAsync(
                connection,
                transaction,
                "canonical_effect_intents",
                column,
                declaration,
                cancellationToken);
        }
    }

    private static async Task<string> EnsureImmutableWorkspaceIdentityAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkspaceSchemaInspection inspection,
        string? preservedWorkspaceId,
        CancellationToken cancellationToken)
    {
        string? existing = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT workspace_id FROM workspace_identity WHERE id = 1;",
            cancellationToken);
        string? legacy = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT value FROM workspace_metadata WHERE key = 'workspace_id';",
            cancellationToken);
        string workspaceId = preservedWorkspaceId ?? existing ?? legacy ?? WorkspaceIdentity.New().Value;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("Workspace identity must not be empty.");
        }

        if (existing is not null && legacy is not null && !string.Equals(existing, legacy, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Workspace identity is immutable, but canonical and legacy identity records disagree.");
        }


        if (preservedWorkspaceId is not null &&
            ((existing is not null && !string.Equals(existing, preservedWorkspaceId, StringComparison.Ordinal)) ||
             (legacy is not null && !string.Equals(legacy, preservedWorkspaceId, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Workspace identity changed between schema inspection and convergence.");
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_identity (id, workspace_id, created_at)
            VALUES (1, $workspace_id, $created_at)
            ON CONFLICT(id) DO NOTHING;
            """,
            cancellationToken,
            ("$workspace_id", workspaceId),
            ("$created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        string identityFormat = workspaceId.StartsWith("ws_", StringComparison.Ordinal)
            ? "prefixed-ulid"
            : "legacy-opaque";
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_identity_metadata (
                id, identity_format, migration_source, imported_at
            )
            VALUES (1, $identity_format, $migration_source, $imported_at)
            ON CONFLICT(id) DO NOTHING;
            """,
            cancellationToken,
            ("$identity_format", identityFormat),
            ("$migration_source", inspection.Family == WorkspaceSchemaFamily.Empty
                ? "fresh-v14"
                : $"{inspection.Shape}:v{inspection.Version}"),
            ("$imported_at", inspection.Family == WorkspaceSchemaFamily.Empty
                ? null
                : DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        return workspaceId;
    }

    private static async Task StampCanonicalSchemaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach ((string key, string value) in (KeyValuePair<string, string>[])
        [
            new("schema_identity", SchemaIdentity),
            new("schema_family", SchemaFamily),
            new("schema_version", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)),
            new(SchemaShapeMetadataKey, CanonicalV15ShapeFingerprint),
        ])
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO schema_metadata (key, value)
                VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """,
                cancellationToken,
                ("$key", key),
                ("$value", value));
        }
    }

    private static async Task RecordSchemaConvergenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkspaceSchemaInspection inspection,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_metadata (key, value)
            VALUES ('persistence_state', 'empty')
            ON CONFLICT(key) DO NOTHING;
            """,
            cancellationToken);

        string completedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (inspection.Version != CurrentSchemaVersion)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO workspace_schema_migrations (
                    migration_id, schema_identity, schema_family, from_version, to_version,
                    workspace_id, applied_at
                )
                VALUES (
                    $migration_id, $schema_identity, $schema_family, $from_version, $to_version,
                    $workspace_id, $applied_at
                )
                ON CONFLICT(migration_id) DO NOTHING;
                """,
                cancellationToken,
                ("$migration_id", $"{SchemaIdentity}:{inspection.Version ?? 0}->{CurrentSchemaVersion}:{workspaceId}"),
                ("$schema_identity", SchemaIdentity),
                ("$schema_family", SchemaFamily),
                ("$from_version", inspection.Version ?? 0),
                ("$to_version", CurrentSchemaVersion),
                ("$workspace_id", workspaceId),
                ("$applied_at", completedAt));
        }

        string sourceFingerprint = inspection.ShapeFingerprint ?? "none";
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_schema_convergences (
                convergence_id, source_shape, source_fingerprint, source_version,
                target_fingerprint, workspace_id, completed_at
            )
            VALUES (
                $convergence_id, $source_shape, $source_fingerprint, $source_version,
                $target_fingerprint, $workspace_id, $completed_at
            )
            ON CONFLICT(convergence_id) DO NOTHING;
            """,
            cancellationToken,
            ("$convergence_id", $"canonical-v15:{workspaceId}:{inspection.Shape}:{sourceFingerprint}"),
            ("$source_shape", inspection.Shape.ToString()),
            ("$source_fingerprint", sourceFingerprint),
            ("$source_version", inspection.Version),
            ("$target_fingerprint", CanonicalV15ShapeFingerprint),
            ("$workspace_id", workspaceId),
            ("$completed_at", completedAt));
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string column,
        string declaration,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, table, column, cancellationToken, transaction))
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"ALTER TABLE {table} ADD COLUMN {column} {declaration};",
                cancellationToken);
        }
    }

    private static async Task EnsureV14ColumnsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await AddColumnIfMissingAsync(connection, transaction, "runs", "catalog_identity",
            "text not null default ''", cancellationToken);
        await AddColumnIfMissingAsync(connection, transaction, "runs", "catalog_version",
            "text not null default ''", cancellationToken);
        await AddColumnIfMissingAsync(connection, transaction, "workflow_instances", "catalog_identity",
            "text not null default ''", cancellationToken);
        await AddColumnIfMissingAsync(connection, transaction, "attempts", "agent_role_policy_id",
            "text", cancellationToken);
    }

    public static async Task<string> ReadWorkspaceIdentityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT workspace_id FROM workspace_identity WHERE id = 1;";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        string? value = scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Workspace identity has not been seeded in the workspace database.");
        }

        return value;
    }

    public static async Task<string> EnsureSchemaAndReadWorkspaceIdAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken);
        return await ReadWorkspaceIdentityAsync(connection, cancellationToken);
    }

    private static async Task<int?> ReadExistingSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "schema_metadata", cancellationToken))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = 'schema_version';";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        string? value = scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        if (value is null)
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new InvalidOperationException($"Unsupported SQLite schema version `{value}`; expected `1..{CurrentSchemaVersion}`.");
        }

        return parsed;
    }

    private static async Task<string?> ReadMetadataValueAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountUserTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
    }

    private static readonly (string Table, string Column)[] V6LineageColumns =
    [
        ("loop_history", "history_id"),
        ("loop_history", "run_id"),
        ("loop_history", "transition_run_id"),
        ("loop_history", "attempt_id"),
        ("read_receipts", "transition_run_id"),
        ("evaluation_warnings", "transition_run_id"),
        ("canonical_gate_evaluations", "transition_run_id"),
    ];

    private static readonly (string Table, string Column)[] V9CausalColumns =
    [
        ("loop_history", "workspace_id"),
        ("loop_history", "workflow_instance_id"),
        ("loop_history", "session_id"),
        ("loop_history", "turn_id"),
        ("loop_history", "supersedes_id"),
        ("loop_history", "producer_run_id"),
        ("loop_history", "producer_lineage_id"),
        ("loop_history", "provider_thread_id"),
        ("loop_history", "provider_turn_id"),
        ("loop_history", "recovery_attempt_id"),
        ("canonical_rendered_prompts", "persistence_id"),
        ("canonical_rendered_prompts", "prompt_policy_profile_id"),
        ("canonical_rendered_prompts", "consumed_input_manifest_id"),
        ("canonical_rendered_prompts", "rendered_encoding"),
        ("prompt_dispatch_events", "session_id"),
        ("prompt_dispatch_events", "turn_id"),
        ("runtime_profile_evaluations", "provider_capability_json"),
        ("session_telemetry_events", "provider_thread_id"),
        ("session_telemetry_events", "lineage_id"),
        ("session_telemetry_events", "transition_run_id"),
        ("session_telemetry_events", "recovery_attempt_id"),
        ("session_telemetry_events", "continuity_event_type"),
        ("session_telemetry_events", "continuity_outcome"),
    ];

    private static readonly (string Column, string Type)[] V9TurnEvidenceColumns =
    [
        ("state", "text"),
        ("prompt_sha256", "text"),
        ("prompt_tokens", "integer"),
        ("output_tokens", "integer"),
        ("cached_input_tokens", "integer"),
        ("diagnostics_kind", "text"),
        ("diagnostics", "text"),
    ];

    private static readonly (string Column, string Declaration)[] V10EffectIntentColumns =
    [
        ("workspace_id", "text"),
        ("run_id", "text"),
        ("workflow_instance_id", "text"),
        ("semantic_operation_key", "text"),
        ("executor_key", "text"),
        ("executor_version", "text"),
        ("target_json", "text"),
        ("payload_json", "text"),
        ("payload_hash", "text"),
        ("requiredness", "text"),
        ("dependencies_json", "text"),
        ("precondition_json", "text"),
        ("postcondition_json", "text"),
        ("reconciliation_policy", "text"),
        ("row_version", "integer not null default 0"),
        ("lease_owner", "text"),
        ("lease_expires_at", "text"),
        ("attempt_count", "integer not null default 0"),
        ("terminal_receipt_id", "text"),
    ];

    private static readonly string[] CanonicalCoreTableNames =
    [
        "schema_metadata",
        "workspace_metadata",
        "canonical_workflow_states",
        "canonical_stage_states",
        "canonical_transition_runs",
        "canonical_transition_evidence",
        "canonical_product_records",
        "canonical_gate_evaluations",
        "canonical_effect_records",
        "evaluation_warnings",
        "canonical_recovery_markers",
        "canonical_chain_boundary_events",
        "session_telemetry_events",
        "canonical_policy_resolutions",
        "canonical_rendered_prompts",
        "workspace_identity",
        "runs",
        "workflow_instances",
        "attempts",
        "agent_sessions",
        "agent_turns",
        "read_receipts",
    ];

    private static readonly string[] Merge4V9TableNames =
    [
        "workspace_schema_migrations",
        "workspace_identity_metadata",
        "session_continuity_profiles",
        "decision_session_scopes",
        "decision_session_lineage",
        "decision_session_active",
        "session_recovery_plans",
        "session_recovery_attempts",
        "session_recovery_sources",
        "decision_session_turns",
        "session_transition_correlations",
        "decision_session_legacy_imports",
        "history_evidence_sets",
        "history_evidence_items",
        "compatibility_import_operations",
        "compatibility_import_events",
        "canonical_projection_effects",
        "transition_recovery_plans",
        "canonical_effect_intents",
        "execution_recommendation_evidence",
        "runtime_profile_evaluations",
        "prompt_dispatch_events",
        "persistence_projection_checkpoints",
    ];

    private static readonly string[] Merge4V9IndexNames =
    [
        "idx_history_evidence_items_set",
        "idx_loop_history_history_id",
        "idx_history_evidence_provider",
        "idx_history_evidence_recovery",
        "idx_compatibility_import_events_operation",
        "idx_projection_effects_status",
        "idx_transition_recovery_plans_run",
        "idx_canonical_effect_intents_status",
        "idx_prompt_dispatch_events_dispatch",
        "idx_prompt_dispatch_events_attempt",
        "idx_execution_recommendation_decision",
        "idx_runtime_profile_evaluation_decision",
        "idx_decision_scope_lifecycle",
        "idx_decision_lineage_scope_authority",
        "idx_decision_lineage_parent",
        "idx_recovery_attempt_scope_status",
        "idx_decision_turn_transition",
    ];

    private static IReadOnlyList<ShapeRequirement> CoreCanonicalSignatureRequirements =>
    [
        ShapeRequirement.Table("workspace_identity"),
        ShapeRequirement.Table("runs"),
        ShapeRequirement.Table("workflow_instances"),
        ShapeRequirement.Table("attempts"),
        ShapeRequirement.Table("agent_sessions"),
        ShapeRequirement.Table("agent_turns"),
        ShapeRequirement.Table("canonical_rendered_prompts"),
    ];

    private static IReadOnlyList<ShapeRequirement> Merge4V9Requirements =>
        Merge4V9TableNames.Select(ShapeRequirement.Table)
            .Concat(V9CausalColumns.Select(item => ShapeRequirement.Column(item.Table, item.Column, "text")))
            .Concat(Merge4V9IndexNames.Select(ShapeRequirement.Index))
            .Concat(
            [
                ShapeRequirement.ForeignKey("decision_session_lineage", "scope_id", "decision_session_scopes", "scope_id"),
                ShapeRequirement.ForeignKey("decision_session_lineage", "parent_lineage_id", "decision_session_lineage", "lineage_id"),
                ShapeRequirement.ForeignKey("session_recovery_attempts", "scope_id", "decision_session_scopes", "scope_id"),
                ShapeRequirement.ForeignKey("decision_session_turns", "scope_id", "decision_session_scopes", "scope_id"),
                ShapeRequirement.ForeignKey("history_evidence_items", "evidence_set_id", "history_evidence_sets", "evidence_set_id"),
            ])
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> ArchitectureConvergenceV9Requirements =>
        V9TurnEvidenceColumns
            .Select(item => ShapeRequirement.Column("agent_turns", item.Column, item.Type))
            .Concat(
            [
                ShapeRequirement.Table("canonical_runtime_prerequisites"),
                ShapeRequirement.Column("canonical_runtime_prerequisites", "prerequisite_check_id", "text"),
                ShapeRequirement.Column("canonical_runtime_prerequisites", "run_id", "text"),
                ShapeRequirement.Column("canonical_runtime_prerequisites", "checked_at", "text"),
                ShapeRequirement.Column("canonical_runtime_prerequisites", "diagnostics_json", "text"),
                ShapeRequirement.Index("idx_canonical_runtime_prerequisites_run"),
            ])
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> ConvergenceReceiptRequirements =>
    [
        ShapeRequirement.Table("workspace_schema_convergences"),
        ShapeRequirement.Column("workspace_schema_convergences", "convergence_id", "text"),
        ShapeRequirement.Column("workspace_schema_convergences", "source_shape", "text"),
        ShapeRequirement.Column("workspace_schema_convergences", "source_fingerprint", "text"),
        ShapeRequirement.Column("workspace_schema_convergences", "source_version", "integer"),
        ShapeRequirement.Column("workspace_schema_convergences", "target_fingerprint", "text"),
        ShapeRequirement.Column("workspace_schema_convergences", "workspace_id", "text"),
        ShapeRequirement.Column("workspace_schema_convergences", "completed_at", "text"),
    ];

    private static IReadOnlyList<ShapeRequirement> CanonicalV9Requirements =>
        CanonicalCoreTableNames.Select(ShapeRequirement.Table)
            .Concat(Merge4V9Requirements)
            .Concat(ArchitectureConvergenceV9Requirements)
            .Concat(ConvergenceReceiptRequirements)
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> CanonicalV10Requirements =>
        CanonicalV9Requirements
            .Concat(V10EffectIntentColumns.Select(item => ShapeRequirement.Column(
                "canonical_effect_intents",
                item.Column,
                item.Declaration.StartsWith("integer", StringComparison.Ordinal) ? "integer" : "text")))
            .Concat(
            [
                ShapeRequirement.Table("canonical_effect_lifecycle_events"),
                ShapeRequirement.Table("canonical_effect_receipts"),
                ShapeRequirement.Table("canonical_effect_reconciliation_attempts"),
                ShapeRequirement.Index("idx_effect_intents_unsettled"),
                ShapeRequirement.Index("idx_effect_intents_lease"),
                ShapeRequirement.Index("idx_effect_intents_transition_attempt"),
                ShapeRequirement.Index("idx_effect_intents_semantic_operation"),
                ShapeRequirement.Index("idx_effect_receipts_intent"),
            ])
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> CanonicalV11Requirements =>
        CanonicalV10Requirements
            .Concat(
            [
                ShapeRequirement.Table("canonical_recovery_cases"),
                ShapeRequirement.Table("canonical_recovery_classifications"),
                ShapeRequirement.Table("canonical_recovery_source_links"),
                ShapeRequirement.Table("canonical_recovery_plans"),
                ShapeRequirement.Table("canonical_recovery_action_events"),
                ShapeRequirement.Column("canonical_recovery_cases", "scope_identity", "text"),
                ShapeRequirement.Column("canonical_recovery_plans", "compatibility_document_json", "text"),
                ShapeRequirement.Column("canonical_recovery_action_events", "document_json", "text"),
                ShapeRequirement.Index("idx_recovery_cases_subject"),
                ShapeRequirement.Index("idx_recovery_classifications_case"),
                ShapeRequirement.Index("idx_recovery_source_links_classification"),
                ShapeRequirement.Index("idx_recovery_plans_case"),
                ShapeRequirement.Index("idx_recovery_action_events_plan"),
            ])
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> CanonicalV12Requirements =>
        CanonicalV11Requirements
            .Concat(
            [
                ShapeRequirement.Table("canonical_interaction_requests"),
                ShapeRequirement.Table("canonical_interaction_policy_evaluations"),
                ShapeRequirement.Table("canonical_interaction_responses"),
                ShapeRequirement.Table("canonical_interaction_lifecycle_events"),
                ShapeRequirement.Index("idx_interaction_requests_state"),
                ShapeRequirement.Index("idx_interaction_requests_causality"),
                ShapeRequirement.Index("idx_interaction_events_request"),
                ShapeRequirement.Index("idx_interaction_responses_semantic"),
            ])
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> CanonicalV13Requirements =>
        CanonicalV12Requirements
            .Concat(
            [
                ShapeRequirement.Table("canonical_storage_operation_plans"),
                ShapeRequirement.Table("canonical_storage_operation_events"),
                ShapeRequirement.Table("canonical_storage_operation_receipts"),
                ShapeRequirement.Index("idx_storage_operation_events_operation"),
                ShapeRequirement.Index("idx_storage_operation_plans_lifecycle"),
            ])
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> CanonicalV14Requirements =>
        CanonicalV13Requirements.Concat(
            [
                ShapeRequirement.Table("canonical_import_detections"),
                ShapeRequirement.Table("canonical_import_previews"),
                ShapeRequirement.Table("canonical_import_mappings"),
                ShapeRequirement.Table("canonical_import_verifications"),
                ShapeRequirement.Table("canonical_import_receipts"),
                ShapeRequirement.Table("canonical_source_authority"),
                ShapeRequirement.Table("canonical_import_adapter_exhaustion"),
                ShapeRequirement.Table("canonical_kernel_decisions"),
                ShapeRequirement.Column("canonical_import_previews", "import_id", "text"),
                ShapeRequirement.Column("runs", "catalog_identity", "text"),
                ShapeRequirement.Column("runs", "catalog_version", "text"),
                ShapeRequirement.Column("workflow_instances", "catalog_identity", "text"),
                ShapeRequirement.Column("attempts", "agent_role_policy_id", "text"),
                ShapeRequirement.Table("canonical_agent_role_policies"),
                ShapeRequirement.Index("idx_import_previews_fingerprint"),
                ShapeRequirement.Index("idx_import_mappings_preview"),
                ShapeRequirement.Index("idx_import_receipts_fingerprint"),
            ])
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ShapeRequirement> CanonicalV15Requirements =>
        CanonicalV14Requirements.Concat(
            [
                ShapeRequirement.Table("canonical_completion_decisions"),
                ShapeRequirement.Table("canonical_completion_certificates"),
                ShapeRequirement.Table("canonical_completion_closure_plans"),
                ShapeRequirement.Table("canonical_completion_settlements"),
                ShapeRequirement.Table("canonical_certified_terminal_facts"),
                ShapeRequirement.Index("idx_completion_decisions_root"),
                ShapeRequirement.Index("idx_completion_settlements_plan"),
            ])
            .DistinctBy(requirement => requirement.Token, StringComparer.Ordinal)
            .ToArray();

    private enum ShapeRequirementKind
    {
        Table,
        Column,
        Index,
        ForeignKey,
    }

    private sealed record ShapeRequirement(
        ShapeRequirementKind Kind,
        string TableName,
        string? ColumnName,
        string? ExpectedType,
        string? TargetTable,
        string? TargetColumn,
        string Token)
    {
        public static ShapeRequirement Table(string table) =>
            new(ShapeRequirementKind.Table, table, null, null, null, null, $"table:{table}");

        public static ShapeRequirement Column(string table, string column, string expectedType) =>
            new(
                ShapeRequirementKind.Column,
                table,
                column,
                expectedType,
                null,
                null,
                $"column:{table}.{column}:{expectedType.ToLowerInvariant()}");

        public static ShapeRequirement Index(string index) =>
            new(ShapeRequirementKind.Index, index, null, null, null, null, $"index:{index}");

        public static ShapeRequirement ForeignKey(
            string table,
            string column,
            string targetTable,
            string targetColumn) =>
            new(
                ShapeRequirementKind.ForeignKey,
                table,
                column,
                null,
                targetTable,
                targetColumn,
                $"foreign-key:{table}.{column}->{targetTable}.{targetColumn}");
    }

    private static async Task<HashSet<string>> ReadSatisfiedRequirementsAsync(
        SqliteConnection connection,
        IReadOnlyList<ShapeRequirement> requirements,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        foreach (ShapeRequirement requirement in requirements.DistinctBy(item => item.Token, StringComparer.Ordinal))
        {
            if (await RequirementExistsAsync(connection, transaction, requirement, cancellationToken))
            {
                satisfied.Add(requirement.Token);
            }
        }

        return satisfied;
    }

    private static async Task<bool> RequirementExistsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ShapeRequirement requirement,
        CancellationToken cancellationToken)
    {
        switch (requirement.Kind)
        {
            case ShapeRequirementKind.Table:
                return await TableExistsAsync(
                    connection, requirement.TableName, cancellationToken, transaction);
            case ShapeRequirementKind.Column:
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT type FROM pragma_table_info($table) WHERE name = $column;";
                    command.Parameters.AddWithValue("$table", requirement.TableName);
                    command.Parameters.AddWithValue("$column", requirement.ColumnName!);
                    object? scalar = await command.ExecuteScalarAsync(cancellationToken);
                    string? actual = scalar is null or DBNull
                        ? null
                        : Convert.ToString(scalar, CultureInfo.InvariantCulture);
                    return string.Equals(actual, requirement.ExpectedType, StringComparison.OrdinalIgnoreCase);
                }
            case ShapeRequirementKind.Index:
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $index;";
                    command.Parameters.AddWithValue("$index", requirement.TableName);
                    object? scalar = await command.ExecuteScalarAsync(cancellationToken);
                    return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
                }
            case ShapeRequirementKind.ForeignKey:
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT [table], [from], [to] FROM pragma_foreign_key_list($table);";
                    command.Parameters.AddWithValue("$table", requirement.TableName);
                    await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        if (string.Equals(reader.GetString(0), requirement.TargetTable, StringComparison.Ordinal) &&
                            string.Equals(reader.GetString(1), requirement.ColumnName, StringComparison.Ordinal) &&
                            string.Equals(reader.GetString(2), requirement.TargetColumn, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(requirement));
        }
    }

    private static bool HasAll(
        IReadOnlySet<string> satisfied,
        IReadOnlyList<ShapeRequirement> requirements) =>
        requirements.All(requirement => satisfied.Contains(requirement.Token));

    private static bool HasAny(
        IReadOnlySet<string> satisfied,
        IReadOnlyList<ShapeRequirement> requirements) =>
        requirements.Any(requirement => satisfied.Contains(requirement.Token));

    private static string ComputeShapeFingerprint(IEnumerable<string> tokens)
    {
        string manifest = string.Join(
            '\n',
            tokens.Distinct(StringComparer.Ordinal).OrderBy(token => token, StringComparer.Ordinal));
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)));
        return $"canonical-v9-shape-{hash[..32]}";
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    public static SqliteConnection OpenReadWriteCreate(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());

    public static SqliteConnection OpenReadWrite(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());

    public static SqliteConnection OpenReadOnly(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

    private static async Task<LegacyResumeImport?> ReadLegacyResumeAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "decision_session_resume", cancellationToken))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT document_json, saved_at FROM decision_session_resume WHERE id = 1;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        string document = reader.GetString(0);
        string savedAt = reader.GetString(1);
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document)));
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(document);
            JsonElement root = parsed.RootElement;
            string? threadId = GetPropertyIgnoreCase(root, "threadId")?.GetString();
            int? schemaVersion = GetPropertyIgnoreCase(root, "schemaVersion") is { } schema &&
                schema.TryGetInt32(out int value)
                    ? value
                    : null;
            bool valid = schemaVersion == 1 && !string.IsNullOrWhiteSpace(threadId);
            return new LegacyResumeImport(savedAt, digest, schemaVersion, threadId, valid,
                valid ? "LegacyScopeUnverified" : "Legacy resume document failed its schema/integrity contract.");
        }
        catch (JsonException exception)
        {
            return new LegacyResumeImport(savedAt, digest, null, null, false, exception.GetType().Name);
        }
    }

    private static JsonElement? GetPropertyIgnoreCase(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static async Task ImportLegacyResumeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LegacyResumeImport legacy,
        CancellationToken cancellationToken)
    {
        string importId = $"legacy-sqlite-{legacy.Digest[..16]}";
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT OR IGNORE INTO decision_session_legacy_imports (
                import_id, source_kind, source_digest, document_schema, parse_status,
                provider_thread_id, diagnostic, imported_at
            ) VALUES (
                $import_id, 'LegacySqliteResume', $source_digest, $document_schema, $parse_status,
                $provider_thread_id, $diagnostic, $imported_at
            );
            """,
            cancellationToken,
            ("$import_id", importId),
            ("$source_digest", legacy.Digest),
            ("$document_schema", legacy.SchemaVersion?.ToString(CultureInfo.InvariantCulture)),
            ("$parse_status", legacy.Valid ? "LegacyScopeUnverified" : "QuarantinedCorrupt"),
            ("$provider_thread_id", legacy.ThreadId),
            ("$diagnostic", legacy.Diagnostic),
            ("$imported_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

        if (!legacy.Valid)
        {
            return;
        }

        string lineageId = $"legacy-{legacy.Digest[..24]}";
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT OR IGNORE INTO decision_session_lineage (
                lineage_id, scope_id, provider, provider_session_id, parent_lineage_id, root_lineage_id,
                mechanism, completeness, source_digest, profile_digest, plan_digest,
                created_at, activated_at, retired_at, authority_state
            ) VALUES (
                $lineage_id, NULL, 'codex', $provider_session_id, NULL, $lineage_id,
                'LegacyUnscoped', 'Unknown', $source_digest, NULL, NULL,
                $created_at, NULL, NULL, 'LegacyScopeUnverified'
            );
            """,
            cancellationToken,
            ("$lineage_id", lineageId),
            ("$provider_session_id", legacy.ThreadId),
            ("$source_digest", legacy.Digest),
            ("$created_at", legacy.SavedAt));
    }

    private sealed record LegacyResumeImport(
        string SavedAt,
        string Digest,
        int? SchemaVersion,
        string? ThreadId,
        bool Valid,
        string Diagnostic);

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string SchemaV9Sql = """
        CREATE TABLE IF NOT EXISTS workspace_schema_migrations(
            migration_id text primary key,
            schema_identity text not null,
            schema_family text not null,
            from_version integer not null,
            to_version integer not null,
            workspace_id text not null,
            applied_at text not null
        );

        CREATE TABLE IF NOT EXISTS workspace_schema_convergences(
            convergence_id text primary key,
            source_shape text not null,
            source_fingerprint text not null,
            source_version integer,
            target_fingerprint text not null,
            workspace_id text not null,
            completed_at text not null
        );

        CREATE TABLE IF NOT EXISTS workspace_identity_metadata(
            id integer primary key check (id = 1),
            identity_format text not null,
            migration_source text not null,
            imported_at text
        );

        CREATE TABLE IF NOT EXISTS session_continuity_profiles(
            profile_digest text primary key,
            provider text not null,
            server_version text,
            schema_digest text,
            profile_json text not null,
            evidence_source text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS decision_session_scopes(
            scope_id text primary key,
            workspace_id text not null,
            workflow_identity text not null,
            prepared_epic_causal_id text not null,
            executable_plan_causal_id text not null,
            session_role text not null,
            contract_version text not null,
            lifecycle_state text not null,
            created_at text not null,
            retired_at text
        );

        CREATE TABLE IF NOT EXISTS decision_session_lineage(
            lineage_id text primary key,
            scope_id text,
            provider text not null,
            provider_session_id text not null,
            parent_lineage_id text,
            root_lineage_id text not null,
            mechanism text not null,
            completeness text not null,
            source_digest text,
            profile_digest text,
            plan_digest text,
            created_at text not null,
            activated_at text,
            retired_at text,
            authority_state text not null,
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(parent_lineage_id) references decision_session_lineage(lineage_id),
            foreign key(profile_digest) references session_continuity_profiles(profile_digest),
            unique(provider, provider_session_id)
        );

        CREATE TABLE IF NOT EXISTS decision_session_active(
            scope_id text primary key,
            lineage_id text not null unique,
            occupancy_tokens integer not null,
            reuse_cost real not null,
            reuse_cycles integer not null,
            last_cycle_cost real not null,
            previous_cycle_cost real not null,
            transfer_cost real not null,
            transfer_count integer not null,
            previous_context_size integer,
            context_growth_streak integer not null,
            policy_digest text not null,
            projection_digest text,
            row_version integer not null,
            activated_at text not null,
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(lineage_id) references decision_session_lineage(lineage_id)
        );

        CREATE TABLE IF NOT EXISTS session_recovery_plans(
            plan_digest text primary key,
            plan_id text not null unique,
            schema_version text not null,
            planner_version text not null,
            policy_version text not null,
            mechanism_identity text not null,
            mechanism_version text not null,
            activation_strategy text not null,
            validation_strategy text not null,
            reconciliation_strategy text not null,
            expected_completeness text not null,
            profile_digest text not null,
            canonical_json text not null,
            created_at text not null,
            foreign key(profile_digest) references session_continuity_profiles(profile_digest)
        );

        CREATE TABLE IF NOT EXISTS session_recovery_attempts(
            attempt_id text primary key,
            previous_attempt_id text,
            scope_id text not null,
            original_lineage_id text not null,
            replacement_lineage_id text,
            transition_run_id text,
            status text not null,
            row_version integer not null,
            profile_digest text not null,
            plan_digest text,
            failure_classification text,
            failure_json text,
            trigger text not null,
            mechanism_identity text,
            mechanism_version text,
            idempotency_key text not null unique,
            provider_request_id text,
            provider_correlation_id text,
            retry_count integer not null,
            diagnostic_json text,
            created_at text not null,
            updated_at text not null,
            completed_at text,
            foreign key(previous_attempt_id) references session_recovery_attempts(attempt_id),
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(original_lineage_id) references decision_session_lineage(lineage_id),
            foreign key(replacement_lineage_id) references decision_session_lineage(lineage_id),
            foreign key(profile_digest) references session_continuity_profiles(profile_digest),
            foreign key(plan_digest) references session_recovery_plans(plan_digest)
        );

        CREATE TABLE IF NOT EXISTS session_recovery_sources(
            attempt_id text not null,
            source_order integer not null,
            source_kind text not null,
            source_location text not null,
            source_digest text not null,
            verified_boundary text,
            normalizer_version text not null,
            completeness text not null,
            omissions_json text not null,
            descriptor_json text not null,
            primary key(attempt_id, source_order),
            unique(attempt_id, source_kind, source_digest),
            foreign key(attempt_id) references session_recovery_attempts(attempt_id)
        );

        CREATE TABLE IF NOT EXISTS decision_session_turns(
            turn_record_id text primary key,
            scope_id text not null,
            lineage_id text not null,
            transition_run_id text not null,
            input_snapshot_hash text not null,
            provider_thread_id text not null,
            provider_turn_id text,
            request_id text,
            state text not null,
            write_started integer not null,
            submitted integer not null,
            accepted integer not null,
            terminal integer not null,
            output_body text,
            output_hash text,
            history_kind text,
            history_sequence integer,
            artifact_materialized integer not null,
            reconciliation_json text,
            row_version integer not null,
            created_at text not null,
            updated_at text not null,
            unique(transition_run_id, input_snapshot_hash),
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(lineage_id) references decision_session_lineage(lineage_id)
        );

        CREATE TABLE IF NOT EXISTS session_transition_correlations(
            transition_run_id text primary key,
            looprelay_session_id text not null,
            lineage_id text not null,
            recovery_attempt_id text,
            provider_thread_id text not null,
            provider_turn_id text,
            turn_record_id text,
            created_at text not null,
            foreign key(lineage_id) references decision_session_lineage(lineage_id),
            foreign key(recovery_attempt_id) references session_recovery_attempts(attempt_id),
            foreign key(turn_record_id) references decision_session_turns(turn_record_id)
        );

        CREATE TABLE IF NOT EXISTS decision_session_legacy_imports(
            import_id text primary key,
            source_kind text not null,
            source_digest text not null,
            document_schema text,
            parse_status text not null,
            provider_thread_id text,
            diagnostic text not null,
            imported_at text not null,
            unique(source_kind, source_digest)
        );

        CREATE TABLE IF NOT EXISTS history_evidence_sets(
            evidence_set_id text primary key,
            history_id text not null unique,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS history_evidence_items(
            evidence_item_id text primary key,
            evidence_set_id text not null,
            evidence_kind text not null,
            schema_version text not null,
            provider text,
            provider_thread_id text,
            provider_turn_id text,
            continuity_lineage_id text,
            recovery_attempt_id text,
            repository_commit text,
            effect_identity text,
            payload_json text not null,
            recorded_at text not null,
            foreign key(evidence_set_id) references history_evidence_sets(evidence_set_id)
        );

        CREATE TABLE IF NOT EXISTS compatibility_import_operations(
            import_id text primary key,
            source_schema_identity text,
            source_schema_family text not null,
            source_schema_version integer,
            source_digest text not null,
            plan_hash text not null,
            state text not null,
            planned_at text not null,
            started_at text,
            verified_at text,
            completed_at text,
            diagnostic_json text not null,
            unique(source_schema_family, source_digest)
        );

        CREATE TABLE IF NOT EXISTS compatibility_import_events(
            event_id text primary key,
            import_id text not null,
            state text not null,
            recorded_at text not null,
            evidence_json text not null,
            foreign key(import_id) references compatibility_import_operations(import_id)
        );

        CREATE TABLE IF NOT EXISTS canonical_projection_effects(
            effect_id text primary key,
            history_id text not null,
            target_path text not null,
            content_hash text not null,
            status text not null,
            idempotency_key text not null unique,
            planned_at text not null,
            started_at text,
            completed_at text,
            failure text
        );

        CREATE TABLE IF NOT EXISTS transition_recovery_plans(
            recovery_id text primary key,
            transition_run_id text not null,
            source_attempt_id text not null,
            classification text not null,
            action text not null,
            resulting_attempt_mode text not null,
            next_attempt_index integer not null,
            evidence_json text not null,
            preconditions_json text not null,
            planned_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_effect_intents(
            effect_intent_id text primary key,
            transition_run_id text not null,
            attempt_id text not null,
            effect_identity text not null,
            category text not null,
            effect_order integer not null,
            idempotency_key text not null unique,
            status text not null,
            definition_json text not null,
            planned_at text not null,
            started_at text,
            completed_at text,
            failure text
        );

        CREATE TABLE IF NOT EXISTS execution_recommendation_evidence(
            recommendation_id text primary key,
            decision_product_id text not null,
            workspace_id text not null,
            run_id text not null,
            workflow_instance_id text not null,
            transition_run_id text not null,
            attempt_id text not null,
            session_id text not null,
            turn_id text not null,
            recommended_model text not null,
            recommended_effort text not null,
            rationale text not null,
            schema_version text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS runtime_profile_evaluations(
            evaluation_id text primary key,
            recommendation_id text,
            decision_product_id text not null,
            policy_id text not null,
            provider_capability_id text not null,
            provider_capability_json text not null,
            outcome text not null,
            runtime_profile_id text not null,
            effective_profile_json text not null,
            reasons_json text not null,
            evaluated_at text not null
        );

        CREATE TABLE IF NOT EXISTS prompt_dispatch_events(
            event_id integer primary key autoincrement,
            dispatch_id text not null,
            rendered_prompt_id text not null,
            persistence_id text not null,
            workspace_id text not null,
            run_id text not null,
            workflow_instance_id text not null,
            transition_run_id text not null,
            attempt_id text not null,
            runtime_profile_id text not null,
            session_id text,
            turn_id text,
            state text not null,
            recorded_at text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS persistence_projection_checkpoints(
            projection_identity text primary key,
            ledger_sequence integer not null,
            projected_at text not null,
            model_hash text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_runtime_prerequisites(
            prerequisite_check_id text primary key,
            run_id text,
            checked_at text not null,
            diagnostics_json text not null
        );

        CREATE INDEX IF NOT EXISTS idx_history_evidence_items_set
            ON history_evidence_items(evidence_set_id, evidence_kind);
        CREATE UNIQUE INDEX IF NOT EXISTS idx_loop_history_history_id
            ON loop_history(history_id) WHERE history_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS idx_history_evidence_provider
            ON history_evidence_items(provider, provider_thread_id, provider_turn_id);
        CREATE INDEX IF NOT EXISTS idx_history_evidence_recovery
            ON history_evidence_items(recovery_attempt_id);
        CREATE INDEX IF NOT EXISTS idx_compatibility_import_events_operation
            ON compatibility_import_events(import_id, recorded_at);
        CREATE INDEX IF NOT EXISTS idx_projection_effects_status
            ON canonical_projection_effects(status, planned_at);
        CREATE INDEX IF NOT EXISTS idx_transition_recovery_plans_run
            ON transition_recovery_plans(transition_run_id, planned_at);
        CREATE INDEX IF NOT EXISTS idx_canonical_effect_intents_status
            ON canonical_effect_intents(status, effect_order, planned_at);
        CREATE INDEX IF NOT EXISTS idx_prompt_dispatch_events_dispatch
            ON prompt_dispatch_events(dispatch_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_prompt_dispatch_events_attempt
            ON prompt_dispatch_events(attempt_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_execution_recommendation_decision
            ON execution_recommendation_evidence(decision_product_id, created_at);
        CREATE INDEX IF NOT EXISTS idx_runtime_profile_evaluation_decision
            ON runtime_profile_evaluations(decision_product_id, evaluated_at);
        CREATE INDEX IF NOT EXISTS idx_decision_scope_lifecycle
            ON decision_session_scopes(lifecycle_state, scope_id);
        CREATE INDEX IF NOT EXISTS idx_decision_lineage_scope_authority
            ON decision_session_lineage(scope_id, authority_state);
        CREATE INDEX IF NOT EXISTS idx_decision_lineage_parent
            ON decision_session_lineage(parent_lineage_id);
        CREATE INDEX IF NOT EXISTS idx_recovery_attempt_scope_status
            ON session_recovery_attempts(scope_id, status, updated_at);
        CREATE INDEX IF NOT EXISTS idx_decision_turn_transition
            ON decision_session_turns(transition_run_id);
        CREATE INDEX IF NOT EXISTS idx_canonical_runtime_prerequisites_run
            ON canonical_runtime_prerequisites(run_id);
        """;

    private const string SchemaV10Sql = """
        CREATE TABLE IF NOT EXISTS canonical_effect_lifecycle_events(
            event_id integer primary key autoincrement,
            effect_intent_id text not null,
            lifecycle text not null,
            worker_id text not null,
            explanation text not null,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_effect_receipts(
            receipt_id text primary key,
            effect_intent_id text not null unique,
            executor_key text not null,
            executor_version text not null,
            observed_target_identity text not null,
            before_facts_json text not null,
            after_facts_json text not null,
            postcondition_satisfied integer not null,
            external_correlation text,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_effect_reconciliation_attempts(
            reconciliation_id text primary key,
            effect_intent_id text not null,
            worker_id text not null,
            verdict text not null,
            before_facts_json text not null,
            after_facts_json text not null,
            external_correlation text,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_effect_intents_unsettled
            ON canonical_effect_intents(status, requiredness, effect_order, planned_at);
        CREATE INDEX IF NOT EXISTS idx_effect_intents_lease
            ON canonical_effect_intents(lease_expires_at, row_version);
        CREATE INDEX IF NOT EXISTS idx_effect_intents_transition_attempt
            ON canonical_effect_intents(transition_run_id, attempt_id, effect_order);
        CREATE INDEX IF NOT EXISTS idx_effect_intents_semantic_operation
            ON canonical_effect_intents(semantic_operation_key, idempotency_key);
        CREATE INDEX IF NOT EXISTS idx_effect_receipts_intent
            ON canonical_effect_receipts(effect_intent_id, recorded_at);
        CREATE INDEX IF NOT EXISTS idx_effect_lifecycle_intent
            ON canonical_effect_lifecycle_events(effect_intent_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_effect_reconciliation_intent
            ON canonical_effect_reconciliation_attempts(effect_intent_id, recorded_at);

        INSERT INTO canonical_effect_lifecycle_events (
            effect_intent_id, lifecycle, worker_id, explanation, evidence_json, recorded_at
        )
        SELECT intent.effect_intent_id,
               CASE record.status WHEN 'PartiallyFailed' THEN 'Unknown' ELSE record.status END,
               'v9-migration', record.explanation, record.evidence_json, record.recorded_at
        FROM canonical_effect_records AS record
        JOIN canonical_effect_intents AS intent
          ON intent.transition_run_id = record.run_id
         AND intent.effect_identity = record.effect_identity
        WHERE NOT EXISTS (
            SELECT 1 FROM canonical_effect_lifecycle_events AS existing
            WHERE existing.effect_intent_id = intent.effect_intent_id
        )
        ORDER BY record.record_id;

        INSERT INTO canonical_effect_lifecycle_events (
            effect_intent_id, lifecycle, worker_id, explanation, evidence_json, recorded_at
        )
        SELECT effect_intent_id, 'HumanActionRequired', 'v9-migration',
               'Legacy effect intent has no exact executor/target contract and cannot be replayed automatically.',
               '["legacy-v9-effect-intent","no-receipt-invented"]', planned_at
        FROM canonical_effect_intents
        WHERE executor_key IS NULL
          AND NOT EXISTS (
              SELECT 1 FROM canonical_effect_lifecycle_events AS existing
              WHERE existing.effect_intent_id = canonical_effect_intents.effect_intent_id
                AND existing.lifecycle = 'HumanActionRequired'
          );

        UPDATE canonical_effect_intents
        SET workspace_id = COALESCE(workspace_id, (SELECT workspace_id FROM workspace_identity WHERE id = 1)),
            run_id = COALESCE(run_id, (SELECT run_id FROM attempts WHERE attempts.attempt_id = canonical_effect_intents.attempt_id)),
            workflow_instance_id = COALESCE(workflow_instance_id, (SELECT workflow_instance_id FROM attempts WHERE attempts.attempt_id = canonical_effect_intents.attempt_id)),
            semantic_operation_key = COALESCE(semantic_operation_key, 'legacy:' || effect_identity),
            executor_key = COALESCE(executor_key, 'legacy-unresolved'),
            executor_version = COALESCE(executor_version, '0'),
            target_json = COALESCE(target_json, '{"kind":"legacy-unresolved"}'),
            payload_json = COALESCE(payload_json, definition_json),
            payload_hash = COALESCE(payload_hash, 'legacy-unverified'),
            requiredness = COALESCE(requiredness, 'BlockingLocal'),
            dependencies_json = COALESCE(dependencies_json, '[]'),
            precondition_json = COALESCE(precondition_json, '{"kind":"legacy-unknown"}'),
            postcondition_json = COALESCE(postcondition_json, '{"kind":"legacy-unknown"}'),
            reconciliation_policy = COALESCE(reconciliation_policy, 'human-decision-required'),
            status = CASE WHEN executor_key IS NULL THEN 'HumanActionRequired' ELSE status END,
            row_version = CASE WHEN executor_key IS NULL THEN row_version + 1 ELSE row_version END,
            lease_owner = NULL,
            lease_expires_at = NULL;
        """;

    private const string SchemaV11Sql = """
        CREATE TABLE IF NOT EXISTS canonical_recovery_cases(
            case_id text primary key,
            scope_kind text not null,
            scope_identity text,
            subject_json text not null,
            transition_run_id text,
            attempt_id text,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_recovery_classifications(
            classification_id text primary key,
            case_id text not null,
            classification text not null,
            cancellation_boundary text not null,
            source_evidence_json text not null,
            supersedes_classification_id text,
            observed_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_recovery_source_links(
            classification_id text not null,
            source_kind text not null,
            source_identity text not null,
            source_digest text,
            primary key(classification_id, source_kind, source_identity)
        );

        CREATE TABLE IF NOT EXISTS canonical_recovery_plans(
            plan_id text primary key,
            case_id text not null,
            classification_id text not null,
            action text not null,
            resolved_policy_identity text not null,
            exact_profile_identity text not null,
            source_evidence_json text not null,
            preconditions_json text not null,
            postconditions_json text not null,
            idempotency_key text not null unique,
            new_attempt_id text,
            compatibility_document_json text,
            planned_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_recovery_action_events(
            event_id integer primary key autoincrement,
            action_id text not null,
            plan_id text not null,
            lifecycle text not null,
            explanation text not null,
            evidence_json text not null,
            document_json text,
            recorded_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_recovery_cases_subject
            ON canonical_recovery_cases(scope_kind, transition_run_id, attempt_id, created_at);
        CREATE INDEX IF NOT EXISTS idx_recovery_classifications_case
            ON canonical_recovery_classifications(case_id, observed_at, classification_id);
        CREATE INDEX IF NOT EXISTS idx_recovery_source_links_classification
            ON canonical_recovery_source_links(classification_id, source_kind);
        CREATE INDEX IF NOT EXISTS idx_recovery_plans_case
            ON canonical_recovery_plans(case_id, planned_at);
        CREATE INDEX IF NOT EXISTS idx_recovery_action_events_plan
            ON canonical_recovery_action_events(plan_id, event_id);

        INSERT OR IGNORE INTO canonical_recovery_cases (
            case_id, scope_kind, scope_identity, subject_json, transition_run_id, attempt_id, created_at
        )
        SELECT recovery_id, 'Transition', NULL,
               json_object('transitionRunId', transition_run_id, 'attemptId', source_attempt_id),
               transition_run_id, source_attempt_id, planned_at
        FROM transition_recovery_plans;

        INSERT OR IGNORE INTO canonical_recovery_classifications (
            classification_id, case_id, classification, cancellation_boundary,
            source_evidence_json, supersedes_classification_id, observed_at
        )
        SELECT 'legacy-transition-classification:' || recovery_id,
               recovery_id,
               CASE classification
                   WHEN 'SafeRetry' THEN 'NotStarted'
                   WHEN 'ReconcileProvider' THEN 'AcceptedUnknown'
                   WHEN 'MaterializeCommittedOutput' THEN 'SucceededUncommitted'
                   WHEN 'ApplyVerifiedEffects' THEN 'PartiallyEffected'
                   WHEN 'Cancelled' THEN 'Cancelled'
                   WHEN 'FailClosedUnknownSideEffect' THEN 'PartiallyEffected'
                   WHEN 'NonRecoverableCorruption' THEN 'Corrupt'
                   ELSE 'EvidenceIncomplete'
               END,
               CASE classification WHEN 'Cancelled' THEN 'BeforeDispatch' ELSE 'None' END,
               evidence_json, NULL, planned_at
        FROM transition_recovery_plans;

        INSERT OR IGNORE INTO canonical_recovery_plans (
            plan_id, case_id, classification_id, action, resolved_policy_identity,
            exact_profile_identity, source_evidence_json, preconditions_json,
            postconditions_json, idempotency_key, new_attempt_id, planned_at
        )
        SELECT recovery_id, recovery_id, 'legacy-transition-classification:' || recovery_id,
               CASE action
                   WHEN 'ReconcileProviderOutcome' THEN 'ReconcileProvider'
                   WHEN 'RetryAsNewAttempt' THEN 'RetryNewAttempt'
                   WHEN 'ReusePersistedRawResult' THEN 'ReuseRawOutput'
                   WHEN 'CannotProceed' THEN 'RequestHumanDecision'
                   ELSE action
               END,
               'legacy-policy-unresolved', 'legacy-profile-unresolved', evidence_json,
               preconditions_json, '[]', 'legacy-transition-recovery-plan:' || recovery_id,
               NULL, planned_at
        FROM transition_recovery_plans;

        INSERT OR IGNORE INTO canonical_recovery_cases (
            case_id, scope_kind, scope_identity, subject_json, transition_run_id, attempt_id, created_at
        )
        SELECT attempt_id, 'WarmSession', scope_id,
               json_object('scopeId', scope_id, 'lineageId', original_lineage_id),
               transition_run_id, NULL, created_at
        FROM session_recovery_attempts;

        INSERT OR IGNORE INTO canonical_recovery_classifications (
            classification_id, case_id, classification, cancellation_boundary,
            source_evidence_json, supersedes_classification_id, observed_at
        )
        SELECT 'legacy-session-classification:' || attempt_id, attempt_id,
               CASE
                   WHEN status = 'UnknownOutcome' THEN 'ProviderUnknown'
                   WHEN failure_classification IS NOT NULL THEN 'Failed'
                   WHEN status IN ('RecoveryCompleted','ResumeSucceeded') THEN 'SucceededUncommitted'
                   ELSE 'EvidenceIncomplete'
               END,
               'None', COALESCE(diagnostic_json, '[]'), NULL, updated_at
        FROM session_recovery_attempts;

        INSERT OR IGNORE INTO canonical_recovery_source_links (
            classification_id, source_kind, source_identity, source_digest
        )
        SELECT 'legacy-session-classification:' || attempt_id,
               source_kind, source_location, source_digest
        FROM session_recovery_sources;

        INSERT OR IGNORE INTO canonical_recovery_plans (
            plan_id, case_id, classification_id, action, resolved_policy_identity,
            exact_profile_identity, source_evidence_json, preconditions_json,
            postconditions_json, idempotency_key, new_attempt_id, planned_at
        )
        SELECT plan.plan_id, attempt.attempt_id,
               'legacy-session-classification:' || attempt.attempt_id,
               CASE plan.mechanism_identity
                   WHEN 'native-resume' THEN 'ResumeSession'
                   WHEN 'native-fork' THEN 'NativeFork'
                   ELSE 'ReconstructContext'
               END,
               plan.policy_version, plan.profile_digest, '[]', '[]', '[]',
               'legacy-session-recovery-plan:' || plan.plan_id, NULL, plan.created_at
        FROM session_recovery_plans AS plan
        JOIN session_recovery_attempts AS attempt ON attempt.plan_digest = plan.plan_digest
        ORDER BY attempt.created_at;
        """;

    private const string SchemaV12Sql = """
        CREATE TABLE IF NOT EXISTS canonical_interaction_policy_evaluations(
            policy_evaluation_id text primary key,
            category text not null,
            question_version text not null,
            response_schema_version text not null,
            response_json_schema text not null,
            response_schema_hash text not null,
            deadline_behavior text not null,
            deadline text,
            default_response_json text,
            headless_outcome text not null,
            required_trust_evidence_json text not null,
            resolver_owner text not null,
            resolved_policy_identity text not null,
            evaluated_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_interaction_requests(
            request_id text primary key,
            category text not null,
            workspace_id text not null,
            run_id text not null,
            workflow_instance_id text not null,
            transition_run_id text not null,
            attempt_id text not null,
            subject_json text not null,
            question text not null,
            presentation_json text not null,
            policy_evaluation_id text not null,
            creation_evidence_json text not null,
            semantic_idempotency_key text not null unique,
            current_state text not null,
            row_version integer not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_interaction_responses(
            response_id text primary key,
            request_id text not null unique,
            response_json text not null,
            semantic_response_hash text not null,
            semantic_idempotency_key text not null,
            trust_evidence_json text not null,
            responder_identity text not null,
            responded_at text not null,
            unique(request_id, semantic_idempotency_key)
        );

        CREATE TABLE IF NOT EXISTS canonical_interaction_lifecycle_events(
            event_id integer primary key autoincrement,
            event_identity text not null unique,
            request_id text not null,
            lifecycle text not null,
            explanation text not null,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_interaction_requests_state
            ON canonical_interaction_requests(current_state, category, created_at);
        CREATE INDEX IF NOT EXISTS idx_interaction_requests_causality
            ON canonical_interaction_requests(transition_run_id, attempt_id, created_at);
        CREATE INDEX IF NOT EXISTS idx_interaction_events_request
            ON canonical_interaction_lifecycle_events(request_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_interaction_responses_semantic
            ON canonical_interaction_responses(request_id, semantic_response_hash, semantic_idempotency_key);
        """;

    private const string SchemaV13Sql = """
        CREATE TABLE IF NOT EXISTS canonical_storage_operation_plans(
            operation_id text primary key,
            operation_kind text not null,
            workspace_id text not null,
            run_id text not null,
            workflow_instance_id text not null,
            transition_run_id text not null,
            attempt_id text not null,
            source_fingerprint text not null,
            target_manifest text not null,
            preconditions_json text not null,
            postconditions_json text not null,
            semantic_idempotency_key text not null unique,
            current_lifecycle text not null,
            planned_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_storage_operation_events(
            event_id integer primary key autoincrement,
            operation_id text not null,
            lifecycle text not null,
            explanation text not null,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_storage_operation_receipts(
            operation_id text primary key,
            observed_fingerprint text not null,
            effect_receipts_json text not null,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_storage_operation_events_operation
            ON canonical_storage_operation_events(operation_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_storage_operation_plans_lifecycle
            ON canonical_storage_operation_plans(current_lifecycle, operation_kind, planned_at);
        """;

    private const string SchemaV14Sql = """
        CREATE TABLE IF NOT EXISTS canonical_import_detections(
            detection_id text primary key, source_kind text not null, source_family text not null,
            source_version text, source_fingerprint text not null, document_json text not null,
            detected_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_import_previews(
            preview_id text primary key, detection_id text not null, source_fingerprint text not null,
            lifecycle text not null, import_id text, approval_json text, document_json text not null, previewed_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_import_mappings(
            mapping_id integer primary key autoincrement, preview_id text not null, domain text not null,
            source_identity text not null, target_identity text, preserved integer not null,
            rule text not null, conflict text
        );
        CREATE TABLE IF NOT EXISTS canonical_import_verifications(
            verification_id text primary key, import_id text not null, equivalent integer not null,
            target_logical_fingerprint text not null, domain_diffs_json text not null, verified_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_import_receipts(
            receipt_id text primary key, import_id text not null unique, preview_id text not null,
            source_fingerprint text not null, target_logical_fingerprint text not null,
            evidence_json text not null, recorded_at text not null,
            unique(source_fingerprint, target_logical_fingerprint)
        );
        CREATE TABLE IF NOT EXISTS canonical_source_authority(
            id integer primary key check(id=1), canonical_only integer not null,
            import_receipt_id text, source_non_authoritative_at text, marker_sequence integer not null
        );
        CREATE TABLE IF NOT EXISTS canonical_import_adapter_exhaustion(
            adapter_identity text not null, adapter_version text not null,
            portfolio_fingerprint text not null, fixture_receipts_json text not null,
            canonical_only_runs_json text not null, disabled_results_json text not null,
            exhausted_at text not null, superseded_at text,
            primary key(adapter_identity, adapter_version, portfolio_fingerprint)
        );
        CREATE TABLE IF NOT EXISTS canonical_kernel_decisions(
            decision_id text primary key, catalog_identity text not null, snapshot_identity text not null,
            root_run_id text not null, workflow_instance_id text, transition_run_id text, attempt_id text,
            eligible_json text not null, rejected_json text not null, selected_action text not null,
            outcome text not null, evidence_json text not null, recorded_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_agent_role_policies(
            role_policy_id text primary key, operational_policy_id text not null,
            runtime_profile_id text not null, document_json text not null,
            provenance text not null, recorded_at text not null
        );
        CREATE INDEX IF NOT EXISTS idx_import_previews_fingerprint
            ON canonical_import_previews(source_fingerprint, lifecycle);
        CREATE INDEX IF NOT EXISTS idx_import_mappings_preview
            ON canonical_import_mappings(preview_id, domain, source_identity);
        CREATE INDEX IF NOT EXISTS idx_import_receipts_fingerprint
            ON canonical_import_receipts(source_fingerprint, target_logical_fingerprint);
        INSERT INTO canonical_source_authority(id, canonical_only, import_receipt_id, source_non_authoritative_at, marker_sequence)
            VALUES(1,0,NULL,NULL,0) ON CONFLICT(id) DO NOTHING;
        """;

    private const string SchemaV15Sql = """
        CREATE TABLE IF NOT EXISTS canonical_completion_decisions(
            decision_id text primary key, root_run_id text not null, attempt_id text not null,
            kind text not null, reason text, evidence_json text not null, gate_identities_json text not null,
            review_identities_json text not null, decided_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_completion_certificates(
            certificate_id text primary key, decision_id text not null unique,
            evidence_json text not null, certified_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_completion_closure_plans(
            plan_id text primary key, decision_id text not null unique, certificate_id text not null unique,
            operations_json text not null, content_hash text not null, planned_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_completion_settlements(
            settlement_id text primary key, plan_id text not null, kind text not null,
            pending_operations_json text not null, evidence_json text not null, reason text,
            settled_at text not null
        );
        CREATE TABLE IF NOT EXISTS canonical_certified_terminal_facts(
            terminal_id text primary key, root_run_id text not null unique, decision_id text not null,
            certificate_id text not null, plan_id text not null, settlement_id text not null,
            effect_receipts_json text not null, recorded_at text not null
        );
        CREATE INDEX IF NOT EXISTS idx_completion_decisions_root
            ON canonical_completion_decisions(root_run_id, decided_at);
        CREATE INDEX IF NOT EXISTS idx_completion_settlements_plan
            ON canonical_completion_settlements(plan_id, settled_at);
        """;

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS schema_metadata(
            key text primary key,
            value text not null
        );

        CREATE TABLE IF NOT EXISTS workspace_metadata(
            key text primary key,
            value text not null
        );

        CREATE TABLE IF NOT EXISTS sync_markers(
            domain text primary key,
            canonical_hash text not null,
            export_hash text,
            generation integer not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS decision_ledger(
            decision_id text primary key,
            timestamp text not null,
            state text not null,
            transition text not null,
            prompt text not null,
            projection_path text not null,
            input_paths_json text not null,
            output_paths_json text not null,
            decision text not null,
            confidence text not null,
            rationale_excerpt text not null
        );

        CREATE TABLE IF NOT EXISTS roadmap_state(
            id integer primary key check (id = 1),
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS artifact_lifecycle(
            path_key text primary key,
            path text not null,
            state text not null,
            updated_at text not null,
            notes text not null
        );

        CREATE TABLE IF NOT EXISTS split_families(
            family_id text primary key,
            proposal text not null,
            selected_child text not null,
            selected_child_rationale text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS split_family_children(
            family_id text not null,
            ordinal integer not null,
            child_path text not null,
            primary key(family_id, ordinal),
            unique(family_id, child_path)
        );

        CREATE TABLE IF NOT EXISTS split_family_dependency_order(
            family_id text not null,
            ordinal integer not null,
            child_path text not null,
            primary key(family_id, ordinal)
        );

        CREATE TABLE IF NOT EXISTS execution_preparation_manifest(
            id integer primary key check (id = 1),
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS selection_provenance_manifest(
            id integer primary key check (id = 1),
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS projection_manifest_entries(
            runtime_prompt text primary key,
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS transition_journal(
            event_order integer primary key autoincrement,
            correlation_id text not null,
            event_name text not null,
            recorded_at text not null,
            from_state text not null,
            to_state text not null,
            transition text not null,
            projection_path text not null,
            prompt_contract text not null,
            input_hashes_json text not null,
            output_paths_json text not null,
            duration_milliseconds integer not null,
            retry_count integer not null,
            result text not null,
            decision text not null,
            error text,
            input_snapshot_json text
        );

        CREATE TABLE IF NOT EXISTS loop_history(
            kind text not null,
            sequence integer not null,
            logical_path text not null unique,
            body text not null,
            content_hash text not null,
            created_at text not null,
            history_id text,
            run_id text,
            transition_run_id text,
            attempt_id text,
            primary key(kind, sequence)
        );

        CREATE TABLE IF NOT EXISTS execution_evidence(
            logical_path text primary key,
            stem text not null,
            sequence integer not null,
            body text not null,
            content_hash text not null,
            created_at text not null,
            writer text,
            metadata_json text not null,
            unique(stem, sequence)
        );

        CREATE TABLE IF NOT EXISTS completed_epic_archives(
            archive_index integer primary key,
            archive_directory text not null unique,
            synthesis_path text not null unique,
            created_at text not null,
            metadata_json text not null
        );

        CREATE TABLE IF NOT EXISTS completed_epic_records(
            archive_index integer not null,
            domain text not null,
            logical_path text not null,
            export_path text not null,
            content_hash text not null,
            primary key(archive_index, domain, logical_path)
        );

        CREATE TABLE IF NOT EXISTS workflow_transactions(
            transaction_id text primary key,
            workflow_name text not null,
            correlation_id text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            marker_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_workflow_states(
            workflow_identity text primary key,
            state text not null,
            current_stage text,
            outcome text,
            updated_at text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_stage_states(
            workflow_identity text not null,
            stage_identity text not null,
            state text not null,
            updated_at text not null,
            evidence_json text not null,
            primary key(workflow_identity, stage_identity)
        );

        CREATE TABLE IF NOT EXISTS canonical_transition_runs(
            run_id text primary key,
            workflow_identity text not null,
            stage_identity text not null,
            transition_identity text not null,
            state text not null,
            outcome text not null,
            started_at text not null,
            completed_at text,
            input_snapshot_hash text,
            explanation text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_transition_evidence(
            evidence_id integer primary key autoincrement,
            run_id text not null,
            transition_identity text not null,
            event_name text not null,
            recorded_at text not null,
            state text not null,
            explanation text not null,
            evidence_json text not null,
            document_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_product_records(
            product_identity text primary key,
            producer_workflow text not null,
            producer_transition text not null,
            intended_consumers_json text not null,
            repository_ownership text not null,
            authority text not null,
            storage_representations_json text not null,
            causal_identity text not null,
            freshness text not null,
            validation_state text not null,
            lifecycle text not null,
            evidence_locations_json text not null,
            updated_at text not null,
            schema_version text not null default '1'
        );

        CREATE TABLE IF NOT EXISTS canonical_gate_evaluations(
            evaluation_id integer primary key autoincrement,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            gate_identity text not null,
            status text not null,
            evaluated_at text not null,
            requirements_json text not null,
            explanation text not null,
            evidence_json text not null,
            transition_run_id text
        );

        CREATE TABLE IF NOT EXISTS canonical_effect_records(
            record_id integer primary key autoincrement,
            run_id text not null,
            effect_identity text not null,
            category text not null,
            status text not null,
            recorded_at text not null,
            explanation text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS evaluation_warnings(
            warning_id text primary key,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            category text not null,
            concern text not null,
            authority text not null,
            remediation text not null,
            evidence_json text not null,
            created_at text not null,
            transition_run_id text
        );

        CREATE TABLE IF NOT EXISTS canonical_recovery_markers(
            marker_id text primary key,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            semantics text not null,
            supported_actions_json text not null,
            unsupported_actions_json text not null,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_chain_boundary_events(
            boundary_id text primary key,
            run_id text,
            chain_identity text not null,
            source_workflow text not null,
            target_workflow text,
            exit_gate_status text not null,
            entry_gate_status text,
            transfer_gate_status text,
            decision text not null,
            explanation text not null,
            evidence_json text not null,
            boundary_json text not null,
            recorded_at text not null
        );

        CREATE TABLE IF NOT EXISTS decision_session_resume(
            id integer primary key check (id = 1),
            document_json text not null,
            saved_at text not null
        );

        CREATE TABLE IF NOT EXISTS session_telemetry_events(
            event_id integer primary key autoincrement,
            recorded_at text not null,
            repo_name text not null,
            session_id text not null,
            session_type text not null,
            turn_index integer not null,
            document_json text not null,
            content_hash text not null
        );

        CREATE INDEX IF NOT EXISTS idx_artifact_lifecycle_path_key ON artifact_lifecycle(path_key);
        CREATE INDEX IF NOT EXISTS idx_split_family_children_child_path ON split_family_children(child_path);
        CREATE INDEX IF NOT EXISTS idx_transition_journal_correlation_id ON transition_journal(correlation_id);
        CREATE INDEX IF NOT EXISTS idx_loop_history_kind_sequence_desc ON loop_history(kind, sequence desc);
        CREATE INDEX IF NOT EXISTS idx_execution_evidence_stem_sequence_desc ON execution_evidence(stem, sequence desc);
        CREATE INDEX IF NOT EXISTS idx_canonical_stage_states_workflow ON canonical_stage_states(workflow_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_transition_runs_workflow_stage
            ON canonical_transition_runs(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_transition_evidence_run ON canonical_transition_evidence(run_id);
        CREATE INDEX IF NOT EXISTS idx_canonical_gate_evaluations_workflow
            ON canonical_gate_evaluations(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_effect_records_run ON canonical_effect_records(run_id);
        CREATE INDEX IF NOT EXISTS idx_evaluation_warnings_workflow ON evaluation_warnings(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_chain_boundary_events_run ON canonical_chain_boundary_events(run_id);
        CREATE INDEX IF NOT EXISTS idx_session_telemetry_order
            ON session_telemetry_events(recorded_at, event_id);

        CREATE TABLE IF NOT EXISTS workspace_identity(
            id integer primary key check (id = 1),
            workspace_id text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS runs(
            run_id text primary key,
            workspace_id text not null,
            chain_identity text not null,
            invocation_mode text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            stop_reason text,
            explanation text not null,
            catalog_identity text not null default '',
            catalog_version text not null default ''
        );

        CREATE TABLE IF NOT EXISTS workflow_instances(
            workflow_instance_id text primary key,
            run_id text not null,
            workflow_identity text not null,
            catalog_version text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            outcome text,
            catalog_identity text not null default ''
        );

        CREATE TABLE IF NOT EXISTS attempts(
            attempt_id text primary key,
            transition_run_id text not null,
            workflow_instance_id text not null,
            run_id text not null,
            attempt_index integer not null,
            started_at text not null,
            completed_at text,
            outcome text,
            policy_id text,
            agent_role_policy_id text,
            unique(transition_run_id, attempt_index)
        );

        CREATE TABLE IF NOT EXISTS agent_sessions(
            session_id text primary key,
            attempt_id text,
            workspace_id text,
            provider text not null,
            provider_thread_id text,
            role text not null,
            legacy_session_guid text,
            started_at text not null,
            completed_at text,
            effort text,
            sandbox text
        );

        CREATE TABLE IF NOT EXISTS agent_turns(
            turn_id text primary key,
            session_id text not null,
            turn_index integer not null,
            recorded_at text not null,
            unique(session_id, turn_index)
        );

        CREATE INDEX IF NOT EXISTS idx_runs_workspace_started ON runs(workspace_id, started_at);
        CREATE INDEX IF NOT EXISTS idx_workflow_instances_run ON workflow_instances(run_id);
        CREATE INDEX IF NOT EXISTS idx_workflow_instances_workflow ON workflow_instances(workflow_identity, started_at);
        CREATE INDEX IF NOT EXISTS idx_attempts_transition_run ON attempts(transition_run_id);
        CREATE INDEX IF NOT EXISTS idx_agent_sessions_attempt ON agent_sessions(attempt_id);

        CREATE TABLE IF NOT EXISTS read_receipts(
            receipt_id text primary key,
            run_id text not null,
            workflow_identity text not null,
            transition_identity text not null,
            attempt_id text,
            commit_hash text,
            input_surfaces_json text not null,
            surface_tree_hashes_json text,
            files_json text not null,
            products_json text not null,
            validation text not null,
            consumed_at text not null,
            transition_run_id text
        );

        CREATE TABLE IF NOT EXISTS canonical_policy_resolutions(
            resolution_id text primary key,
            policy_id text not null,
            schema_version text not null,
            resolved_json text not null,
            provenance_json text not null,
            source_description text not null,
            recorded_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_canonical_policy_resolutions_policy ON canonical_policy_resolutions(policy_id);

        CREATE TABLE IF NOT EXISTS canonical_rendered_prompts(
            rendered_prompt_id text primary key,
            transition_run_id text not null,
            attempt_id text,
            session_id text,
            turn_id text,
            prompt_identity text not null,
            template_source_hash text,
            rendered_sha256 text not null,
            rendered_text text not null,
            consumed_inputs_json text not null,
            policy_id text,
            rendered_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_canonical_rendered_prompts_transition ON canonical_rendered_prompts(transition_run_id);

        """;

    private const string CanonicalDataRepairSql = """
        UPDATE canonical_workflow_states SET state = 'Resumable' WHERE state = 'Blocked';
        UPDATE canonical_workflow_states SET outcome = 'MissingRequiredInput' WHERE outcome = 'Blocked';
        UPDATE canonical_stage_states SET state = 'Resumable' WHERE state = 'Blocked';
        UPDATE canonical_transition_runs SET state = 'InputUnsatisfied' WHERE state = 'Blocked';
        UPDATE canonical_transition_runs SET outcome = 'MissingRequiredInput' WHERE outcome = 'Blocked';
        DROP TABLE IF EXISTS canonical_blockers;
        """;
}
