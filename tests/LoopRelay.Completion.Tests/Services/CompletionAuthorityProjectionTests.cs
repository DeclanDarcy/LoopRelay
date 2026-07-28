using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Completion.Tests.Services;

/// <summary>
/// Task 3.4: <see cref="CompletionAuthorityProjection"/> used to hydrate every historical row across
/// every completion-authority table via <see cref="CanonicalCompletionAuthorityStore.ReadSnapshotAsync(CancellationToken)"/>
/// (the unscoped overload) on a write connection, and throw almost all of it away with in-memory
/// <c>LastOrDefault</c> lookups. It now reads only the latest linked chain plus a count per table, via
/// <see cref="CanonicalCompletionAuthorityStore.ReadHeadsAndCountsAsync"/>, on a read-only connection.
/// </summary>
public sealed class CompletionAuthorityProjectionTests
{
#if DEBUG
    /// <summary>
    /// Characterization plus cross-epic discrimination in one test. Two root runs ("epics") are
    /// seeded so their table-order and their decision-order disagree: run B's full certified chain
    /// commits its certificate/plan/settlement/terminal rows chronologically last in their own
    /// tables, but run A's later, uncertified "Continue" decision is chronologically last in the
    /// decisions table. A correct implementation walks forward from the latest *decision* by its own
    /// foreign keys and must find no certificate, plan, settlement, or terminal fact for it (run A's
    /// decision carries none). An implementation that instead took "the latest row in each of the
    /// five tables" independently - the bug per-chain linking exists to prevent - would report run
    /// B's certificate, plan, and settlement against run A's decision: a mismatched chain a
    /// single-epic fixture cannot produce, because with only one epic the latest row per table and
    /// the latest linked row are always the same row.
    /// <para>
    /// The baseline is <see cref="LegacyProjectAsync"/>, byte-for-byte the pre-Task-3.4
    /// <c>CompletionAuthorityProjection.ProjectAsync</c> body (verified identical against the
    /// pre-change file), run through <see cref="CanonicalCompletionAuthorityStore.ReadSnapshotAsync(CancellationToken)"/>
    /// - untouched by this task and still used by other run-scoped callers. Comparing its real output
    /// to the new read-only implementation's real output, rather than reasoning about the diff, is
    /// what makes this a characterization test.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ProjectAsync_matches_the_legacy_full_hydration_projection_and_does_not_cross_contaminate_epics()
    {
        Repository repository = NewRepository();
        var store = new CanonicalCompletionAuthorityStore(repository);
        var authority = new CompletionAuthority();
        DateTimeOffset baseline = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        RunIdentity runB = RunIdentity.New();
        CompletionDecision bDecision = authority.Decide(new(runB, AttemptIdentity.New(),
            false, false, false, false, null,
            ["evidence:b"], ["gate:b"], ["review:b"]), baseline);
        CompletionCertificate bCertificate = CompletionCertificate.Create(bDecision, baseline.AddSeconds(10));
        CompletionClosurePlan bPlan = CompletionClosurePlan.Build(
            bDecision, bCertificate, nestedAgentsChanged: true, parentRepositoryChanged: true,
            baseline.AddSeconds(11));
        await store.PersistCertifiedCandidateAsync(bDecision, bCertificate, bPlan);
        Dictionary<string, CompletionClosureOperationState> bStates = bPlan.Operations
            .ToDictionary(item => item.Identity, _ => CompletionClosureOperationState.Succeeded);
        CompletionSettlement bTerminal = authority.Settle(bPlan, bStates, ["postcondition:verified"], baseline.AddSeconds(12));
        CompletionClosureReceipt[] bReceipts = bPlan.Operations
            .Where(item => item.Kind != CompletionClosureOperationKind.CertifiedTerminalFact)
            .Select(item => new CompletionClosureReceipt(item.Identity, "receipt:" + item.Identity)).ToArray();
        await store.AppendSettlementAsync(bDecision, bCertificate, bPlan, bTerminal, bReceipts);

        // Run A's decision lands after every row run B ever wrote, but it is merely "Continue" - it
        // carries no certificate, so nothing downstream of it should exist either.
        RunIdentity runA = RunIdentity.New();
        CompletionDecision aDecision = authority.Decide(new(runA, AttemptIdentity.New(),
            false, false, false, true, null, ["evidence:a-continue"], [], []), baseline.AddSeconds(20));
        await store.AppendDecisionAsync(aDecision);

        bool hydrationPathInvoked = false;
        store.ReadSnapshotAsyncInvokedForTesting = () => hydrationPathInvoked = true;
        CompletionAuthorityProjectionSnapshot actual;
        try
        {
            actual = await new CompletionAuthorityProjection(store).ProjectAsync();
        }
        finally
        {
            store.ReadSnapshotAsyncInvokedForTesting = null;
        }

        // The actual regression this task exists to prevent, expressed directly rather than inferred
        // from a statement total: the new read-only path must never fall back to the unkeyed,
        // full-hydration read it replaced.
        Assert.False(hydrationPathInvoked,
            "ProjectAsync must not fall back to the unscoped, full-hydration ReadSnapshotAsync.");

        // Baseline, captured by executing the pre-Task-3.4 algorithm for real - not by reasoning
        // about what it ought to return.
        CompletionAuthorityProjectionSnapshot expected = await LegacyProjectAsync(store, CancellationToken.None);
        AssertSameProjection(expected, actual);

        // Discriminating assertions: the latest decision is run A's, and it carries nothing
        // downstream. A buggy "latest row per table" implementation would fail every one of these by
        // reporting run B's certificate, plan, settlement, or terminal fact instead.
        Assert.Equal(aDecision.Identity, actual.LatestDecision!.Identity);
        Assert.Null(actual.Certificate);
        Assert.Null(actual.ClosurePlan);
        Assert.Null(actual.LatestSettlement);
        Assert.Null(actual.TerminalFact);
        // Trailing watermark token falls back to the latest decision's own identity - not "empty" -
        // because a decision does exist here; only settlement, plan, and decision all being absent
        // (the truly empty workspace) yields "empty" (see the missing-database test below).
        Assert.Equal($"2:1:1:1:1:{aDecision.Identity.Value}", actual.Watermark);
    }

