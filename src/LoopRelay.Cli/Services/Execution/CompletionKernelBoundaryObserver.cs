using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Cli.Services.Execution;

internal sealed class CompletionKernelBoundaryObserver(Repository _repository) : IKernelBoundaryObserver
{
    public async Task ObserveAsync(
        KernelCommand command,
        WorkflowChainRunResult boundary,
        CancellationToken cancellationToken)
    {
        if (boundary.ControllerResult?.Transition?.Transition !=
                new WorkflowTransitionIdentity("VerifyWorkflowExitGate"))
            return;
        var store = new CanonicalCompletionAuthorityStore(_repository);
        CanonicalCompletionSnapshot snapshot = await store.ReadSnapshotAsync(cancellationToken);
        CompletionDecision? decision = snapshot.Decisions.LastOrDefault(item =>
            item.RootRun == command.Context.Run && item.Kind == CompletionDecisionKind.CertifiedCandidate);
        if (decision is null) return;
        CompletionCertificate certificate = snapshot.Certificates.Single(item => item.Decision == decision.Identity);
        CompletionClosurePlan plan = snapshot.ClosurePlans.Single(item => item.Certificate == certificate.Identity);
        if (snapshot.TerminalFacts.Any(item => item.RootRun == decision.RootRun)) return;
        IReadOnlyList<EffectWorkItem> effects = await new CanonicalEffectWorkStore(_repository)
            .ReadRunAsync(command.Context.Run, cancellationToken);
        var receipts = new List<CompletionClosureReceipt>();
        var states = new Dictionary<string, CompletionClosureOperationState>(StringComparer.Ordinal);
        foreach (CompletionClosureOperation operation in plan.Operations)
        {
            if (operation.Kind == CompletionClosureOperationKind.CertifiedTerminalFact)
            {
                states[operation.Identity] = CompletionClosureOperationState.Succeeded;
                continue;
            }
            string[] semanticOperations = SemanticOperations(operation.Kind);
            EffectWorkItem[] matches = semanticOperations
                .Select(semantic => effects.SingleOrDefault(item =>
                    string.Equals(item.Intent.SemanticOperationKey, semantic, StringComparison.Ordinal)))
                .Where(item => item is not null)
                .Cast<EffectWorkItem>()
                .ToArray();
            if (matches.Length != semanticOperations.Length || matches.Any(item =>
                    item.State != EffectLifecycle.Succeeded ||
                    item.Receipt is not { PostconditionSatisfied: true }))
            {
                states[operation.Identity] = matches.Any(item => item.State == EffectLifecycle.Unknown)
                    ? CompletionClosureOperationState.Unknown
                    : matches.Any(item => item.State is EffectLifecycle.Failed or EffectLifecycle.Stalled)
                        ? CompletionClosureOperationState.Failed
                        : CompletionClosureOperationState.Pending;
                continue;
            }
            states[operation.Identity] = CompletionClosureOperationState.Succeeded;
            receipts.AddRange(matches.Select(item =>
                new CompletionClosureReceipt(operation.Identity, item.Receipt!.Identity.Value)));
        }
        CompletionSettlement settlement = new CompletionAuthority().Settle(
            plan,
            states,
            receipts.Select(receipt => receipt.EffectReceiptIdentity).Distinct(StringComparer.Ordinal).ToArray(),
            DateTimeOffset.UtcNow);
        CompletionSettlement? prior = snapshot.Settlements.LastOrDefault(item => item.Plan == plan.Identity);
        if (prior is not null && prior.Kind == settlement.Kind &&
            prior.PendingOperations.SequenceEqual(settlement.PendingOperations, StringComparer.Ordinal))
            return;
        await store.AppendSettlementAsync(decision, certificate, plan, settlement, receipts, cancellationToken);
    }

    private static string[] SemanticOperations(CompletionClosureOperationKind kind) => kind switch
    {
        CompletionClosureOperationKind.ArchiveMaterialization or
            CompletionClosureOperationKind.ArchiveSemanticVerification =>
            ["completion:archive-and-synthesize"],
        CompletionClosureOperationKind.RoadmapCompletionContextMaterialization =>
            ["completion:roadmap-context-materialization"],
        CompletionClosureOperationKind.NestedAgentsCommit => ["completion:nested-agents-commit"],
        CompletionClosureOperationKind.NestedAgentsRequiredPush => ["completion:nested-agents-push"],
        CompletionClosureOperationKind.ParentRepositoryCommit =>
            ["completion:parent-gitlink-commit", "completion:parent-working-tree-commit"],
        CompletionClosureOperationKind.ParentRepositoryRequiredPush => ["completion:parent-repository-push"],
        CompletionClosureOperationKind.CompletionRouteEvidence => ["transition-effect:record-completion-route"],
        CompletionClosureOperationKind.IndependentPostconditionVerification =>
            ["transition-effect:record-certified-completion"],
        CompletionClosureOperationKind.DecisionScopeRetirement => ["completion:retire-decision-continuity"],
        CompletionClosureOperationKind.WarmSessionRetirement => ["completion:retire-execution-warm-session"],
        CompletionClosureOperationKind.CertificationCheckpointRetirement =>
            ["completion:retire-certification-checkpoint"],
        _ => throw new InvalidOperationException($"No effect mapping exists for completion operation `{kind}`."),
    };
}
