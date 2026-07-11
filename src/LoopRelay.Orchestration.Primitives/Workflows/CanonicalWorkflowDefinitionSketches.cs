namespace LoopRelay.Orchestration.Workflows;

public static class CanonicalWorkflowDefinitionSketches
{
    public static IReadOnlyList<WorkflowDefinition> CreateAll() =>
    [
        CreateTraditionalRoadmap(),
        CreateEvalRoadmap(),
        CreatePlan(),
        CreateExecute(),
    ];

    public static IReadOnlyList<WorkflowChainDefinition> CreateChains()
    {
        WorkflowDefinition traditional = CreateTraditionalRoadmap();
        WorkflowDefinition eval = CreateEvalRoadmap();
        WorkflowDefinition plan = CreatePlan();
        WorkflowDefinition execute = CreateExecute();

        return
        [
            new WorkflowChainDefinition(
                "TraditionalRoadmapToExecute",
                "Produce a prepared epic from strategic roadmap context, plan it, then execute it.",
                WorkflowIdentity.TraditionalRoadmap,
                [traditional, plan, execute]),
            new WorkflowChainDefinition(
                "EvalRoadmapToExecute",
                "Produce a prepared epic from evaluation intent, plan it, then execute it.",
                WorkflowIdentity.EvalRoadmap,
                [eval, plan, execute]),
        ];
    }

