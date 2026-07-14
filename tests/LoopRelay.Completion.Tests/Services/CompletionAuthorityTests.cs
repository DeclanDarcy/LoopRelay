using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using Xunit;

namespace LoopRelay.Completion.Tests.Services;

public sealed class CompletionAuthorityTests
{
    [Theory]
    [InlineData(false, false, false, false, null, CompletionDecisionKind.CertifiedCandidate)]
    [InlineData(false, false, false, true, null, CompletionDecisionKind.Continue)]
    [InlineData(false, false, true, false, null, CompletionDecisionKind.Waiting)]
    [InlineData(false, true, false, false, null, CompletionDecisionKind.Failed)]
    [InlineData(true, false, false, false, null, CompletionDecisionKind.Cancelled)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.MissingEvidence, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.InvalidEvidence, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.GateRejected, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.ReviewRejected, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.AmbiguousEvidence, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.DirtyInputSurface, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.UnsupportedProviderCapability, CompletionDecisionKind.SpecificCannotProceed)]
    [InlineData(false, false, false, false, CompletionCannotProceedReason.StorageUnavailable, CompletionDecisionKind.SpecificCannotProceed)]
    public void Decision_vocabulary_is_typed_and_pure(
        bool cancelled, bool failed, bool waiting, bool continued,
        CompletionCannotProceedReason? reason, CompletionDecisionKind expected)
    {
        CompletionDecision decision = new CompletionAuthority().Decide(new(
            RunIdentity.New(), AttemptIdentity.New(), cancelled, failed, waiting, continued, reason,
            ["evidence:1"], ["gate:1"], ["review:1"]), DateTimeOffset.UtcNow);

        Assert.Equal(expected, decision.Kind);
        Assert.Equal(reason, decision.CannotProceedReason);
    }

    [Fact]
    public void Certificate_and_plan_exist_only_for_certified_candidate_and_order_publication_before_cleanup()
    {
        var authority = new CompletionAuthority();
        CompletionDecision decision = Certified(authority);
        CompletionCertificate certificate = CompletionCertificate.Create(decision, DateTimeOffset.UtcNow);
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            decision, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true,
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => CompletionCertificate.Create(
            authority.Decide(new(RunIdentity.New(), AttemptIdentity.New(), false, false, false, true,
                null, ["evidence"], [], []), DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));
        Assert.Equal(Enumerable.Range(0, plan.Operations.Count), plan.Operations.Select(item => item.Order));
        Assert.Equal(CompletionClosureOperationKind.ArchiveMaterialization, plan.Operations[0].Kind);
        Assert.True(Index(CompletionClosureOperationKind.NestedAgentsRequiredPush) <
            Index(CompletionClosureOperationKind.ParentRepositoryCommit));
        Assert.True(Index(CompletionClosureOperationKind.IndependentPostconditionVerification) <
            Index(CompletionClosureOperationKind.DecisionScopeRetirement));
        Assert.Equal(CompletionClosureOperationKind.CertifiedTerminalFact, plan.Operations[^1].Kind);
        int Index(CompletionClosureOperationKind kind) => plan.Operations.Single(item => item.Kind == kind).Order;
    }

    [Fact]
    public async Task Partial_or_unknown_closure_never_promotes_terminal_and_full_receipts_settle_once()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-completion-authority").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        var authority = new CompletionAuthority();
        CompletionDecision decision = Certified(authority);
        CompletionCertificate certificate = CompletionCertificate.Create(decision, DateTimeOffset.UtcNow);
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            decision, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true,
            DateTimeOffset.UtcNow);
        var store = new CanonicalCompletionAuthorityStore(repository);
        await store.PersistCertifiedCandidateAsync(decision, certificate, plan);

        Dictionary<string, CompletionClosureOperationState> states = plan.Operations
            .ToDictionary(item => item.Identity, _ => CompletionClosureOperationState.Planned);
        states[plan.Operations[0].Identity] = CompletionClosureOperationState.Succeeded;
        states[plan.Operations[1].Identity] = CompletionClosureOperationState.Unknown;
        CompletionSettlement recovery = authority.Settle(plan, states, ["effect:unknown"], DateTimeOffset.UtcNow);
        Assert.Equal(CompletionSettlementKind.RecoveryRequired, recovery.Kind);
        await store.AppendSettlementAsync(decision, certificate, plan, recovery, []);
        Assert.Empty((await store.ReadSnapshotAsync()).TerminalFacts);

        foreach (CompletionClosureOperation operation in plan.Operations)
            states[operation.Identity] = CompletionClosureOperationState.Succeeded;
        CompletionSettlement terminal = authority.Settle(plan, states, ["postcondition:verified"], DateTimeOffset.UtcNow);
        CompletionClosureReceipt[] receipts = plan.Operations
            .Where(item => item.Kind != CompletionClosureOperationKind.CertifiedTerminalFact)
            .Select(item => new CompletionClosureReceipt(item.Identity, "receipt:" + item.Identity)).ToArray();
        await store.AppendSettlementAsync(decision, certificate, plan, terminal, receipts);
        await store.AppendSettlementAsync(decision, certificate, plan, terminal, receipts);

        CanonicalCompletionSnapshot snapshot = await store.ReadSnapshotAsync();
        Assert.Single(snapshot.Decisions);
        Assert.Single(snapshot.Certificates);
        Assert.Single(snapshot.ClosurePlans);
        Assert.Equal(2, snapshot.Settlements.Count);
        CertifiedTerminalFact fact = Assert.Single(snapshot.TerminalFacts);
        Assert.Equal(decision.RootRun, fact.RootRun);
        Assert.Equal(
            receipts.OrderBy(item => item.OperationIdentity, StringComparer.Ordinal),
            fact.EffectReceipts);
    }

    [Fact]
    public async Task Every_closure_boundary_fails_closed_for_pending_unknown_failed_stalled_and_cancelled_state()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-completion-fault-campaign").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        var authority = new CompletionAuthority();
        CompletionDecision decision = Certified(authority);
        CompletionCertificate certificate = CompletionCertificate.Create(decision, DateTimeOffset.UtcNow);
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            decision, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true,
            DateTimeOffset.UtcNow);
        var store = new CanonicalCompletionAuthorityStore(repository);
        await store.PersistCertifiedCandidateAsync(decision, certificate, plan);
        CompletionClosureOperation[] effectOperations = plan.Operations
            .Where(item => item.Kind != CompletionClosureOperationKind.CertifiedTerminalFact).ToArray();
        CompletionClosureOperationState[] faults =
        [
            CompletionClosureOperationState.Pending,
            CompletionClosureOperationState.Unknown,
            CompletionClosureOperationState.Failed,
            CompletionClosureOperationState.Stalled,
            CompletionClosureOperationState.Cancelled,
        ];

        foreach (CompletionClosureOperation operation in effectOperations)
        foreach (CompletionClosureOperationState fault in faults)
        {
            Dictionary<string, CompletionClosureOperationState> states = plan.Operations
                .ToDictionary(item => item.Identity, _ => CompletionClosureOperationState.Succeeded);
            states[operation.Identity] = fault;
            CompletionSettlement settlement = authority.Settle(
                plan, states, [$"fault:{operation.Kind}:{fault}"], DateTimeOffset.UtcNow);
            Assert.NotEqual(CompletionSettlementKind.CertifiedTerminal, settlement.Kind);
            await store.AppendSettlementAsync(decision, certificate, plan, settlement, []);
            Assert.Empty((await store.ReadSnapshotAsync()).TerminalFacts);
        }
    }

    [Fact]
    public async Task Terminal_settlement_rejects_receipts_from_unrelated_effects_or_incomplete_operation_maps()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-completion-receipt-map").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        var authority = new CompletionAuthority();
        CompletionDecision decision = Certified(authority);
        CompletionCertificate certificate = CompletionCertificate.Create(decision, DateTimeOffset.UtcNow);
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            decision, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true,
            DateTimeOffset.UtcNow);
        var store = new CanonicalCompletionAuthorityStore(repository);
        await store.PersistCertifiedCandidateAsync(decision, certificate, plan);
        Dictionary<string, CompletionClosureOperationState> states = plan.Operations
            .ToDictionary(item => item.Identity, _ => CompletionClosureOperationState.Succeeded);
        CompletionSettlement terminal = authority.Settle(plan, states, ["receipt:unrelated"], DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.AppendSettlementAsync(
            decision,
            certificate,
            plan,
            terminal,
            [new CompletionClosureReceipt("unrelated-operation", "receipt:unrelated")]));
        Assert.Empty((await store.ReadSnapshotAsync()).TerminalFacts);
    }

    private static CompletionDecision Certified(CompletionAuthority authority) => authority.Decide(new(
        RunIdentity.New(), AttemptIdentity.New(), false, false, false, false, null,
        ["evidence:completion"], ["gate:milestones"], ["review:non-implementation"]),
        DateTimeOffset.UtcNow);
}
