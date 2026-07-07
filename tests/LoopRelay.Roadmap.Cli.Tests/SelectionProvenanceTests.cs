using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class SelectionProvenanceTests
{
    [Fact]
    public async Task Matching_selection_cycle_provenance_is_fresh()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.True(freshness.IsFresh);
    }

    [Fact]
    public async Task Completion_context_change_invalidates_selection_cycle()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Changed Completion Context");

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.RoadmapCompletionContextDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Roadmap_change_invalidates_selection_cycle()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(".agents/roadmap/001-roadmap.md", "changed roadmap");

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.RoadmapSourceDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Retired_initiative_change_invalidates_selection_cycle()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo, [RetiredEpic()]);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.RetiredEpicStateDrift, freshness.Reasons);
    }

    [Fact]
    public async Task Superseded_selection_is_not_fresh_even_when_file_remains()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        Cli.SelectionProvenanceService provenance = SelectionProvenanceTestSupport.CreateProvenance(repo);

        await provenance.SupersedeActiveSelectionAsync([Cli.DerivedArtifactStaleReason.RoadmapCompletionContextDrift]);
        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.Superseded, freshness.Reasons);
        Assert.Equal(Selection(), repo.Read(Cli.RoadmapArtifactPaths.Selection));
    }

    [Fact]
    public async Task Missing_selection_provenance_is_unknown_and_not_fresh()
    {
        using var repo = SeedRepo();
        repo.Write(Cli.RoadmapArtifactPaths.Selection, Selection());

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.MissingManifest, freshness.Reasons);
    }

    [Fact]
    public async Task Corrupted_selection_provenance_is_unknown_and_not_fresh()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(Cli.RoadmapArtifactPaths.SelectionProvenanceManifest, "{not-json");

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.MissingManifest, freshness.Reasons);
    }

    [Fact]
    public async Task Selection_file_change_invalidates_selection_provenance()
    {
        using var repo = SeedRepo();
        await SelectionProvenanceTestSupport.SeedCurrentSelectionAsync(repo, Selection());
        repo.Write(Cli.RoadmapArtifactPaths.Selection, Selection("Changed Initiative"));

        Cli.DerivedArtifactFreshness freshness = await EvaluateAsync(repo);

        Assert.False(freshness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.ArtifactHashDrift, freshness.Reasons);
    }

    private static async Task<Cli.DerivedArtifactFreshness> EvaluateAsync(
        TempRepo repo,
        IReadOnlyList<Cli.RetiredEpic>? retiredEpics = null)
    {
        IReadOnlyList<Cli.RetiredEpic> effectiveRetiredEpics = retiredEpics ?? [];
        Cli.SelectionProvenanceService provenance = SelectionProvenanceTestSupport.CreateProvenance(repo);
        Cli.TransitionInputSnapshot cycle = await provenance.CaptureCurrentCycleAsync(
            repo.Read(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]),
            effectiveRetiredEpics);
        return await provenance.EvaluateActiveSelectionFreshnessAsync(cycle, effectiveRetiredEpics);
    }

    private static TempRepo SeedRepo()
    {
        var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        return repo;
    }

    private static Cli.RetiredEpic RetiredEpic() =>
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