    public static WorkflowDefinition CreateTraditionalRoadmap()
    {
        WorkflowIdentity workflow = WorkflowIdentity.TraditionalRoadmap;
        WorkflowIdentity downstream = WorkflowIdentity.Plan;
        var bootstrapContext = new WorkflowTransitionIdentity("BootstrapRoadmapCompletionContext");
        var updateContext = new WorkflowTransitionIdentity("UpdateRoadmapCompletionContext");
        var selectStrategicInitiative = new WorkflowTransitionIdentity("SelectStrategicInitiative");
        var auditEpic = new WorkflowTransitionIdentity("AuditExistingEpic");
        var createEpic = new WorkflowTransitionIdentity("CreateEpic");
        var splitEpic = new WorkflowTransitionIdentity("SplitEpic");
        var realignEpic = new WorkflowTransitionIdentity("RealignEpic");
        var reimagineEpic = new WorkflowTransitionIdentity("ReimagineEpic");
        var retireEpic = new WorkflowTransitionIdentity("RetireEpic");
        var generateSpecs = new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic");
        var verifyPlanEntry = new WorkflowTransitionIdentity("VerifyPlanEntryContract");

        ProductDefinition roadmapContext = Product(
            ProductIdentity.RoadmapCompletionContext,
            workflow,
            bootstrapContext,
            workflow,
            "roadmap completion context representation");
        ProductDefinition updatedRoadmapContext = Product(
            ProductIdentity.RoadmapCompletionContext,
            workflow,
            updateContext,
            workflow,
            "updated roadmap completion context representation");
        ProductDefinition selection = Product(
            ProductIdentity.StrategicInitiativeSelection,
            workflow,
            selectStrategicInitiative,
            workflow,
            "selected strategic initiative representation");
        ProductDefinition preparedEpic = Product(
            ProductIdentity.PreparedEpic,
            workflow,
            createEpic,
            downstream,
            "prepared epic representation");
        ProductDefinition auditedEpic = Product(
            ProductIdentity.EpicPreparationAudit,
            workflow,
            auditEpic,
            workflow,
            "epic preparation audit representation");
        ProductDefinition splitPreparedEpic = Product(
            ProductIdentity.PreparedEpic,
            workflow,
            splitEpic,
            downstream,
            "split epic representation");
        ProductDefinition realignedPreparedEpic = Product(
            ProductIdentity.PreparedEpic,
            workflow,
            realignEpic,
            downstream,
            "realigned epic representation");
        ProductDefinition reimaginedPreparedEpic = Product(
            ProductIdentity.PreparedEpic,
            workflow,
            reimagineEpic,
            downstream,
            "reimagined epic representation");
        ProductDefinition retirementContext = Product(
            ProductIdentity.RoadmapCompletionContext,
            workflow,
            retireEpic,
            workflow,
            "retired epic context representation");
        ProductDefinition specs = Product(
            ProductIdentity.MilestoneSpecificationSet,
            workflow,
            generateSpecs,
            downstream,
            "milestone specification set representation");

        return new WorkflowDefinition(
            workflow,
            "Select the next strategic initiative, prepare an epic, and prove it is ready for planning.",
            [],
            Gate("TraditionalRoadmap.Entry", "Traditional roadmap may start from repository-owned roadmap context."),
            [
                Stage(
                    "Roadmap Context",
                    "Establish roadmap context required for roadmap decisions.",
                    [],
                    [ProductIdentity.RoadmapCompletionContext],
                    [],
                    [bootstrapContext, updateContext],
                    ["Strategic Initiative Selection"]),
                Stage(
                    "Strategic Initiative Selection",
                    "Select the next roadmap initiative to prepare.",
                    [Requirement(ProductIdentity.RoadmapCompletionContext)],
                    [ProductIdentity.StrategicInitiativeSelection],
                    [ProductDependency(ProductIdentity.RoadmapCompletionContext, bootstrapContext, selectStrategicInitiative)],
                    [selectStrategicInitiative],
                    ["Epic Preparation"]),
                Stage(
                    "Epic Preparation",
                    "Prepare the selected initiative through audit, create, split, realign, reimagine, or retire decisions.",
                    [Requirement(ProductIdentity.StrategicInitiativeSelection)],
                    [ProductIdentity.EpicPreparationAudit, ProductIdentity.PreparedEpic, ProductIdentity.RoadmapCompletionContext],
                    [ProductDependency(ProductIdentity.StrategicInitiativeSelection, selectStrategicInitiative, createEpic)],
                    [auditEpic, createEpic, splitEpic, realignEpic, reimagineEpic, retireEpic],
                    ["Milestone Specification"]),
                Stage(
                    "Milestone Specification",
                    "Generate milestone specifications that preserve the prepared epic contract.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    [ProductIdentity.MilestoneSpecificationSet],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, generateSpecs)],
                    [generateSpecs],
                    ["Workflow Completion"]),
                Stage(
                    "Workflow Completion",
                    "Verify the prepared epic and milestone specifications satisfy the planning entry contract.",
                    [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)],
                    [],
                    [
                        ProductDependency(ProductIdentity.PreparedEpic, createEpic, verifyPlanEntry),
                        ProductDependency(ProductIdentity.MilestoneSpecificationSet, generateSpecs, verifyPlanEntry),
                    ],
                    [verifyPlanEntry],
                    []),
            ],
            [
                Transition(
                    workflow,
                    bootstrapContext,
                    "Bootstrap roadmap completion context from repository evidence.",
                    [],
                    "BootstrapRoadmapCompletionContext",
                    ExecutionPosture.ScopedArtifactOperation,
                    [roadmapContext],
                    [roadmapContext.Identity],
                    [Effect("persist-roadmap-completion-context", EffectCategory.ProductPersistence, [], [roadmapContext.Identity])],
                    []),
                Transition(
                    workflow,
                    updateContext,
                    "Update roadmap completion context against current repository evidence.",
                    [Requirement(ProductIdentity.RoadmapCompletionContext)],
                    "UpdateRoadmapCompletionContext",
                    ExecutionPosture.ScopedArtifactOperation,
                    [updatedRoadmapContext],
                    [updatedRoadmapContext.Identity],
                    [Effect("persist-updated-roadmap-context", EffectCategory.ProductPersistence, [roadmapContext.Identity], [updatedRoadmapContext.Identity])],
                    [ProductDependency(ProductIdentity.RoadmapCompletionContext, bootstrapContext, updateContext)]),
                Transition(
                    workflow,
                    selectStrategicInitiative,
                    "Select the next roadmap initiative from validated strategic context.",
                    [Requirement(ProductIdentity.RoadmapCompletionContext)],
                    "SelectStrategicInitiative",
                    ExecutionPosture.OneShotAgentPrompt,
                    [selection],
                    [selection.Identity],
                    [Effect("record-strategic-selection", EffectCategory.Evidence, [roadmapContext.Identity], [selection.Identity])],
                    [ProductDependency(ProductIdentity.RoadmapCompletionContext, bootstrapContext, selectStrategicInitiative)]),
                Transition(
                    workflow,
                    auditEpic,
                    "Audit an existing epic against the selected strategic initiative.",
                    [Requirement(ProductIdentity.StrategicInitiativeSelection)],
                    "AuditExistingEpic",
                    ExecutionPosture.OneShotAgentPrompt,
                    [auditedEpic],
                    [auditedEpic.Identity],
                    [Effect("persist-audited-epic", EffectCategory.ProductPersistence, [selection.Identity], [auditedEpic.Identity])],
                    [ProductDependency(ProductIdentity.StrategicInitiativeSelection, selectStrategicInitiative, auditEpic)]),
                Transition(
                    workflow,
                    createEpic,
                    "Create an implementation-ready epic from the selected strategic initiative.",
                    [Requirement(ProductIdentity.StrategicInitiativeSelection)],
                    "CreateNewEpic",
                    ExecutionPosture.OneShotAgentPrompt,
                    [preparedEpic],
                    [preparedEpic.Identity],
                    [Effect("persist-prepared-epic", EffectCategory.ProductPersistence, [selection.Identity], [preparedEpic.Identity])],
                    [ProductDependency(ProductIdentity.StrategicInitiativeSelection, selectStrategicInitiative, createEpic)]),
                Transition(
                    workflow,
                    splitEpic,
                    "Split an oversized epic while preserving implementation-ready evidence.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    "SplitEpic",
                    ExecutionPosture.OneShotAgentPrompt,
                    [splitPreparedEpic],
                    [splitPreparedEpic.Identity],
                    [Effect("persist-split-epic", EffectCategory.ProductPersistence, [preparedEpic.Identity], [splitPreparedEpic.Identity])],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, splitEpic)]),
                Transition(
                    workflow,
                    realignEpic,
                    "Realign an epic to current strategic and repository evidence.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    "RealignEpic",
                    ExecutionPosture.OneShotAgentPrompt,
                    [realignedPreparedEpic],
                    [realignedPreparedEpic.Identity],
                    [Effect("persist-realigned-epic", EffectCategory.ProductPersistence, [preparedEpic.Identity], [realignedPreparedEpic.Identity])],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, realignEpic)]),
                Transition(
                    workflow,
                    reimagineEpic,
                    "Reimagine an epic when the current approach no longer fits the evidence.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    "ReimagineEpic",
                    ExecutionPosture.OneShotAgentPrompt,
                    [reimaginedPreparedEpic],
                    [reimaginedPreparedEpic.Identity],
                    [Effect("persist-reimagined-epic", EffectCategory.ProductPersistence, [preparedEpic.Identity], [reimaginedPreparedEpic.Identity])],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, reimagineEpic)]),
                Transition(
                    workflow,
                    retireEpic,
                    "Retire an epic from active planning and record recovery evidence.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    "RetireEpic",
                    ExecutionPosture.ReadOnlyPrompt,
                    [retirementContext],
                    [retirementContext.Identity],
                    [Effect("record-retired-epic", EffectCategory.RecoveryBookkeeping, [preparedEpic.Identity], [retirementContext.Identity])],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, retireEpic)]),
                Transition(
                    workflow,
                    generateSpecs,
                    "Generate milestone deep dives from the prepared epic.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    "GenerateMilestoneDeepDivesForEpic",
                    ExecutionPosture.OneShotAgentPrompt,
                    [specs],
                    [specs.Identity],
                    [Effect("persist-milestone-specifications", EffectCategory.ProductPersistence, [preparedEpic.Identity], [specs.Identity])],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, generateSpecs)]),
                Transition(
                    workflow,
                    verifyPlanEntry,
                    "Verify the roadmap products satisfy the universal planning entry contract.",
                    [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)],
                    "VerifyPlanEntryContract",
                    ExecutionPosture.ReadOnlyPrompt,
                    [],
                    [ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet],
                    [Effect("record-plan-entry-evidence", EffectCategory.Evidence, [preparedEpic.Identity, specs.Identity], [])],
                    [
                        ProductDependency(ProductIdentity.PreparedEpic, createEpic, verifyPlanEntry),
                        ProductDependency(ProductIdentity.MilestoneSpecificationSet, generateSpecs, verifyPlanEntry),
                    ]),
            ],
            [preparedEpic, specs],
            Gate(
                "TraditionalRoadmap.Exit",
                "Prepared epic and milestone specification products are valid for planning.",
                ProductIdentity.PreparedEpic,
                ProductIdentity.MilestoneSpecificationSet),
            downstream,
            Completion("TraditionalRoadmap.Complete", ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet),
            Blocker("TraditionalRoadmap.Blocked"),
            Recovery("TraditionalRoadmap.Recovery"));
    }

    public static WorkflowDefinition CreateEvalRoadmap()
    {
        WorkflowIdentity workflow = WorkflowIdentity.EvalRoadmap;
        WorkflowIdentity downstream = WorkflowIdentity.Plan;
        var selectEvaluation = new WorkflowTransitionIdentity("SelectEvaluationIntent");
        var createDependencyInventory = new WorkflowTransitionIdentity("CreateEvalDependencyInventory");
        var createHypothesisInventory = new WorkflowTransitionIdentity("CreateEvalHypothesisInventory");
        var createArchitecturalCatalog = new WorkflowTransitionIdentity("CreateEvalArchitecturalCatalog");
        var createEvalDag = new WorkflowTransitionIdentity("CreateEvalDag");
        var createRoadmap = new WorkflowTransitionIdentity("CreateNextEpicRoadmap");
        var createEpic = new WorkflowTransitionIdentity("CreateNextEpicActiveEpic");
        var refreshDependencyInventory = new WorkflowTransitionIdentity("RefreshEvalDependencyInventoryStatus");
        var refreshHypothesisInventory = new WorkflowTransitionIdentity("RefreshEvalHypothesisInventoryStatus");
        var refreshRoadmap = new WorkflowTransitionIdentity("RefreshNextEpicRoadmapStatus");
        var generateSpecs = new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic");
        var verifyPlanEntry = new WorkflowTransitionIdentity("VerifyPlanEntryContract");

        ProductDefinition evaluationIntent = Product(
            ProductIdentity.EvaluationIntent,
            workflow,
            selectEvaluation,
            workflow,
            "evaluation intent representation");
        ProductDefinition dependencyInventory = Product(
            ProductIdentity.DependencyInventory,
            workflow,
            createDependencyInventory,
            workflow,
            "evaluation dependency inventory representation");
        ProductDefinition refreshedDependencyInventory = Product(
            ProductIdentity.DependencyInventory,
            workflow,
            refreshDependencyInventory,
            workflow,
            "refreshed evaluation dependency inventory representation");
        ProductDefinition hypothesisInventory = Product(
            ProductIdentity.HypothesisInventory,
            workflow,
            createHypothesisInventory,
            workflow,
            "evaluation hypothesis inventory representation");
        ProductDefinition refreshedHypothesisInventory = Product(
            ProductIdentity.HypothesisInventory,
            workflow,
            refreshHypothesisInventory,
            workflow,
            "refreshed evaluation hypothesis inventory representation");
        ProductDefinition architecturalCatalog = Product(
            ProductIdentity.ArchitecturalCatalog,
            workflow,
            createArchitecturalCatalog,
            workflow,
            "evaluation architectural catalog representation");
        ProductDefinition evalDag = Product(
            ProductIdentity.EvalDag,
            workflow,
            createEvalDag,
            workflow,
            "evaluation dependency graph representation");
        ProductDefinition nextEpicRoadmap = Product(
            ProductIdentity.NextEpicRoadmap,
            workflow,
            createRoadmap,
            workflow,
            "next epic roadmap representation");
        ProductDefinition refreshedNextEpicRoadmap = Product(
            ProductIdentity.NextEpicRoadmap,
            workflow,
            refreshRoadmap,
            workflow,
            "refreshed next epic roadmap representation");
        ProductDefinition preparedEpic = Product(
            ProductIdentity.PreparedEpic,
            workflow,
            createEpic,
            downstream,
            "prepared epic representation");
        ProductDefinition specs = Product(
            ProductIdentity.MilestoneSpecificationSet,
            workflow,
            generateSpecs,
            downstream,
            "milestone specification set representation");

        return new WorkflowDefinition(
            workflow,
            "Convert evaluation intent into the same prepared epic and milestone specifications used by planning.",
            [Requirement(ProductIdentity.EvaluationIntent)],
            Gate("EvalRoadmap.Entry", "Evaluation intent is available and authoritative.", ProductIdentity.EvaluationIntent),
            [
                Stage(
                    "Evaluation Foundation",
                    "Select the evaluation intent to analyze.",
                    [Requirement(ProductIdentity.EvaluationIntent)],
                    [ProductIdentity.EvaluationIntent],
                    [],
                    [selectEvaluation],
                    ["Dependency Inventory"]),
                Stage(
                    "Dependency Inventory",
                    "Create the dependency inventory from evaluation intent.",
                    [Requirement(ProductIdentity.EvaluationIntent)],
                    [ProductIdentity.DependencyInventory],
                    [ProductDependency(ProductIdentity.EvaluationIntent, selectEvaluation, createDependencyInventory)],
                    [createDependencyInventory, refreshDependencyInventory],
                    ["Hypothesis Inventory"]),
                Stage(
                    "Hypothesis Inventory",
                    "Create the hypothesis inventory from dependency evidence.",
                    [Requirement(ProductIdentity.DependencyInventory)],
                    [ProductIdentity.HypothesisInventory],
                    [ProductDependency(ProductIdentity.DependencyInventory, createDependencyInventory, createHypothesisInventory)],
                    [createHypothesisInventory, refreshHypothesisInventory],
                    ["Architectural Catalog"]),
                Stage(
                    "Architectural Catalog",
                    "Create architectural catalog evidence for roadmap synthesis.",
                    [Requirement(ProductIdentity.DependencyInventory), Requirement(ProductIdentity.HypothesisInventory)],
                    [ProductIdentity.ArchitecturalCatalog],
                    [
                        ProductDependency(ProductIdentity.DependencyInventory, createDependencyInventory, createArchitecturalCatalog),
                        ProductDependency(ProductIdentity.HypothesisInventory, createHypothesisInventory, createArchitecturalCatalog),
                    ],
                    [createArchitecturalCatalog],
                    ["Eval DAG"]),
                Stage(
                    "Eval DAG",
                    "Create the evaluation dependency graph.",
                    [Requirement(ProductIdentity.ArchitecturalCatalog)],
                    [ProductIdentity.EvalDag],
                    [ProductDependency(ProductIdentity.ArchitecturalCatalog, createArchitecturalCatalog, createEvalDag)],
                    [createEvalDag],
                    ["Next Epic Roadmap"]),
                Stage(
                    "Next Epic Roadmap",
                    "Select the next epic roadmap from evaluation graph evidence.",
                    [Requirement(ProductIdentity.EvalDag)],
                    [ProductIdentity.NextEpicRoadmap],
                    [ProductDependency(ProductIdentity.EvalDag, createEvalDag, createRoadmap)],
                    [createRoadmap, refreshRoadmap],
                    ["Active Epic Preparation"]),
                Stage(
                    "Active Epic Preparation",
                    "Prepare an active epic from the next epic roadmap.",
                    [Requirement(ProductIdentity.NextEpicRoadmap)],
                    [ProductIdentity.PreparedEpic],
                    [ProductDependency(ProductIdentity.NextEpicRoadmap, createRoadmap, createEpic)],
                    [createEpic],
                    ["Milestone Specification"]),
                Stage(
                    "Milestone Specification",
                    "Generate milestone deep dives from the prepared epic.",
                    [Requirement(ProductIdentity.PreparedEpic)],
                    [ProductIdentity.MilestoneSpecificationSet],
                    [ProductDependency(ProductIdentity.PreparedEpic, createEpic, generateSpecs)],
                    [generateSpecs],
                    ["Workflow Completion"]),
                Stage(
                    "Workflow Completion",
                    "Verify EvalRoadmap produced the universal planning entry products.",
                    [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)],
                    [],
                    [
                        ProductDependency(ProductIdentity.PreparedEpic, createEpic, verifyPlanEntry),
                        ProductDependency(ProductIdentity.MilestoneSpecificationSet, generateSpecs, verifyPlanEntry),
                    ],
                    [verifyPlanEntry],
                    []),
            ],
            [
                Transition(workflow, selectEvaluation, "Select one evaluation intent for the run.", [Requirement(ProductIdentity.EvaluationIntent)], "SelectEvaluationIntent", ExecutionPosture.ReadOnlyPrompt, [evaluationIntent], [ProductIdentity.EvaluationIntent], [Effect("record-evaluation-selection", EffectCategory.Evidence, [ProductIdentity.EvaluationIntent], [ProductIdentity.EvaluationIntent])], []),
                Transition(workflow, createDependencyInventory, "Create dependency inventory from selected evaluation intent.", [Requirement(ProductIdentity.EvaluationIntent)], "CreateEvalDependencyInventory", ExecutionPosture.OneShotAgentPrompt, [dependencyInventory], [ProductIdentity.DependencyInventory], [Effect("persist-dependency-inventory", EffectCategory.ProductPersistence, [ProductIdentity.EvaluationIntent], [ProductIdentity.DependencyInventory])], [ProductDependency(ProductIdentity.EvaluationIntent, selectEvaluation, createDependencyInventory)]),
                Transition(workflow, refreshDependencyInventory, "Refresh dependency inventory status against repository evidence.", [Requirement(ProductIdentity.DependencyInventory)], "UpdateDependencyInventory", ExecutionPosture.ReadOnlyPrompt, [refreshedDependencyInventory], [ProductIdentity.DependencyInventory], [Effect("record-refreshed-dependency-inventory", EffectCategory.Evidence, [ProductIdentity.DependencyInventory], [ProductIdentity.DependencyInventory])], [ProductDependency(ProductIdentity.DependencyInventory, createDependencyInventory, refreshDependencyInventory)]),
                Transition(workflow, createHypothesisInventory, "Create hypothesis inventory from dependency inventory.", [Requirement(ProductIdentity.DependencyInventory)], "CreateEvalHypothesisInventory", ExecutionPosture.OneShotAgentPrompt, [hypothesisInventory], [ProductIdentity.HypothesisInventory], [Effect("persist-hypothesis-inventory", EffectCategory.ProductPersistence, [ProductIdentity.DependencyInventory], [ProductIdentity.HypothesisInventory])], [ProductDependency(ProductIdentity.DependencyInventory, createDependencyInventory, createHypothesisInventory)]),
                Transition(workflow, refreshHypothesisInventory, "Refresh hypothesis inventory status against repository evidence.", [Requirement(ProductIdentity.HypothesisInventory)], "UpdateHypothesisInventory", ExecutionPosture.ReadOnlyPrompt, [refreshedHypothesisInventory], [ProductIdentity.HypothesisInventory], [Effect("record-refreshed-hypothesis-inventory", EffectCategory.Evidence, [ProductIdentity.HypothesisInventory], [ProductIdentity.HypothesisInventory])], [ProductDependency(ProductIdentity.HypothesisInventory, createHypothesisInventory, refreshHypothesisInventory)]),
                Transition(workflow, createArchitecturalCatalog, "Create architectural catalog from evaluation inventories.", [Requirement(ProductIdentity.DependencyInventory), Requirement(ProductIdentity.HypothesisInventory)], "CreateArchitecturalCatalog", ExecutionPosture.OneShotAgentPrompt, [architecturalCatalog], [ProductIdentity.ArchitecturalCatalog], [Effect("persist-architectural-catalog", EffectCategory.ProductPersistence, [ProductIdentity.DependencyInventory, ProductIdentity.HypothesisInventory], [ProductIdentity.ArchitecturalCatalog])], [ProductDependency(ProductIdentity.DependencyInventory, createDependencyInventory, createArchitecturalCatalog), ProductDependency(ProductIdentity.HypothesisInventory, createHypothesisInventory, createArchitecturalCatalog)]),
                Transition(workflow, createEvalDag, "Create evaluation dependency graph from architectural catalog.", [Requirement(ProductIdentity.ArchitecturalCatalog)], "CreateEvalDag", ExecutionPosture.OneShotAgentPrompt, [evalDag], [ProductIdentity.EvalDag], [Effect("persist-eval-dag", EffectCategory.ProductPersistence, [ProductIdentity.ArchitecturalCatalog], [ProductIdentity.EvalDag])], [ProductDependency(ProductIdentity.ArchitecturalCatalog, createArchitecturalCatalog, createEvalDag)]),
                Transition(workflow, createRoadmap, "Create next epic roadmap from evaluation graph evidence.", [Requirement(ProductIdentity.EvalDag)], "CreateNextEpicRoadmap", ExecutionPosture.OneShotAgentPrompt, [nextEpicRoadmap], [ProductIdentity.NextEpicRoadmap], [Effect("persist-next-epic-roadmap", EffectCategory.ProductPersistence, [ProductIdentity.EvalDag], [ProductIdentity.NextEpicRoadmap])], [ProductDependency(ProductIdentity.EvalDag, createEvalDag, createRoadmap)]),
                Transition(workflow, refreshRoadmap, "Refresh next epic roadmap status against repository evidence.", [Requirement(ProductIdentity.NextEpicRoadmap)], "UpdateRoadmap", ExecutionPosture.ReadOnlyPrompt, [refreshedNextEpicRoadmap], [ProductIdentity.NextEpicRoadmap], [Effect("record-refreshed-next-epic-roadmap", EffectCategory.Evidence, [ProductIdentity.NextEpicRoadmap], [ProductIdentity.NextEpicRoadmap])], [ProductDependency(ProductIdentity.NextEpicRoadmap, createRoadmap, refreshRoadmap)]),
                Transition(workflow, createEpic, "Create prepared epic from next epic roadmap evidence.", [Requirement(ProductIdentity.NextEpicRoadmap)], "CreateNextEpicImplementationSpec", ExecutionPosture.OneShotAgentPrompt, [preparedEpic], [ProductIdentity.PreparedEpic], [Effect("persist-eval-prepared-epic", EffectCategory.ProductPersistence, [ProductIdentity.NextEpicRoadmap], [ProductIdentity.PreparedEpic])], [ProductDependency(ProductIdentity.NextEpicRoadmap, createRoadmap, createEpic)]),
                Transition(workflow, generateSpecs, "Generate milestone deep dives from EvalRoadmap prepared epic.", [Requirement(ProductIdentity.PreparedEpic)], "GenerateMilestoneDeepDivesForEpic", ExecutionPosture.OneShotAgentPrompt, [specs], [ProductIdentity.MilestoneSpecificationSet], [Effect("persist-eval-milestone-specifications", EffectCategory.ProductPersistence, [ProductIdentity.PreparedEpic], [ProductIdentity.MilestoneSpecificationSet])], [ProductDependency(ProductIdentity.PreparedEpic, createEpic, generateSpecs)]),
                Transition(workflow, verifyPlanEntry, "Verify the EvalRoadmap products satisfy the universal planning entry contract.", [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)], "VerifyPlanEntryContract", ExecutionPosture.ReadOnlyPrompt, [], [ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet], [Effect("record-eval-plan-entry-evidence", EffectCategory.Evidence, [ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet], [])], [ProductDependency(ProductIdentity.PreparedEpic, createEpic, verifyPlanEntry), ProductDependency(ProductIdentity.MilestoneSpecificationSet, generateSpecs, verifyPlanEntry)]),
            ],
            [preparedEpic, specs],
            Gate("EvalRoadmap.Exit", "Prepared epic and milestone specification products are valid for planning.", ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet),
            downstream,
            Completion("EvalRoadmap.Complete", ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet),
            Blocker("EvalRoadmap.Blocked"),
            Recovery("EvalRoadmap.Recovery"));
    }

    public static WorkflowDefinition CreatePlan()
    {
        WorkflowIdentity workflow = WorkflowIdentity.Plan;
        WorkflowIdentity downstream = WorkflowIdentity.Execute;
        var writePlan = new WorkflowTransitionIdentity("WriteExecutablePlan");
        var generateAdversarialProjection = new WorkflowTransitionIdentity("GenerateAdversarialProjection");
        var runAdversarialReview = new WorkflowTransitionIdentity("RunAdversarialReview");
        var revisePlan = new WorkflowTransitionIdentity("RevisePlan");
        var generateOperationalContext = new WorkflowTransitionIdentity("GenerateOperationalContext");
        var collectDetails = new WorkflowTransitionIdentity("CollectExecutionDetails");
        var generateMilestones = new WorkflowTransitionIdentity("GenerateExecutionMilestones");
        var refineDetails = new WorkflowTransitionIdentity("RefineExecutionDetails");
        var verifyExecuteEntry = new WorkflowTransitionIdentity("VerifyExecuteEntryContract");

        ProductDefinition draftPlan = Product(ProductIdentity.ExecutablePlan, workflow, writePlan, workflow, "draft executable plan representation");
        ProductDefinition adversarialProjection = Product(ProductIdentity.AdversarialProjection, workflow, generateAdversarialProjection, workflow, "adversarial projection representation");
        ProductDefinition adversarialReview = Product(ProductIdentity.AdversarialReview, workflow, runAdversarialReview, workflow, "adversarial review representation");
        ProductDefinition executablePlan = Product(ProductIdentity.ExecutablePlan, workflow, revisePlan, downstream, "validated executable plan representation");
        ProductDefinition executionDetails = Product(ProductIdentity.ExecutionDetails, workflow, collectDetails, downstream, "execution details representation");
        ProductDefinition refinedDetails = Product(ProductIdentity.ExecutionDetails, workflow, refineDetails, downstream, "refined execution details representation");
        ProductDefinition operationalContext = Product(ProductIdentity.OperationalContext, workflow, generateOperationalContext, downstream, "operational context representation");
        ProductDefinition milestoneSet = Product(ProductIdentity.ExecutionMilestoneSet, workflow, generateMilestones, downstream, "execution milestone set representation");
        ProductDefinition executionReadiness = Product(ProductIdentity.ExecutionReadiness, workflow, verifyExecuteEntry, downstream, "execution readiness representation");

        return new WorkflowDefinition(
            workflow,
            "Convert roadmap products into an executable plan and execution-ready operational products.",
            [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)],
            Gate("Plan.Entry", "Prepared epic and milestone specifications are valid and current.", ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet),
            [
                Stage(
                    "Planning",
                    "Create the executable implementation plan from roadmap products.",
                    [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)],
                    [ProductIdentity.ExecutablePlan],
                    [],
                    [writePlan],
                    ["Plan Validation"]),
                Stage(
                    "Plan Validation",
                    "Critically evaluate and revise the executable plan.",
                    [Requirement(ProductIdentity.ExecutablePlan)],
                    [ProductIdentity.AdversarialProjection, ProductIdentity.AdversarialReview, ProductIdentity.ExecutablePlan],
                    [ProductDependency(ProductIdentity.ExecutablePlan, writePlan, generateAdversarialProjection)],
                    [generateAdversarialProjection, runAdversarialReview, revisePlan],
                    ["Execution Preparation"]),
                Stage(
                    "Execution Preparation",
                    "Create the companion execution products required by Execute.",
                    [Requirement(ProductIdentity.ExecutablePlan)],
                    [ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet],
                    [ProductDependency(ProductIdentity.ExecutablePlan, revisePlan, generateOperationalContext)],
                    [generateOperationalContext, collectDetails, generateMilestones, refineDetails],
                    ["Workflow Completion"]),
                Stage(
                    "Workflow Completion",
                    "Verify the execution entry contract is satisfied.",
                    [Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.OperationalContext), Requirement(ProductIdentity.ExecutionDetails), Requirement(ProductIdentity.ExecutionMilestoneSet)],
                    [ProductIdentity.ExecutionReadiness],
                    [
                        ProductDependency(ProductIdentity.OperationalContext, generateOperationalContext, verifyExecuteEntry),
                        ProductDependency(ProductIdentity.ExecutionDetails, refineDetails, verifyExecuteEntry),
                        ProductDependency(ProductIdentity.ExecutionMilestoneSet, generateMilestones, verifyExecuteEntry),
                    ],
                    [verifyExecuteEntry],
                    []),
            ],
            [
                Transition(workflow, writePlan, "Write the first executable plan from roadmap products.", [Requirement(ProductIdentity.PreparedEpic), Requirement(ProductIdentity.MilestoneSpecificationSet)], "WritePlan", ExecutionPosture.WarmSession, [draftPlan], [ProductIdentity.ExecutablePlan], [Effect("persist-draft-plan", EffectCategory.ProductPersistence, [ProductIdentity.PreparedEpic, ProductIdentity.MilestoneSpecificationSet], [ProductIdentity.ExecutablePlan]), Effect("publish-agents-write-plan", EffectCategory.Publication, [ProductIdentity.ExecutablePlan], [ProductIdentity.ExecutablePlan], order: 1)], []),
                Transition(workflow, generateAdversarialProjection, "Generate an adversarial projection for plan validation.", [Requirement(ProductIdentity.ExecutablePlan)], "GenerateAdversarialProjection", ExecutionPosture.ScopedArtifactOperation, [adversarialProjection], [ProductIdentity.AdversarialProjection], [Effect("persist-adversarial-projection", EffectCategory.ProductPersistence, [ProductIdentity.ExecutablePlan], [ProductIdentity.AdversarialProjection]), Effect("publish-agents-adversarial-projection", EffectCategory.Publication, [ProductIdentity.AdversarialProjection], [ProductIdentity.AdversarialProjection], order: 1)], [ProductDependency(ProductIdentity.ExecutablePlan, writePlan, generateAdversarialProjection)]),
                Transition(workflow, runAdversarialReview, "Run the adversarial review against the executable plan and projection.", [Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.AdversarialProjection)], "RunAdversarialReview", ExecutionPosture.ReadOnlyPrompt, [adversarialReview], [ProductIdentity.AdversarialReview], [Effect("persist-adversarial-review", EffectCategory.Evidence, [ProductIdentity.ExecutablePlan, ProductIdentity.AdversarialProjection], [ProductIdentity.AdversarialReview])], [ProductDependency(ProductIdentity.AdversarialProjection, generateAdversarialProjection, runAdversarialReview)]),
                Transition(workflow, revisePlan, "Revise the executable plan using adversarial review evidence.", [Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.AdversarialReview)], "ReviewAndRevisePlan", ExecutionPosture.WarmSession, [executablePlan], [ProductIdentity.ExecutablePlan], [Effect("persist-reviewed-plan", EffectCategory.ProductPersistence, [ProductIdentity.ExecutablePlan, ProductIdentity.AdversarialReview], [ProductIdentity.ExecutablePlan])], [ProductDependency(ProductIdentity.AdversarialReview, runAdversarialReview, revisePlan)]),
                Transition(workflow, generateOperationalContext, "Generate operational context from the validated executable plan.", [Requirement(ProductIdentity.ExecutablePlan)], "SeedOperationalContext", ExecutionPosture.ScopedArtifactOperation, [operationalContext], [ProductIdentity.OperationalContext], [Effect("persist-operational-context", EffectCategory.ProductPersistence, [ProductIdentity.ExecutablePlan], [ProductIdentity.OperationalContext]), Effect("publish-agents-operational-context", EffectCategory.Publication, [ProductIdentity.OperationalContext], [ProductIdentity.OperationalContext], order: 1)], [ProductDependency(ProductIdentity.ExecutablePlan, revisePlan, generateOperationalContext)]),
                Transition(workflow, collectDetails, "Collect execution details needed by implementation agents.", [Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.MilestoneSpecificationSet)], "CollectDetails", ExecutionPosture.ScopedArtifactOperation, [executionDetails], [ProductIdentity.ExecutionDetails], [Effect("persist-execution-details", EffectCategory.ProductPersistence, [ProductIdentity.ExecutablePlan, ProductIdentity.MilestoneSpecificationSet], [ProductIdentity.ExecutionDetails]), Effect("publish-agents-execution-details", EffectCategory.Publication, [ProductIdentity.ExecutionDetails], [ProductIdentity.ExecutionDetails], order: 1)], [ProductDependency(ProductIdentity.ExecutablePlan, revisePlan, collectDetails)]),
                Transition(workflow, generateMilestones, "Generate execution milestone checklists from the executable plan.", [Requirement(ProductIdentity.ExecutablePlan)], "ExtractMilestones", ExecutionPosture.ScopedArtifactOperation, [milestoneSet], [ProductIdentity.ExecutionMilestoneSet], [Effect("persist-execution-milestones", EffectCategory.ProductPersistence, [ProductIdentity.ExecutablePlan], [ProductIdentity.ExecutionMilestoneSet]), Effect("publish-agents-execution-milestones", EffectCategory.Publication, [ProductIdentity.ExecutionMilestoneSet], [ProductIdentity.ExecutionMilestoneSet], order: 1)], [ProductDependency(ProductIdentity.ExecutablePlan, revisePlan, generateMilestones)]),
                Transition(workflow, refineDetails, "Refine execution details against the generated execution milestones.", [Requirement(ProductIdentity.ExecutionDetails), Requirement(ProductIdentity.ExecutionMilestoneSet)], "ExtractDetails", ExecutionPosture.ScopedArtifactOperation, [refinedDetails], [ProductIdentity.ExecutionDetails], [Effect("persist-refined-execution-details", EffectCategory.ProductPersistence, [ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet], [ProductIdentity.ExecutionDetails]), Effect("publish-agents-refined-details", EffectCategory.Publication, [ProductIdentity.ExecutionDetails], [ProductIdentity.ExecutionDetails], order: 1)], [ProductDependency(ProductIdentity.ExecutionDetails, collectDetails, refineDetails), ProductDependency(ProductIdentity.ExecutionMilestoneSet, generateMilestones, refineDetails)]),
                Transition(workflow, verifyExecuteEntry, "Verify the planning products satisfy the universal execution entry contract.", [Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.OperationalContext), Requirement(ProductIdentity.ExecutionDetails), Requirement(ProductIdentity.ExecutionMilestoneSet)], "VerifyExecuteEntryContract", ExecutionPosture.ReadOnlyPrompt, [executionReadiness], [ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet, ProductIdentity.ExecutionReadiness], [Effect("record-execute-entry-evidence", EffectCategory.Evidence, [ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet], [ProductIdentity.ExecutionReadiness]), Effect("record-plan-parent-gitlink", EffectCategory.Git, [ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet, ProductIdentity.ExecutionReadiness], [ProductIdentity.ExecutionReadiness], order: 1)], [ProductDependency(ProductIdentity.OperationalContext, generateOperationalContext, verifyExecuteEntry), ProductDependency(ProductIdentity.ExecutionDetails, refineDetails, verifyExecuteEntry), ProductDependency(ProductIdentity.ExecutionMilestoneSet, generateMilestones, verifyExecuteEntry)]),
            ],
            [executablePlan, operationalContext, refinedDetails, milestoneSet, executionReadiness],
            Gate("Plan.Exit", "Executable plan, operational context, details, milestone set, and readiness are valid for execution.", ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet, ProductIdentity.ExecutionReadiness),
            downstream,
            Completion("Plan.Complete", ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet, ProductIdentity.ExecutionReadiness),
            Blocker("Plan.Blocked"),
            Recovery("Plan.Recovery"));
    }

    public static WorkflowDefinition CreateExecute()
    {
        WorkflowIdentity workflow = WorkflowIdentity.Execute;
        var verifyReadiness = new WorkflowTransitionIdentity("VerifyExecutionReadiness");
        var generateDecision = new WorkflowTransitionIdentity("GenerateDecision");
        var transferDecision = new WorkflowTransitionIdentity("TransferDecisionSession");
        var continueDecision = new WorkflowTransitionIdentity("ContinueDecisionSession");
        var executeSlice = new WorkflowTransitionIdentity("ExecuteImplementationSlice");
        var generateHandoff = new WorkflowTransitionIdentity("GenerateHandoff");
        var updateContext = new WorkflowTransitionIdentity("UpdateOperationalContext");
        var publishRepository = new WorkflowTransitionIdentity("PublishRepositoryState");
        var evaluateCommit = new WorkflowTransitionIdentity("EvaluateCommit");
        var evaluateMilestoneCompletion = new WorkflowTransitionIdentity("EvaluateMilestoneCompletion");
        var runNonImplementationReview = new WorkflowTransitionIdentity("RunNonImplementationReview");
        var runCompletionCertification = new WorkflowTransitionIdentity("RunCompletionCertification");
        var interpretCompletionRoute = new WorkflowTransitionIdentity("InterpretCompletionRoute");
        var verifyWorkflowExit = new WorkflowTransitionIdentity("VerifyWorkflowExitGate");

        ProductDefinition readiness = Product(ProductIdentity.ExecutionReadiness, workflow, verifyReadiness, workflow, "execution readiness representation");
        ProductDefinition decision = Product(ProductIdentity.DecisionSet, workflow, generateDecision, workflow, "decision set representation");
        ProductDefinition transferredDecision = Product(ProductIdentity.DecisionSet, workflow, transferDecision, workflow, "transferred decision set representation");
        ProductDefinition continuedDecision = Product(ProductIdentity.DecisionSet, workflow, continueDecision, workflow, "continued decision set representation");
        ProductDefinition implementationSlice = Product(ProductIdentity.ImplementationSlice, workflow, executeSlice, workflow, "implementation slice evidence representation");
        ProductDefinition repositoryChanges = Product(ProductIdentity.RepositoryChanges, workflow, executeSlice, workflow, "repository change evidence representation");
        ProductDefinition handoff = Product(ProductIdentity.ExecutionHandoff, workflow, generateHandoff, workflow, "execution handoff representation");
        ProductDefinition operationalDelta = Product(ProductIdentity.OperationalDelta, workflow, updateContext, workflow, "operational delta representation");
        ProductDefinition publishedRepositoryChanges = Product(ProductIdentity.RepositoryChanges, workflow, publishRepository, workflow, "published repository state representation");
        ProductDefinition commitEvaluation = Product(ProductIdentity.CompletionEvidence, workflow, evaluateCommit, workflow, "commit evaluation representation");
        ProductDefinition milestoneCompletion = Product(ProductIdentity.CompletionEvidence, workflow, evaluateMilestoneCompletion, workflow, "milestone completion evaluation representation");
        ProductDefinition nonImplementationReview = Product(ProductIdentity.CompletionEvidence, workflow, runNonImplementationReview, workflow, "non-implementation review representation");
        ProductDefinition completionEvidence = Product(ProductIdentity.CompletionEvidence, workflow, runCompletionCertification, workflow, "completion certification representation");
        ProductDefinition completionRoute = Product(ProductIdentity.CompletionRoute, workflow, interpretCompletionRoute, workflow, "completion route representation");
        ProductDefinition certifiedCompletion = Product(ProductIdentity.CertifiedCompletion, workflow, verifyWorkflowExit, workflow, "certified completion representation");

        return new WorkflowDefinition(
            workflow,
            "Iterate implementation slices until completion can be certified and durably closed.",
            [Requirement(ProductIdentity.ExecutionReadiness), Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.OperationalContext), Requirement(ProductIdentity.ExecutionDetails), Requirement(ProductIdentity.ExecutionMilestoneSet)],
            Gate("Execute.Entry", "Planning products are valid and execution is ready.", ProductIdentity.ExecutionReadiness, ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet),
            [
                Stage("Execution Readiness", "Verify all execution inputs are usable.", [Requirement(ProductIdentity.ExecutionReadiness), Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.OperationalContext), Requirement(ProductIdentity.ExecutionDetails), Requirement(ProductIdentity.ExecutionMilestoneSet)], [ProductIdentity.ExecutionReadiness], [], [verifyReadiness], ["Implementation Planning"]),
                Stage("Implementation Planning", "Create, transfer, or continue the decision context for the next slice.", [Requirement(ProductIdentity.ExecutionReadiness)], [ProductIdentity.DecisionSet], [ProductDependency(ProductIdentity.ExecutionReadiness, verifyReadiness, generateDecision)], [generateDecision, transferDecision, continueDecision], ["Implementation"]),
                Stage("Implementation", "Execute one implementation slice and record repository evidence.", [Requirement(ProductIdentity.DecisionSet)], [ProductIdentity.ImplementationSlice, ProductIdentity.RepositoryChanges], [ProductDependency(ProductIdentity.DecisionSet, generateDecision, executeSlice)], [executeSlice], ["Execution Continuity"]),
                Stage("Execution Continuity", "Produce handoff, update operational context, publish state, and evaluate repository progress.", [Requirement(ProductIdentity.ImplementationSlice), Requirement(ProductIdentity.RepositoryChanges)], [ProductIdentity.ExecutionHandoff, ProductIdentity.OperationalDelta, ProductIdentity.RepositoryChanges, ProductIdentity.CompletionEvidence], [ProductDependency(ProductIdentity.ImplementationSlice, executeSlice, generateHandoff)], [generateHandoff, updateContext, publishRepository, evaluateCommit], ["Completion"]),
                Stage("Completion", "Evaluate milestone state, review non-implementation work, certify completion, and route the next step.", [Requirement(ProductIdentity.RepositoryChanges), Requirement(ProductIdentity.ExecutionHandoff), Requirement(ProductIdentity.ExecutionMilestoneSet)], [ProductIdentity.CompletionEvidence, ProductIdentity.CompletionRoute], [ProductDependency(ProductIdentity.RepositoryChanges, publishRepository, evaluateMilestoneCompletion), ProductDependency(ProductIdentity.ExecutionMilestoneSet, verifyReadiness, evaluateMilestoneCompletion)], [evaluateMilestoneCompletion, runNonImplementationReview, runCompletionCertification, interpretCompletionRoute], ["Execution Readiness", "Workflow Completion"]),
                Stage("Workflow Completion", "Expose certified closure as the terminal workflow state.", [Requirement(ProductIdentity.CompletionEvidence), Requirement(ProductIdentity.CompletionRoute)], [ProductIdentity.CertifiedCompletion], [ProductDependency(ProductIdentity.CompletionRoute, interpretCompletionRoute, verifyWorkflowExit)], [verifyWorkflowExit], []),
            ],
            [
                Transition(workflow, verifyReadiness, "Verify execution input products and outstanding blockers.", [Requirement(ProductIdentity.ExecutionReadiness), Requirement(ProductIdentity.ExecutablePlan), Requirement(ProductIdentity.OperationalContext), Requirement(ProductIdentity.ExecutionDetails), Requirement(ProductIdentity.ExecutionMilestoneSet)], "VerifyExecutionReadiness", ExecutionPosture.ReadOnlyPrompt, [readiness], [ProductIdentity.ExecutionReadiness], [Effect("record-execution-readiness", EffectCategory.Evidence, [ProductIdentity.ExecutionReadiness, ProductIdentity.ExecutablePlan, ProductIdentity.OperationalContext, ProductIdentity.ExecutionDetails, ProductIdentity.ExecutionMilestoneSet], [ProductIdentity.ExecutionReadiness])], []),
                Transition(workflow, generateDecision, "Generate or continue the decision set for the next implementation slice.", [Requirement(ProductIdentity.ExecutionReadiness)], "GenerateDecision", ExecutionPosture.DecisionSession, [decision], [ProductIdentity.DecisionSet], [Effect("persist-decision-set", EffectCategory.DecisionRecording, [ProductIdentity.ExecutionReadiness], [ProductIdentity.DecisionSet])], [ProductDependency(ProductIdentity.ExecutionReadiness, verifyReadiness, generateDecision)]),
                Transition(workflow, transferDecision, "Transfer the active decision session into the execution context.", [Requirement(ProductIdentity.DecisionSet)], "TransferDecisionSession", ExecutionPosture.DecisionSession, [transferredDecision], [ProductIdentity.DecisionSet], [Effect("record-decision-transfer", EffectCategory.DecisionRecording, [ProductIdentity.DecisionSet], [ProductIdentity.DecisionSet])], [ProductDependency(ProductIdentity.DecisionSet, generateDecision, transferDecision)]),
                Transition(workflow, continueDecision, "Continue an existing decision session when prior decision context is still active.", [Requirement(ProductIdentity.DecisionSet)], "ContinueDecisionSession", ExecutionPosture.DecisionSession, [continuedDecision], [ProductIdentity.DecisionSet], [Effect("record-decision-continuation", EffectCategory.DecisionRecording, [ProductIdentity.DecisionSet], [ProductIdentity.DecisionSet])], [ProductDependency(ProductIdentity.DecisionSet, transferDecision, continueDecision)]),
                Transition(workflow, executeSlice, "Execute the selected implementation slice and capture repository delta.", [Requirement(ProductIdentity.DecisionSet)], "ExecuteImplementationSlice", ExecutionPosture.WarmSession, [implementationSlice, repositoryChanges], [ProductIdentity.ImplementationSlice, ProductIdentity.RepositoryChanges], [Effect("record-implementation-slice", EffectCategory.Evidence, [ProductIdentity.DecisionSet], [ProductIdentity.ImplementationSlice, ProductIdentity.RepositoryChanges])], [ProductDependency(ProductIdentity.DecisionSet, generateDecision, executeSlice)]),
                Transition(workflow, generateHandoff, "Generate execution handoff from the completed implementation slice.", [Requirement(ProductIdentity.ImplementationSlice)], "GenerateHandoff", ExecutionPosture.OneShotAgentPrompt, [handoff], [ProductIdentity.ExecutionHandoff], [Effect("persist-execution-handoff", EffectCategory.ProductPersistence, [ProductIdentity.ImplementationSlice], [ProductIdentity.ExecutionHandoff])], [ProductDependency(ProductIdentity.ImplementationSlice, executeSlice, generateHandoff)]),
                Transition(workflow, updateContext, "Update operational context from implementation evidence and handoff.", [Requirement(ProductIdentity.ImplementationSlice), Requirement(ProductIdentity.ExecutionHandoff)], "UpdateOperationalContext", ExecutionPosture.OneShotAgentPrompt, [operationalDelta], [ProductIdentity.OperationalDelta], [Effect("persist-operational-delta", EffectCategory.ProductPersistence, [ProductIdentity.ImplementationSlice, ProductIdentity.ExecutionHandoff], [ProductIdentity.OperationalDelta])], [ProductDependency(ProductIdentity.ExecutionHandoff, generateHandoff, updateContext)]),
                Transition(workflow, publishRepository, "Publish repository state evidence after implementation and handoff generation.", [Requirement(ProductIdentity.RepositoryChanges), Requirement(ProductIdentity.ExecutionHandoff)], "PublishRepositoryState", ExecutionPosture.ScopedArtifactOperation, [publishedRepositoryChanges], [ProductIdentity.RepositoryChanges], [Effect("publish-repository-state", EffectCategory.Publication, [ProductIdentity.RepositoryChanges, ProductIdentity.ExecutionHandoff], [ProductIdentity.RepositoryChanges])], [ProductDependency(ProductIdentity.ExecutionHandoff, generateHandoff, publishRepository)]),
                Transition(workflow, evaluateCommit, "Evaluate whether the repository delta represents substantive progress.", [Requirement(ProductIdentity.RepositoryChanges)], "EvaluateCommit", ExecutionPosture.ReadOnlyPrompt, [commitEvaluation], [ProductIdentity.CompletionEvidence], [Effect("record-commit-evaluation", EffectCategory.Git, [ProductIdentity.RepositoryChanges], [ProductIdentity.CompletionEvidence])], [ProductDependency(ProductIdentity.RepositoryChanges, publishRepository, evaluateCommit)]),
                Transition(workflow, evaluateMilestoneCompletion, "Evaluate milestone completion evidence after repository publication.", [Requirement(ProductIdentity.RepositoryChanges), Requirement(ProductIdentity.ExecutionHandoff), Requirement(ProductIdentity.ExecutionMilestoneSet)], "EvaluateMilestoneCompletion", ExecutionPosture.ReadOnlyPrompt, [milestoneCompletion], [ProductIdentity.CompletionEvidence], [Effect("record-milestone-completion", EffectCategory.Evidence, [ProductIdentity.RepositoryChanges, ProductIdentity.ExecutionHandoff, ProductIdentity.ExecutionMilestoneSet], [ProductIdentity.CompletionEvidence])], [ProductDependency(ProductIdentity.RepositoryChanges, publishRepository, evaluateMilestoneCompletion), ProductDependency(ProductIdentity.ExecutionMilestoneSet, verifyReadiness, evaluateMilestoneCompletion)]),
                Transition(workflow, runNonImplementationReview, "Run non-implementation review before completion certification.", [Requirement(ProductIdentity.RepositoryChanges), Requirement(ProductIdentity.ExecutionHandoff)], "RunNonImplementationReview", ExecutionPosture.ReadOnlyPrompt, [nonImplementationReview], [ProductIdentity.CompletionEvidence], [Effect("record-non-implementation-review", EffectCategory.Evidence, [ProductIdentity.RepositoryChanges, ProductIdentity.ExecutionHandoff], [ProductIdentity.CompletionEvidence])], [ProductDependency(ProductIdentity.RepositoryChanges, publishRepository, runNonImplementationReview)]),
                Transition(workflow, runCompletionCertification, "Run completion evaluation and certification review.", [Requirement(ProductIdentity.RepositoryChanges), Requirement(ProductIdentity.ExecutionHandoff)], "RunCompletionCertification", ExecutionPosture.OneShotAgentPrompt, [completionEvidence], [ProductIdentity.CompletionEvidence], [Effect("record-completion-evidence", EffectCategory.Evidence, [ProductIdentity.RepositoryChanges, ProductIdentity.ExecutionHandoff], [ProductIdentity.CompletionEvidence])], [ProductDependency(ProductIdentity.RepositoryChanges, publishRepository, runCompletionCertification)]),
                Transition(workflow, interpretCompletionRoute, "Interpret completion evidence as continue, block, wait, fail, or certified complete.", [Requirement(ProductIdentity.CompletionEvidence)], "InterpretCompletionRoute", ExecutionPosture.ReadOnlyPrompt, [completionRoute], [ProductIdentity.CompletionRoute], [Effect("record-completion-route", EffectCategory.Evidence, [ProductIdentity.CompletionEvidence], [ProductIdentity.CompletionRoute])], [ProductDependency(ProductIdentity.CompletionEvidence, runCompletionCertification, interpretCompletionRoute)]),
                Transition(workflow, verifyWorkflowExit, "Verify the workflow exit gate only when completion evidence is certified.", [Requirement(ProductIdentity.CompletionEvidence), Requirement(ProductIdentity.CompletionRoute)], "VerifyWorkflowExitGate", ExecutionPosture.ReadOnlyPrompt, [certifiedCompletion], [ProductIdentity.CertifiedCompletion], [Effect("record-certified-completion", EffectCategory.Archive, [ProductIdentity.CompletionEvidence, ProductIdentity.CompletionRoute], [ProductIdentity.CertifiedCompletion])], [ProductDependency(ProductIdentity.CompletionRoute, interpretCompletionRoute, verifyWorkflowExit)]),
            ],
            [certifiedCompletion],
            Gate("Execute.Exit", "Certified completion is valid and discoverable.", ProductIdentity.CertifiedCompletion),
            null,
            Completion("Execute.Complete", ProductIdentity.CertifiedCompletion),
            Blocker("Execute.Blocked"),
            Recovery("Execute.Recovery"));
    }

    private static WorkflowStageDefinition Stage(
        string identity,
        string purpose,
        IReadOnlyList<ProductRequirement> requiredProducts,
        IReadOnlyList<ProductIdentity> producedProducts,
        IReadOnlyList<TransitionDependency> dependencies,
        IReadOnlyList<WorkflowTransitionIdentity> transitions,
        IReadOnlyList<string> allowedSuccessors) =>
        new(
            new WorkflowStageIdentity(identity),
            purpose,
            requiredProducts,
            producedProducts,
            dependencies,
            transitions,
            allowedSuccessors.Select(successor => new WorkflowStageIdentity(successor)).ToArray(),
            Gate($"{identity}.Entry", $"Entry requirements for {identity}."),
            Gate($"{identity}.Complete", $"Completion requirements for {identity}."),
            [RuntimeOutcomeKind.Completed, RuntimeOutcomeKind.Blocked, RuntimeOutcomeKind.Failed, RuntimeOutcomeKind.Cancelled]);

    private static WorkflowTransitionDefinition Transition(
        WorkflowIdentity workflow,
        WorkflowTransitionIdentity identity,
        string purpose,
        IReadOnlyList<ProductRequirement> inputs,
        string promptIdentity,
        ExecutionPosture posture,
        IReadOnlyList<ProductDefinition> producedProducts,
        IReadOnlyList<ProductIdentity> outputProducts,
        IReadOnlyList<EffectDefinition> effects,
        IReadOnlyList<TransitionDependency> dependencies) =>
        new(
            identity,
            purpose,
            inputs,
            Gate($"{identity}.Input", $"Required input products for {identity}.", inputs.Select(input => input.Product).ToArray()),
            promptIdentity,
            posture,
            producedProducts,
            Gate($"{identity}.Output", $"Validated output products for {identity}.", outputProducts.ToArray()),
            producedProducts.Count == 0 ? ["NoOutputProductValidator"] : producedProducts.Select(product => $"{product.Identity}Validator").ToArray(),
            effects,
            dependencies,
            [],
            Recovery($"{workflow}.{identity}.Recovery"));

    private static ProductDefinition Product(
        ProductIdentity identity,
        WorkflowIdentity workflow,
        WorkflowTransitionIdentity transition,
        WorkflowIdentity consumer,
        string representation) =>
        new(
            identity,
            workflow,
            transition,
            [consumer],
            "repository-owned orchestration evidence",
            "canonical workflow contract",
            ProductLifecycle.Active,
            ProductValidationState.Valid,
            ProductFreshness.Fresh,
            [],
            [representation]);

    private static ProductRequirement Requirement(ProductIdentity product, bool requiresFreshness = true) =>
        new(product, requiresFreshness ? DependencyStrength.FreshnessSensitive : DependencyStrength.Required, requiresFreshness, "canonical product authority", $"Require {product}.");

    private static GateDefinition Gate(string identity, string purpose, params ProductIdentity[] products) =>
        new(
            new GateIdentity(identity),
            purpose,
            products.Length == 0
                ? [new GateRequirementDefinition($"{identity}.Explainable", "Gate has explainable workflow requirements.", null, DependencyStrength.Required, true)]
                : products.Select(product => new GateRequirementDefinition($"{identity}.{product}", $"Validate {product}.", product, DependencyStrength.Required, true)).ToArray(),
            "canonical gate authority",
            "Unsatisfied, blocked, waiting, invalid, or ambiguous requirements stop progress with evidence.");

    private static EffectDefinition Effect(
        string identity,
        EffectCategory category,
        IReadOnlyList<ProductIdentity> inputs,
        IReadOnlyList<ProductIdentity> outputs,
        int order = 0) =>
        new(
            new EffectIdentity(identity),
            category,
            "Run after output gate satisfaction.",
            inputs,
            outputs,
            order,
            "Failure is persisted as transition evidence and prevents transition completion.");

    private static TransitionDependency ProductDependency(
        ProductIdentity product,
        WorkflowTransitionIdentity producer,
        WorkflowTransitionIdentity consumer,
        DependencyStrength strength = DependencyStrength.Required) =>
        new(
            $"{consumer}.{product}",
            DependencyTargetKind.Product,
            strength,
            producer.Value,
            consumer.Value,
            product,
            null,
            null,
            null,
            strength == DependencyStrength.Invalidating
                ? "Producer changes invalidate the consumer product."
                : "Consumer requires the producer product.");

    private static WorkflowCompletionDefinition Completion(string identity, params ProductIdentity[] products) =>
        new(identity, "Workflow completes when its exit gate and required products are satisfied.", Gate($"{identity}.Gate", "Completion gate.", products), products);

    private static BlockerDefinition Blocker(string identity) =>
        new(identity, "Blocking conditions preserve evidence and require explicit recovery or user action.", RuntimeOutcomeKind.Blocked, ["Resolve blocker evidence before retrying."]);

    private static RecoveryDefinition Recovery(string identity) =>
        new(identity, "Recovery is based on repository-owned evidence and never assumes prompt success is completion.", ["restart", "resume", "rerun"], ["silent repair", "discard state"]);
}
