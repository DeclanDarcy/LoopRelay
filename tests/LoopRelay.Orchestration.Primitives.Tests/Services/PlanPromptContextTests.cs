using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class PlanPromptContextTests
{
    [Fact]
    public void Build_write_plan_context_loads_prepared_epic_and_specifications()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/epic.md", "# Epic\n\nBuild the capability.");
        Write(repo, ".agents/specs/s1.md", "# Spec 1\n\n- [ ] Implement it.");
        WorkflowTransitionDefinition transition = PlanTransition("WriteExecutablePlan");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(
                Product(ProductIdentity.PreparedEpic, ".agents/epic.md"),
                Product(ProductIdentity.MilestoneSpecificationSet, ".agents/specs/s1.md")));

        Assert.True(result.IsUsable, result.Explanation);
        Assert.Equal(
            ["Prepared Epic", "Milestone Specification"],
            result.Sections.Select(section => section.Title));
        Assert.Contains(result.Sections, section => section.SourcePath == ".agents/epic.md");
        Assert.Contains(result.Sections, section => section.SourcePath == ".agents/specs/s1.md");
        Assert.Equal("WriteExecutablePlan", result.Metadata["plan.context.transition"]);
        Assert.Equal("2", result.Metadata["plan.context.section_count"]);
        Assert.Contains(result.Metadata, entry => entry.Key.Contains("prepared_epic", StringComparison.Ordinal) &&
            entry.Value.Length == 64);
    }

    [Fact]
    public void Build_collect_details_context_uses_plan_and_specifications()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan\n\nUse the spec.");
        Write(repo, ".agents/specs/s1.md", "# Spec 1\n\n- [ ] Implement it.");
        WorkflowTransitionDefinition transition = PlanTransition("CollectExecutionDetails");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(
                Product(ProductIdentity.ExecutablePlan, ".agents/plan.md"),
                Product(ProductIdentity.MilestoneSpecificationSet, ".agents/specs/s1.md")));

        Assert.True(result.IsUsable, result.Explanation);
        Assert.Equal(
            ["Scoped Operation Contract", "Executable Plan", "Milestone Specification"],
            result.Sections.Select(section => section.Title));
        Assert.Equal("CollectExecutionDetails", result.Metadata["plan.scoped_operation.transition"]);
        Assert.Equal("collect-details", result.Metadata["plan.scoped_operation.label"]);
        Assert.Equal(".agents/specs/*.md", result.Metadata["plan.scoped_operation.allowed_read_globs"]);
        Assert.Equal(".agents/details.md", result.Metadata["plan.scoped_operation.required_outputs"]);
    }

    [Fact]
    public void Build_adversarial_review_context_uses_plan_and_projection()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan\n\nImplement capability.");
        Write(repo, PlanPromptContext.AdversarialPlanReviewProjectionPath, "# Projection\n\nProject-specific risk.");
        WorkflowTransitionDefinition transition = PlanTransition("RunAdversarialReview");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(
                Product(ProductIdentity.ExecutablePlan, ".agents/plan.md"),
                Product(ProductIdentity.AdversarialProjection, PlanPromptContext.AdversarialPlanReviewProjectionPath)));

        Assert.True(result.IsUsable, result.Explanation);
        Assert.Equal(
            ["Executable Plan", "Adversarial Projection"],
            result.Sections.Select(section => section.Title));
        Assert.Contains(result.Sections, section =>
            section.SourcePath == PlanPromptContext.AdversarialPlanReviewProjectionPath &&
            section.Content.Contains("Project-specific risk.", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_revise_plan_context_uses_plan_and_adversarial_review()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan\n\nImplement capability.");
        Write(repo, ".LoopRelay/evidence/plan/adversarial-review.md", "# Review\n\nTighten the validation path.");
        WorkflowTransitionDefinition transition = PlanTransition("RevisePlan");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(
                Product(ProductIdentity.ExecutablePlan, ".agents/plan.md"),
                Product(ProductIdentity.AdversarialReview, ".LoopRelay/evidence/plan/adversarial-review.md")));

        Assert.True(result.IsUsable, result.Explanation);
        Assert.Equal(
            ["Executable Plan", "Adversarial Review"],
            result.Sections.Select(section => section.Title));
        Assert.Contains(result.Sections, section =>
            section.SourcePath == ".LoopRelay/evidence/plan/adversarial-review.md" &&
            section.Content.Contains("Tighten the validation path.", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_generate_milestones_context_exposes_split_guard_and_checkbox_contract()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan\n\n- [ ] Extract milestones.");
        WorkflowTransitionDefinition transition = PlanTransition("GenerateExecutionMilestones");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(Product(ProductIdentity.ExecutablePlan, ".agents/plan.md")));

        Assert.True(result.IsUsable, result.Explanation);
        Assert.Equal(
            ["Scoped Operation Contract", "Executable Plan"],
            result.Sections.Select(section => section.Title));
        Assert.Equal("GenerateExecutionMilestones", result.Metadata["plan.scoped_operation.transition"]);
        Assert.Equal("extract-milestones", result.Metadata["plan.scoped_operation.label"]);
        Assert.Equal(".agents/plan.md", result.Metadata["plan.scoped_operation.changed_guard"]);
        Assert.Equal(".agents/milestones/m*.md", result.Metadata["plan.scoped_operation.required_output_glob"]);
        Assert.Equal("true", result.Metadata["plan.scoped_operation.require_checklist_in_glob"]);
        Assert.Contains(result.Sections, section =>
            section.Title == "Scoped Operation Contract" &&
            section.Content.Contains("Required output glob: .agents/milestones/m*.md", StringComparison.Ordinal) &&
            section.Content.Contains("Requires checklist in required output glob: true", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_refine_details_context_uses_details_and_execution_milestones()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/details.md", "# Details\n\nExecution detail.");
        Write(repo, ".agents/milestones/m1.md", "# M1\n\n- [ ] Implement it.");
        WorkflowTransitionDefinition transition = PlanTransition("RefineExecutionDetails");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(
                Product(ProductIdentity.ExecutionDetails, ".agents/details.md"),
                Product(ProductIdentity.ExecutionMilestoneSet, ".agents/milestones/m1.md")));

        Assert.True(result.IsUsable, result.Explanation);
        Assert.Equal(
            ["Scoped Operation Contract", "Execution Details", "Execution Milestone"],
            result.Sections.Select(section => section.Title));
        Assert.Equal(".agents/milestones/m*.md", result.Metadata["plan.scoped_operation.allowed_read_globs"]);
        Assert.Equal(".agents/milestones/m*.md", result.Metadata["plan.scoped_operation.allowed_write_globs"]);
    }

    [Fact]
    public void Build_blocks_when_required_context_source_is_empty()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "   ");
        Write(repo, ".agents/specs/s1.md", "# Spec 1");
        WorkflowTransitionDefinition transition = PlanTransition("CollectExecutionDetails");

        PlanPromptContextResult result = PlanPromptContext.Build(
            repo,
            transition,
            Inputs(
                Product(ProductIdentity.ExecutablePlan, ".agents/plan.md"),
                Product(ProductIdentity.MilestoneSpecificationSet, ".agents/specs/s1.md")));

        Assert.False(result.IsUsable);
        Assert.Contains("empty", result.Explanation, StringComparison.Ordinal);
        Assert.Contains(".agents/plan.md", result.Evidence);
    }

    [Theory]
    [InlineData("WriteExecutablePlan")]
    [InlineData("RunAdversarialReview")]
    [InlineData("RevisePlan")]
    [InlineData("GenerateOperationalContext")]
    [InlineData("CollectExecutionDetails")]
    [InlineData("GenerateExecutionMilestones")]
    [InlineData("RefineExecutionDetails")]
    public void Supports_returns_true_for_plan_artifact_context_transitions(string transition)
    {
        Assert.True(PlanPromptContext.Supports(new WorkflowTransitionIdentity(transition)));
    }

    private static WorkflowTransitionDefinition PlanTransition(string identity) =>
        CanonicalWorkflowDefinitionSketches
            .CreatePlan()
            .Transitions
            .Single(transition => transition.Identity == new WorkflowTransitionIdentity(identity));

    private static ProductResolutionResult Inputs(params ProductRecord[] products) =>
        new(products, Missing: [], Stale: [], Invalid: [], Ambiguous: []);

    private static ProductRecord Product(ProductIdentity identity, params string[] paths) =>
        new(
            identity,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity($"Observed{identity.Value}"),
            [WorkflowIdentity.Plan],
            "repository-owned test product",
            "test",
            paths,
            $"{identity.Value}:test",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            paths);

    private static string CreateRepo() =>
        Directory.CreateTempSubdirectory("looprelay-plan-context-").FullName;

    private static void Write(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
