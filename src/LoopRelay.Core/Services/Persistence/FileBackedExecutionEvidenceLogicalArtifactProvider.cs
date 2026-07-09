using System.Text.RegularExpressions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;

namespace LoopRelay.Core.Services.Persistence;

public sealed partial class FileBackedExecutionEvidenceLogicalArtifactProvider(
    IExecutionEvidenceStore evidenceStore) : ILogicalArtifactProvider
{
    public bool CanResolve(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/').StartsWith(
            FileBackedExecutionEvidenceStore.ExecutionEvidenceDirectory + "/",
            StringComparison.OrdinalIgnoreCase);

    public async Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        Match match = ExecutionEvidencePathRegex().Match(normalizedPath);
        bool validSequence = match.Success &&
            int.TryParse(match.Groups["number"].Value, out int sequence) &&
            sequence > 0;
        LogicalArtifactDescriptor descriptor = new(
            normalizedPath,
            LogicalArtifactDomain.ExecutionEvidence,
            LogicalArtifactStorageKind.FileBackedMigratedDomain,
            validSequence ? $"{match.Groups["stem"].Value}:{match.Groups["number"].Value}" : normalizedPath);
        if (!validSequence)
        {
            return LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.InvalidPath,
                "Execution evidence path must end with a positive four-digit sequence.");
        }

        ExecutionEvidenceRecord? record = await evidenceStore.ReadAsync(normalizedPath);
        if (record is null)
        {
            return LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"Execution evidence record is missing: {normalizedPath}");
        }

        return LogicalArtifactResolutionResult.Resolved(descriptor, record.Content);
    }

    [GeneratedRegex(@"^\.agents/evidence/execution/(?<stem>.+)\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionEvidencePathRegex();
}
