using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class TransitionInputResolverTests
{
    [Fact]
    public async Task Create_completion_context_resolves_projection_as_single_artifact_input()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "CreateRoadmapCompletionContext");

        TransitionInputSnapshot snapshot = await ResolveAsync(repo, "CreateRoadmapCompletionContext", projectionPath);

        TransitionArtifactInput input = Assert.Single(snapshot.ArtifactInputs);
        Assert.Equal(projectionPath, input.Path);
        Assert.Equal(TransitionInputRole.Projection, input.Roles);
        Assert.Equal(RoadmapHash.Sha256(repo.Read(projectionPath)), input.Hash);
        Assert.Equal(input.Hash, snapshot.Projection.ProjectionHash);
        Assert.Equal(input.Hash, snapshot.ToInputArtifactHashes()[projectionPath]);
    }

    [Fact]
    public async Task Selection_resolves_ordered_roadmap_file_set()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "SelectNextEpic");
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "root roadmap");
        repo.Write(".agents/roadmap/b.md", "b");
        repo.Write(".agents/roadmap/a.md", "a");

        TransitionInputSnapshot snapshot = await ResolveAsync(repo, "SelectNextEpic", projectionPath);

        string[] roadmapInputs = snapshot.ArtifactInputs
            .Where(input => input.Roles == TransitionInputRole.RoadmapSource)
            .Select(input => input.Path)
            .ToArray();
        Assert.Equal(
            [RoadmapArtifactPaths.RoadmapFile, ".agents/roadmap/a.md", ".agents/roadmap/b.md"],
            roadmapInputs);
    }

    [Fact]
    public async Task Selection_records_missing_optional_roadmap_file_when_directory_sources_exist()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "SelectNextEpic");
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(".agents/roadmap/a.md", "a");

        TransitionInputSnapshot snapshot = await ResolveAsync(repo, "SelectNextEpic", projectionPath);

        TransitionArtifactInput roadmapFile = snapshot.ArtifactInputs.Single(input => input.Path == RoadmapArtifactPaths.RoadmapFile);
        Assert.False(roadmapFile.Required);
        Assert.Equal(TransitionInputPresence.MissingOptional, roadmapFile.Presence);
        Assert.Null(roadmapFile.Hash);
        Assert.False(snapshot.ToInputArtifactHashes().ContainsKey(RoadmapArtifactPaths.RoadmapFile));
    }

    [Fact]
    public async Task Resolver_deduplicates_paths_and_preserves_roles()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "RealignEpic");
        repo.Write(RoadmapArtifactPaths.Selection, "selection and audit");

        TransitionInputSnapshot snapshot = await ResolveAsync(
            repo,
            "RealignEpic",
            projectionPath,
            context: TransitionInputContext.AuditEvidence(RoadmapArtifactPaths.Selection));

        TransitionArtifactInput selection = Assert.Single(snapshot.ArtifactInputs, input => input.Path == RoadmapArtifactPaths.Selection);
        Assert.True(selection.Required);
        Assert.Equal("AuditEvidence+Selection", selection.Roles);
    }

    [Fact]
    public async Task Snapshot_hash_is_stable_and_changes_when_artifact_or_rendered_context_changes()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "CreateNewEpic");
        repo.Write(RoadmapArtifactPaths.Selection, "selection v1");

        TransitionInputSnapshot first = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v1");
        TransitionInputSnapshot second = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v1");

        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
        Assert.Equal(first.ToInputArtifactHashes(), second.ToInputArtifactHashes());

        repo.Write(RoadmapArtifactPaths.Selection, "selection v2");
        TransitionInputSnapshot changedArtifact = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v1");
        TransitionInputSnapshot changedContext = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v2");

        Assert.NotEqual(first.SnapshotHash, changedArtifact.SnapshotHash);
        Assert.NotEqual(changedArtifact.SnapshotHash, changedContext.SnapshotHash);
        Assert.Equal(changedArtifact.ToInputArtifactHashes(), changedContext.ToInputArtifactHashes());
        Assert.NotEqual(changedArtifact.PromptContextHash, changedContext.PromptContextHash);
    }

    [Fact]
    public async Task Update_completion_context_resolves_latest_evaluation_as_causal_input()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "UpdateRoadmapCompletionContext");
        string evaluationPath = ".agents/evidence/evaluations/epic-completion-and-drift.0001.md";
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(evaluationPath, "evaluation");

        TransitionInputSnapshot snapshot = await ResolveAsync(
            repo,
            "UpdateRoadmapCompletionContext",
            projectionPath,
            context: TransitionInputContext.CompletionEvaluation(evaluationPath));

        Assert.Contains(snapshot.ArtifactInputs, input => input.Path == evaluationPath && input.Roles == TransitionInputRole.CompletionEvaluation);
        Assert.Contains(evaluationPath, snapshot.ToInputArtifactHashes().Keys);
    }

    private static async Task<TransitionInputSnapshot> ResolveAsync(
        TempRepo repo,
        string runtimePromptName,
        string projectionPath,
        string renderedContext = "rendered context",
        string secondaryInput = "",
        TransitionInputContext? context = null)
    {
        return await new TransitionInputResolver(repo.Artifacts).ResolveAsync(new TransitionInputRequest(
            runtimePromptName,
            projectionPath,
            renderedContext,
            secondaryInput,
            context ?? TransitionInputContext.Empty));
    }

    private static string SeedProjection(TempRepo repo, string runtimePromptName)
    {
        string path = RoadmapArtifactPaths.ProjectionPaths[runtimePromptName];
        repo.Write(path, ProjectionSamples.Valid(runtimePromptName));
        return path;
    }
}
