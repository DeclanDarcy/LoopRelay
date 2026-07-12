using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Workflows;

public sealed class WorkflowDefinitionValidatorTests
{
    [Fact]
    public void Canonical_sketches_are_valid_and_cover_the_four_workflow_identities()
    {
        IReadOnlyList<WorkflowDefinition> definitions = CanonicalWorkflowDefinitionSketches.CreateAll();

        Assert.Equal(
            [
                WorkflowIdentity.TraditionalRoadmap,
                WorkflowIdentity.EvalRoadmap,
                WorkflowIdentity.Plan,
                WorkflowIdentity.Execute,
            ],
            definitions.Select(definition => definition.Identity));

        foreach (WorkflowDefinition definition in definitions)
        {
            WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);
            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        }
    }

    [Fact]
    public void Disk_reading_transitions_declare_exactly_their_clean_input_surfaces()
    {
        IReadOnlyList<WorkflowDefinition> definitions = CanonicalWorkflowDefinitionSketches.CreateAll();
        IReadOnlyDictionary<(string Workflow, string Transition), string[]> expected =
            new Dictionary<(string Workflow, string Transition), string[]>
            {
                [("Plan", "WriteExecutablePlan")] = [".agents/"],
                [("Plan", "RunAdversarialReview")] = [".agents/plan.md", ".agents/projections/"],
                [("Plan", "RevisePlan")] = [".agents/plan.md", ".agents/projections/"],
                [("Plan", "GenerateOperationalContext")] = [".agents/plan.md"],
                [("Plan", "CollectExecutionDetails")] = [".agents/plan.md", ".agents/specs/"],
                [("Plan", "GenerateExecutionMilestones")] = [".agents/plan.md"],
                [("Plan", "RefineExecutionDetails")] = [".agents/details.md", ".agents/milestones/"],
                [("EvalRoadmap", "GenerateMilestoneDeepDivesForEpic")] = [".agents/epic.md"],
            };

        foreach (WorkflowDefinition definition in definitions)
        {
            foreach (WorkflowTransitionDefinition transition in definition.Transitions)
            {
                GateRequirementDefinition[] surfaced = transition.InputGate.Requirements
                    .Where(requirement => requirement.InputSurface is not null)
                    .ToArray();
                string[] declared = surfaced
                    .Select(requirement => requirement.InputSurface!)
                    .ToArray();
                string[] expectedSurfaces = expected.TryGetValue(
                    (definition.Identity.Value, transition.Identity.Value),
                    out string[]? surfaces)
                    ? surfaces
                    : [];
                string[] expectedIdentities = expectedSurfaces.Length == 1
                    ? [$"{transition.Identity.Value}.CleanInput"]
                    : expectedSurfaces
                        .Select(surface => $"{transition.Identity.Value}.CleanInput:{surface}")
                        .ToArray();

                Assert.Equal(expectedSurfaces, declared);
                Assert.Equal(expectedIdentities, surfaced.Select(requirement => requirement.Identity).ToArray());
                Assert.All(surfaced, requirement =>
                {
                    Assert.Null(requirement.Product);
                    Assert.True(requirement.BlocksProgress);
                });
            }
        }
    }

    [Fact]
    public void Validate_rejects_a_gate_requirement_declaring_both_product_and_input_surface()
    {
        WorkflowDefinition definition = CanonicalWorkflowDefinitionSketches.CreatePlan();
        WorkflowTransitionDefinition transition = Assert.Single(
            definition.Transitions,
            item => item.Identity == new WorkflowTransitionIdentity("WriteExecutablePlan"));
        GateRequirementDefinition cleanInput = Assert.Single(
            transition.InputGate.Requirements,
            item => item.InputSurface is not null);
        GateRequirementDefinition dualShape = cleanInput with { Product = ProductIdentity.PreparedEpic };
        GateDefinition inputGate = transition.InputGate with
        {
            Requirements = transition.InputGate.Requirements
                .Select(item => item.Identity == cleanInput.Identity ? dualShape : item)
                .ToArray(),
        };
        WorkflowTransitionDefinition mutatedTransition = transition with { InputGate = inputGate };
        WorkflowDefinition mutatedDefinition = definition with
        {
            Transitions = definition.Transitions
                .Select(item => item.Identity == transition.Identity ? mutatedTransition : item)
                .ToArray(),
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(mutatedDefinition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("WriteExecutablePlan.CleanInput", StringComparison.Ordinal) &&
                error.Contains("must not declare both a product and an input surface", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", "must not be empty")]
    [InlineData("   ", "must not be empty")]
    [InlineData("/absolute/surface/", "must be repository-relative, not rooted")]
    [InlineData("C:/absolute/surface/", "must be repository-relative, not rooted")]
    [InlineData(".agents\\plan.md", "must be forward-slash normalized")]
    [InlineData(".agents/../secrets/", "must not contain '..' segments")]
    [InlineData("..", "must not contain '..' segments")]
    public void Validate_rejects_malformed_input_surfaces(string surface, string expectedError)
    {
        WorkflowDefinition definition = WithWriteExecutablePlanSurface(surface);

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("WriteExecutablePlan.CleanInput", StringComparison.Ordinal) &&
                error.Contains(expectedError, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_accepts_the_canonical_relative_forward_slash_surfaces()
    {
        WorkflowDefinition definition = CanonicalWorkflowDefinitionSketches.CreatePlan();

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    private static WorkflowDefinition WithWriteExecutablePlanSurface(string surface)
    {
        WorkflowDefinition definition = CanonicalWorkflowDefinitionSketches.CreatePlan();
        WorkflowTransitionDefinition transition = Assert.Single(
            definition.Transitions,
            item => item.Identity == new WorkflowTransitionIdentity("WriteExecutablePlan"));
        GateRequirementDefinition cleanInput = Assert.Single(
            transition.InputGate.Requirements,
            item => item.InputSurface is not null);
        GateRequirementDefinition mutated = cleanInput with { InputSurface = surface };
        GateDefinition inputGate = transition.InputGate with
        {
            Requirements = transition.InputGate.Requirements
                .Select(item => item.Identity == cleanInput.Identity ? mutated : item)
                .ToArray(),
        };
        WorkflowTransitionDefinition mutatedTransition = transition with { InputGate = inputGate };
        return definition with
        {
            Transitions = definition.Transitions
                .Select(item => item.Identity == transition.Identity ? mutatedTransition : item)
                .ToArray(),
        };
    }

    [Fact]
    public void Canonical_chains_express_traditional_and_eval_routes_into_the_same_plan_and_execute_workflows()
    {
        IReadOnlyList<WorkflowChainDefinition> chains = CanonicalWorkflowDefinitionSketches.CreateChains();

        WorkflowChainDefinition traditional = Assert.Single(chains, chain => chain.InitialWorkflow == WorkflowIdentity.TraditionalRoadmap);
        WorkflowChainDefinition eval = Assert.Single(chains, chain => chain.InitialWorkflow == WorkflowIdentity.EvalRoadmap);

        Assert.Equal(
            [WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            traditional.Workflows.Select(workflow => workflow.Identity));
        Assert.Equal(
            [WorkflowIdentity.EvalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            eval.Workflows.Select(workflow => workflow.Identity));
    }

    [Fact]
    public void Canonical_completion_closure_products_are_owned_only_by_execute()
    {
        IReadOnlyList<WorkflowDefinition> definitions = CanonicalWorkflowDefinitionSketches.CreateAll();
        ProductIdentity[] completionProducts =
        [
            ProductIdentity.CompletionEvidence,
            ProductIdentity.CompletionRoute,
            ProductIdentity.CertifiedCompletion,
        ];

        ProductDefinition[] producers = definitions
            .SelectMany(definition => definition.Transitions)
            .SelectMany(transition => transition.ProducedProducts)
            .Where(product => completionProducts.Contains(product.Identity))
            .ToArray();

        Assert.NotEmpty(producers);
        Assert.All(producers, product => Assert.Equal(WorkflowIdentity.Execute, product.ProducerWorkflow));
        Assert.Contains(producers, product => product.Identity == ProductIdentity.CompletionRoute);
        Assert.Contains(producers, product => product.Identity == ProductIdentity.CertifiedCompletion);
    }

    [Fact]
    public void TraditionalRoadmap_sketch_declares_migration_stages_and_transitions()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreateTraditionalRoadmap();

        Assert.Equal(
            [
                "Roadmap Context",
                "Strategic Initiative Selection",
                "Epic Preparation",
                "Milestone Specification",
                "Workflow Completion",
            ],
            workflow.Stages.Select(stage => stage.Identity.Value));
        Assert.Equal(
            [
                "BootstrapRoadmapCompletionContext",
                "UpdateRoadmapCompletionContext",
                "SelectStrategicInitiative",
                "AuditExistingEpic",
                "CreateEpic",
                "SplitEpic",
                "RealignEpic",
                "ReimagineEpic",
                "RetireEpic",
                "GenerateMilestoneDeepDivesForEpic",
                "VerifyPlanEntryContract",
            ],
            workflow.Transitions.Select(transition => transition.Identity.Value));
    }

    [Fact]
    public void TraditionalRoadmap_transitions_declare_prompt_identities_and_product_requirements()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreateTraditionalRoadmap();

        AssertTransition(workflow, "BootstrapRoadmapCompletionContext", "BootstrapRoadmapCompletionContext");
        AssertTransition(
            workflow,
            "UpdateRoadmapCompletionContext",
            "UpdateRoadmapCompletionContext",
            ProductIdentity.RoadmapCompletionContext);
        AssertTransition(
            workflow,
            "SelectStrategicInitiative",
            "SelectStrategicInitiative",
            ProductIdentity.RoadmapCompletionContext);
        AssertTransition(
            workflow,
            "AuditExistingEpic",
            "AuditExistingEpic",
            ProductIdentity.StrategicInitiativeSelection);
        AssertTransition(
            workflow,
            "CreateEpic",
            "CreateNewEpic",
            ProductIdentity.StrategicInitiativeSelection);
        AssertTransition(workflow, "SplitEpic", "SplitEpic", ProductIdentity.PreparedEpic);
        AssertTransition(workflow, "RealignEpic", "RealignEpic", ProductIdentity.PreparedEpic);
        AssertTransition(workflow, "ReimagineEpic", "ReimagineEpic", ProductIdentity.PreparedEpic);
        AssertTransition(workflow, "RetireEpic", "RetireEpic", ProductIdentity.PreparedEpic);
        AssertTransition(
            workflow,
            "GenerateMilestoneDeepDivesForEpic",
            "GenerateMilestoneDeepDivesForEpic",
            ProductIdentity.PreparedEpic);
        AssertTransition(
            workflow,
            "VerifyPlanEntryContract",
            "VerifyPlanEntryContract",
            ProductIdentity.PreparedEpic,
            ProductIdentity.MilestoneSpecificationSet);
    }

    [Fact]
    public void Plan_sketch_declares_plan_validation_and_execution_preparation_transitions()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreatePlan();

        Assert.Equal(
            ["Planning", "Plan Validation", "Execution Preparation", "Workflow Completion"],
            workflow.Stages.Select(stage => stage.Identity.Value));
        Assert.Equal(
            [
                "WriteExecutablePlan",
                "GenerateAdversarialProjection",
                "RunAdversarialReview",
                "RevisePlan",
                "GenerateOperationalContext",
                "CollectExecutionDetails",
                "GenerateExecutionMilestones",
                "RefineExecutionDetails",
                "VerifyExecuteEntryContract",
            ],
            workflow.Transitions.Select(transition => transition.Identity.Value));
        Assert.Equal(
            ExecutionPostureKind.WarmSession,
            workflow.Transitions.Single(transition => transition.Identity.Value == "WriteExecutablePlan").ExecutionPosture.Kind);
        Assert.Equal(
            ExecutionPostureKind.WarmSession,
            workflow.Transitions.Single(transition => transition.Identity.Value == "RevisePlan").ExecutionPosture.Kind);
        Assert.Equal(
            "CollectDetails",
            workflow.Transitions.Single(transition => transition.Identity.Value == "CollectExecutionDetails").PromptIdentity);
        Assert.Equal(
            "ExtractDetails",
            workflow.Transitions.Single(transition => transition.Identity.Value == "RefineExecutionDetails").PromptIdentity);
        Assert.Contains(
            ProductIdentity.MilestoneSpecificationSet,
            workflow.Transitions.Single(transition => transition.Identity.Value == "CollectExecutionDetails")
                .RequiredInputProducts
                .Select(requirement => requirement.Product));
    }

    [Fact]
    public void Plan_transitions_declare_ordered_publication_and_parent_gitlink_effects()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreatePlan();

        AssertOrderedEffects(
            workflow,
            "WriteExecutablePlan",
            ("persist-draft-plan", EffectCategory.ProductPersistence, 0),
            ("publish-agents-write-plan", EffectCategory.Publication, 1));
        AssertOrderedEffects(
            workflow,
            "GenerateAdversarialProjection",
            ("persist-adversarial-projection", EffectCategory.ProductPersistence, 0),
            ("publish-agents-adversarial-projection", EffectCategory.Publication, 1));
        AssertOrderedEffects(
            workflow,
            "RunAdversarialReview",
            ("persist-adversarial-review", EffectCategory.Evidence, 0));
        AssertOrderedEffects(
            workflow,
            "GenerateOperationalContext",
            ("persist-operational-context", EffectCategory.ProductPersistence, 0),
            ("publish-agents-operational-context", EffectCategory.Publication, 1));
        AssertOrderedEffects(
            workflow,
            "CollectExecutionDetails",
            ("persist-execution-details", EffectCategory.ProductPersistence, 0),
            ("publish-agents-execution-details", EffectCategory.Publication, 1));
        AssertOrderedEffects(
            workflow,
            "GenerateExecutionMilestones",
            ("persist-execution-milestones", EffectCategory.ProductPersistence, 0),
            ("publish-agents-execution-milestones", EffectCategory.Publication, 1));
        AssertOrderedEffects(
            workflow,
            "RefineExecutionDetails",
            ("persist-refined-execution-details", EffectCategory.ProductPersistence, 0),
            ("publish-agents-refined-details", EffectCategory.Publication, 1));
        AssertOrderedEffects(
            workflow,
            "VerifyExecuteEntryContract",
            ("record-execute-entry-evidence", EffectCategory.Evidence, 0),
            ("record-plan-parent-gitlink", EffectCategory.Git, 1));
    }

    [Fact]
    public void Plan_permissioned_artifact_operations_are_scoped_operation_postures()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreatePlan();

        foreach (PlanScopedArtifactOperationSpec operation in PlanScopedArtifactOperationCatalog.All)
        {
            WorkflowTransitionDefinition transition = workflow.Transitions
                .Single(item => item.Identity == operation.Transition);

            Assert.Equal(ExecutionPostureKind.ScopedArtifactOperation, transition.ExecutionPosture.Kind);
            Assert.Equal(operation.PromptIdentity, transition.PromptIdentity);
        }
    }

    [Fact]
    public void Execute_sketch_declares_iterative_execution_and_completion_transitions()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreateExecute();

        Assert.Equal(
            [
                "VerifyExecutionReadiness",
                "GenerateDecision",
                "TransferDecisionSession",
                "ContinueDecisionSession",
                "ExecuteImplementationSlice",
                "GenerateHandoff",
                "UpdateOperationalContext",
                "PublishRepositoryState",
                "EvaluateCommit",
                "EvaluateMilestoneCompletion",
                "RunNonImplementationReview",
                "RunCompletionCertification",
                "InterpretCompletionRoute",
                "VerifyWorkflowExitGate",
            ],
            workflow.Transitions.Select(transition => transition.Identity.Value));
        Assert.Equal(
            ["Execution Readiness", "Implementation Planning", "Implementation", "Execution Continuity", "Completion", "Workflow Completion"],
            workflow.Stages.Select(stage => stage.Identity.Value));
        Assert.Contains(
            new WorkflowStageIdentity("Execution Readiness"),
            workflow.Stages.Single(stage => stage.Identity.Value == "Completion").AllowedSuccessors);
        Assert.Contains(
            ProductIdentity.ExecutionMilestoneSet,
            workflow.Stages.Single(stage => stage.Identity.Value == "Completion")
                .RequiredProducts
                .Select(requirement => requirement.Product));
        Assert.Contains(
            ProductIdentity.ExecutionMilestoneSet,
            workflow.Transitions.Single(transition => transition.Identity.Value == "EvaluateMilestoneCompletion")
                .RequiredInputProducts
                .Select(requirement => requirement.Product));
    }

    [Fact]
    public void EvalRoadmap_sketch_references_all_eval_prompt_assets_and_refresh_transitions()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreateEvalRoadmap();

        string[] promptIdentities = workflow.Transitions
            .Select(transition => transition.PromptIdentity)
            .ToArray();
        foreach (string promptIdentity in new[]
        {
            "CreateEvalDependencyInventory",
            "UpdateDependencyInventory",
            "CreateEvalHypothesisInventory",
            "UpdateHypothesisInventory",
            "CreateArchitecturalCatalog",
            "CreateEvalDag",
            "CreateNextEpicRoadmap",
            "UpdateRoadmap",
            "CreateNextEpicImplementationSpec",
        })
        {
            Assert.Contains(promptIdentity, promptIdentities);
        }

        Assert.Contains(workflow.Transitions, transition => transition.Identity.Value == "RefreshEvalDependencyInventoryStatus");
        Assert.Contains(workflow.Transitions, transition => transition.Identity.Value == "RefreshEvalHypothesisInventoryStatus");
        Assert.Contains(workflow.Transitions, transition => transition.Identity.Value == "RefreshNextEpicRoadmapStatus");
    }

    [Fact]
    public void Gate_status_vocabulary_is_structured_and_complete()
    {
        Assert.Equal(
            [
                GateStatus.Satisfied,
                GateStatus.Unsatisfied,
                GateStatus.Waiting,
                GateStatus.Invalid,
                GateStatus.Ambiguous,
            ],
            Enum.GetValues<GateStatus>());
    }

    [Fact]
    public void Runtime_outcome_vocabulary_is_structured_and_complete()
    {
        Assert.Equal(
            [
                RuntimeOutcomeKind.Completed,
                RuntimeOutcomeKind.Paused,
                RuntimeOutcomeKind.MissingRequiredInput,
                RuntimeOutcomeKind.DirtyInputSurface,
                RuntimeOutcomeKind.UnversionedInputSurface,
                RuntimeOutcomeKind.Failed,
                RuntimeOutcomeKind.Cancelled,
                RuntimeOutcomeKind.Waiting,
                RuntimeOutcomeKind.Stalled,
                RuntimeOutcomeKind.Ambiguous,
                RuntimeOutcomeKind.EffectsPending,
                RuntimeOutcomeKind.RecoveryRequired,
                RuntimeOutcomeKind.InputInvalidated,
                RuntimeOutcomeKind.ConcurrentStateConflict,
                RuntimeOutcomeKind.HumanDecisionRequired,
                RuntimeOutcomeKind.UnsupportedProviderCapability,
                RuntimeOutcomeKind.CompatibilityImportRequired,
            ],
            Enum.GetValues<RuntimeOutcomeKind>());
    }

    [Fact]
    public void Dependency_strength_vocabulary_covers_required_optional_advisory_freshness_sensitive_and_invalidating()
    {
        Assert.Equal(
            [
                DependencyStrength.Required,
                DependencyStrength.Optional,
                DependencyStrength.Advisory,
                DependencyStrength.FreshnessSensitive,
                DependencyStrength.Invalidating,
            ],
            Enum.GetValues<DependencyStrength>());
    }

    [Fact]
    public void Execution_posture_vocabulary_is_workflow_agnostic()
    {
        Assert.Equal(
            [
                ExecutionPostureKind.OneShotAgentPrompt,
                ExecutionPostureKind.PersistentSession,
                ExecutionPostureKind.WarmSession,
                ExecutionPostureKind.ScopedArtifactOperation,
                ExecutionPostureKind.DecisionSession,
                ExecutionPostureKind.ReadOnlyPrompt,
            ],
            Enum.GetValues<ExecutionPostureKind>());
    }

    [Fact]
    public void Validation_rejects_missing_workflow_identity()
    {
        WorkflowDefinition definition = CanonicalWorkflowDefinitionSketches.CreatePlan() with
        {
            Identity = default,
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Contains("Workflow identity", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_stage_transition_references_that_do_not_exist()
    {
        WorkflowDefinition source = CanonicalWorkflowDefinitionSketches.CreatePlan();
        WorkflowStageDefinition brokenStage = source.Stages[0] with
        {
            Transitions = [new WorkflowTransitionIdentity("MissingTransition")],
        };
        WorkflowDefinition definition = source with
        {
            Stages = [brokenStage, .. source.Stages.Skip(1)],
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Contains("unknown transition", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_dependencies_that_reference_unknown_products()
    {
        WorkflowDefinition source = CanonicalWorkflowDefinitionSketches.CreatePlan();
        WorkflowTransitionDefinition brokenTransition = source.Transitions[0] with
        {
            Dependencies =
            [
                new TransitionDependency(
                    "missing-product",
                    DependencyTargetKind.Product,
                    DependencyStrength.Required,
                    "producer",
                    "consumer",
                    new ProductIdentity("MissingProduct"),
                    null,
                    null,
                    null,
                    "Consumer requires producer product."),
            ],
        };
        WorkflowDefinition definition = source with
        {
            Transitions = [brokenTransition, .. source.Transitions.Skip(1)],
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Contains("unknown product", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_products_without_intended_consumers()
    {
        WorkflowDefinition source = CanonicalWorkflowDefinitionSketches.CreateExecute();
        WorkflowTransitionDefinition transition = source.Transitions[^1];
        ProductDefinition product = transition.ProducedProducts[0] with
        {
            IntendedConsumers = [],
        };
        WorkflowTransitionDefinition brokenTransition = transition with
        {
            ProducedProducts = [product],
        };
        WorkflowDefinition definition = source with
        {
            Transitions = [.. source.Transitions.Take(source.Transitions.Count - 1), brokenTransition],
            ExitProducts = [product],
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Contains("intended consumer", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_gates_without_explainable_requirements()
    {
        WorkflowDefinition source = CanonicalWorkflowDefinitionSketches.CreateExecute();
        WorkflowDefinition definition = source with
        {
            EntryGate = source.EntryGate with
            {
                Requirements = [],
            },
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Contains("explainable requirements", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_workflow_definitions_that_embed_cli_or_persistence_details()
    {
        WorkflowDefinition source = CanonicalWorkflowDefinitionSketches.CreatePlan();
        WorkflowDefinition definition = source with
        {
            Purpose = "Run LoopRelay.Cli and write SQLite rows directly.",
        };

        WorkflowDefinitionValidationResult result = WorkflowDefinitionValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Contains("implementation detail", StringComparison.Ordinal));
    }

    private static void AssertTransition(
        WorkflowDefinition workflow,
        string transitionIdentity,
        string promptIdentity,
        params ProductIdentity[] requiredProducts)
    {
        WorkflowTransitionDefinition transition = workflow.Transitions
            .Single(item => item.Identity == new WorkflowTransitionIdentity(transitionIdentity));

        Assert.Equal(promptIdentity, transition.PromptIdentity);
        Assert.Equal(
            requiredProducts,
            transition.RequiredInputProducts.Select(requirement => requirement.Product));
        Assert.All(transition.RequiredInputProducts, requirement =>
        {
            Assert.True(requirement.RequiresFreshness);
            Assert.False(string.IsNullOrWhiteSpace(requirement.RequiredAuthority));
        });
    }

    private static void AssertOrderedEffects(
        WorkflowDefinition workflow,
        string transitionIdentity,
        params (string Identity, EffectCategory Category, int Order)[] expected)
    {
        WorkflowTransitionDefinition transition = workflow.Transitions
            .Single(item => item.Identity == new WorkflowTransitionIdentity(transitionIdentity));

        Assert.Equal(
            expected.Select(item => item.Identity),
            transition.Effects.Select(effect => effect.Identity.Value));
        Assert.Equal(
            expected.Select(item => item.Category),
            transition.Effects.Select(effect => effect.Category));
        Assert.Equal(
            expected.Select(item => item.Order),
            transition.Effects.Select(effect => effect.Order));
    }
}
