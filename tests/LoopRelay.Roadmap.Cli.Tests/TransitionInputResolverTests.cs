using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class TransitionInputResolverTests
{
    [Fact]
    public async Task Create_completion_context_resolves_projection_as_single_artifact_input()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "CreateRoadmapCompletionContext");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(repo, "CreateRoadmapCompletionContext", projectionPath);

        Cli.TransitionArtifactInput input = Assert.Single(snapshot.ArtifactInputs);
        Assert.Equal(projectionPath, input.Path);
        Assert.Equal(Cli.TransitionInputRole.Projection, input.Roles);
        Assert.Equal(Cli.RoadmapHash.Sha256(repo.Read(projectionPath)), input.Hash);
        Assert.Equal(input.Hash, snapshot.Projection.ProjectionHash);
        Assert.Equal(input.Hash, snapshot.ToInputArtifactHashes()[projectionPath]);
    }

    [Fact]
    public async Task Create_completion_context_resolves_archived_epics_as_completed_epic_inputs()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "CreateRoadmapCompletionContext");
        repo.Write(".agents/archive/epics/002-beta.md", "beta");
        repo.Write(".agents/archive/epics/001-alpha.md", "alpha");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(repo, "CreateRoadmapCompletionContext", projectionPath);

        Assert.Equal(
            [".agents/archive/epics/001-alpha.md", ".agents/archive/epics/002-beta.md", projectionPath],
            snapshot.ArtifactInputs.Select(input => input.Path).ToArray());
        Assert.Contains(snapshot.ArtifactInputs, input =>
            input.Path == ".agents/archive/epics/001-alpha.md" &&
            input.Roles == Cli.TransitionInputRole.CompletedEpic &&
            input.Hash == Cli.RoadmapHash.Sha256("alpha"));
        Assert.Contains(snapshot.ArtifactInputs, input =>
            input.Path == ".agents/archive/epics/002-beta.md" &&
            input.Roles == Cli.TransitionInputRole.CompletedEpic &&
            input.Hash == Cli.RoadmapHash.Sha256("beta"));
        Assert.Equal(Cli.RoadmapHash.Sha256("alpha"), snapshot.ToInputArtifactHashes()[".agents/archive/epics/001-alpha.md"]);
        Assert.Equal(Cli.RoadmapHash.Sha256("beta"), snapshot.ToInputArtifactHashes()[".agents/archive/epics/002-beta.md"]);
    }

    [Fact]
    public async Task Selection_resolves_ordered_roadmap_file_set()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "SelectNextEpic");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "root roadmap");
        repo.Write(".agents/roadmap/b.md", "b");
        repo.Write(".agents/roadmap/a.md", "a");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(repo, "SelectNextEpic", projectionPath);

        string[] roadmapInputs = snapshot.ArtifactInputs
            .Where(input => input.Roles == Cli.TransitionInputRole.RoadmapSource)
            .Select(input => input.Path)
            .ToArray();
        Assert.Equal(
            [Cli.RoadmapArtifactPaths.RoadmapFile, ".agents/roadmap/a.md", ".agents/roadmap/b.md"],
            roadmapInputs);
    }

    [Fact]
    public async Task Selection_records_missing_optional_roadmap_file_when_directory_sources_exist()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "SelectNextEpic");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(".agents/roadmap/a.md", "a");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(repo, "SelectNextEpic", projectionPath);

        Cli.TransitionArtifactInput roadmapFile = snapshot.ArtifactInputs.Single(input => input.Path == Cli.RoadmapArtifactPaths.RoadmapFile);
        Assert.False(roadmapFile.Required);
        Assert.Equal(Cli.TransitionInputPresence.MissingOptional, roadmapFile.Presence);
        Assert.Null(roadmapFile.Hash);
        Assert.False(snapshot.ToInputArtifactHashes().ContainsKey(Cli.RoadmapArtifactPaths.RoadmapFile));
    }

    [Fact]
    public async Task Resolver_deduplicates_paths_and_preserves_roles()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "RealignEpic");
        repo.Write(Cli.RoadmapArtifactPaths.Selection, "selection and audit");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(
            repo,
            "RealignEpic",
            projectionPath,
            context: Cli.TransitionInputContext.AuditEvidence(Cli.RoadmapArtifactPaths.Selection));

        Cli.TransitionArtifactInput selection = Assert.Single(snapshot.ArtifactInputs, input => input.Path == Cli.RoadmapArtifactPaths.Selection);
        Assert.True(selection.Required);
        Assert.Equal("AuditEvidence+Selection", selection.Roles);
    }

    [Fact]
    public async Task Snapshot_hash_is_stable_and_changes_when_artifact_or_rendered_context_changes()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "CreateNewEpic");
        repo.Write(Cli.RoadmapArtifactPaths.Selection, "selection v1");

        Cli.TransitionInputSnapshot first = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v1");
        Cli.TransitionInputSnapshot second = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v1");

        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
        Assert.Equal(first.ToInputArtifactHashes(), second.ToInputArtifactHashes());

        repo.Write(Cli.RoadmapArtifactPaths.Selection, "selection v2");
        Cli.TransitionInputSnapshot changedArtifact = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v1");
        Cli.TransitionInputSnapshot changedContext = await ResolveAsync(repo, "CreateNewEpic", projectionPath, renderedContext: "rendered v2");

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
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(evaluationPath, "evaluation");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(
            repo,
            "UpdateRoadmapCompletionContext",
            projectionPath,
            context: Cli.TransitionInputContext.CompletionEvaluation(evaluationPath));

        Assert.Contains(snapshot.ArtifactInputs, input => input.Path == evaluationPath && input.Roles == Cli.TransitionInputRole.CompletionEvaluation);
        Assert.Contains(evaluationPath, snapshot.ToInputArtifactHashes().Keys);
    }

    [Fact]
    public async Task Completion_evaluation_resolves_manifest_active_milestone_specs_only()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "EvaluateEpicCompletionAndDrift");
        string evidencePath = ".agents/evidence/execution/execution.0001.md";
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(evidencePath, "execution evidence");
        repo.Write(".agents/specs/active.md", "Epic Path: .agents/epic.md");
        repo.Write(".agents/specs/stale.md", "Epic Path: .agents/epic.md");
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/active.md");

        Cli.TransitionInputSnapshot snapshot = await ResolveAsync(
            repo,
            "EvaluateEpicCompletionAndDrift",
            projectionPath,
            context: Cli.TransitionInputContext.ExecutionEvidence(evidencePath));

        Assert.Contains(snapshot.ArtifactInputs, input => input.Path == ".agents/specs/active.md" && input.Roles == Cli.TransitionInputRole.MilestoneSpec);
        Assert.DoesNotContain(snapshot.ArtifactInputs, input => input.Path == ".agents/specs/stale.md");
    }

    private static async Task<Cli.TransitionInputSnapshot> ResolveAsync(
        TempRepo repo,
        string runtimePromptName,
        string projectionPath,
        string renderedContext = "rendered context",
        string secondaryInput = "",
        Cli.TransitionInputContext? context = null)
    {
        return await new Cli.TransitionInputResolver(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo)).ResolveAsync(new Cli.TransitionInputRequest(
            runtimePromptName,
            projectionPath,
            renderedContext,
            secondaryInput,
            context ?? Cli.TransitionInputContext.Empty));
    }

    private static string SeedProjection(TempRepo repo, string runtimePromptName)
    {
        string path = Cli.RoadmapArtifactPaths.ProjectionPaths[runtimePromptName];
        repo.Write(path, ProjectionSamples.Valid(runtimePromptName));
        return path;
    }
}
