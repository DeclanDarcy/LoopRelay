using System.Text.RegularExpressions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal sealed partial class SqliteLoopHistoryLogicalArtifactProvider(Repository repository) : ILogicalArtifactProvider
{
    public bool CanResolve(string relativePath) =>
        MatchesHistoryPattern(relativePath);

    public async Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        LoopHistoryDescriptor descriptor = DescriptorFor(normalizedPath);
        LogicalArtifactDescriptor logicalDescriptor = new(
            normalizedPath,
            LogicalArtifactDomain.LoopHistory,
            LogicalArtifactStorageKind.SqliteCanonicalRecord,
            descriptor.Identity);
        if (!descriptor.Valid)
        {
            return LogicalArtifactResolutionResult.Unresolved(
                logicalDescriptor,
                LogicalArtifactResolutionStatus.InvalidPath,
                "Loop history path must end with a positive four-digit sequence.");
        }

        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return LogicalArtifactResolutionResult.Unresolved(
                logicalDescriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"SQLite loop history database is missing for: {normalizedPath}");
        }

        await using SqliteConnection connection = WorkspaceSqliteStore.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT body, content_hash
            FROM loop_history
            WHERE kind = $kind AND sequence = $sequence AND logical_path = $logical_path;
            """;
        command.Parameters.AddWithValue("$kind", descriptor.Kind);
        command.Parameters.AddWithValue("$sequence", descriptor.Sequence);
        command.Parameters.AddWithValue("$logical_path", normalizedPath);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return LogicalArtifactResolutionResult.Unresolved(
                logicalDescriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"SQLite loop history record is missing: {normalizedPath}");
        }

        string body = reader.GetString(0);
        string hash = reader.GetString(1);
        if (!string.Equals(hash, WorkspaceSqliteStore.Sha256(body), StringComparison.Ordinal))
        {
            return LogicalArtifactResolutionResult.Unresolved(
                logicalDescriptor,
                LogicalArtifactResolutionStatus.Invalid,
                $"SQLite loop history hash mismatch for: {normalizedPath}");
        }

        return LogicalArtifactResolutionResult.Resolved(logicalDescriptor, body);
    }

    private static bool MatchesHistoryPattern(string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        return normalizedPath.StartsWith(OrchestrationArtifactPaths.DecisionsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(OrchestrationArtifactPaths.HandoffsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(OrchestrationArtifactPaths.DeltasDirectory + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static LoopHistoryDescriptor DescriptorFor(string normalizedPath)
    {
        Match decision = DecisionHistoryPathRegex().Match(normalizedPath);
        if (decision.Success)
        {
            return FromMatch("Decisions", "decisions", normalizedPath, decision);
        }

        Match handoff = HandoffHistoryPathRegex().Match(normalizedPath);
        if (handoff.Success)
        {
            return FromMatch("Handoff", "handoff", normalizedPath, handoff);
        }

        Match delta = DeltaHistoryPathRegex().Match(normalizedPath);
        if (delta.Success)
        {
            return FromMatch("OperationalDelta", "operational_delta", normalizedPath, delta);
        }

        return new LoopHistoryDescriptor(false, string.Empty, 0, normalizedPath);
    }

    private static LoopHistoryDescriptor FromMatch(
        string kind,
        string identityPrefix,
        string normalizedPath,
        Match match)
    {
        bool valid = int.TryParse(match.Groups["number"].Value, out int sequence) && sequence > 0;
        return new LoopHistoryDescriptor(
            valid,
            kind,
            sequence,
            $"{identityPrefix}:{normalizedPath}");
    }

    [GeneratedRegex(@"^\.agents/decisions/decisions\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex DecisionHistoryPathRegex();

    [GeneratedRegex(@"^\.agents/handoffs/handoff\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex HandoffHistoryPathRegex();

    [GeneratedRegex(@"^\.agents/deltas/operational_delta\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex DeltaHistoryPathRegex();

    private readonly record struct LoopHistoryDescriptor(
        bool Valid,
        string Kind,
        int Sequence,
        string Identity);
}
