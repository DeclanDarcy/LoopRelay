using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class UnifiedGateEvaluatorTests
{
    [Fact]
    public async Task Multi_requirement_gate_yields_one_result_per_requirement_with_distinct_statuses()
    {
        var resolved = new ProductIdentity("ResolvedProduct");
        var missing = new ProductIdentity("MissingProduct");
        GateDefinition gate = Gate(ProductRequirementFor(resolved), ProductRequirementFor(missing));
        var inputs = new ProductResolutionResult([Record(resolved)], [Requirement(missing)], [], [], []);

        GateResult result = await Evaluator(TempRepo()).EvaluateInputGateAsync(gate, inputs, CancellationToken.None);

        Assert.Equal(GateStatus.Unsatisfied, result.Status);
        Assert.Equal(2, result.Requirements.Count);
        GateRequirementResult satisfied = result.Requirements[0];
        Assert.Equal($"TestGate.{resolved}", satisfied.RequirementIdentity);
        Assert.Equal(GateStatus.Satisfied, satisfied.Status);
        Assert.Contains("ResolvedProduct.md", satisfied.Evidence);
        GateRequirementResult unsatisfied = result.Requirements[1];
        Assert.Equal($"TestGate.{missing}", unsatisfied.RequirementIdentity);
        Assert.Equal(GateStatus.Unsatisfied, unsatisfied.Status);
        Assert.Contains("MissingProduct", unsatisfied.Evidence);
        Assert.NotEqual(satisfied.Explanation, unsatisfied.Explanation);
        Assert.Contains("MissingProduct", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("ResolvedProduct.md", result.Evidence);
        Assert.Contains("MissingProduct", result.Evidence);
    }

    [Fact]
    public async Task Ambiguous_product_requirement_yields_an_ambiguous_gate()
    {
        var ambiguous = new ProductIdentity("AmbiguousProduct");
        GateDefinition gate = Gate(ProductRequirementFor(ambiguous));
        ProductRecord record = Record(ambiguous);
        var inputs = new ProductResolutionResult([record], [], [], [], [record]);

        GateResult result = await Evaluator(TempRepo()).EvaluateInputGateAsync(gate, inputs, CancellationToken.None);

        Assert.Equal(GateStatus.Ambiguous, result.Status);
        GateRequirementResult requirement = Assert.Single(result.Requirements);
        Assert.Equal(GateStatus.Ambiguous, requirement.Status);
    }

    [Fact]
    public async Task Unsatisfied_requirement_dominates_ambiguous_in_worst_of_aggregation()
    {
        var ambiguous = new ProductIdentity("AmbiguousProduct");
        var missing = new ProductIdentity("MissingProduct");
        GateDefinition gate = Gate(ProductRequirementFor(ambiguous), ProductRequirementFor(missing));
        ProductRecord record = Record(ambiguous);
        var inputs = new ProductResolutionResult([record], [Requirement(missing)], [], [], [record]);

        GateResult result = await Evaluator(TempRepo()).EvaluateInputGateAsync(gate, inputs, CancellationToken.None);

        Assert.Equal(GateStatus.Unsatisfied, result.Status);
        Assert.Equal(GateStatus.Ambiguous, result.Requirements[0].Status);
        Assert.Equal(GateStatus.Unsatisfied, result.Requirements[1].Status);
    }

    [Theory]
    [InlineData(GateStatus.Satisfied, GateStatus.Waiting, GateStatus.Waiting)]
    [InlineData(GateStatus.Waiting, GateStatus.Ambiguous, GateStatus.Ambiguous)]
    [InlineData(GateStatus.Ambiguous, GateStatus.Unsatisfied, GateStatus.Unsatisfied)]
    [InlineData(GateStatus.Unsatisfied, GateStatus.Invalid, GateStatus.Invalid)]
    public void Worst_of_aggregation_orders_statuses_regardless_of_position(
        GateStatus left,
        GateStatus right,
        GateStatus expected)
    {
        Assert.Equal(expected, UnifiedCliComposition.UnifiedGateEvaluator.WorstOf([left, right]));
        Assert.Equal(expected, UnifiedCliComposition.UnifiedGateEvaluator.WorstOf([right, left]));
    }

    [Fact]
    public async Task Gate_with_zero_requirements_is_satisfied_with_an_explainable_requirement_result()
    {
        var gate = new GateDefinition(
            new GateIdentity("EmptyGate"),
            "Empty gate purpose.",
            [],
            "canonical gate authority",
            "Unsatisfied requirements stop progress with evidence.");
        var inputs = new ProductResolutionResult([], [], [], [], []);

        GateResult result = await Evaluator(TempRepo()).EvaluateInputGateAsync(gate, inputs, CancellationToken.None);

        Assert.Equal(GateStatus.Satisfied, result.Status);
        GateRequirementResult explainable = Assert.Single(result.Requirements);
        Assert.Equal("EmptyGate.Explainable", explainable.RequirementIdentity);
        Assert.Equal(GateStatus.Satisfied, explainable.Status);
        Assert.Contains("declares no requirements", explainable.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requirement_without_product_or_surface_is_satisfied_as_an_explainable_declaration()
    {
        GateDefinition gate = Gate(new GateRequirementDefinition(
            "TestGate.Explainable",
            "Gate has explainable workflow requirements.",
            null,
            DependencyStrength.Required,
            true));
        var inputs = new ProductResolutionResult([], [], [], [], []);

        GateResult result = await Evaluator(TempRepo()).EvaluateInputGateAsync(gate, inputs, CancellationToken.None);

        Assert.Equal(GateStatus.Satisfied, result.Status);
        GateRequirementResult requirement = Assert.Single(result.Requirements);
        Assert.Equal(GateStatus.Satisfied, requirement.Status);
        Assert.Contains("explainable declaration", requirement.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dirty_file_inside_surface_is_unsatisfied_with_the_dirty_paths_as_evidence()
    {
        string repo = TempRepo();
        await GitAsync(repo, "init");
        Directory.CreateDirectory(Path.Combine(repo, ".agents"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agents", "epic.md"), "# Epic");
        await GitAsync(repo, "add", ".agents/epic.md");
        GateDefinition gate = Gate(CleanInputRequirement(".agents/"));

        GateResult result = await Evaluator(repo).EvaluateInputGateAsync(
            gate,
            new ProductResolutionResult([], [], [], [], []),
            CancellationToken.None);

        Assert.Equal(GateStatus.Unsatisfied, result.Status);
        GateRequirementResult requirement = Assert.Single(result.Requirements);
        Assert.Equal(GateStatus.Unsatisfied, requirement.Status);
        Assert.Contains(".agents/epic.md", requirement.Evidence);
        Assert.Contains("commit the listed files under '.agents/'", requirement.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dirty_file_outside_surface_is_satisfied()
    {
        string repo = TempRepo();
        await GitAsync(repo, "init");
        await File.WriteAllTextAsync(Path.Combine(repo, "notes.md"), "# Notes");
        GateDefinition gate = Gate(CleanInputRequirement(".agents/"));

        GateResult result = await Evaluator(repo).EvaluateInputGateAsync(
            gate,
            new ProductResolutionResult([], [], [], [], []),
            CancellationToken.None);

        Assert.Equal(GateStatus.Satisfied, result.Status);
        GateRequirementResult requirement = Assert.Single(result.Requirements);
        Assert.Equal(GateStatus.Satisfied, requirement.Status);
        Assert.Contains(".agents/", requirement.Evidence);
    }

    [Fact]
    public async Task Clean_repository_is_satisfied()
    {
        string repo = TempRepo();
        await GitAsync(repo, "init");
        GateDefinition gate = Gate(CleanInputRequirement(".agents/"));

        GateResult result = await Evaluator(repo).EvaluateInputGateAsync(
            gate,
            new ProductResolutionResult([], [], [], [], []),
            CancellationToken.None);

        Assert.Equal(GateStatus.Satisfied, result.Status);
        GateRequirementResult requirement = Assert.Single(result.Requirements);
        Assert.Contains("clean in the git working tree", requirement.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Directory_without_a_git_working_tree_satisfies_the_clean_input_requirement()
    {
        string repo = TempRepo();
        GateDefinition gate = Gate(CleanInputRequirement(".agents/"));

        GateResult result = await Evaluator(repo).EvaluateInputGateAsync(
            gate,
            new ProductResolutionResult([], [], [], [], []),
            CancellationToken.None);

        Assert.Equal(GateStatus.Satisfied, result.Status);
        GateRequirementResult requirement = Assert.Single(result.Requirements);
        Assert.Contains("no git working tree", requirement.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Output_gate_evaluates_each_product_requirement_against_validation()
    {
        var valid = new ProductIdentity("ValidOutput");
        var missing = new ProductIdentity("MissingOutput");
        GateDefinition gate = Gate(ProductRequirementFor(valid), ProductRequirementFor(missing));
        var validation = new ProductValidationResult(
            ProductValidationStatus.Missing,
            [Record(valid)],
            [missing],
            [],
            [],
            [],
            "Missing output product.",
            ["validation.md"]);

        GateResult result = await Evaluator(TempRepo()).EvaluateOutputGateAsync(gate, validation, CancellationToken.None);

        Assert.Equal(GateStatus.Unsatisfied, result.Status);
        Assert.Equal(GateStatus.Satisfied, result.Requirements[0].Status);
        Assert.Equal(GateStatus.Unsatisfied, result.Requirements[1].Status);
        Assert.Contains("MissingOutput", result.Requirements[1].Evidence);
    }

    private static UnifiedCliComposition.UnifiedGateEvaluator Evaluator(string repositoryPath) =>
        new(
            new ProcessRunner(),
            new Repository
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(repositoryPath),
                Path = repositoryPath,
            });

    private static GateDefinition Gate(params GateRequirementDefinition[] requirements) =>
        new(
            new GateIdentity("TestGate"),
            "Test gate purpose.",
            requirements,
            "canonical gate authority",
            "Unsatisfied requirements stop progress with evidence.");

    private static GateRequirementDefinition ProductRequirementFor(ProductIdentity product) =>
        new($"TestGate.{product}", $"Validate {product}.", product, DependencyStrength.Required, true);

    private static GateRequirementDefinition CleanInputRequirement(string surface) =>
        new(
            "TestGate.CleanInput",
            $"Working tree under '{surface}' is committed input.",
            null,
            DependencyStrength.Required,
            true,
            surface);

    private static ProductRequirement Requirement(ProductIdentity product) =>
        new(product, DependencyStrength.Required, false, "canonical product authority", $"Require {product}.");

    private static ProductRecord Record(ProductIdentity product) =>
        new(
            product,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity($"Produce{product}"),
            [WorkflowIdentity.Execute],
            "repository-owned orchestration evidence",
            "canonical product authority",
            [$"{product}.md"],
            $"hash-{product}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{product}.md"]);

    private static string TempRepo() =>
        Directory.CreateTempSubdirectory("cc-cli-gate-evaluator").FullName;

    private static async Task GitAsync(string repositoryPath, params string[] arguments)
    {
        ProcessRunResult result = await new ProcessRunner().RunAsync("git", arguments, repositoryPath);
        Assert.Equal(0, result.ExitCode);
    }
}
