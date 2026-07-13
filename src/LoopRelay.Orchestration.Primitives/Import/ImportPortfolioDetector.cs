using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Storage;

namespace LoopRelay.Orchestration.Import;

public sealed class ImportPortfolioDetector : IImportPortfolioDetector
{
    private static readonly string[] RoadmapPaths =
    [
        ".agents/state.json", ".agents/state.md", ".agents/decisions.json", ".agents/decisions.md",
        ".agents/artifacts/lifecycle.json", ".agents/splits.json", ".agents/projections/manifest.json",
        ".agents/journal/transitions.jsonl", ".agents/execution-preparation.json",
    ];
    private static readonly string[] PlanningPaths =
    [
        ".agents/plan.md", ".agents/details.md", ".agents/operational-context.md",
        ".agents/projections/adversarial-plan-review.md", ".agents/projections/plan.md",
    ];
    private static readonly string[] ExecutePaths =
    [
        ".agents/decision-session.json", ".agents/handoff.md", ".agents/evidence.md",
        ".agents/history", ".agents/archive/epics",
    ];

    public async Task<ImportDetection> DetectAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(repositoryPath);
        string database = Path.Combine(root,
            LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
        var adapters = new List<ImportAdapterDescriptor>();
        var evidence = new List<string>();
        var conflicts = new List<string>();
        var unsupported = new List<string>();
        WorkspaceSchemaInspection? schema = null;
        if (File.Exists(database))
        {
            try
            {
                schema = await new WorkspaceSchemaReadOnlyInspector().InspectAsync(database, cancellationToken);
                evidence.Add($"database:{schema.Family}:v{schema.Version}:{schema.Shape}");
                if (schema.Family == WorkspaceSchemaFamily.LegacyContinuity)
                    adapters.Add(Descriptors.LegacyContinuity);
                else if (schema.Family == WorkspaceSchemaFamily.CanonicalWorkspace &&
                         schema.Version != LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
                    adapters.Add(Descriptors.CanonicalMigration);
                else if (schema.Family == WorkspaceSchemaFamily.Unknown)
                    unsupported.Add(schema.Diagnostic);
            }
            catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or InvalidDataException)
            {
                unsupported.Add($"database-unreadable:{exception.GetType().Name}");
            }
        }

        bool roadmap = AddExisting(root, RoadmapPaths, evidence);
        bool planning = AddExisting(root, PlanningPaths, evidence);
        string milestones = Path.Combine(root, ".agents", "milestones");
        if (Directory.Exists(milestones))
        {
            evidence.Add(".agents/milestones");
            planning = true;
        }
        bool execute = AddExisting(root, ExecutePaths, evidence);
        if (roadmap) adapters.Add(Descriptors.Roadmap);
        if (planning) adapters.Add(Descriptors.Planning);
        if (execute) adapters.Add(Descriptors.Execute);
        if (schema?.Family == WorkspaceSchemaFamily.CanonicalWorkspace &&
            schema.Version == LoopRelayWorkspaceDatabase.CurrentSchemaVersion && adapters.Count > 0)
            conflicts.Add("A current canonical authority already exists beside legacy import surfaces.");

        string[] packages = Directory.Exists(Path.Combine(root, ".LoopRelay", "imports"))
            ? Directory.GetFiles(Path.Combine(root, ".LoopRelay", "imports"), "*.canonical.json")
            : [];
        if (packages.Length == 1)
        {
            try
            {
                _ = new CanonicalStorageExportCodec().Decode(await File.ReadAllTextAsync(packages[0], cancellationToken));
                adapters.Add(Descriptors.CanonicalPackage);
                evidence.Add(Path.GetRelativePath(root, packages[0]).Replace('\\', '/'));
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                unsupported.Add($"malformed-canonical-package:{exception.Message}");
            }
        }
        else if (packages.Length > 1)
            conflicts.Add("Multiple canonical import packages are present; select exactly one source.");

        bool databaseCollision = adapters.Count(item => item.SourceKind is
            ImportSourceKind.LegacyContinuityV3 or ImportSourceKind.CanonicalMigrationRequired or
            ImportSourceKind.CanonicalExportPackage) > 1;
        if (databaseCollision) conflicts.Add("Multiple database authorities match the import portfolio.");
        ImportSourceKind kind = conflicts.Count > 0 ? ImportSourceKind.Ambiguous : adapters.Count switch
        {
            0 => ImportSourceKind.Unknown,
            1 => adapters[0].SourceKind,
            _ => ImportSourceKind.CompositeOwnedWorkspace,
        };
        if (kind == ImportSourceKind.Unknown) unsupported.Add("No accepted import adapter matched this workspace.");
        string fingerprint = await FingerprintAsync(root, database, evidence, cancellationToken);
        return new ImportDetection(
            ImportDetectionIdentity.New(), root, kind,
            schema?.Family.ToString() ?? (adapters.Count > 0 ? "OwnedFilesystemPortfolio" : "Unknown"),
            schema?.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture), fingerprint,
            adapters, conflicts, unsupported, evidence.Order(StringComparer.Ordinal).ToArray(), DateTimeOffset.UtcNow);
    }

    public Task<ImportPreview> PreviewAsync(
        ImportDetection detection,
        CancellationToken cancellationToken = default)
    {
        if (!detection.CanPreview)
            throw new InvalidOperationException("Unknown or ambiguous import detection cannot be previewed without resolution.");
        var mappings = detection.Adapters.SelectMany(adapter => adapter.MappedDomains.Select(domain =>
            new ImportIdentityMapping(domain, $"{adapter.AdapterIdentity}:{domain}",
                $"canonical:{domain}", Preserved: false, adapter.IdentityRules.FirstOrDefault() ?? "durable-correspondence", null)))
            .ToArray();
        var deltas = mappings.GroupBy(item => item.Domain, StringComparer.Ordinal)
            .Select(group => new ImportSemanticDelta(group.Key, group.Count(), 0, group.Count(), []))
            .ToArray();
        var preview = new ImportPreview(
            ImportPreviewIdentity.New(), detection, mappings, deltas, detection.Conflicts,
            detection.UnsupportedFacts, detection.Adapters.SelectMany(item => item.UnsupportedFields).Distinct().ToArray(),
            null, DateTimeOffset.UtcNow);
        return Task.FromResult(preview);
    }

    private static bool AddExisting(string root, IEnumerable<string> relativePaths, List<string> evidence)
    {
        bool found = false;
        foreach (string relative in relativePaths)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            evidence.Add(relative); found = true;
        }
        return found;
    }

    private static async Task<string> FingerprintAsync(string root, string database,
        IReadOnlyList<string> evidence, CancellationToken token)
    {
        var entries = new List<string>();
        foreach (string relative in evidence.Where(item => !item.StartsWith("database:", StringComparison.Ordinal))
                     .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) entries.Add($"{relative}:{await HashFileAsync(path, token)}");
            else if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                    entries.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{await HashFileAsync(file, token)}");
            }
        }
        if (File.Exists(database)) entries.Add($"database:{await HashFileAsync(database, token)}");
        return Hash(string.Join("\n", entries));
    }
    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static class Descriptors
    {
        public static readonly ImportAdapterDescriptor CanonicalMigration = new(
            "storage-convergence", "1", ImportSourceKind.CanonicalMigrationRequired,
            ["workspace-schema"], ["preserve-workspace-identity"], [], ["canonical-v8", "partial-v9"],
            "Retained by Storage Authority, never an Import Gateway runtime adapter.");
        public static readonly ImportAdapterDescriptor LegacyContinuity = new(
            "legacy-continuity-v3", "1", ImportSourceKind.LegacyContinuityV3,
            ["session-scopes", "lineage", "turns", "recovery-plans", "recovery-attempts", "provider-correlation"],
            ["preserve-valid-causal-identities", "mint-and-correspond-on-collision"], ["unobserved-history:null"],
            ["legacy-continuity-v3"], "All LegacyContinuity fixtures import and run with reader disabled.");
        public static readonly ImportAdapterDescriptor Roadmap = new(
            "pre-unification-roadmap", "1", ImportSourceKind.PreUnificationRoadmap,
            ["roadmap-state", "decision-ledger", "artifact-lifecycle", "split-family-order", "selection-provenance",
             "projection-manifests", "execution-preparation", "transition-journal", "history-evidence"],
            ["path-and-domain-stable-identity", "durable-correspondence-on-collision"], ["unobserved-history:null"],
            ["roadmap-filesystem", "roadmap-sqlite"], "All roadmap fixtures import with legacy observation disabled.");
        public static readonly ImportAdapterDescriptor Planning = new(
            "planning-artifacts", "1", ImportSourceKind.PlanningArtifacts,
            ["plan", "details", "milestones", "operational-context", "adversarial-projection", "publication"],
            ["canonical-product-identity-by-declared-path"], ["unobserved-publication:null"], ["partial-plan"],
            "All incomplete planning surfaces converge to canonical product facts.");
        public static readonly ImportAdapterDescriptor Execute = new(
            "execute-artifacts", "1", ImportSourceKind.ExecuteArtifacts,
            ["decision-sessions", "numbered-history", "handoff", "evidence", "completion-archives"],
            ["preserve-session-and-history-identities-when-valid"], ["unobserved-provider-fields:null"],
            ["decision-session", "numbered-history", "completion-archive"],
            "All execute fixtures run canonical-only with numbered-history reader disabled.");
        public static readonly ImportAdapterDescriptor CanonicalPackage = new(
            "canonical-export-codec-v1", "1", ImportSourceKind.CanonicalExportPackage,
            ["all-canonical-domains"], ["preserve-package-identities"], [], ["canonical-export-v1"],
            "Codec remains supported by M11; no legacy fallback exists.");
    }
}
