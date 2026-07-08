using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

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
    public async Task Create_completion_context_resolves_archived_epics_as_completed_epic_inputs()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "CreateRoadmapCompletionContext");
        repo.Write(".agents/archive/epics/002-beta.md", "beta");
        repo.Write(".agents/archive/epics/001-alpha.md", "alpha");

        TransitionInputSnapshot snapshot = await ResolveAsync(repo, "CreateRoadmapCompletionContext", projectionPath);

        Assert.Equal(
            [".agents/archive/epics/001-alpha.md", ".agents/archive/epics/002-beta.md", projectionPath],
            snapshot.ArtifactInputs.Select(input => input.Path).ToArray());
        Assert.Contains(snapshot.ArtifactInputs, input =>
            input.Path == ".agents/archive/epics/001-alpha.md" &&
            input.Roles == TransitionInputRole.CompletedEpic &&
            input.Hash == RoadmapHash.Sha256("alpha"));
        Assert.Contains(snapshot.ArtifactInputs, input =>
            input.Path == ".agents/archive/epics/002-beta.md" &&
            input.Roles == TransitionInputRole.CompletedEpic &&
            input.Hash == RoadmapHash.Sha256("beta"));
        Assert.Equal(RoadmapHash.Sha256("alpha"), snapshot.ToInputArtifactHashes()[".agents/archive/epics/001-alpha.md"]);
        Assert.Equal(RoadmapHash.Sha256("beta"), snapshot.ToInputArtifactHashes()[".agents/archive/epics/002-beta.md"]);
    }

    [Fact]
    public async Task Selection_resolves_ordered_roadmap_directory_file_set()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "SelectNextEpic");
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap 001");
        repo.Write(".agents/roadmap/b.md", "b");
        repo.Write(".agents/roadmap/a.md", "a");

        TransitionInputSnapshot snapshot = await ResolveAsync(repo, "SelectNextEpic", projectionPath);

        string[] roadmapInputs = snapshot.ArtifactInputs
            .Where(input => input.Roles == TransitionInputRole.RoadmapSource)
            .Select(input => input.Path)
            .ToArray();
        Assert.Equal(
            [".agents/roadmap/001-roadmap.md", ".agents/roadmap/a.md", ".agents/roadmap/b.md"],
            roadmapInputs);
    }

    [Fact]
    public async Task Selection_requires_roadmap_directory_sources()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "SelectNextEpic");
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "completion");

        RoadmapStepException exception = await Assert.ThrowsAsync<RoadmapStepException>(
            () => ResolveAsync(repo, "SelectNextEpic", projectionPath));

        Assert.Contains(RoadmapArtifactPaths.RoadmapDirectoryPattern, exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task Completion_evaluation_resolves_manifest_active_milestone_specs_only()
    {
        using var repo = new TempRepo();
        string projectionPath = SeedProjection(repo, "EvaluateEpicCompletionAndDrift");
        string evidencePath = ".agents/evidence/execution/execution.0001.md";
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(evidencePath, "execution evidence");
        repo.Write(".agents/specs/active.md", "Epic Path: .agents/epic.md");
        repo.Write(".agents/specs/stale.md", "Epic Path: .agents/epic.md");
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/active.md");

        TransitionInputSnapshot snapshot = await ResolveAsync(
            repo,
            "EvaluateEpicCompletionAndDrift",
            projectionPath,
            context: TransitionInputContext.ExecutionEvidence(evidencePath));

        Assert.Contains(snapshot.ArtifactInputs, input => input.Path == ".agents/specs/active.md" && input.Roles == TransitionInputRole.MilestoneSpec);
        Assert.DoesNotContain(snapshot.ArtifactInputs, input => input.Path == ".agents/specs/stale.md");
    }

    private static async Task<TransitionInputSnapshot> ResolveAsync(
        TempRepo repo,
        string runtimePromptName,
        string projectionPath,
        string renderedContext = "rendered context",
        string secondaryInput = "",
        TransitionInputContext? context = null)
    {
        return await new TransitionInputResolver(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo)).ResolveAsync(new TransitionInputRequest(
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