    /// <summary>
    /// Numeric bound, deliberately separate from the characterization test above (whose hydration-path
    /// hook already proves the correctness-critical invariant): reading heads and counts across the
    /// whole store must compile a fixed number of statements - 2 for the read-only schema memo's live
    /// stamp re-check, one <c>LIMIT 1</c> per chain link actually reached, and one <c>COUNT(*)</c> per
    /// table - regardless of how many unrelated rows already exist in every one of those tables.
    /// </summary>
    [Fact]
    public async Task ReadHeadsAndCountsAsync_compiles_a_fixed_statement_count_regardless_of_history_size()
    {
        Repository repository = NewRepository();
        var store = new CanonicalCompletionAuthorityStore(repository);
        var authority = new CompletionAuthority();
        DateTimeOffset baseline = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

        // Unrelated history the head-and-count read must not scan row-by-row: three full chains
        // (decision, certificate, plan, settlement, terminal fact) besides the one actually measured.
        for (int lane = 0; lane < 3; lane++)
        {
            await SeedFullChainAsync(store, authority, RunIdentity.New(), baseline.AddMinutes(lane));
        }

        await SeedFullChainAsync(store, authority, RunIdentity.New(), baseline.AddMinutes(10));

        var counter = new PreparedStatementCounter();
        store.ConnectionObserverForTesting = counter.Watch;
        CompletionAuthorityHeadsAndCounts heads;
        try
        {
            heads = await store.ReadHeadsAndCountsAsync();
        }
        finally
        {
            store.ConnectionObserverForTesting = null;
        }

        Assert.NotNull(heads.LatestDecision);
        Assert.NotNull(heads.Certificate);
        Assert.NotNull(heads.ClosurePlan);
        Assert.NotNull(heads.Settlement);
        Assert.NotNull(heads.TerminalFact);
        Assert.Equal(4, heads.DecisionCount);
        Assert.Equal(4, heads.CertificateCount);
        Assert.Equal(4, heads.ClosurePlanCount);
        Assert.Equal(4, heads.SettlementCount);
        Assert.Equal(4, heads.TerminalFactCount);
        // 2 (memo stamp re-check) + 5 (one LIMIT 1 per chain link: decision, certificate, plan,
        // settlement, terminal) + 5 (one COUNT(*) per table) - fixed regardless of how many
        // unrelated rows exist, and nowhere near a per-row hydration cost.
        Assert.Equal(12, counter.Statements);
    }

