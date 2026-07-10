using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Resolution;

public sealed class RepositoryObserver(IStorageVerifier? _storageVerifier = null)
{
    private const string WorkspaceDatabaseRelativePath = ".LoopRelay/persistence/looprelay.sqlite3";

    private static readonly JsonSerializerOptions DecisionResumeJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> PreUnificationExecutionHandoffStates = new(StringComparer.Ordinal)
    {
        "GenerateOperationalContext",
        "OperationalContextReady",
        "GenerateExecutionPrompt",
        "ExecutionPromptReady",
        "ExecutionLoop",
        "ExecutionBlocked",
    };

    private readonly IStorageVerifier _storageVerifier = _storageVerifier ?? new FileSystemStorageVerifier();

    public async Task<RepositoryObservation> ObserveAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(repositoryPath);
        StorageVerificationResult verification = await _storageVerifier.VerifyAsync(root, cancellationToken);
        var authority = new StorageAuthoritySnapshot(
            verification.Authority,
            verification.UsableAuthority,
            verification.UsableAuthority ? "observed" : "blocked",
            verification.Evidence);

        string agents = Path.Combine(root, OrchestrationArtifactPaths.AgentsDirectory);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = root,
        };
        CanonicalWorkflowPersistenceSnapshot canonicalSnapshot = verification.UsableAuthority
            ? await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync(cancellationToken)
            : new CanonicalWorkflowPersistenceSnapshot([], [], [], [], [], [], [], [], [], []);
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
            products.RemoveAll(observed => observed.Product.Identity == product.Identity);
            products.Add(new ObservedProduct(
                product,
                GateUsable: product.ValidationState is ProductValidationState.Valid or ProductValidationState.Unknown,
                product.EvidenceLocations));
        }

        IReadOnlyList<string> completionArchiveEvidence = AddCompletionArchiveProducts(products, root);
        IReadOnlyList<ObservedLifecycleRow> decisionResumeRows = ObserveDecisionSessionResume(repository, verification);
        IReadOnlyList<ObservedLifecycleRow> preUnificationRows = ObservePreUnificationRoadmapEvidence(repository, verification);

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
            .Concat(canonicalSnapshot.WorkflowChainRuns.SelectMany(item => item.Evidence.Select(location => new ObservedEvidence(
                item.ChainIdentity,
                location,
                "canonical workflow persistence",
                Ignored: false))))
            .Concat(decisionResumeRows.SelectMany(row => row.Evidence.Select(location => new ObservedEvidence(
                row.Identity,
                location,
                "decision session resume observation",
                Ignored: false))))
            .Concat(preUnificationRows.SelectMany(row => row.Evidence.Select(location => new ObservedEvidence(
                row.Identity,
                location,
                "pre-unification roadmap observation",
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
        workflowStates = AddInferredExecuteCompletionState(workflowStates, completionArchiveEvidence);
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
                product.EvidenceLocations))).Concat(decisionResumeRows).Concat(preUnificationRows).ToArray(),
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

    private static IReadOnlyList<ObservedLifecycleRow> ObserveDecisionSessionResume(
        Repository repository,
        StorageVerificationResult verification)
    {
        var rows = new List<ObservedLifecycleRow>();
        string legacyRelativePath = ".LoopRelay/decision-session.json";
        string legacyPath = Path.Combine(repository.Path, Normalize(legacyRelativePath));
        if (File.Exists(legacyPath))
        {
            rows.Add(new ObservedLifecycleRow(
                "DecisionSessionResume:LegacyFile",
                ReadDecisionResumeState(legacyPath) is null ? "Invalid" : "Present",
                [legacyRelativePath]));
        }

        if (verification.UsableAuthority)
        {
            string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
            if (File.Exists(databasePath))
            {
                ObservedLifecycleRow? sqliteRow = ObserveSqliteDecisionResume(databasePath);
                if (sqliteRow is not null)
                {
                    rows.Add(sqliteRow);
                }
            }
        }

        return rows;
    }

    private static IReadOnlyList<ObservedLifecycleRow> ObservePreUnificationRoadmapEvidence(
        Repository repository,
        StorageVerificationResult verification)
    {
        var rows = new List<ObservedLifecycleRow>();
        AddFilesystemEvidenceRow(
            rows,
            repository.Path,
            "PreUnificationRoadmapState:Filesystem",
            [".agents/state.json", ".agents/state.md"]);
        AddFilesystemExecutionHandoffStateRow(rows, repository.Path);
        AddFilesystemEvidenceRow(
            rows,
            repository.Path,
            "PreUnificationTransitionJournal:Filesystem",
            [".agents/journal/transitions.jsonl"]);
        AddFilesystemEvidenceRow(
            rows,
            repository.Path,
            "PreUnificationArtifactLifecycle:Filesystem",
            [".agents/artifacts/lifecycle.json", ".agents/artifacts/lifecycle.md"]);

        if (!verification.UsableAuthority)
        {
            return rows;
        }

        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return rows;
        }

        rows.AddRange(ObservePreUnificationSqliteRows(databasePath));
        return rows;
    }

    private static void AddFilesystemEvidenceRow(
        List<ObservedLifecycleRow> rows,
        string root,
        string identity,
        IReadOnlyList<string> relativePaths)
    {
        IReadOnlyList<string> evidence = relativePaths
            .Where(path => File.Exists(Path.Combine(root, Normalize(path))))
            .ToArray();
        if (evidence.Count == 0)
        {
            return;
        }

        rows.Add(new ObservedLifecycleRow(identity, "Present", evidence));
    }

    private static IReadOnlyList<ObservedLifecycleRow> ObservePreUnificationSqliteRows(string databasePath)
    {
        try
        {
            using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            connection.Open();
            var rows = new List<ObservedLifecycleRow>();
            AddSqliteTableEvidenceRow(
                rows,
                connection,
                "roadmap_state",
                "PreUnificationRoadmapState:Sqlite",
                "SELECT COUNT(*) FROM roadmap_state WHERE id = 1;");
            AddSqliteExecutionHandoffStateRow(rows, connection);
            AddSqliteTableEvidenceRow(
                rows,
                connection,
                "transition_journal",
                "PreUnificationTransitionJournal:Sqlite",
                "SELECT COUNT(*) FROM transition_journal;");
            AddSqliteTableEvidenceRow(
                rows,
                connection,
                "artifact_lifecycle",
                "PreUnificationArtifactLifecycle:Sqlite",
                "SELECT COUNT(*) FROM artifact_lifecycle;");
            return rows;
        }
        catch (SqliteException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private static void AddSqliteTableEvidenceRow(
        List<ObservedLifecycleRow> rows,
        SqliteConnection connection,
        string table,
        string identity,
        string countCommandText)
    {
        if (!ObservationTableExists(connection, table))
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = countCommandText;
        object? scalar = command.ExecuteScalar();
        if (Convert.ToInt64(scalar) <= 0)
        {
            return;
        }

        rows.Add(new ObservedLifecycleRow(
            identity,
            "Present",
            [$"{WorkspaceDatabaseRelativePath}:{table}"]));
    }

    private static void AddFilesystemExecutionHandoffStateRow(
        List<ObservedLifecycleRow> rows,
        string root)
    {
        string relativePath = ".agents/state.json";
        string path = Path.Combine(root, Normalize(relativePath));
        if (!File.Exists(path) ||
            !TryReadLegacyRoadmapCurrentState(File.ReadAllText(path), out string state) ||
            !PreUnificationExecutionHandoffStates.Contains(state))
        {
            return;
        }

        rows.Add(new ObservedLifecycleRow(
            "PreUnificationExecutionHandoffState:Filesystem",
            $"MigrationOnly:{state}",
            [relativePath]));
    }

    private static void AddSqliteExecutionHandoffStateRow(
        List<ObservedLifecycleRow> rows,
        SqliteConnection connection)
    {
        if (!ObservationTableExists(connection, "roadmap_state"))
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT document_json FROM roadmap_state WHERE id = 1;";
        object? scalar = command.ExecuteScalar();
        string json = scalar is null or DBNull ? string.Empty : Convert.ToString(scalar) ?? string.Empty;
        if (!TryReadLegacyRoadmapCurrentState(json, out string state) ||
            !PreUnificationExecutionHandoffStates.Contains(state))
        {
            return;
        }

        rows.Add(new ObservedLifecycleRow(
            "PreUnificationExecutionHandoffState:Sqlite",
            $"MigrationOnly:{state}",
            [$"{WorkspaceDatabaseRelativePath}:roadmap_state"]));
    }

    private static bool TryReadLegacyRoadmapCurrentState(
        string json,
        out string state)
    {
        state = string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetPropertyCaseInsensitive(root, "currentState", out JsonElement currentState) ||
                currentState.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            state = currentState.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(state);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetPropertyCaseInsensitive(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static ObservedLifecycleRow? ObserveSqliteDecisionResume(string databasePath)
    {
        try
        {
            using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
            connection.Open();
            if (!ObservationTableExists(connection, "decision_session_resume"))
            {
                return null;
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT document_json FROM decision_session_resume WHERE id = 1;";
            object? scalar = command.ExecuteScalar();
            if (scalar is null or DBNull)
            {
                return null;
            }

            string json = Convert.ToString(scalar) ?? string.Empty;
            DecisionSessionResumeState? state = ReadDecisionResumeState(json);
            return new ObservedLifecycleRow(
                "DecisionSessionResume:Sqlite",
                state is null ? "Invalid" : "Present",
                [$"{WorkspaceDatabaseRelativePath}:decision_session_resume"]);
        }
        catch (SqliteException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static DecisionSessionResumeState? ReadDecisionResumeState(string pathOrJson)
    {
        try
        {
            string json = File.Exists(pathOrJson)
                ? File.ReadAllText(pathOrJson)
                : pathOrJson;
            DecisionSessionResumeState? state =
                JsonSerializer.Deserialize<DecisionSessionResumeState>(json, DecisionResumeJson);
            return state is not null &&
                state.SchemaVersion == DecisionSessionResumeState.CurrentSchemaVersion &&
                !string.IsNullOrWhiteSpace(state.ThreadId)
                    ? state
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
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
        AddObservedArchiveProduct(
            root,
            products,
            ProductIdentity.CertifiedCompletion,
            new WorkflowTransitionIdentity("VerifyWorkflowExitGate"),
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
        products.RemoveAll(observed => observed.Product.Identity == identity);
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
            canonicalSnapshot.Blockers
                .Where(blocker => blocker.Workflow == state.Workflow && blocker.ResolvedAt is null)
                .Select(blocker => blocker.Blocker)
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

    private static IReadOnlyList<ObservedWorkflowState> AddInferredExecuteCompletionState(
        IReadOnlyList<ObservedWorkflowState> workflowStates,
        IReadOnlyList<string> completionArchiveEvidence)
    {
        if (completionArchiveEvidence.Count == 0 ||
            workflowStates.Any(state => state.Workflow == WorkflowIdentity.Execute))
        {
            return workflowStates;
        }

        var state = new ObservedWorkflowState(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Completed,
            CurrentStage: null,
            CompletedStages:
            [
                new WorkflowStageIdentity("Execution Readiness"),
                new WorkflowStageIdentity("Implementation Planning"),
                new WorkflowStageIdentity("Implementation"),
                new WorkflowStageIdentity("Execution Continuity"),
                new WorkflowStageIdentity("Completion"),
                new WorkflowStageIdentity("Workflow Completion"),
            ],
            Blockers: [],
            Evidence: completionArchiveEvidence
                .Concat(["repository-observation:Execute:completion-archive-closed-state"])
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        return workflowStates.Concat([state]).ToArray();
    }

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

    private static bool ObservationTableExists(SqliteConnection connection, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = command.ExecuteScalar();
        return Convert.ToInt64(scalar) == 1;
    }

    private static ObservedGitFacts ObserveGit(string root)
    {
        string git = Path.Combine(root, ".git");
        bool isRepository = Directory.Exists(git) || File.Exists(git);
        string branch = "unknown";
        string head = Path.Combine(git, "HEAD");
        if (File.Exists(head))
        {
            string text = File.ReadAllText(head).Trim();
            const string prefix = "ref: refs/heads/";
            branch = text.StartsWith(prefix, StringComparison.Ordinal)
                ? text[prefix.Length..]
                : text;
        }

        return new ObservedGitFacts(
            isRepository,
            HasWorkingTreeChanges: false,
            branch,
            isRepository ? [".git"] : []);
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
            TransitionDurableState.Stalled => TransitionEligibilityState.Blocked,
            TransitionDurableState.Blocked => TransitionEligibilityState.Blocked,
            TransitionDurableState.Cancelled => TransitionEligibilityState.Blocked,
            TransitionDurableState.Failed => TransitionEligibilityState.Invalid,
            _ => TransitionEligibilityState.Waiting,
        };

    private sealed record CompletionArchiveRecord(
        int Index,
        IReadOnlyList<string> Evidence);
}

public sealed class FileSystemStorageVerifier : IStorageVerifier
{
    private const string DatabaseRelativePath = ".LoopRelay/persistence/looprelay.sqlite3";

    public Task<StorageVerificationResult> VerifyAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(repositoryPath);
        string database = Path.Combine(root, DatabaseRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string agents = Path.Combine(root, OrchestrationArtifactPaths.AgentsDirectory);
        bool hasDatabase = File.Exists(database);
        bool hasAgents = Directory.Exists(agents);
        var evidence = new List<string>();
        if (hasDatabase)
        {
            evidence.Add(DatabaseRelativePath);
        }

        if (hasAgents)
        {
            evidence.Add(OrchestrationArtifactPaths.AgentsDirectory);
        }

        DatabaseInspection inspection = hasDatabase
            ? InspectSqliteDatabase(database)
            : DatabaseInspection.Empty;
        if (hasDatabase && !inspection.CanOpen)
        {
            var blocker = new ResolutionBlocker(
                BlockerCategory.Storage,
                "Workspace database is not a valid SQLite database.",
                "storage verifier",
                "Restore or explicitly repair workspace storage.",
                Recoverable: true,
                [DatabaseRelativePath]);
            return Task.FromResult(new StorageVerificationResult(
                StorageAuthorityKind.Corrupt,
                UsableAuthority: false,
                StaleExports: [],
                Conflicts: [],
                Corruption: [DatabaseRelativePath],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: [],
                BlockingConditions: [blocker],
                Evidence: evidence));
        }

        StorageAuthorityKind authority = (hasDatabase, hasAgents) switch
        {
            (true, true) => StorageAuthorityKind.Mixed,
            (true, false) => StorageAuthorityKind.CanonicalSqlite,
            (false, true) => StorageAuthorityKind.FilesystemExport,
            _ => StorageAuthorityKind.Missing,
        };
        if (hasDatabase && inspection.UnsupportedSchema.Count > 0)
        {
            var blocker = new ResolutionBlocker(
                BlockerCategory.Storage,
                "Workspace database schema version is unsupported.",
                "storage verifier",
                "Use a compatible LoopRelay version or explicitly migrate workspace storage.",
                Recoverable: true,
                [DatabaseRelativePath]);
            return Task.FromResult(new StorageVerificationResult(
                StorageAuthorityKind.Unsupported,
                UsableAuthority: false,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: inspection.UnsupportedSchema,
                UnresolvedReferences: [],
                PartialTransactions: [],
                BlockingConditions: [blocker],
                Evidence: evidence));
        }

        if (hasDatabase && inspection.PartialTransactions.Count > 0)
        {
            var blocker = new ResolutionBlocker(
                BlockerCategory.Storage,
                "Workspace database contains partial workflow transaction markers.",
                "storage verifier",
                "Resolve or recover partial workflow transactions before mutating orchestration.",
                Recoverable: true,
                inspection.PartialTransactions);
            return Task.FromResult(new StorageVerificationResult(
                authority,
                UsableAuthority: false,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: inspection.PartialTransactions,
                BlockingConditions: [blocker],
                Evidence: evidence.Concat(inspection.PartialTransactions).ToArray()));
        }

        return Task.FromResult(new StorageVerificationResult(
            authority,
            UsableAuthority: true,
            StaleExports: [],
            Conflicts: [],
            Corruption: [],
            UnsupportedSchema: [],
            UnresolvedReferences: [],
            PartialTransactions: [],
            BlockingConditions: [],
            Evidence: evidence));
    }

    private static DatabaseInspection InspectSqliteDatabase(string path)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            _ = command.ExecuteScalar();
            IReadOnlyList<string> unsupportedSchema = ReadUnsupportedSchema(connection);
            IReadOnlyList<string> partialTransactions = ReadPartialTransactions(connection);
            return new DatabaseInspection(
                CanOpen: true,
                unsupportedSchema,
                partialTransactions);
        }
        catch (SqliteException)
        {
            return DatabaseInspection.Corrupt;
        }
        catch (InvalidOperationException)
        {
            return DatabaseInspection.Corrupt;
        }
    }

    private static IReadOnlyList<string> ReadUnsupportedSchema(SqliteConnection connection)
    {
        if (!TableExists(connection, "schema_metadata"))
        {
            return [];
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = 'schema_version';";
        object? scalar = command.ExecuteScalar();
        string? value = scalar is null or DBNull ? null : Convert.ToString(scalar);
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (!int.TryParse(value, out int version) ||
            version < 1 ||
            version > LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
        {
            return [$"schema_metadata:schema_version={value}"];
        }

        return [];
    }

    private static IReadOnlyList<string> ReadPartialTransactions(SqliteConnection connection)
    {
        if (!TableExists(connection, "workflow_transactions"))
        {
            return [];
        }

        var partial = new List<string>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT transaction_id, status, completed_at
            FROM workflow_transactions
            WHERE status <> 'Completed' OR completed_at IS NULL
            ORDER BY started_at, transaction_id;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string transactionId = reader.GetString(0);
            string status = reader.GetString(1);
            partial.Add($"workflow_transactions:{transactionId}:{status}");
        }

        return partial;
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = command.ExecuteScalar();
        return Convert.ToInt64(scalar) == 1;
    }

    private sealed record DatabaseInspection(
        bool CanOpen,
        IReadOnlyList<string> UnsupportedSchema,
        IReadOnlyList<string> PartialTransactions)
    {
        public static DatabaseInspection Empty { get; } = new(true, [], []);

        public static DatabaseInspection Corrupt { get; } = new(false, [], []);
    }
}
