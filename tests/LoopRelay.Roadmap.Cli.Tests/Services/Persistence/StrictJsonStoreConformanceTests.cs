using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.Services.Decisions.DecisionLedgerStore;
using ProjectionManifestStore = LoopRelay.Roadmap.Cli.Services.Projections.ProjectionManifestStore;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class StrictJsonStoreConformanceTests
{
    [Fact]
    public async Task Decision_ledger_rejects_malformed_canonical_json_without_legacy_fallback()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.DecisionLedgerJson, "{");
        repo.Write(RoadmapArtifactPaths.DecisionLedger, """
                                                        # Decision Ledger

                                                        ## D0007

                                                        | Field | Value |
                                                        |---|---|
                                                        | Timestamp | 2026-01-01T00:00:00.0000000+00:00 |
                                                        | State | SelectNextStrategicInitiative |
                                                        | Transition | SelectNextEpic |
                                                        | Prompt | SelectNextEpic |
                                                        | Projection Path | projection |
                                                        | Input Artifact Paths | input |
                                                        | Output Artifact Paths | output |
                                                        | Decision / Disposition | Select Existing Epic |
                                                        | Confidence | High |
                                                        | Rationale Excerpt | reason |
                                                        """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new DecisionLedgerStore(repo.Artifacts).LastDecisionIdAsync());

        AssertStrictJsonFailure(ex, RoadmapArtifactPaths.DecisionLedgerJson);
        Assert.Equal("{", repo.Read(RoadmapArtifactPaths.DecisionLedgerJson));
    }

    [Fact]
    public async Task Roadmap_state_rejects_malformed_canonical_json_without_legacy_fallback()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.StateJson, "{");
        repo.Write(RoadmapArtifactPaths.State, """
                                               # Engineering Loop State

                                               ## Current State

                                               ActiveEpicReady
                                               """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new RoadmapStateStore(repo.Artifacts).LoadAsync());

        AssertStrictJsonFailure(ex, RoadmapArtifactPaths.StateJson);
        Assert.Equal("{", repo.Read(RoadmapArtifactPaths.StateJson));
    }

    [Fact]
    public async Task Artifact_lifecycle_rejects_malformed_canonical_json_without_legacy_fallback()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.LifecycleJson, "{");
        repo.Write(RoadmapArtifactPaths.Lifecycle, """
                                                   # Artifact Lifecycle

                                                   | Path | State | Updated At | Notes |
                                                   |---|---|---|---|
                                                   | .agents/epic.md | Ready | 2026-01-01T00:00:00.0000000+00:00 | legacy |
                                                   """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new ArtifactLifecycleStore(repo.Artifacts).LoadAsync());

        AssertStrictJsonFailure(ex, RoadmapArtifactPaths.LifecycleJson);
        Assert.Equal("{", repo.Read(RoadmapArtifactPaths.LifecycleJson));
    }

    [Fact]
    public async Task Projection_manifest_rejects_malformed_canonical_json_without_legacy_fallback()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ProjectionsManifestJson, "{");
        repo.Write(RoadmapArtifactPaths.ProjectionsManifest, """
                                                             # Projection Manifest

                                                             | Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |
                                                             |---|---|---|---|---|---|---|---|---|---|---|
                                                             | SelectNextEpic | ProjectionForSelectNextEpic | .agents/projections/select-next-epic.md | source-hash | .agents/ctx/01-purpose.md | context-hash | projection-hash | 2026-01-01T00:00:00.0000000+00:00 | Valid | Fresh | None |
                                                             """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new ProjectionManifestStore(repo.Artifacts).LoadAsync());

        AssertStrictJsonFailure(ex, RoadmapArtifactPaths.ProjectionsManifestJson);
        Assert.Equal("{", repo.Read(RoadmapArtifactPaths.ProjectionsManifestJson));
    }

    [Fact]
    public async Task Split_family_rejects_malformed_canonical_json_without_legacy_fallback()
    {
        using var repo = new TempRepo();
        string jsonPath = RoadmapArtifactPaths.SplitFamilyJson("legacy");
        repo.Write(jsonPath, "{");
        repo.Write(RoadmapArtifactPaths.SplitFamily("legacy"), """
                                                               # Split Family

                                                               | Field | Value |
                                                               |---|---|
                                                               | Family ID | legacy |
                                                               | Created At | 2026-01-01T00:00:00.0000000+00:00 |
                                                               | Selected Child | .agents/epic-2.md |
                                                               | Selected Child Rationale | valid |

                                                               ## Proposal

                                                               Split this epic.

                                                               ## Child Epics

                                                               - .agents/epic-1.md
                                                               - .agents/epic-2.md

                                                               ## Dependency Order

                                                               - .agents/epic-1.md
                                                               - .agents/epic-2.md
                                                               """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new SplitFamilyStore(repo.Artifacts).ExistsForChildAsync(".agents/epic-2.md"));

        AssertStrictJsonFailure(ex, jsonPath);
        Assert.Equal("{", repo.Read(jsonPath));
    }

    private static void AssertStrictJsonFailure(RoadmapStepException ex, string path)
    {
        Assert.Contains("Canonical structured persistence is invalid JSON", ex.Message, StringComparison.Ordinal);
        Assert.Contains(path, ex.Message, StringComparison.Ordinal);
    }
}