    /// <summary>
    /// The brief requires the connection this method reads on to be genuinely read-only, not merely
    /// opened through a method named "ReadOnly". This proves it by attempting a write - a
    /// <c>CREATE TABLE</c>, chosen because it cannot fail for any reason other than the connection's
    /// open mode (no column constraints, no target-table shape to get wrong) - on the exact connection
    /// handle <see cref="CanonicalCompletionAuthorityStore.ReadHeadsAndCountsAsync"/> used, captured via
    /// the same test-only observer seam the statement counter uses.
    /// </summary>
    [Fact]
    public async Task ReadHeadsAndCountsAsync_opens_a_connection_that_rejects_a_write()
    {
        Repository repository = NewRepository();
        var store = new CanonicalCompletionAuthorityStore(repository);
        await store.AppendDecisionAsync(new CompletionAuthority().Decide(new(
            RunIdentity.New(), AttemptIdentity.New(), false, false, false, true, null,
            ["evidence:continue"], [], []), DateTimeOffset.UtcNow));

        SqliteException? rejected = null;
        store.ConnectionObserverForTesting = connection =>
        {
            try
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE zzz_read_only_probe(id INTEGER);";
                command.ExecuteNonQuery();
            }
            catch (SqliteException exception)
            {
                rejected = exception;
            }
        };
        try
        {
            await store.ReadHeadsAndCountsAsync();
        }
        finally
        {
            store.ConnectionObserverForTesting = null;
        }

        Assert.NotNull(rejected);
        Assert.Contains("readonly", rejected!.Message, StringComparison.OrdinalIgnoreCase);
    }
