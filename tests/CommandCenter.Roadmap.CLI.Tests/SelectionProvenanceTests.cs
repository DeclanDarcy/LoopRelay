using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class SelectionProvenanceTests
{
    [Fact]
    public async Task Matching_selection_cycle_provenance_is_fresh()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.True(freshness.IsFresh);
    }

    [Fact]
    public async Task Completion_context_change_invalidates_selection_cycle()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "# Changed Completion Context");

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.RoadmapCompletionContextDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Roadmap_change_invalidates_selection_cycle()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "changed roadmap");

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.RoadmapSourceDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Retired_initiative_change_invalidates_selection_cycle()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo, [RetiredEpic()]);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.RetiredEpicStateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Superseded_selection_is_not_fresh_even_when_file_remains()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        SelectionProvenanceService provenance = SelectionProvenanceTestSupport.CreateProvenance(repo);

        await provenance.SupersedeActiveSelectionAsync([DerivedArtifactStaleReason.RoadmapCompletionContextDrift]);
        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.Superseded, freshness.Reasons);
        Assert.Equal(Selection(), repo.Read(RoadmapArtifactPaths.Selection));
    }

    [Fact]
    public async Task Missing_selection_provenance_is_unknown_and_not_fresh()
    {
        using var repo = SeedRepo();
        repo.Write(RoadmapArtifactPaths.Selection, Selection());

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.MissingManifest, freshness.Reasons);
    }

    [Fact]
    public async Task Corrupted_selection_provenance_is_unknown_and_not_fresh()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(RoadmapArtifactPaths.SelectionProvenanceManifest, "{not-json");

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.MissingManifest, freshness.Reasons);
    }

    [Fact]
    public async Task Selection_file_change_invalidates_selection_provenance()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(RoadmapArtifactPaths.Selection, Selection("Changed Initiative"));

        DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ArtifactHashDrift, freshness.Reasons);
    }

    private static async Task<DerivedArtifactFreshness> EvaluateAsync(
        TempRepo repo,
        IReadOnlyList<RetiredEpic>? retiredEpics = null)
    {
        IReadOnlyList<RetiredEpic> effectiveRetiredEpics = retiredEpics ?? [];
        SelectionProvenanceService provenance = SelectionProvenanceTestSupport.CreateProvenance(repo);
        TransitionInputSnapshot cycle = await provenance.CaptureCurrentCycleAsync(
            repo.Read(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]),
            effectiveRetiredEpics);
        return await provenance.EvaluateActiveSelectionFreshnessAsync(cycle, effectiveRetiredEpics);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        return repo;
    }

    private static RetiredEpic RetiredEpic() =>
        new("EPIC-001", "Retired Epic", "Already complete.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow);

    private static string Selection(string initiative = "Investigate A") => $$"""
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Strategic Investigation Required |
        | Recommended Initiative | {{initiative}} |
        | Initiative Type | Strategic Investigation |
        | Confidence | Medium |
        | Primary Reason | Evidence is insufficient |
        """;
}
