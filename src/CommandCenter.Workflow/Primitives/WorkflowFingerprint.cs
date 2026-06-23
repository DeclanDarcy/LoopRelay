using System.Security.Cryptography;
using System.Text;
using CommandCenter.Workflow.Models;

namespace CommandCenter.Workflow.Primitives;

public readonly record struct WorkflowFingerprint(string Value)
{
    public override string ToString() => Value;

    public static WorkflowFingerprint ForEntry(
        WorkflowTimelineEventType eventType,
        WorkflowStage stage,
        DateTimeOffset occurredAt,
        string summary,
        string sourceDomain,
        string sourceArtifact)
    {
        string normalized = string.Join(
            '\n',
            "entry",
            eventType,
            stage,
            occurredAt.UtcDateTime.ToString("O"),
            summary,
            sourceDomain,
            sourceArtifact);
        return FromNormalizedEvidence(normalized);
    }

    public static WorkflowFingerprint ForTimeline(
        Guid repositoryId,
        WorkflowStage currentStage,
        WorkflowStage previousStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        IReadOnlyList<WorkflowTimelineEntry> entries,
        IReadOnlyList<WorkflowBlockingCondition?> blockingConditions)
    {
        string lastEntry = entries.Count == 0
            ? "none"
            : entries[^1].Fingerprint;
        string normalized = string.Join(
            '\n',
            "timeline",
            repositoryId,
            currentStage,
            previousStage,
            progressState,
            blockingGate,
            $"timeline-count:{entries.Count}",
            $"last-entry:{lastEntry}",
            $"blocking-conditions:{string.Join(",", blockingConditions.Select(condition => condition?.ToString() ?? "None"))}",
            $"gate-state:{blockingGate}");
        return FromNormalizedEvidence(normalized);
    }

    public static WorkflowFingerprint FromNormalizedEvidence(string evidence)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(evidence));
        return new WorkflowFingerprint(Convert.ToHexString(hash).ToLowerInvariant());
    }
}
