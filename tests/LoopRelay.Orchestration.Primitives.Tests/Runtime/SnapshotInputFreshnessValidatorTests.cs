using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Primitives.Tests.Runtime;

public sealed class SnapshotInputFreshnessValidatorTests
{
    [Fact]
    public async Task Own_in_place_candidate_does_not_invalidate_its_frozen_input()
    {
        CanonicalCausalContext causality = NewCausality();
        ProductRecord frozen = Record("input-version", ProductLifecycle.Active);
        ProductRecord candidate = Record(causality.Attempt.Value, ProductLifecycle.Proposed);
        WorkflowTransitionDefinition definition = Definition();
        var validator = new SnapshotInputFreshnessValidator(
            new FixedProductResolver(new ProductResolutionResult([candidate], [], [candidate], [], [])),
            new ProductHashContextBuilder());

        InputFreshnessResult result = await validator.ValidateAsync(
            causality,
            Request(definition),
            definition,
            FrozenContext(definition, frozen),
            CancellationToken.None);

        Assert.Equal(InputFreshnessStatus.Fresh, result.Status);
    }

    [Fact]
    public async Task Foreign_in_place_candidate_still_invalidates_the_frozen_input()
    {
        CanonicalCausalContext causality = NewCausality();
        ProductRecord frozen = Record("input-version", ProductLifecycle.Active);
        ProductRecord candidate = Record("another-attempt", ProductLifecycle.Proposed);
        WorkflowTransitionDefinition definition = Definition();
        var validator = new SnapshotInputFreshnessValidator(
            new FixedProductResolver(new ProductResolutionResult([candidate], [], [candidate], [], [])),
            new ProductHashContextBuilder());

        InputFreshnessResult result = await validator.ValidateAsync(
            causality,
            Request(definition),
            definition,
            FrozenContext(definition, frozen),
            CancellationToken.None);

        Assert.Equal(InputFreshnessStatus.InputInvalidated, result.Status);
    }

    [Fact]
    public async Task Own_in_place_file_and_derived_hash_changes_do_not_invalidate_other_inputs()
    {
        CanonicalCausalContext causality = NewCausality();
        ProductRecord frozen = Record("input-version", ProductLifecycle.Active, ".agents/plan.md");
        ProductRecord candidate = Record(
            "new-file-content",
            ProductLifecycle.Active,
            ".agents/plan.md",
            "repository observation");
        WorkflowTransitionDefinition definition = Definition();
        PromptContextSection[] frozenSections =
        [
            new("Executable Plan", "old", ".agents/plan.md", [".agents/plan.md"]),
            new("Adversarial Review", "stable", "ledger:review", ["ledger:review"]),
        ];
        var frozenMetadata = new Dictionary<string, string>
        {
            ["plan.context.executable_plan.agents_plan_md.hash"] = "old-hash",
            ["stable"] = "same",
        };
        PromptContext frozenContext = Context(definition, frozen, frozenMetadata, frozenSections);
        var currentMetadata = new Dictionary<string, string>(frozenMetadata)
        {
            ["plan.context.executable_plan.agents_plan_md.hash"] = "new-hash",
        };
        PromptContextSection[] currentSections =
        [
            new("Executable Plan", "new", ".agents/plan.md", [".agents/plan.md"]),
            new("Adversarial Review", "stable", "ledger:review", ["ledger:review"]),
        ];
        var validator = new SnapshotInputFreshnessValidator(
            new FixedProductResolver(new ProductResolutionResult([candidate], [], [candidate], [], [])),
            new FixedContextBuilder(definition, currentMetadata, currentSections));

        InputFreshnessResult result = await validator.ValidateAsync(
            causality,
            Request(definition),
            definition,
            frozenContext,
            CancellationToken.None);

        Assert.Equal(InputFreshnessStatus.Fresh, result.Status);
    }

