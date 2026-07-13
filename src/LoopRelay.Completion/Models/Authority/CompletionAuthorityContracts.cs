using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Completion.Models.Authority;

public readonly record struct CompletionDecisionIdentity(string Value)
{
    public static CompletionDecisionIdentity New() => new(CausalUlid.NewId("completiondecision"));
}

public readonly record struct CompletionCertificateIdentity(string Value)
{
    public static CompletionCertificateIdentity New() => new(CausalUlid.NewId("completioncertificate"));
}

public readonly record struct CompletionClosurePlanIdentity(string Value)
{
    public static CompletionClosurePlanIdentity New() => new(CausalUlid.NewId("completionplan"));
}

public readonly record struct CompletionSettlementIdentity(string Value)
{
    public static CompletionSettlementIdentity New() => new(CausalUlid.NewId("completionsettlement"));
}

public readonly record struct CertifiedTerminalIdentity(string Value)
{
    public static CertifiedTerminalIdentity New() => new(CausalUlid.NewId("certifiedterminal"));
}

public enum CompletionDecisionKind
{
    CertifiedCandidate,
    Continue,
    Waiting,
    Failed,
    Cancelled,
    SpecificCannotProceed,
}

public enum CompletionCannotProceedReason
{
    MissingEvidence,
    InvalidEvidence,
    GateRejected,
    ReviewRejected,
    AmbiguousEvidence,
    DirtyInputSurface,
    UnsupportedProviderCapability,
    StorageUnavailable,
}

public sealed record CompletionDecision(
    CompletionDecisionIdentity Identity,
    RunIdentity RootRun,
    AttemptIdentity Attempt,
    CompletionDecisionKind Kind,
    CompletionCannotProceedReason? CannotProceedReason,
    IReadOnlyList<string> EvidenceIdentities,
    IReadOnlyList<string> GateIdentities,
    IReadOnlyList<string> ReviewIdentities,
    DateTimeOffset DecidedAt)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Identity.Value) || RootRun.IsEmpty || Attempt.IsEmpty)
            throw new ArgumentException("Completion decisions require complete causal identities.");
        if (EvidenceIdentities.Count == 0)
            throw new ArgumentException("Completion decisions require evidence identities.");
        if ((Kind == CompletionDecisionKind.SpecificCannotProceed) != CannotProceedReason.HasValue)
            throw new ArgumentException("A specific cannot-proceed decision requires exactly one typed reason.");
        if (Kind == CompletionDecisionKind.CertifiedCandidate &&
            (GateIdentities.Count == 0 || ReviewIdentities.Count == 0))
            throw new ArgumentException("A certified candidate requires gate and review identities.");
    }
}

public sealed record CompletionCertificate(
    CompletionCertificateIdentity Identity,
    CompletionDecisionIdentity Decision,
    IReadOnlyList<string> EvidenceIdentities,
    DateTimeOffset CertifiedAt)
{
    public static CompletionCertificate Create(CompletionDecision decision, DateTimeOffset certifiedAt)
    {
        decision.Validate();
        if (decision.Kind != CompletionDecisionKind.CertifiedCandidate)
            throw new InvalidOperationException("Only a certified-candidate decision can produce a certificate.");
        return new(CompletionCertificateIdentity.New(), decision.Identity,
            decision.EvidenceIdentities.Concat(decision.GateIdentities).Concat(decision.ReviewIdentities)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), certifiedAt);
    }
}

public enum CompletionClosureOperationKind
{
    ArchiveMaterialization,
    ArchiveSemanticVerification,
    RoadmapCompletionContextMaterialization,
    NestedAgentsCommit,
    NestedAgentsRequiredPush,
    ParentRepositoryCommit,
    ParentRepositoryRequiredPush,
    CompletionRouteEvidence,
    IndependentPostconditionVerification,
    DecisionScopeRetirement,
    WarmSessionRetirement,
    CertificationCheckpointRetirement,
    CertifiedTerminalFact,
}

public sealed record CompletionClosureOperation(
    string Identity,
    CompletionClosureOperationKind Kind,
    int Order,
    IReadOnlyList<string> Dependencies,
    bool Required);

