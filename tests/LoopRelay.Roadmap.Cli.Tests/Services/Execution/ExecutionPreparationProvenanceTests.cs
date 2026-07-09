using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Persistence;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using ExecutionCompatibilityMaterializer = LoopRelay.Roadmap.Cli.Services.Execution.ExecutionCompatibilityMaterializer;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Execution;

public sealed class ExecutionPreparationProvenanceTests
{
    [Fact]
    public async Task Matching_provenance_allows_execution_preparation_reuse()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);

        Assert.True(readiness.IsFresh);
    }

    [Fact]
    public async Task Active_epic_change_invalidates_downstream_provenance()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW"));

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);

        Assert.False(readiness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ActiveEpicDrift, readiness.Artifacts.SelectMany(result => result.Freshness.Reasons));
    }

    [Fact]
    public async Task Milestone_spec_change_invalidates_execution_preparation()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(".agents/specs/a.md", Spec("Changed Spec", "- [ ] Changed work."));

        DerivedArtifactFreshness specs = await provenance.EvaluateMilestoneSpecsFreshnessAsync();

        Assert.False(specs.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ArtifactHashDrift, specs.Reasons);
    }

    [Fact]
    public async Task Operational_context_change_invalidates_execution_prompt_and_compatibility()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.OperationalContext, "# Tampered Operational Context");

        DerivedArtifactFreshness prompt = await provenance.EvaluateExecutionPromptFreshnessAsync();
        DerivedArtifactFreshness compatibility = await provenance.EvaluateCompatibilityFreshnessAsync();

        Assert.False(prompt.IsFresh);
        Assert.False(compatibility.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.OperationalContextDrift, compatibility.Reasons);
    }

    [Fact]
    public async Task Execution_prompt_change_invalidates_compatibility_artifacts()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "# Tampered Execution Prompt");

        DerivedArtifactFreshness compatibility = await provenance.EvaluateCompatibilityFreshnessAsync();

        Assert.False(compatibility.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ExecutionPromptDrift, compatibility.Reasons);
    }

    [Fact]
    public async Task Deleted_execution_prompt_is_not_fresh()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        await repo.Artifacts.DeleteAsync(RoadmapArtifactPaths.ExecutionPrompt);

        DerivedArtifactFreshness prompt = await provenance.EvaluateExecutionPromptFreshnessAsync();

        Assert.False(prompt.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ArtifactMissing, prompt.Reasons);
    }

    [Fact]
    public async Task Decision_ledger_change_supports_partial_regeneration()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.DecisionLedgerJson, "{}");

        DerivedArtifactFreshness operationalContext = await provenance.EvaluateOperationalContextFreshnessAsync();
        Assert.False(operationalContext.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.DecisionLedgerDrift, operationalContext.Reasons);

        await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        Assert.False((await provenance.EvaluateExecutionPromptFreshnessAsync()).IsFresh);

        await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);
        Assert.True(readiness.IsFresh);
    }

    [Fact]
    public async Task Decision_ledger_input_uses_canonical_hash()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(".agents/specs/a.md", Spec("Spec A", "- [ ] Do A."));
        repo.Write(RoadmapArtifactPaths.DecisionLedgerJson, "{\"raw\":\"file-backed export\"}");

        const string canonicalLedgerHash = "canonical-decision-ledger-hash";
        var provenance = new ExecutionPreparationProvenanceService(
            repo.Artifacts,
            new ExecutionPreparationManifestStore(repo.Artifacts),
            new DecisionLedgerHashOverride(
                RoadmapLogicalArtifactServices.CreateCanonicalHasher(repo.Artifacts),
                canonicalLedgerHash));

        await provenance.RecordMilestoneSpecsAsync([".agents/specs/a.md"]);
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(
            provenance,
            repo,
            "# Operational Context");

        ExecutionPreparationManifest manifest = await new ExecutionPreparationManifestStore(repo.Artifacts).LoadAsync();
        DerivedArtifactManifestEntry operationalContext = manifest.FindActive(
            ExecutionPreparationProvenanceService.OperationalContextArtifactKind,
            RoadmapArtifactPaths.OperationalContext)!;
        DerivedArtifactCausalInput ledgerInput = Assert.Single(
            operationalContext.CausalInputs,
            input => input.Kind == ExecutionPreparationProvenanceService.DecisionLedgerInputKind);

        Assert.Equal(RoadmapArtifactPaths.DecisionLedgerJson, ledgerInput.Identity);
        Assert.Equal(canonicalLedgerHash, ledgerInput.Version);
    }

    [Fact]
    public async Task SQLite_decision_ledger_drift_invalidates_operational_context_even_when_export_is_stale()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(".agents/specs/a.md", Spec("Spec A", "- [ ] Do A."));
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(
            RoadmapArtifactPaths.ActiveEpic,
            ArtifactLifecycleState.Ready);
        await new DecisionLedgerStore(repo.Artifacts).AppendAsync(Decision("D0001"));

        ExecutionPreparationProvenanceService fileBackedProvenance =
            await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        await new OperationalContextGenerator(
            repo.Artifacts,
            new ArtifactLifecycleStore(repo.Artifacts),
            fileBackedProvenance).GenerateAsync();
        string staleExport = await repo.Artifacts.ReadRequiredAsync(RoadmapArtifactPaths.DecisionLedgerJson);

        await new WorkspaceSqliteStore().ImportAsync(repo.Artifacts);
        var sqliteBackedProvenance = new ExecutionPreparationProvenanceService(
            repo.Artifacts,
            new ExecutionPreparationManifestStore(repo.Artifacts));
        Assert.True((await sqliteBackedProvenance.EvaluateOperationalContextFreshnessAsync()).IsFresh);

        await new SqliteDecisionLedgerStore(repo.Repository).AppendAsync(Decision("D0002"));
        Assert.Equal(staleExport, await repo.Artifacts.ReadRequiredAsync(RoadmapArtifactPaths.DecisionLedgerJson));
        CanonicalArtifactHash currentLedgerHash =
            await RoadmapLogicalArtifactServices.CreateCanonicalHasher(repo.Artifacts)
                .RequireHashAsync(RoadmapArtifactPaths.DecisionLedgerJson);

        DerivedArtifactFreshness operationalContext =
            await sqliteBackedProvenance.EvaluateOperationalContextFreshnessAsync();

        Assert.Equal(LogicalArtifactStorageKind.SqliteCanonicalRecord, currentLedgerHash.Descriptor.StorageKind);
        Assert.False(operationalContext.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.DecisionLedgerDrift, operationalContext.Reasons);

        string regenerated = await new OperationalContextGenerator(
            repo.Artifacts,
            new ArtifactLifecycleStore(repo.Artifacts),
            sqliteBackedProvenance).GenerateAsync();

        Assert.Contains("D0002", regenerated, StringComparison.Ordinal);
        Assert.True((await sqliteBackedProvenance.EvaluateOperationalContextFreshnessAsync()).IsFresh);
    }

    [Fact]
    public async Task Reduced_milestone_count_supersedes_old_compatibility_artifacts_without_reusing_them()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo, specCount: 2);
        Assert.True((bool)await repo.Artifacts.ExistsAsync(".agents/milestones/m002.md"));

        await provenance.RecordMilestoneSpecsAsync([".agents/specs/a.md"]);
        await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);
        ExecutionPreparationManifest manifest = await new ExecutionPreparationManifestStore(repo.Artifacts).LoadAsync();
        DerivedArtifactManifestEntry oldMilestone = Assert.Single(
            manifest.Artifacts,
            entry => entry.ArtifactIdentity == ".agents/milestones/m002.md");

        Assert.True(readiness.IsFresh);
        Assert.Equal(DerivedArtifactProvenanceStatus.Superseded, oldMilestone.ProvenanceStatus);
    }

    [Fact]
    public async Task Resume_does_not_advance_legacy_execution_preparation()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        ProjectContext context = await SeedProjectAsync(repo);
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW"));

        RoadmapResumePlan plan = await CreatePlanner(repo, provenance).PlanAsync(
            State(RoadmapState.ExecutionPromptReady, output: RoadmapArtifactPaths.ExecutionPrompt),
            context,
            CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Terminal, plan.Action);
        Assert.Equal(RoadmapOutcome.Paused, plan.TerminalOutcome);
        Assert.Contains("no longer advanced by Roadmap CLI", plan.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("operational-context")]
    [InlineData("execution-prompt")]
    public async Task Invariant_rejects_stale_execution_preparation_before_execution(string staleArtifact)
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);

        switch (staleArtifact)
        {
            case "spec":
                repo.Write(".agents/specs/a.md", Spec("Changed Spec", "- [ ] Changed work."));
                break;
            case "operational-context":
                repo.Write(RoadmapArtifactPaths.OperationalContext, "# Tampered Operational Context");
                break;
            case "execution-prompt":
                repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "# Tampered Execution Prompt");
                break;
        }

        InvariantValidationResult result = await CreateInvariantValidator(repo, provenance)
            .ValidateAsync(RoadmapState.ExecutionPromptReady, context.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ExecutionPreparationStale", result.FailureCategory);
        Assert.Contains("provenance is not fresh", result.Error, StringComparison.Ordinal);
    }

    private static async Task<ExecutionPreparationProvenanceService> SeedFullPreparationAsync(
        TempRepo repo,
        int specCount = 1)
    {
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);

        var specPaths = new List<string> { ".agents/specs/a.md" };
        repo.Write(".agents/specs/a.md", Spec("Spec A", "- [ ] Do A."));
        if (specCount > 1)
        {
            specPaths.Add(".agents/specs/b.md");
            repo.Write(".agents/specs/b.md", Spec("Spec B", "- [ ] Do B."));
        }

        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, specPaths.ToArray());
        await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();
        return provenance;
    }

    private static async Task<ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        return await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static RoadmapResumePlanner CreatePlanner(
        TempRepo repo,
        ExecutionPreparationProvenanceService provenance)
    {
        var projections = new ProjectionRegistry();
        var contextBuilder = new RoadmapPromptContextBuilder(repo.Artifacts, provenance);
        var inputResolver = new TransitionInputResolver(repo.Artifacts, provenance);
        var selectionProvenance = new SelectionProvenanceService(
            repo.Artifacts,
            new SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
        return new RoadmapResumePlanner(
            repo.Artifacts,
            new PromptContractRegistry(projections),
            new ProjectionManifestStore(repo.Artifacts),
            new ArtifactLifecycleStore(repo.Artifacts),
            new ProjectionProvenanceFactory(projections),
            selectionProvenance,
            provenance);
    }

    private static InvariantValidator CreateInvariantValidator(
        TempRepo repo,
        ExecutionPreparationProvenanceService provenance)
    {
        var projections = new ProjectionRegistry();
        return new InvariantValidator(
            repo.Artifacts,
            new ProjectContextLoader(repo.Artifacts),
            projections,
            new PromptContractRegistry(projections),
            new ProjectionManifestStore(repo.Artifacts),
            new ArtifactLifecycleStore(repo.Artifacts),
            new SplitFamilyStore(repo.Artifacts),
            provenance);
    }

    private static RoadmapStateDocument State(
        RoadmapState state,
        TransitionStatus status = TransitionStatus.Completed,
        RoadmapState? from = null,
        RoadmapState? to = null,
        string prompt = "None",
        string output = "None") =>
        new(
            state,
            [],
            new RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", status, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            RoadmapTransitionIntent.Empty(state),
            [],
            []);

    private static string Spec(string title, string checklist) => $$"""
        # {{title}}

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        {{checklist}}
        """;

    private static DecisionLedgerEntry Decision(string decisionId) =>
        new(
            decisionId,
            DateTimeOffset.UtcNow,
            RoadmapState.CoreReady,
            "SelectNextEpic",
            "SelectNextEpic",
            RoadmapArtifactPaths.Selection,
            [RoadmapArtifactPaths.ActiveEpic],
            [RoadmapArtifactPaths.Selection],
            "Select Existing Epic",
            "High",
            "reason");

    private sealed class DecisionLedgerHashOverride(
        ICanonicalArtifactHasher inner,
        string decisionLedgerHash) : ICanonicalArtifactHasher
    {
        public async Task<CanonicalArtifactHash?> HashIfPresentAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(relativePath, RoadmapArtifactPaths.DecisionLedgerJson, StringComparison.OrdinalIgnoreCase))
            {
                return new CanonicalArtifactHash(
                    new LogicalArtifactDescriptor(
                        RoadmapArtifactPaths.DecisionLedgerJson,
                        LogicalArtifactDomain.DecisionLedger,
                        LogicalArtifactStorageKind.SqliteCanonicalRecord,
                        RoadmapArtifactPaths.DecisionLedgerJson),
                    CanonicalArtifactHasher.Sha256Algorithm,
                    decisionLedgerHash);
            }

            return await inner.HashIfPresentAsync(relativePath, cancellationToken);
        }

        public async Task<CanonicalArtifactHash> RequireHashAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            CanonicalArtifactHash? hash = await HashIfPresentAsync(relativePath, cancellationToken);
            if (hash is null)
            {
                throw new InvalidOperationException($"Logical artifact could not be resolved: {relativePath}");
            }

            return hash;
        }
    }
}
