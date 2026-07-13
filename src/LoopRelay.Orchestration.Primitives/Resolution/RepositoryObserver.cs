using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Resolution;

public sealed class RepositoryObserver(
    IStorageVerifier? _storageVerifier = null,
    ICanonicalPersistenceProjection? _persistenceProjection = null)
{
    // The per-type authority split (M3): these products are hand-editable collaboration files
    // observed from the working tree; the filesystem decides their presence and content, and
    // ledger rows never substitute for or mask them. Every other product identity is a
    // system-owned fact whose ledger row remains authoritative. This set must cover exactly
    // the identities observed by AddProductIfPresent/AddExecutionMilestoneSetIfPresent.
    private static readonly HashSet<ProductIdentity> FilesystemAuthoritativeProducts =
    [
        ProductIdentity.EvaluationIntent,
        ProductIdentity.DependencyInventory,
        ProductIdentity.HypothesisInventory,
        ProductIdentity.ArchitecturalCatalog,
        ProductIdentity.EvalDag,
        ProductIdentity.NextEpicRoadmap,
        ProductIdentity.PreparedEpic,
        ProductIdentity.MilestoneSpecificationSet,
        ProductIdentity.ExecutablePlan,
        ProductIdentity.AdversarialProjection,
        ProductIdentity.OperationalContext,
        ProductIdentity.ExecutionDetails,
        ProductIdentity.ExecutionMilestoneSet,
        ProductIdentity.DecisionSet,
        ProductIdentity.ImplementationSlice,
        ProductIdentity.ExecutionHandoff,
        ProductIdentity.OperationalDelta,
        ProductIdentity.CompletionEvidence,
    ];

    private readonly IStorageVerifier _storageVerifier = _storageVerifier ?? new FileSystemStorageVerifier();
    private readonly ICanonicalPersistenceProjection _persistenceProjection =
        _persistenceProjection ?? new CanonicalPersistenceProjection();

    public async Task<RepositoryObservation> ObserveAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(repositoryPath);
        StorageVerificationResult verification = await _storageVerifier.VerifyAsync(root, cancellationToken);
        var authority = new StorageAuthoritySnapshot(
            verification.Authority,
            verification.UsableAuthority,
            verification.UsableAuthority ? "observed" : "unusable",
            verification.Evidence);

        string agents = Path.Combine(root, OrchestrationArtifactPaths.AgentsDirectory);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = root,
        };
        CanonicalPersistenceReadModel persistenceReadModel = verification.UsableAuthority
            ? await _persistenceProjection.ProjectAsync(repository, cancellationToken)
            : CanonicalPersistenceReadModel.Empty;
        CanonicalWorkflowPersistenceSnapshot canonicalSnapshot = persistenceReadModel.Workflow;
        HashSet<string> attemptsWithUnsettledRequiredEffects = persistenceReadModel
            .UnsettledRequiredEffectAttempts.ToHashSet(StringComparer.Ordinal);
        HashSet<string> certifiedTerminalAttempts = persistenceReadModel.CertifiedTerminalAttempts
            .ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> evalIntentPaths = ListRelativeFiles(root, Path.Combine(agents, "evals"), "*.md");
        var products = new List<ObservedProduct>();

        AddProductIfPresent(products, root, ProductIdentity.EvaluationIntent, evalIntentPaths, WorkflowIdentity.EvalRoadmap, WorkflowIdentity.EvalRoadmap);
        AddProductIfPresent(products, root, ProductIdentity.DependencyInventory, [EvaluationArtifactPaths.DependencyInventory], WorkflowIdentity.EvalRoadmap, WorkflowIdentity.EvalRoadmap);
        AddProductIfPresent(products, root, ProductIdentity.HypothesisInventory, [EvaluationArtifactPaths.HypothesisInventory], WorkflowIdentity.EvalRoadmap, WorkflowIdentity.EvalRoadmap);
        AddProductIfPresent(products, root, ProductIdentity.ArchitecturalCatalog, [EvaluationArtifactPaths.ArchitecturalCatalog], WorkflowIdentity.EvalRoadmap, WorkflowIdentity.EvalRoadmap);
        AddProductIfPresent(products, root, ProductIdentity.EvalDag, [EvaluationArtifactPaths.EvalDag], WorkflowIdentity.EvalRoadmap, WorkflowIdentity.EvalRoadmap);
        AddProductIfPresent(products, root, ProductIdentity.NextEpicRoadmap, [EvaluationArtifactPaths.NextEpicRoadmap], WorkflowIdentity.EvalRoadmap, WorkflowIdentity.EvalRoadmap);
        AddProductIfPresent(products, root, ProductIdentity.PreparedEpic, [OrchestrationArtifactPaths.AgentsDirectory + "/epic.md"], WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan);
        AddProductIfPresent(products, root, ProductIdentity.MilestoneSpecificationSet, ListRelativeFiles(root, Path.Combine(agents, "specs"), "*.md"), WorkflowIdentity.TraditionalRoadmap, WorkflowIdentity.Plan);
        AddProductIfPresent(products, root, ProductIdentity.ExecutablePlan, [OrchestrationArtifactPaths.Plan], WorkflowIdentity.Plan, WorkflowIdentity.Execute);
        AddProductIfPresent(products, root, ProductIdentity.AdversarialProjection, [PlanPromptContext.AdversarialPlanReviewProjectionPath], WorkflowIdentity.Plan, WorkflowIdentity.Plan);
        AddProductIfPresent(products, root, ProductIdentity.OperationalContext, [OrchestrationArtifactPaths.OperationalContext], WorkflowIdentity.Plan, WorkflowIdentity.Execute);
        AddProductIfPresent(products, root, ProductIdentity.ExecutionDetails, [OrchestrationArtifactPaths.Details], WorkflowIdentity.Plan, WorkflowIdentity.Execute);
        AddExecutionMilestoneSetIfPresent(products, root, ListRelativeFiles(root, Path.Combine(agents, "milestones"), OrchestrationArtifactPaths.MilestoneSearchPattern));
        AddProductIfPresent(products, root, ProductIdentity.DecisionSet, ExecuteDecisionPaths(root, agents), WorkflowIdentity.Execute, WorkflowIdentity.Execute);
        AddProductIfPresent(products, root, ProductIdentity.ImplementationSlice, ExecuteImplementationSlicePaths(root, agents), WorkflowIdentity.Execute, WorkflowIdentity.Execute);
        AddProductIfPresent(products, root, ProductIdentity.ExecutionHandoff, ExecuteHandoffPaths(root, agents), WorkflowIdentity.Execute, WorkflowIdentity.Execute);
        AddProductIfPresent(products, root, ProductIdentity.OperationalDelta, ExecuteOperationalDeltaPaths(root, agents), WorkflowIdentity.Execute, WorkflowIdentity.Execute);
        AddProductIfPresent(products, root, ProductIdentity.CompletionEvidence, ExecuteCompletionEvidencePaths(root, agents), WorkflowIdentity.Execute, WorkflowIdentity.Execute);
        foreach (ProductRecord product in canonicalSnapshot.Products)
        {
            // Collaboration-file products are filesystem-authoritative: the working tree decides
            // presence and content, so a ledger row can neither substitute for a missing file nor
            // mask a present one. System-owned facts keep the ledger row as authority.
            if (FilesystemAuthoritativeProducts.Contains(product.Identity))
            {
                continue;
            }

            products.RemoveAll(observed => observed.Product.Identity == product.Identity);
            products.Add(new ObservedProduct(
                product,
                GateUsable: (product.ValidationState is ProductValidationState.Valid or ProductValidationState.Unknown) &&
                    !attemptsWithUnsettledRequiredEffects.Contains(product.CausalIdentity) &&
                    (product.Identity != ProductIdentity.CertifiedCompletion ||
                        certifiedTerminalAttempts.Contains(product.CausalIdentity)),
                product.EvidenceLocations));
        }
        EnforceLiveDecisionRecommendation(products, root);

        AddCompletionArchiveProducts(products, root);

        IReadOnlyList<ObservedEvidence> evidence = products
            .SelectMany(product => product.Evidence.Select(location => new ObservedEvidence(
                product.Product.Identity.Value,
                location,
                product.Product.Authority,
                Ignored: false)))
            .Concat(canonicalSnapshot.TransitionEvidence.SelectMany(item => item.Evidence.Select(location => new ObservedEvidence(
                item.EventName,
                location,
                "canonical workflow persistence",
                Ignored: false))))
            .Concat(canonicalSnapshot.GateEvaluations.SelectMany(item => item.Evidence.Select(location => new ObservedEvidence(
                item.Gate.Value,
                location,
                "canonical workflow persistence",
                Ignored: false))))
            .Concat(canonicalSnapshot.EffectRecords.SelectMany(item => item.Evidence.Select(location => new ObservedEvidence(
                item.Effect.Value,
                location,
                "canonical workflow persistence",
                Ignored: false))))
            .Concat(persistenceReadModel.ChainBoundaries.SelectMany(item => item.Evidence.Select(location => new ObservedEvidence(
                item.ChainIdentity,
                location,
                "canonical workflow persistence",
                Ignored: false))))
            .Concat(canonicalSnapshot.RecoveryMarkers.SelectMany(item => item.Evidence.Select(location => new ObservedEvidence(
                item.MarkerId,
                location,
                "canonical transition recovery",
                Ignored: false))))
            .Concat(verification.Evidence.Select(location => new ObservedEvidence(
                "storage",
                location,
                verification.Authority.ToString(),
                Ignored: false)))
            .ToArray();

        IReadOnlyList<ObservedWorkflowState> workflowStates = ObservedWorkflowStates(canonicalSnapshot);
        workflowStates = AddInferredRoadmapWorkflowState(
            workflowStates,
            products,
            WorkflowIdentity.TraditionalRoadmap);
        if (evalIntentPaths.Count > 0)
        {
            workflowStates = AddInferredRoadmapWorkflowState(
                workflowStates,
                products,
                WorkflowIdentity.EvalRoadmap);
        }

        workflowStates = AddInferredPlanWorkflowState(workflowStates, products);
        workflowStates = AddInferredExecuteWorkflowState(workflowStates, products);

        return new RepositoryObservation(
            root,
            authority,
            WorkflowStates: workflowStates,
            Products: products,
            LifecycleRows: canonicalSnapshot.StageStates.Select(stageState => new ObservedLifecycleRow(
                $"{stageState.Workflow}:{stageState.Stage}",
                stageState.State.ToString(),
                stageState.Evidence)).Concat(canonicalSnapshot.Products.Select(product => new ObservedLifecycleRow(
                product.Identity.Value,
                product.Lifecycle.ToString(),
                product.EvidenceLocations))).Concat(canonicalSnapshot.RecoveryMarkers.Select(marker => new ObservedLifecycleRow(
                    $"TransitionRecovery:{marker.MarkerId}",
                    string.Join(",", marker.Recovery.SupportedActions),
                    marker.Evidence))).Concat(persistenceReadModel.UnsettledEffects.Select(effect => new ObservedLifecycleRow(
                    $"Effect:{effect.Identity}", effect.State, effect.Evidence))).ToArray(),
            Evidence: evidence,
            TransitionRuns: canonicalSnapshot.TransitionRuns.Select(run => new ObservedTransitionRun(
                run.Workflow,
                run.Stage,
                run.Transition,
                ToEligibilityState(run),
                canonicalSnapshot.Products
                    .Where(product => product.ProducerWorkflow == run.Workflow &&
                        product.ProducerTransition == run.Transition)
                    .Select(product => product.Identity)
                    .ToArray(),
                run.Evidence)).ToArray(),
            GitFacts: ObserveGit(root),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: evalIntentPaths,
            StorageVerification: verification);
    }

    private static IReadOnlyList<string> AddCompletionArchiveProducts(
        List<ObservedProduct> products,
        string root)
    {
        if (products.Any(product => product.Product.Identity == ProductIdentity.CertifiedCompletion))
        {
            return [];
        }

        CompletionArchiveRecord? archive = FindLatestCompletionArchive(root);
        if (archive is null)
        {
            return [];
        }

        AddObservedArchiveProduct(
            root,
            products,
            ProductIdentity.CompletionEvidence,
            new WorkflowTransitionIdentity("RunCompletionCertification"),
            archive.Evidence);
        return archive.Evidence;
    }

    private static void AddObservedArchiveProduct(
        string root,
        List<ObservedProduct> products,
        ProductIdentity identity,
        WorkflowTransitionIdentity producerTransition,
        IReadOnlyList<string> evidence)
    {
        // An archived representation is never selected over an active one: it only becomes
        // observable when no live observation exists for the identity.
        if (products.Any(observed => observed.Product.Identity == identity))
        {
            return;
        }
        var record = new ProductRecord(
            identity,
            WorkflowIdentity.Execute,
            producerTransition,
            [WorkflowIdentity.Execute],
            "repository-owned completion archive evidence",
            "completion archive observation",
            evidence,
            HashExistingFiles(root, evidence),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Archived,
            evidence);
        products.Add(new ObservedProduct(record, GateUsable: true, evidence));
    }

    private static IReadOnlyList<ObservedWorkflowState> ObservedWorkflowStates(
        CanonicalWorkflowPersistenceSnapshot canonicalSnapshot) =>
        canonicalSnapshot.WorkflowStates.Select(state => new ObservedWorkflowState(
            state.Workflow,
            state.State,
            state.CurrentStage,
            canonicalSnapshot.StageStates
                .Where(stageState => stageState.Workflow == state.Workflow &&
                    stageState.State == WorkflowResolutionState.Completed)
                .Select(stageState => stageState.Stage)
                .ToArray(),
            canonicalSnapshot.Warnings
                .Where(warning => warning.Workflow == state.Workflow)
                .Select(warning => new ResolutionWarning(
                    warning.Category,
                    warning.Concern,
                    warning.Authority,
                    warning.Remediation,
                    warning.Evidence))
                .ToArray(),
            state.Evidence)).ToArray();

    private static IReadOnlyList<ObservedWorkflowState> AddInferredRoadmapWorkflowState(
        IReadOnlyList<ObservedWorkflowState> workflowStates,
        IReadOnlyList<ObservedProduct> products,
        WorkflowIdentity workflow)
    {
        if (workflowStates.Any(state => state.Workflow == workflow))
        {
            return workflowStates;
        }

        var observation = new RepositoryObservation(
            RepositoryPath: string.Empty,
            StorageAuthority: new StorageAuthoritySnapshot(StorageAuthorityKind.FilesystemExport, true, "inference", []),
            WorkflowStates: [],
            Products: products,
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: false, HasWorkingTreeChanges: false, CurrentBranch: "unknown", Evidence: []),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: new StorageVerificationResult(
                StorageAuthorityKind.FilesystemExport,
                UsableAuthority: true,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: [],
                BlockingConditions: [],
                Evidence: []));
        RoadmapWorkflowState roadmap = RoadmapWorkflowStateClassifier.Classify(observation, workflow);
        ObservedWorkflowState? inferred = roadmap.Kind switch
        {
            RoadmapWorkflowStateKind.HypothesisInventoryInProgress => InferredRoadmapState(
                workflow,
                "Hypothesis Inventory",
                EvalRoadmapCompletedStages("Dependency Inventory"),
                roadmap.Evidence),
            RoadmapWorkflowStateKind.ArchitecturalCatalogInProgress => InferredRoadmapState(
                workflow,
                "Architectural Catalog",
                EvalRoadmapCompletedStages("Hypothesis Inventory"),
                roadmap.Evidence),
            RoadmapWorkflowStateKind.EvalDagInProgress => InferredRoadmapState(
                workflow,
                "Eval DAG",
                EvalRoadmapCompletedStages("Architectural Catalog"),
                roadmap.Evidence),
            RoadmapWorkflowStateKind.NextEpicRoadmapInProgress => InferredRoadmapState(
                workflow,
                "Next Epic Roadmap",
                EvalRoadmapCompletedStages("Eval DAG"),
                roadmap.Evidence),
            RoadmapWorkflowStateKind.ActiveEpicPreparationInProgress => InferredRoadmapState(
                workflow,
                "Active Epic Preparation",
                EvalRoadmapCompletedStages("Next Epic Roadmap"),
                roadmap.Evidence),
            RoadmapWorkflowStateKind.MilestoneSpecificationInProgress => InferredRoadmapState(
                workflow,
                "Milestone Specification",
                RoadmapCompletedStages(workflow, includeMilestoneSpecification: false),
                roadmap.Evidence),
            RoadmapWorkflowStateKind.PlanEntryVerificationInProgress => InferredRoadmapState(
                workflow,
                "Workflow Completion",
                RoadmapCompletedStages(workflow, includeMilestoneSpecification: true),
                roadmap.Evidence),
            _ => null,
        };

        return inferred is null
            ? workflowStates
            : workflowStates.Concat([inferred]).ToArray();
    }

    private static IReadOnlyList<string> RoadmapCompletedStages(
        WorkflowIdentity workflow,
        bool includeMilestoneSpecification)
    {
        string[] stages = workflow == WorkflowIdentity.EvalRoadmap
            ?
            [
                "Evaluation Foundation",
                "Dependency Inventory",
                "Hypothesis Inventory",
                "Architectural Catalog",
                "Eval DAG",
                "Next Epic Roadmap",
                "Active Epic Preparation",
            ]
            :
            [
                "Roadmap Context",
                "Strategic Initiative Selection",
                "Epic Preparation",
            ];
        return includeMilestoneSpecification
            ? stages.Concat(["Milestone Specification"]).ToArray()
            : stages;
    }

    private static IReadOnlyList<string> EvalRoadmapCompletedStages(string throughStage)
    {
        string[] stages =
        [
            "Evaluation Foundation",
            "Dependency Inventory",
            "Hypothesis Inventory",
            "Architectural Catalog",
            "Eval DAG",
            "Next Epic Roadmap",
            "Active Epic Preparation",
            "Milestone Specification",
        ];
        int index = Array.IndexOf(stages, throughStage);
        return index < 0
            ? []
            : stages.Take(index + 1).ToArray();
    }

    private static ObservedWorkflowState InferredRoadmapState(
        WorkflowIdentity workflow,
        string stage,
        IReadOnlyList<string> completedStages,
        IReadOnlyList<string> evidence) =>
        new(
            workflow,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity(stage),
            completedStages.Select(item => new WorkflowStageIdentity(item)).ToArray(),
            [],
            evidence
                .Concat([$"repository-observation:{workflow}:artifact-inferred-state"])
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private static IReadOnlyList<ObservedWorkflowState> AddInferredPlanWorkflowState(
        IReadOnlyList<ObservedWorkflowState> workflowStates,
        IReadOnlyList<ObservedProduct> products)
    {
        if (workflowStates.Any(state => state.Workflow == WorkflowIdentity.Plan))
        {
            return workflowStates;
        }

        var observation = new RepositoryObservation(
            RepositoryPath: string.Empty,
            StorageAuthority: new StorageAuthoritySnapshot(StorageAuthorityKind.FilesystemExport, true, "inference", []),
            WorkflowStates: [],
            Products: products,
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: false, HasWorkingTreeChanges: false, CurrentBranch: "unknown", Evidence: []),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: new StorageVerificationResult(
                StorageAuthorityKind.FilesystemExport,
                UsableAuthority: true,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: [],
                BlockingConditions: [],
                Evidence: []));
        PlanWorkflowState plan = PlanWorkflowStateClassifier.Classify(observation);
        ObservedWorkflowState? inferred = plan.Kind switch
        {
            PlanWorkflowStateKind.PlanAuthored => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Plan Validation",
                ["Planning"],
                plan.Evidence),
            PlanWorkflowStateKind.ValidationInProgress => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Plan Validation",
                ["Planning"],
                plan.Evidence),
            PlanWorkflowStateKind.ValidationComplete => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Execution Preparation",
                ["Planning", "Plan Validation"],
                plan.Evidence),
            PlanWorkflowStateKind.PartialExecutionProducts => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Execution Preparation",
                ["Planning", "Plan Validation"],
                plan.Evidence),
            PlanWorkflowStateKind.ExecutionPreparationInProgress => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Execution Preparation",
                ["Planning", "Plan Validation"],
                plan.Evidence),
            PlanWorkflowStateKind.ExecutionPreparationComplete => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Workflow Completion",
                ["Planning", "Plan Validation", "Execution Preparation"],
                plan.Evidence),
            PlanWorkflowStateKind.ExecutionReady => InferredPlanState(
                WorkflowResolutionState.Resumable,
                "Workflow Completion",
                ["Planning", "Plan Validation", "Execution Preparation"],
                plan.Evidence),
            _ => null,
        };

        return inferred is null
            ? workflowStates
            : workflowStates.Concat([inferred]).ToArray();
    }

    private static ObservedWorkflowState InferredPlanState(
        WorkflowResolutionState state,
        string stage,
        IReadOnlyList<string> completedStages,
        IReadOnlyList<string> evidence) =>
        new(
            WorkflowIdentity.Plan,
            state,
            new WorkflowStageIdentity(stage),
            completedStages.Select(item => new WorkflowStageIdentity(item)).ToArray(),
            [],
            evidence
                .Concat(["repository-observation:Plan:artifact-inferred-state"])
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private static IReadOnlyList<ObservedWorkflowState> AddInferredExecuteWorkflowState(
        IReadOnlyList<ObservedWorkflowState> workflowStates,
        IReadOnlyList<ObservedProduct> products)
    {
        if (workflowStates.Any(state => state.Workflow == WorkflowIdentity.Execute))
        {
            return workflowStates;
        }

        var observation = new RepositoryObservation(
            RepositoryPath: string.Empty,
            StorageAuthority: new StorageAuthoritySnapshot(StorageAuthorityKind.FilesystemExport, true, "inference", []),
            WorkflowStates: [],
            Products: products,
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: false, HasWorkingTreeChanges: false, CurrentBranch: "unknown", Evidence: []),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: new StorageVerificationResult(
                StorageAuthorityKind.FilesystemExport,
                UsableAuthority: true,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: [],
                BlockingConditions: [],
                Evidence: []));
        ExecuteWorkflowState execute = ExecuteWorkflowStateClassifier.Classify(observation);
        ObservedWorkflowState? inferred = execute.Kind switch
        {
            ExecuteWorkflowStateKind.DecisionPlanningInProgress => InferredExecuteState(
                "Implementation Planning",
                ["Execution Readiness"],
                execute.Evidence),
            ExecuteWorkflowStateKind.ImplementationInProgress => InferredExecuteState(
                "Implementation",
                ["Execution Readiness", "Implementation Planning"],
                execute.Evidence),
            ExecuteWorkflowStateKind.ExecutionContinuityInProgress => InferredExecuteState(
                "Execution Continuity",
                ["Execution Readiness", "Implementation Planning", "Implementation"],
                execute.Evidence),
            ExecuteWorkflowStateKind.CompletionInProgress => InferredExecuteState(
                "Completion",
                ["Execution Readiness", "Implementation Planning", "Implementation", "Execution Continuity"],
                execute.Evidence),
            ExecuteWorkflowStateKind.WorkflowCompletionInProgress => InferredExecuteState(
                "Workflow Completion",
                ["Execution Readiness", "Implementation Planning", "Implementation", "Execution Continuity", "Completion"],
                execute.Evidence),
            _ => null,
        };

        return inferred is null
            ? workflowStates
            : workflowStates.Concat([inferred]).ToArray();
    }

    private static ObservedWorkflowState InferredExecuteState(
        string stage,
        IReadOnlyList<string> completedStages,
        IReadOnlyList<string> evidence) =>
        new(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity(stage),
            completedStages.Select(item => new WorkflowStageIdentity(item)).ToArray(),
            [],
            evidence
                .Concat(["repository-observation:Execute:artifact-inferred-state"])
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private static void AddProductIfPresent(
        List<ObservedProduct> products,
        string root,
        ProductIdentity identity,
        IReadOnlyList<string> relativePaths,
        WorkflowIdentity producer,
        WorkflowIdentity consumer)
    {
        string[] existing = relativePaths
            .Where(path => File.Exists(Path.Combine(root, Normalize(path))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (existing.Length == 0)
        {
            return;
        }

        WorkflowTransitionIdentity producerTransition = new($"Observed{identity}");
        var record = new ProductRecord(
            identity,
            producer,
            producerTransition,
            [consumer],
            "repository-owned observed artifact evidence",
            "repository observation",
            existing,
            HashExistingFiles(root, existing),
            ProductFreshness.Fresh,
            ProductValidationState.Unknown,
            ProductLifecycle.Active,
            existing);
        products.Add(new ObservedProduct(record, GateUsable: true, existing));
    }

    private static void AddExecutionMilestoneSetIfPresent(
        List<ObservedProduct> products,
        string root,
        IReadOnlyList<string> relativePaths)
    {
        string[] existing = relativePaths
            .Where(path => File.Exists(Path.Combine(root, Normalize(path))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (existing.Length == 0)
        {
            return;
        }

        ExecutionMilestoneGateResult gate = ExecutionMilestoneGate.Evaluate(root, existing);
        var record = new ProductRecord(
            ProductIdentity.ExecutionMilestoneSet,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("ObservedExecutionMilestoneSet"),
            [WorkflowIdentity.Execute],
            "repository-owned observed artifact evidence",
            "repository observation",
            existing,
            HashExistingFiles(root, existing),
            ProductFreshness.Fresh,
            gate.MilestoneSetValidationState,
            ProductLifecycle.Active,
            existing);
        products.Add(new ObservedProduct(record, GateUsable: gate.ReadinessSatisfied, gate.Evidence));
    }

    private static void EnforceLiveDecisionRecommendation(List<ObservedProduct> products, string root)
    {
        int index = products.FindIndex(product => product.Product.Identity == ProductIdentity.DecisionSet);
        if (index < 0)
        {
            return;
        }

        string promptPath = Path.Combine(root, Normalize(OrchestrationArtifactPaths.Decisions));
        string recommendationPath = Path.Combine(
            root,
            Normalize(OrchestrationArtifactPaths.ExecutionRecommendation));
        if (!File.Exists(promptPath))
        {
            products.RemoveAt(index);
            return;
        }

        ObservedProduct observed = products[index];
        try
        {
            if (!File.Exists(recommendationPath))
            {
                throw new InvalidDataException("The live execution recommendation is missing.");
            }

            ExecutionRecommendationContract.ValidatePair(
                File.ReadAllText(promptPath),
                File.ReadAllText(recommendationPath));
            IReadOnlyList<string> evidence = observed.Evidence
                .Concat([OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            products[index] = new ObservedProduct(
                observed.Product with
                {
                    ValidationState = ProductValidationState.Valid,
                    StorageRepresentations =
                        [OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation],
                    EvidenceLocations = evidence,
                },
                GateUsable: true,
                evidence);
        }
        catch (InvalidDataException)
        {
            IReadOnlyList<string> evidence = observed.Evidence
                .Concat([OrchestrationArtifactPaths.Decisions])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            products[index] = new ObservedProduct(
                observed.Product with
                {
                    ValidationState = ProductValidationState.Invalid,
                    StorageRepresentations =
                        [OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation],
                    EvidenceLocations = evidence,
                },
                GateUsable: false,
                evidence);
        }
    }

    private static IReadOnlyList<string> ExecuteDecisionPaths(string root, string agents) =>
        OrderedDistinct(new[] { OrchestrationArtifactPaths.Decisions }.Concat(
            ListRelativeFiles(root, Path.Combine(agents, "decisions"), OrchestrationArtifactPaths.HistoricalDecisionSearchPattern)));

    private static IReadOnlyList<string> ExecuteImplementationSlicePaths(string root, string agents) =>
        OrderedDistinct(
            ListRelativeFilesRecursive(root, Path.Combine(agents, "evidence", "execution"), "*.md")
                .Where(path => !Path.GetFileName(path).StartsWith("execution-trust-posture.", StringComparison.Ordinal)));

    private static IReadOnlyList<string> ExecuteHandoffPaths(string root, string agents) =>
        OrderedDistinct(new[] { OrchestrationArtifactPaths.LiveHandoff }.Concat(
            ListRelativeFiles(root, Path.Combine(agents, "handoffs"), OrchestrationArtifactPaths.HistoricalHandoffSearchPattern)));

    private static IReadOnlyList<string> ExecuteOperationalDeltaPaths(string root, string agents) =>
        OrderedDistinct(new[] { OrchestrationArtifactPaths.OperationalDelta }.Concat(
            ListRelativeFiles(root, Path.Combine(agents, "deltas"), OrchestrationArtifactPaths.HistoricalDeltaSearchPattern)));

    private static IReadOnlyList<string> ExecuteCompletionEvidencePaths(string root, string agents) =>
        OrderedDistinct(
            ListRelativeFilesRecursive(root, Path.Combine(agents, "evidence", "evaluations"), "*.md")
                .Concat(ListRelativeFilesRecursive(root, Path.Combine(agents, "evidence", "blockers"), "*.md"))
                .Concat(ListRelativeFiles(root, Path.Combine(agents, "review"), "*.md")));

    private static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> paths) =>
        paths
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ListRelativeFiles(string root, string directory, string pattern)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ListRelativeFilesRecursive(string root, string directory, string pattern)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static CompletionArchiveRecord? FindLatestCompletionArchive(string root)
    {
        string archiveRoot = Path.Combine(root, Normalize(".agents/archive/epics"));
        if (!Directory.Exists(archiveRoot))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(archiveRoot, "*.md", SearchOption.TopDirectoryOnly)
            .Select(path => CompletionArchiveCandidate(root, path))
            .Where(candidate => candidate is not null)
            .OrderByDescending(candidate => candidate!.Index)
            .FirstOrDefault();
    }

    private static CompletionArchiveRecord? CompletionArchiveCandidate(string root, string synthesisPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(synthesisPath);
        if (!int.TryParse(fileName, out int index))
        {
            return null;
        }

        string archiveDirectory = Path.Combine(
            root,
            Normalize($".agents/archive/epics/{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        if (!Directory.Exists(archiveDirectory))
        {
            return null;
        }

        string relativeSynthesis = Relative(root, synthesisPath);
        string relativeDirectory = Relative(root, archiveDirectory);
        IReadOnlyList<string> archivedFiles = Directory
            .EnumerateFiles(archiveDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Relative(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<string> evidence = new[] { relativeSynthesis, relativeDirectory }
            .Concat(archivedFiles)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new CompletionArchiveRecord(index, evidence);
    }

    private static ObservedGitFacts ObserveGit(string root)
    {
        string git = Path.Combine(root, ".git");
        bool isRepository = Directory.Exists(git) || File.Exists(git);
        string agents = Path.Combine(root, ".agents");
        string agentsTopology = !Directory.Exists(agents)
            ? "missing"
            : Directory.Exists(Path.Combine(agents, ".git")) || File.Exists(Path.Combine(agents, ".git"))
                ? "nested-repository"
                : "ordinary-directory";
        if (!isRepository)
        {
            return new ObservedGitFacts(false, false, "unknown", [], false, agentsTopology);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("status");
            startInfo.ArgumentList.Add("--porcelain=v1");
            startInfo.ArgumentList.Add("--branch");
            startInfo.ArgumentList.Add("--untracked-files=normal");
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return new ObservedGitFacts(true, false, "unknown", [".git", "git-status:start-failed"], false, agentsTopology);
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000) || process.ExitCode != 0)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                string diagnostic = string.IsNullOrWhiteSpace(error) ? "failed" : error.Trim();
                return new ObservedGitFacts(true, false, "unknown", [".git", $"git-status:{diagnostic}"], false, agentsTopology);
            }

            string[] lines = output.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string heading = lines.FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal)) ?? "## unknown";
            bool detached = heading.Contains("HEAD (no branch)", StringComparison.Ordinal) ||
                heading.StartsWith("## HEAD (detached", StringComparison.Ordinal);
            string branch = detached
                ? "detached"
                : heading[3..].Split(new[] { "...", " " }, StringSplitOptions.RemoveEmptyEntries)[0];
            bool dirty = lines.Any(line => !line.StartsWith("## ", StringComparison.Ordinal));
            string[] evidence = [".git", $"git:branch={branch}", $"git:dirty={dirty}", $"git:agents={agentsTopology}"];
            return new ObservedGitFacts(true, dirty, branch, evidence, detached, agentsTopology);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ObservedGitFacts(true, false, "unknown", [".git", $"git-status:{exception.GetType().Name}"], false, agentsTopology);
        }
    }

    private static string HashExistingFiles(string root, IReadOnlyList<string> relativePaths)
    {
        var builder = new StringBuilder();
        foreach (string relativePath in relativePaths.Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(root, Normalize(relativePath));
            if (!File.Exists(path))
            {
                continue;
            }

            Append(builder, relativePath);
            Append(builder, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value) =>
        builder
            .Append(value.Length)
            .Append(':')
            .Append(value)
            .AppendLine();

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static TransitionEligibilityState ToEligibilityState(CanonicalTransitionRunRecord run) =>
        run.State switch
        {
            TransitionDurableState.Completed => TransitionEligibilityState.Completed,
            TransitionDurableState.InputUnsatisfied => TransitionEligibilityState.MissingRequiredInput,
            TransitionDurableState.Ambiguous => TransitionEligibilityState.Ambiguous,
            TransitionDurableState.Failed => TransitionEligibilityState.Invalid,
            _ => TransitionEligibilityState.Waiting,
        };

    private sealed record CompletionArchiveRecord(
        int Index,
        IReadOnlyList<string> Evidence);
}

public sealed class FileSystemStorageVerifier : IStorageVerifier
{
    private readonly WorkspaceStorageVerifierAdapter adapter = new();

    public async Task<StorageVerificationResult> VerifyAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        string database = Path.Combine(Path.GetFullPath(repositoryPath),
            LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
        bool agents = Directory.Exists(Path.Combine(repositoryPath, OrchestrationArtifactPaths.AgentsDirectory));
        if (!File.Exists(database))
        {
            return new StorageVerificationResult(
                agents ? StorageAuthorityKind.FilesystemExport : StorageAuthorityKind.Missing,
                true, [], [], [], [], [], [], [],
                agents ? [OrchestrationArtifactPaths.AgentsDirectory] : []);
        }
        StorageVerificationResult result = await adapter.VerifyAsync(repositoryPath, cancellationToken);
        return agents && result.Authority == StorageAuthorityKind.CanonicalSqlite
            ? result with { Authority = StorageAuthorityKind.Mixed }
            : result;
    }
}
