using LoopRelay.Application.ReadModel;
using Xunit;

namespace LoopRelay.Application.Tests;

public sealed class CanonicalWorkspaceSnapshotTests
{
    [Fact]
    public async Task One_snapshot_exposes_partial_operational_state_with_traceable_claims_and_stable_rendering()
    {
        string fixtureRoot = Directory.CreateTempSubdirectory("looprelay-read-model-fixture").FullName;
        string fixtureDatabase = Path.Combine(fixtureRoot, ".LoopRelay", "persistence", "looprelay.sqlite3");
        Directory.CreateDirectory(Path.GetDirectoryName(fixtureDatabase)!);
        byte[] durableFixtureBytes = "canonical-fixture-bytes-that-must-not-be-repaired"u8.ToArray();
        await File.WriteAllBytesAsync(fixtureDatabase, durableFixtureBytes);
        string[] values =
        [
            "migration-required;import-conflict;canonical-only=false",
            "chain=traditional;transition=Completion;terminal=nonterminal",
            "product=ExecutablePlan;freshness=fresh;gate=accepted;receipt=read-1",
            "policy=pol-1;runtime=runtime-1;provider=codex-profile-1;prerequisite=known",
            "prompt=rendered-1;dispatch=dispatch-1;session=session-1;turn=turn-1",
            "pending-push=effect-7;unknown=effect-8;receipt=effectreceipt-6",
            "case=recovery-1;plan=recoveryplan-1;next=reconcile",
            "request=interaction-1;category=CompletionAmbiguity;state=Open",
            "decision=completion-1;plan=closure-1;pending=parent-push;terminal=absent",
            "obligation=completion-terminal;credit=uncredited",
            "linux=Unknown(missing-genuine-evidence);windows=evidence-win-1",
        ];
        ICanonicalOwnerProjection[] projections = CanonicalProjectionOwners.Required
            .Select((owner, index) => new FakeProjection(owner, values[index])).ToArray();
        var firstComposer = new CanonicalWorkspaceSnapshotComposer(projections);

        CanonicalWorkspaceSnapshot first = await firstComposer.ComposeAsync("workspace-1", "schema-v15", "catalog-13");
        ICanonicalOwnerProjection[] restartedProjections = CanonicalProjectionOwners.Required
            .Select((owner, index) => new FakeProjection(owner, values[index])).ToArray();
        CanonicalWorkspaceSnapshot second = await new CanonicalWorkspaceSnapshotComposer(restartedProjections)
            .ComposeAsync("workspace-1", "schema-v15", "catalog-13");

        Assert.Equal(first.SnapshotIdentity, second.SnapshotIdentity);
        Assert.Equal(11, first.OwnerProjections.Count);
        Assert.All(first.OwnerProjections.SelectMany(section => section.Claims), claim =>
        {
            Assert.NotEmpty(claim.SourceIdentities);
            Assert.False(string.IsNullOrWhiteSpace(claim.SourceWatermark));
        });
        Assert.Contains("pending-push=effect-7", first.Section(CanonicalProjectionOwners.Effects).Claims[0].Value);
        Assert.Contains("terminal=absent", first.Section(CanonicalProjectionOwners.Completion).Claims[0].Value);
        string text = CanonicalWorkspaceSnapshotRenderer.RenderText(first);
        string json = CanonicalWorkspaceSnapshotRenderer.RenderJson(first);
        Assert.Equal(text, CanonicalWorkspaceSnapshotRenderer.RenderText(first));
        Assert.Equal(json, CanonicalWorkspaceSnapshotRenderer.RenderJson(first));
        Assert.Equal(text, CanonicalWorkspaceSnapshotRenderer.RenderText(second));
        Assert.Equal(json, CanonicalWorkspaceSnapshotRenderer.RenderJson(second));
        Assert.Equal(durableFixtureBytes, await File.ReadAllBytesAsync(fixtureDatabase));
        Assert.All(projections.Cast<FakeProjection>(), projection => Assert.Equal(1, projection.Reads));
        Assert.All(restartedProjections.Cast<FakeProjection>(), projection => Assert.Equal(1, projection.Reads));
    }

    [Fact]
    public async Task Composer_surfaces_owner_conflict_and_marks_unstable_external_observation_stale()
    {
        var projections = CanonicalProjectionOwners.Required
            .Select(owner => (ICanonicalOwnerProjection)new FakeProjection(owner, owner)).ToList();
        projections[0] = new FakeProjection(CanonicalProjectionOwners.Storage, "storage-value", key: "external",
            externalUnstable: true);
        projections[1] = new FakeProjection(CanonicalProjectionOwners.Workflow, "workflow-value", key: "shared");
        projections[2] = new FakeProjection(CanonicalProjectionOwners.Products, "product-value", key: "shared");

        CanonicalWorkspaceSnapshot snapshot = await new CanonicalWorkspaceSnapshotComposer(
            projections, _externalRetryLimit: 1).ComposeAsync("ws", "schema", "catalog");

        Assert.Single(snapshot.Conflicts);
        Assert.Equal(ClaimKnowledge.Stale,
            snapshot.Section(CanonicalProjectionOwners.Storage).Claims.Single().Knowledge);
        Assert.Equal(2, ((FakeProjection)projections[0]).Reads);
    }

    [Fact]
    public async Task Missing_or_duplicate_owner_registration_fails_before_projection_reads()
    {
        FakeProjection projection = new(CanonicalProjectionOwners.Storage, "value");
        var composer = new CanonicalWorkspaceSnapshotComposer([projection, projection]);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            composer.ComposeAsync("ws", "schema", "catalog"));

        Assert.Contains("missing=", error.Message, StringComparison.Ordinal);
        Assert.Contains("duplicates=[StorageAuthority]", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, projection.Reads);
    }

    private sealed class FakeProjection(
        string owner,
        string value,
        string? key = null,
        bool externalUnstable = false) : ICanonicalOwnerProjection
    {
        public string Owner => owner;
        public int Reads { get; private set; }

        public Task<OwnerProjectionResult> ProjectAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            string watermark = $"{owner}:watermark";
            string after = externalUnstable ? $"{watermark}:{Reads}" : watermark;
            var claim = new CanonicalClaim(key ?? owner, value,
                ClaimKnowledge.Known, owner, [$"{owner}:fact-1"], watermark, "v1");
            return Task.FromResult(new OwnerProjectionResult(
                new OwnerProjectionSection(owner, watermark, [claim]), watermark, after,
                externalUnstable, externalUnstable ? [$"{owner}:external-{Reads}"] : []));
        }
    }
}
