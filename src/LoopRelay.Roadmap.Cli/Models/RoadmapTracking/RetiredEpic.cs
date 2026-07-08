using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapTracking;

internal sealed record RetiredEpic(
    string EpicId,
    string EpicName,
    string PrimaryReason,
    string AuditEvidencePath,
    DateTimeOffset RetiredAt)
{
    private static readonly string[] UnknownValues =
    [
        "unknown",
        "{{epic_id_or_unknown}}",
        "{{epic_name_or_unknown}}",
        "not applicable",
        "n/a",
        "none",
    ];

    private static readonly string[] WorkflowCommands =
    [
        "Realign Epic",
        "Reimagine Epic",
        "Retire Epic",
        "Gather More Evidence",
    ];

    public string StableIdentity => IsKnown(EpicId) ? EpicId : EpicName;

    public string IdentityKind => IsKnown(EpicId) ? "Epic ID" : "Epic Name";

    public string DisplayName => IsKnown(EpicName) ? EpicName : StableIdentity;

    public bool HasStableIdentity => IsKnown(StableIdentity);

    public bool Matches(RetiredEpic other)
    {
        if (IsKnown(EpicId) && IsKnown(other.EpicId))
        {
            return string.Equals(EpicId, other.EpicId, StringComparison.Ordinal);
        }

        return IsKnown(EpicName) &&
            IsKnown(other.EpicName) &&
            string.Equals(EpicName, other.EpicName, StringComparison.Ordinal);
    }

    public static RetiredEpic FromSelectionAndAudit(
        SelectionDecision selection,
        EpicPreparationAuditDecision audit,
        string auditEvidencePath,
        DateTimeOffset retiredAt)
    {
        string epicId = FirstKnown(audit.EpicId, selection.ExistingEpicId) ?? "Unknown";
        string epicName = FirstKnown(audit.EpicName, selection.ExistingEpicName, selection.RecommendedInitiative) ?? "Unknown";
        string reason = FirstKnown(audit.PrimaryReason, selection.PrimaryReason) ?? "Retired by epic preparation audit.";
        var retired = new RetiredEpic(epicId, epicName, reason, auditEvidencePath, retiredAt);

        if (!retired.HasStableIdentity)
        {
            throw new RoadmapStepException("Retire disposition did not include a stable selected epic identity.");
        }

        return retired;
    }

    public static IReadOnlyList<RetiredEpic> Upsert(IReadOnlyList<RetiredEpic> existing, RetiredEpic candidate)
    {
        var updated = existing.ToList();
        int index = updated.FindIndex(item => item.Matches(candidate));
        if (index < 0)
        {
            updated.Add(candidate);
            return updated;
        }

        if (!IsKnown(updated[index].EpicId) && IsKnown(candidate.EpicId))
        {
            updated[index] = candidate with { RetiredAt = updated[index].RetiredAt };
        }

        return updated;
    }

    public static bool IsWorkflowCommand(string value) =>
        WorkflowCommands.Any(command => string.Equals(command, value.Trim(), StringComparison.Ordinal));

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !UnknownValues.Any(unknown => string.Equals(value.Trim(), unknown, StringComparison.OrdinalIgnoreCase));

    private static string? FirstKnown(params string?[] values) =>
        values.FirstOrDefault(IsKnown)?.Trim();
}
