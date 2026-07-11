using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Cli;

internal sealed record ConsumedInputDrift(
    string Path,
    string ConsumedSha256,
    string? CurrentSha256,
    string Workflow,
    string Transition);

// Passive staleness projection (M3): the most recent read receipt per (workflow, transition)
// is compared against the current working tree. Purely informational — no gating, no warning
// rows; a consumer can see that its input has since changed, nothing more.
internal static class ReadReceiptStaleness
{
    public static async Task<IReadOnlyList<ConsumedInputDrift>> ProjectAsync(
        Repository repository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CanonicalReadReceiptRecord> receipts =
            await new CanonicalWorkflowPersistenceStore(repository).ReadReadReceiptsAsync(cancellationToken);
        if (receipts.Count == 0)
        {
            return [];
        }

        var drift = new List<ConsumedInputDrift>();
        IEnumerable<CanonicalReadReceiptRecord> latestPerTransition = receipts
            .GroupBy(receipt => (receipt.WorkflowIdentity, receipt.TransitionIdentity))
            .Select(group => group.Last());
        string root = Path.GetFullPath(repository.Path);
        foreach (CanonicalReadReceiptRecord receipt in latestPerTransition)
        {
            foreach (CanonicalReadReceiptFile file in receipt.Files)
            {
                // Receipt paths come from the workspace database; anything that resolves outside
                // the repository root is not a reportable consumed input.
                if (!TryResolveWithinRoot(root, file.Path, out string absolutePath))
                {
                    continue;
                }

                string? currentSha256 = CurrentHash(absolutePath);
                if (!string.Equals(currentSha256, file.Sha256, StringComparison.Ordinal))
                {
                    drift.Add(new ConsumedInputDrift(
                        file.Path,
                        file.Sha256,
                        currentSha256,
                        receipt.WorkflowIdentity,
                        receipt.TransitionIdentity));
                }
            }
        }

        return drift;
    }

    private static bool TryResolveWithinRoot(string root, string relativePath, out string absolutePath)
    {
        absolutePath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return absolutePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string? CurrentHash(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        return ConsumedInputFile.HashContent(File.ReadAllText(absolutePath));
    }
}