#endif

    /// <summary>
    /// Fail-closed guard: a database this process already admitted (and memoized) is then tampered
    /// with directly - simulating out-of-band corruption or a downgrade - after admission. The read-only
    /// memo's live stamp re-check must still detect the mismatch and fall through to full
    /// classification, so the head-and-count read still rejects the store with the same typed exception
    /// the write path always threw - never a silent pass and never a different, untyped failure.
    /// </summary>
    [Fact]
    public async Task ReadHeadsAndCountsAsync_rejects_a_tampered_schema_version_stamp_even_after_admission()
    {
        Repository repository = NewRepository();
        var store = new CanonicalCompletionAuthorityStore(repository);
        await store.AppendDecisionAsync(new CompletionAuthority().Decide(new(
            RunIdentity.New(), AttemptIdentity.New(), false, false, false, true, null,
            ["evidence:before-tamper"], [], []), DateTimeOffset.UtcNow));

        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_metadata SET value = '999' WHERE key = 'schema_version';";
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        WorkspaceCompatibilityImportRequiredException exception =
            await Assert.ThrowsAsync<WorkspaceCompatibilityImportRequiredException>(
                () => store.ReadHeadsAndCountsAsync());
        Assert.Equal(999, exception.Inspection.Version);
    }

    /// <summary>
    /// A workspace database that has never been created cannot be opened read-only (a read-only open
    /// cannot create one), unlike the write path this task replaced, which silently created and
    /// migrated one as a side effect of a read. The head-and-count read must instead answer with an
    /// empty projection and must not create the file - a status/read surface silently initializing
    /// workspace state as a side effect would itself be a latent defect this task must not introduce.
    /// </summary>
    [Fact]
    public async Task ProjectAsync_reports_empty_and_creates_no_database_when_the_workspace_has_never_been_written()
    {
        Repository repository = NewRepository();
        var store = new CanonicalCompletionAuthorityStore(repository);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Assert.False(File.Exists(databasePath));

        CompletionAuthorityProjectionSnapshot snapshot =
            await new CompletionAuthorityProjection(store).ProjectAsync();

        Assert.Null(snapshot.LatestDecision);
        Assert.Null(snapshot.Certificate);
        Assert.Null(snapshot.ClosurePlan);
        Assert.Null(snapshot.LatestSettlement);
        Assert.Null(snapshot.TerminalFact);
        Assert.Empty(snapshot.PendingOperations);
        Assert.Equal("0:0:0:0:0:empty", snapshot.Watermark);
        Assert.False(File.Exists(databasePath));
    }

    /// <summary>
    /// Seeds one complete chain end to end: a certified decision, its certificate, its closure plan,
    /// and a settlement that reaches <see cref="CompletionSettlementKind.CertifiedTerminal"/> (which
    /// also writes the terminal fact) - the full shape <see cref="CompletionAuthorityHeadsAndCounts"/>
    /// can report every field of.
    /// </summary>
    private static async Task SeedFullChainAsync(
        CanonicalCompletionAuthorityStore store,
        CompletionAuthority authority,
        RunIdentity run,
        DateTimeOffset at)
    {
        CompletionDecision decision = authority.Decide(new(run, AttemptIdentity.New(),
            false, false, false, false, null,
            ["evidence:completion"], ["gate:milestones"], ["review:non-implementation"]), at);
        CompletionCertificate certificate = CompletionCertificate.Create(decision, at.AddSeconds(1));
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            decision, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true, at.AddSeconds(2));
        await store.PersistCertifiedCandidateAsync(decision, certificate, plan);
        Dictionary<string, CompletionClosureOperationState> states = plan.Operations
            .ToDictionary(item => item.Identity, _ => CompletionClosureOperationState.Succeeded);
        CompletionSettlement terminal = authority.Settle(plan, states, ["postcondition:verified"], at.AddSeconds(3));
        CompletionClosureReceipt[] receipts = plan.Operations
            .Where(item => item.Kind != CompletionClosureOperationKind.CertifiedTerminalFact)
            .Select(item => new CompletionClosureReceipt(item.Identity, "receipt:" + item.Identity)).ToArray();
        await store.AppendSettlementAsync(decision, certificate, plan, terminal, receipts);
    }

    /// <summary>
    /// Byte-for-byte reproduction of the pre-Task-3.4 <c>CompletionAuthorityProjection.ProjectAsync</c>
    /// body (verified identical against the pre-change file): hydrate every historical row via
    /// <see cref="CanonicalCompletionAuthorityStore.ReadSnapshotAsync(CancellationToken)"/> - untouched
    /// by this task - then walk the same in-memory <c>LastOrDefault</c> chain in memory. Kept as the
    /// characterization baseline.
    /// </summary>
    private static async Task<CompletionAuthorityProjectionSnapshot> LegacyProjectAsync(
        CanonicalCompletionAuthorityStore store, CancellationToken cancellationToken)
    {
        CanonicalCompletionSnapshot snapshot = await store.ReadSnapshotAsync(cancellationToken);
        CompletionDecision? decision = snapshot.Decisions.LastOrDefault();
        CompletionCertificate? certificate = decision is null ? null : snapshot.Certificates
            .LastOrDefault(item => item.Decision == decision.Identity);
        CompletionClosurePlan? plan = certificate is null ? null : snapshot.ClosurePlans
            .LastOrDefault(item => item.Certificate == certificate.Identity);
        CompletionSettlement? settlement = plan is null ? null : snapshot.Settlements
            .LastOrDefault(item => item.Plan == plan.Identity);
        CertifiedTerminalFact? terminal = decision is null ? null : snapshot.TerminalFacts
            .LastOrDefault(item => item.RootRun == decision.RootRun);
        string watermark = string.Join(':',
            snapshot.Decisions.Count, snapshot.Certificates.Count, snapshot.ClosurePlans.Count,
            snapshot.Settlements.Count, snapshot.TerminalFacts.Count,
            settlement?.Identity.Value ?? plan?.Identity.Value ?? decision?.Identity.Value ?? "empty");
        return new(decision, certificate, plan, settlement, terminal,
            settlement?.PendingOperations ?? plan?.Operations.Select(item => item.Identity).ToArray() ?? [],
            watermark);
    }

    private static void AssertSameProjection(
        CompletionAuthorityProjectionSnapshot expected, CompletionAuthorityProjectionSnapshot actual)
    {
        Assert.Equal(expected.LatestDecision?.Identity, actual.LatestDecision?.Identity);
        Assert.Equal(expected.Certificate?.Identity, actual.Certificate?.Identity);
        Assert.Equal(expected.ClosurePlan?.Identity, actual.ClosurePlan?.Identity);
        Assert.Equal(expected.LatestSettlement?.Identity, actual.LatestSettlement?.Identity);
        Assert.Equal(expected.TerminalFact?.Identity, actual.TerminalFact?.Identity);
        Assert.Equal<string>(expected.PendingOperations, actual.PendingOperations);
        Assert.Equal(expected.Watermark, actual.Watermark);
    }

    private static Repository NewRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-completion-projection").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }
}
