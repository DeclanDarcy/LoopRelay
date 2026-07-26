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

    [Fact]
    public async Task Run_scoped_snapshot_equals_the_in_memory_filtered_unfiltered_snapshot()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-completion-run-scope").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        var store = new CanonicalCompletionAuthorityStore(repository);
        DateTimeOffset baseline = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        // Two runs, interleaved in time, so a wrong filter shows up as extra, missing, or
        // misordered rows instead of as an empty-versus-empty pass.
        RunIdentity first = await SeedRunAsync(store, baseline, lane: 0);
        RunIdentity second = await SeedRunAsync(store, baseline, lane: 1);
        Assert.NotEqual(first, second);

        CanonicalCompletionSnapshot unfiltered = await store.ReadSnapshotAsync();
        Assert.Equal(4, unfiltered.Decisions.Count);
        Assert.Equal(2, unfiltered.Certificates.Count);
        Assert.Equal(2, unfiltered.ClosurePlans.Count);
        Assert.Equal(4, unfiltered.Settlements.Count);
        Assert.Equal(2, unfiltered.TerminalFacts.Count);

        foreach (RunIdentity run in new[] { first, second })
        {
            CanonicalCompletionSnapshot expected = FilterInMemory(unfiltered, run);
            Assert.Equal(2, expected.Decisions.Count);
            Assert.Single(expected.Certificates);
            Assert.Single(expected.ClosurePlans);
            Assert.Equal(2, expected.Settlements.Count);
            Assert.Single(expected.TerminalFacts);

            CanonicalCompletionSnapshot scoped = await store.ReadSnapshotAsync(run, CancellationToken.None);
            AssertSnapshotsMatch(expected, scoped);
        }
    }

    /// <summary>
    /// Seeds one root run: a continue decision, a certified candidate with its certificate and closure
    /// plan, a non-terminal settlement, and a terminal settlement (which also writes the terminal fact).
    /// <paramref name="lane"/> offsets every timestamp by one second so two seeded runs interleave in
    /// the unfiltered ordering and a run-scoped read has to preserve relative order.
    /// </summary>
    private static async Task<RunIdentity> SeedRunAsync(
        CanonicalCompletionAuthorityStore store, DateTimeOffset baseline, int lane)
    {
        var authority = new CompletionAuthority();
        RunIdentity run = RunIdentity.New();
        DateTimeOffset At(int step) => baseline.AddSeconds((step * 2) + lane);

        await store.AppendDecisionAsync(authority.Decide(new(run, AttemptIdentity.New(),
            false, false, false, true, null, ["evidence:continue"], [], []), At(0)));

        CompletionDecision certified = authority.Decide(new(run, AttemptIdentity.New(),
            false, false, false, false, null,
            ["evidence:completion"], ["gate:milestones"], ["review:non-implementation"]), At(1));
        CompletionCertificate certificate = CompletionCertificate.Create(certified, At(2));
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            certified, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true, At(3));
        await store.PersistCertifiedCandidateAsync(certified, certificate, plan);

        Dictionary<string, CompletionClosureOperationState> states = plan.Operations
            .ToDictionary(item => item.Identity, _ => CompletionClosureOperationState.Succeeded);
        states[plan.Operations[0].Identity] = CompletionClosureOperationState.Unknown;
        CompletionSettlement recovery = authority.Settle(plan, states, ["effect:unknown"], At(4));
        Assert.Equal(CompletionSettlementKind.RecoveryRequired, recovery.Kind);
        await store.AppendSettlementAsync(certified, certificate, plan, recovery, []);

        states[plan.Operations[0].Identity] = CompletionClosureOperationState.Succeeded;
        CompletionSettlement terminal = authority.Settle(plan, states, ["postcondition:verified"], At(5));
        CompletionClosureReceipt[] receipts = plan.Operations
            .Where(item => item.Kind != CompletionClosureOperationKind.CertifiedTerminalFact)
            .Select(item => new CompletionClosureReceipt(item.Identity, "receipt:" + item.Identity)).ToArray();
        await store.AppendSettlementAsync(certified, certificate, plan, terminal, receipts);
        return run;
    }

    /// <summary>
    /// The reference semantics the run-scoped read must reproduce: decisions and terminal facts by
    /// their own run column, certificates and closure plans by reachability from this run's decisions,
    /// settlements by reachability from this run's closure plans.
    /// </summary>
    private static CanonicalCompletionSnapshot FilterInMemory(
        CanonicalCompletionSnapshot snapshot, RunIdentity run)
    {
        CompletionDecision[] decisions = snapshot.Decisions.Where(item => item.RootRun == run).ToArray();
        HashSet<CompletionDecisionIdentity> decisionIdentities = decisions.Select(item => item.Identity).ToHashSet();
        CompletionCertificate[] certificates = snapshot.Certificates
            .Where(item => decisionIdentities.Contains(item.Decision)).ToArray();
        CompletionClosurePlan[] plans = snapshot.ClosurePlans
            .Where(item => decisionIdentities.Contains(item.Decision)).ToArray();
        HashSet<CompletionClosurePlanIdentity> planIdentities = plans.Select(item => item.Identity).ToHashSet();
        CompletionSettlement[] settlements = snapshot.Settlements
            .Where(item => planIdentities.Contains(item.Plan)).ToArray();
        CertifiedTerminalFact[] terminal = snapshot.TerminalFacts.Where(item => item.RootRun == run).ToArray();
        return new(decisions, certificates, plans, settlements, terminal);
    }

    // Field-by-field on purpose. `CanonicalCompletionSnapshot` and the records it carries are C#
    // records whose synthesised `==` compares collection-typed members by reference, so
    // `Assert.Equal(expected, actual)` on whole snapshots would compare two structurally identical
    // values as unequal — and a shallow pass would say nothing about nested contents.
    private static void AssertSnapshotsMatch(
        CanonicalCompletionSnapshot expected, CanonicalCompletionSnapshot actual)
    {
        Assert.Equal(expected.Decisions.Count, actual.Decisions.Count);
        for (int index = 0; index < expected.Decisions.Count; index++)
        {
            CompletionDecision want = expected.Decisions[index], got = actual.Decisions[index];
            Assert.Equal(want.Identity.Value, got.Identity.Value);
            Assert.Equal(want.RootRun.Value, got.RootRun.Value);
            Assert.Equal(want.Attempt.Value, got.Attempt.Value);
            Assert.Equal(want.Kind, got.Kind);
            Assert.Equal(want.CannotProceedReason, got.CannotProceedReason);
            Assert.Equal<string>(want.EvidenceIdentities, got.EvidenceIdentities);
            Assert.Equal<string>(want.GateIdentities, got.GateIdentities);
            Assert.Equal<string>(want.ReviewIdentities, got.ReviewIdentities);
            Assert.Equal(want.DecidedAt, got.DecidedAt);
        }

        Assert.Equal(expected.Certificates.Count, actual.Certificates.Count);
        for (int index = 0; index < expected.Certificates.Count; index++)
        {
            CompletionCertificate want = expected.Certificates[index], got = actual.Certificates[index];
            Assert.Equal(want.Identity.Value, got.Identity.Value);
            Assert.Equal(want.Decision.Value, got.Decision.Value);
            Assert.Equal<string>(want.EvidenceIdentities, got.EvidenceIdentities);
            Assert.Equal(want.CertifiedAt, got.CertifiedAt);
        }

        Assert.Equal(expected.ClosurePlans.Count, actual.ClosurePlans.Count);
        for (int index = 0; index < expected.ClosurePlans.Count; index++)
        {
            CompletionClosurePlan want = expected.ClosurePlans[index], got = actual.ClosurePlans[index];
            Assert.Equal(want.Identity.Value, got.Identity.Value);
            Assert.Equal(want.Decision.Value, got.Decision.Value);
            Assert.Equal(want.Certificate.Value, got.Certificate.Value);
            Assert.Equal(want.ContentHash, got.ContentHash);
            Assert.Equal(want.PlannedAt, got.PlannedAt);
            Assert.Equal(want.Operations.Count, got.Operations.Count);
            for (int operation = 0; operation < want.Operations.Count; operation++)
            {
                CompletionClosureOperation wantOperation = want.Operations[operation];
                CompletionClosureOperation gotOperation = got.Operations[operation];
                Assert.Equal(wantOperation.Identity, gotOperation.Identity);
                Assert.Equal(wantOperation.Kind, gotOperation.Kind);
                Assert.Equal(wantOperation.Order, gotOperation.Order);
                Assert.Equal(wantOperation.Required, gotOperation.Required);
                Assert.Equal<string>(wantOperation.Dependencies, gotOperation.Dependencies);
            }
        }

        Assert.Equal(expected.Settlements.Count, actual.Settlements.Count);
        for (int index = 0; index < expected.Settlements.Count; index++)
        {
            CompletionSettlement want = expected.Settlements[index], got = actual.Settlements[index];
            Assert.Equal(want.Identity.Value, got.Identity.Value);
            Assert.Equal(want.Plan.Value, got.Plan.Value);
            Assert.Equal(want.Kind, got.Kind);
            Assert.Equal<string>(want.PendingOperations, got.PendingOperations);
            Assert.Equal<string>(want.EvidenceIdentities, got.EvidenceIdentities);
            Assert.Equal(want.CannotProceedReason, got.CannotProceedReason);
            Assert.Equal(want.SettledAt, got.SettledAt);
        }

        Assert.Equal(expected.TerminalFacts.Count, actual.TerminalFacts.Count);
        for (int index = 0; index < expected.TerminalFacts.Count; index++)
        {
            CertifiedTerminalFact want = expected.TerminalFacts[index], got = actual.TerminalFacts[index];
            Assert.Equal(want.Identity.Value, got.Identity.Value);
            Assert.Equal(want.RootRun.Value, got.RootRun.Value);
            Assert.Equal(want.Decision.Value, got.Decision.Value);
            Assert.Equal(want.Certificate.Value, got.Certificate.Value);
            Assert.Equal(want.Plan.Value, got.Plan.Value);
            Assert.Equal(want.Settlement.Value, got.Settlement.Value);
            Assert.Equal(want.RecordedAt, got.RecordedAt);
            Assert.Equal(want.EffectReceipts.Count, got.EffectReceipts.Count);
            for (int receipt = 0; receipt < want.EffectReceipts.Count; receipt++)
            {
                Assert.Equal(want.EffectReceipts[receipt].OperationIdentity,
                    got.EffectReceipts[receipt].OperationIdentity);
                Assert.Equal(want.EffectReceipts[receipt].EffectReceiptIdentity,
                    got.EffectReceipts[receipt].EffectReceiptIdentity);
            }
        }
    }

    private static CompletionDecision Certified(CompletionAuthority authority) => authority.Decide(new(
        RunIdentity.New(), AttemptIdentity.New(), false, false, false, false, null,
        ["evidence:completion"], ["gate:milestones"], ["review:non-implementation"]),
        DateTimeOffset.UtcNow);
}