public sealed record CompletionClosurePlan(
    CompletionClosurePlanIdentity Identity,
    CompletionDecisionIdentity Decision,
    CompletionCertificateIdentity Certificate,
    IReadOnlyList<CompletionClosureOperation> Operations,
    string ContentHash,
    DateTimeOffset PlannedAt)
{
    public static CompletionClosurePlan Build(
        CompletionDecision decision,
        CompletionCertificate certificate,
        bool nestedAgentsChanged,
        bool parentRepositoryChanged,
        DateTimeOffset plannedAt)
    {
        if (decision.Kind != CompletionDecisionKind.CertifiedCandidate || certificate.Decision != decision.Identity)
            throw new InvalidOperationException("Closure plans require the matching certified candidate and certificate.");
        var kinds = new List<CompletionClosureOperationKind>
        {
            CompletionClosureOperationKind.ArchiveMaterialization,
            CompletionClosureOperationKind.ArchiveSemanticVerification,
            CompletionClosureOperationKind.RoadmapCompletionContextMaterialization,
        };
        if (nestedAgentsChanged)
        {
            kinds.Add(CompletionClosureOperationKind.NestedAgentsCommit);
            kinds.Add(CompletionClosureOperationKind.NestedAgentsRequiredPush);
        }
        if (parentRepositoryChanged)
        {
            kinds.Add(CompletionClosureOperationKind.ParentRepositoryCommit);
            kinds.Add(CompletionClosureOperationKind.ParentRepositoryRequiredPush);
        }
        kinds.AddRange(
        [
            CompletionClosureOperationKind.CompletionRouteEvidence,
            CompletionClosureOperationKind.IndependentPostconditionVerification,
            CompletionClosureOperationKind.DecisionScopeRetirement,
            CompletionClosureOperationKind.WarmSessionRetirement,
            CompletionClosureOperationKind.CertificationCheckpointRetirement,
            CompletionClosureOperationKind.CertifiedTerminalFact,
        ]);
        var operations = new List<CompletionClosureOperation>(kinds.Count);
        for (int index = 0; index < kinds.Count; index++)
        {
            string identity = $"completion:{decision.Identity.Value}:{index:D2}:{kinds[index]}";
            operations.Add(new(identity, kinds[index], index,
                index == 0 ? [] : [operations[index - 1].Identity], Required: true));
        }
        string canonical = JsonSerializer.Serialize(operations);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new(CompletionClosurePlanIdentity.New(), decision.Identity, certificate.Identity,
            operations, hash, plannedAt);
    }
}

public enum CompletionClosureOperationState
{
    Planned,
    Started,
    Pending,
    Succeeded,
    Failed,
    Stalled,
    Unknown,
    Cancelled,
}

public enum CompletionSettlementKind
{
    EffectsPending,
    RecoveryRequired,
    Failed,
    Cancelled,
    SpecificCannotProceed,
    CertifiedTerminal,
}

public sealed record CompletionSettlement(
    CompletionSettlementIdentity Identity,
    CompletionClosurePlanIdentity Plan,
    CompletionSettlementKind Kind,
    IReadOnlyList<string> PendingOperations,
    IReadOnlyList<string> EvidenceIdentities,
    CompletionCannotProceedReason? CannotProceedReason,
    DateTimeOffset SettledAt);

public sealed record CompletionClosureReceipt(
    string OperationIdentity,
    string EffectReceiptIdentity);

public sealed record CertifiedTerminalFact(
    CertifiedTerminalIdentity Identity,
    RunIdentity RootRun,
    CompletionDecisionIdentity Decision,
    CompletionCertificateIdentity Certificate,
    CompletionClosurePlanIdentity Plan,
    CompletionSettlementIdentity Settlement,
    IReadOnlyList<CompletionClosureReceipt> EffectReceipts,
    DateTimeOffset RecordedAt);

public sealed record CompletionDecisionInput(
    RunIdentity RootRun,
    AttemptIdentity Attempt,
    bool Cancelled,
    bool Failed,
    bool Waiting,
    bool ContinueExecution,
    CompletionCannotProceedReason? CannotProceedReason,
    IReadOnlyList<string> EvidenceIdentities,
    IReadOnlyList<string> GateIdentities,
    IReadOnlyList<string> ReviewIdentities);

public sealed class CompletionAuthority
{
    public CompletionDecision Decide(CompletionDecisionInput input, DateTimeOffset now)
    {
        CompletionDecisionKind kind = input.Cancelled ? CompletionDecisionKind.Cancelled
            : input.Failed ? CompletionDecisionKind.Failed
            : input.CannotProceedReason.HasValue ? CompletionDecisionKind.SpecificCannotProceed
            : input.Waiting ? CompletionDecisionKind.Waiting
            : input.ContinueExecution ? CompletionDecisionKind.Continue
            : CompletionDecisionKind.CertifiedCandidate;
        var decision = new CompletionDecision(CompletionDecisionIdentity.New(), input.RootRun, input.Attempt,
            kind, input.CannotProceedReason, input.EvidenceIdentities, input.GateIdentities,
            input.ReviewIdentities, now);
        decision.Validate();
        return decision;
    }

    public CompletionSettlement Settle(
        CompletionClosurePlan plan,
        IReadOnlyDictionary<string, CompletionClosureOperationState> states,
        IReadOnlyList<string> evidence,
        DateTimeOffset now)
    {
        CompletionClosureOperation[] missing = plan.Operations
            .Where(operation => !states.TryGetValue(operation.Identity, out CompletionClosureOperationState state) ||
                state != CompletionClosureOperationState.Succeeded)
            .ToArray();
        CompletionSettlementKind kind = missing.Length == 0
            ? CompletionSettlementKind.CertifiedTerminal
            : missing.Any(operation => states.GetValueOrDefault(operation.Identity) == CompletionClosureOperationState.Cancelled)
                ? CompletionSettlementKind.Cancelled
            : missing.Any(operation => states.GetValueOrDefault(operation.Identity) is
                CompletionClosureOperationState.Unknown or CompletionClosureOperationState.Started)
                ? CompletionSettlementKind.RecoveryRequired
            : missing.Any(operation => states.GetValueOrDefault(operation.Identity) is
                CompletionClosureOperationState.Failed or CompletionClosureOperationState.Stalled)
                ? CompletionSettlementKind.Failed
                : CompletionSettlementKind.EffectsPending;
        return new(CompletionSettlementIdentity.New(), plan.Identity, kind,
            missing.Select(operation => operation.Identity).ToArray(), evidence, null, now);
    }
}
