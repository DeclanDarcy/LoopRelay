using System.Security.Cryptography;
using System.Text;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Decisions.Recovery;

internal sealed class ThreadReadRecoverySource(
    IAgentSessionContinuityRuntime _runtime) : IRecoverySource
{
    public string Kind => "ThreadRead";

    public async Task<RecoverySourceObservation?> ObserveAsync(
        RecoverySourceRequest request,
        CancellationToken cancellationToken)
    {
        SessionContentResult result = await _runtime.ReadSessionAsync(
            new SessionContentRequest(request.SessionSpec, request.Original, request.Profile), cancellationToken);
        if (!result.Succeeded || result.ContentDigest is null || result.Records is null)
        {
            return null;
        }

        var descriptor = new RecoverySourceDescriptor(
            0, Kind, $"provider:{request.Original.ThreadId}", result.ContentDigest, result.VerifiedBoundary,
            "thread-read-normalizer.v1", result.Partial ? RecoveryCompleteness.Selective : RecoveryCompleteness.Full,
            result.Partial ? ["partial-provider-projection"] : [],
            new Dictionary<string, string> { ["profile"] = request.Profile.Digest, ["scope"] = request.ScopeId });
        return new RecoverySourceObservation(descriptor, result.Records);
    }
}

internal sealed class RolloutSalvageRecoverySource(
    CodexRolloutRepository _repository,
    string _codexHome) : IRecoverySource
{
    public string Kind => "RolloutSalvage";

    public async Task<RecoverySourceObservation?> ObserveAsync(
        RecoverySourceRequest request,
        CancellationToken cancellationToken)
    {
        CodexRolloutReadResult result = await _repository.ReadExactAsync(
            _codexHome, request.Original.ThreadId, cancellationToken);
        if (result.Status is not (CodexRolloutReadStatus.Complete or CodexRolloutReadStatus.Partial)
            || result.Digest is null || result.Location is null)
        {
            return null;
        }

        string location = Path.GetRelativePath(Path.GetFullPath(_codexHome), result.Location)
            .Replace(Path.DirectorySeparatorChar, '/');
        var descriptor = new RecoverySourceDescriptor(
            1, Kind, location, result.Digest, result.VerifiedBoundary,
            "codex-rollout-public.v1",
            result.Status == CodexRolloutReadStatus.Complete ? RecoveryCompleteness.Full : RecoveryCompleteness.Selective,
            result.Omissions,
            new Dictionary<string, string> { ["thread-id"] = request.Original.ThreadId, ["scope"] = request.ScopeId });
        return new RecoverySourceObservation(descriptor, result.Records);
    }
}

internal sealed class RepositoryContinuationRecoverySource(Repository _repository) : IRecoverySource
{
    private const int PerFileCharacterLimit = 32_000;
    private const int TotalCharacterLimit = 160_000;

    public string Kind => "Repository";

    public async Task<RecoverySourceObservation?> ObserveAsync(
        RecoverySourceRequest request,
        CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(_repository.Path);
        string[] candidates =
        [
            ".agents/epic.md",
            OrchestrationArtifactPaths.Plan,
            OrchestrationArtifactPaths.Details,
            OrchestrationArtifactPaths.OperationalContext,
            OrchestrationArtifactPaths.Decisions,
            OrchestrationArtifactPaths.LiveHandoff,
        ];
        var records = new List<SessionContentRecord>();
        var omissions = new List<string>();
        int total = 0;
        foreach (string relative in candidates)
        {
            string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(path))
            {
                omissions.Add($"missing:{relative}");
                continue;
            }

            string content = await File.ReadAllTextAsync(path, cancellationToken);
            if (content.Length > PerFileCharacterLimit)
            {
                content = content[..PerFileCharacterLimit];
                omissions.Add($"truncated:{relative}");
            }

            if (total + content.Length > TotalCharacterLimit)
            {
                omissions.Add($"budget:{relative}");
                continue;
            }

            total += content.Length;
            records.Add(new SessionContentRecord(
                records.Count, "repository-artifact", "repository", content, null,
                new Dictionary<string, string> { ["path"] = relative }));
        }

        string milestones = Path.Combine(root, ".agents", "milestones");
        if (Directory.Exists(milestones))
        {
            foreach (string path in Directory.EnumerateFiles(milestones, "*.md").Order(StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
                string content = await File.ReadAllTextAsync(path, cancellationToken);
                if (content.Length > PerFileCharacterLimit || total + content.Length > TotalCharacterLimit)
                {
                    omissions.Add($"budget:{relative}");
                    continue;
                }

                total += content.Length;
                records.Add(new SessionContentRecord(
                    records.Count, "repository-artifact", "repository", content, null,
                    new Dictionary<string, string> { ["path"] = relative }));
            }
        }

        if (records.Count == 0)
        {
            return null;
        }

        string digestMaterial = string.Join('\n', records.Select(record =>
            $"{record.Metadata["path"]}:{Sha256(record.Text)}"));
        var descriptor = new RecoverySourceDescriptor(
            2, Kind, "repository-products", Sha256(digestMaterial), "repository-snapshot.v1",
            "repository-continuation.v1", RecoveryCompleteness.RepositoryOnly,
            omissions.Order(StringComparer.Ordinal).ToArray(),
            new Dictionary<string, string>
            {
                ["scope"] = request.ScopeId,
                ["record-count"] = records.Count.ToString(),
                ["character-count"] = total.ToString(),
            });
        return new RecoverySourceObservation(descriptor, records);
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