    [Fact]
    public async Task Archive_transition_preserves_frozen_identity_for_an_input_it_relocated()
    {
        CanonicalCausalContext causality = NewCausality();
        ProductRecord frozen = Record("archived-input", ProductLifecycle.Active);
        WorkflowTransitionDefinition definition = Definition() with
        {
            Effects =
            [
                new EffectDefinition(
                    new EffectIdentity("archive-input"),
                    EffectCategory.Archive,
                    "after provider success",
                    [Product],
                    [],
                    0,
                    "recoverable"),
            ],
        };
        ProductRequirement requirement = Assert.Single(definition.RequiredInputProducts);
        var validator = new SnapshotInputFreshnessValidator(
            new FixedProductResolver(new ProductResolutionResult([], [requirement], [], [], [])),
            new ProductHashContextBuilder());

        InputFreshnessResult result = await validator.ValidateAsync(
            causality,
            Request(definition),
            definition,
            FrozenContext(definition, frozen),
            CancellationToken.None);

        Assert.Equal(InputFreshnessStatus.Fresh, result.Status);
    }

    private static readonly ProductIdentity Product = new("repository-changes");

    private static CanonicalCausalContext NewCausality() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(),
        AttemptIdentity.New());

    private static TransitionRuntimeRequest Request(WorkflowTransitionDefinition definition) => new(
        WorkflowIdentity.Plan,
        new WorkflowStageIdentity("Planning"),
        definition.Identity,
        new CanonicalTransitionExecutionContext(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            new PolicyIdentity("policy_test"),
            new RuntimeProfileIdentity("runtime_test"),
            new PromptPolicyProfileIdentity("prompt_policy_test")),
        FreshAttemptAuthorization.Instance);

    private static WorkflowTransitionDefinition Definition() => new(
        new WorkflowTransitionIdentity("PublishRepository"),
        "publish repository",
        [new ProductRequirement(Product, DependencyStrength.Required, true, "canonical", "publish")],
        Gate("input"),
        "PublishRepository",
        ExecutionPosture.ScopedArtifactOperation,
        [new ProductDefinition(
            Product,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("PublishRepository"),
            [WorkflowIdentity.Execute],
            "repository",
            "canonical",
            ProductLifecycle.Active,
            ProductValidationState.Valid,
            ProductFreshness.Fresh,
            [],
            ["git"])],
        Gate("output"),
        [],
        [],
        [],
        [],
        new RecoveryDefinition("recovery", "recover", ["retry"], []));

    private static GateDefinition Gate(string identity) =>
        new(new GateIdentity(identity), identity, [], "test", "fail");

    private static ProductRecord Record(
        string causalIdentity,
        ProductLifecycle lifecycle,
        string representation = "git",
        string authority = "canonical") => new(
        Product,
        WorkflowIdentity.Plan,
        new WorkflowTransitionIdentity("PublishRepository"),
        [WorkflowIdentity.Execute],
        "repository",
        authority,
        [representation],
        causalIdentity,
        ProductFreshness.Fresh,
        ProductValidationState.Valid,
        lifecycle,
        [representation]);

    private static PromptContext Context(
        WorkflowTransitionDefinition definition,
        ProductRecord product,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<PromptContextSection> sections)
    {
        var inputs = new ProductResolutionResult([product], [], [], [], []);
        return new PromptContext(
            definition,
            inputs,
            TransitionInputSnapshotHasher.Create(definition, inputs.Products, metadata, sections),
            metadata,
            sections);
    }

    private static PromptContext FrozenContext(
        WorkflowTransitionDefinition definition,
        ProductRecord frozen) => new(
        definition,
        new ProductResolutionResult([frozen], [], [], [], []),
        new TransitionInputSnapshot(frozen.CausalIdentity, [], new Dictionary<string, string>(), []),
        new Dictionary<string, string>(),
        []);

    private sealed class FixedProductResolver(ProductResolutionResult result) : IProductResolver
    {
        public Task<ProductResolutionResult> ResolveAsync(
            IReadOnlyList<ProductRequirement> requirements,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ProductHashContextBuilder : IPromptContextBuilder
    {
        public Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken) => Task.FromResult(new PromptContext(
                definition,
                inputs,
                new TransitionInputSnapshot(
                    Assert.Single(inputs.Products).CausalIdentity,
                    [],
                    new Dictionary<string, string>(),
                    []),
                new Dictionary<string, string>(),
                []));
    }

    private sealed class FixedContextBuilder(
        WorkflowTransitionDefinition definition,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<PromptContextSection> sections) : IPromptContextBuilder
    {
        public Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition ignored,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken) => Task.FromResult(new PromptContext(
                definition,
                inputs,
                TransitionInputSnapshotHasher.Create(definition, inputs.Products, metadata, sections),
                metadata,
                sections));
    }
}
