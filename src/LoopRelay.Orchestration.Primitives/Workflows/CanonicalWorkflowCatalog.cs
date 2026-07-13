using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Workflows;

public sealed record CatalogObligation(
    string Key,
    string Owner,
    string Kind,
    string SemanticIdentity,
    string ContentHash);

public sealed record CanonicalWorkflowCatalogSnapshot(
    string Identity,
    string SemanticVersion,
    IReadOnlyList<WorkflowDefinition> Workflows,
    IReadOnlyList<WorkflowChainDefinition> Chains,
    IReadOnlyList<CatalogObligation> Obligations)
{
    public WorkflowDefinition GetWorkflow(WorkflowIdentity identity) =>
        Workflows.SingleOrDefault(item => item.Identity == identity)
        ?? throw new KeyNotFoundException($"Workflow '{identity}' is not present in catalog {Identity}.");
}

public enum CatalogResolutionKind
{
    Available,
    RecoveryRequired,
}

public sealed record CatalogResolution(
    CatalogResolutionKind Kind,
    CanonicalWorkflowCatalogSnapshot? Catalog,
    string Explanation);

public sealed class CanonicalWorkflowCatalogRegistry(
    IReadOnlyList<CanonicalWorkflowCatalogSnapshot> _acceptedCatalogs)
{
    public CatalogResolution Resolve(string identity, string semanticVersion)
    {
        CanonicalWorkflowCatalogSnapshot? exact = _acceptedCatalogs.SingleOrDefault(item =>
            string.Equals(item.Identity, identity, StringComparison.Ordinal) &&
            string.Equals(item.SemanticVersion, semanticVersion, StringComparison.Ordinal));
        return exact is null
            ? new(CatalogResolutionKind.RecoveryRequired, null,
                $"Exact catalog '{identity}' version '{semanticVersion}' is unavailable; explicit compatible migration is required.")
            : new(CatalogResolutionKind.Available, exact, "Exact active catalog snapshot is available.");
    }
}

public static class CanonicalWorkflowCatalog
{
    public const string SemanticVersion = "13.0.0";
    public static CanonicalWorkflowCatalogSnapshot Current { get; } = BuildCurrent();

    public static IReadOnlyList<WorkflowDefinition> CreateAll() => Current.Workflows;
    public static IReadOnlyList<WorkflowChainDefinition> CreateChains() => Current.Chains;
    public static WorkflowDefinition CreateTraditionalRoadmap() => Current.GetWorkflow(WorkflowIdentity.TraditionalRoadmap);
    public static WorkflowDefinition CreateEvalRoadmap() => Current.GetWorkflow(WorkflowIdentity.EvalRoadmap);
    public static WorkflowDefinition CreatePlan() => Current.GetWorkflow(WorkflowIdentity.Plan);
    public static WorkflowDefinition CreateExecute() => Current.GetWorkflow(WorkflowIdentity.Execute);

    private static CanonicalWorkflowCatalogSnapshot BuildCurrent()
    {
        WorkflowDefinition[] workflows = CanonicalWorkflowDeclarations.CreateAll()
            .Select(DeriveTransitionContracts).ToArray();
        IReadOnlyDictionary<WorkflowIdentity, WorkflowDefinition> byIdentity =
            workflows.ToDictionary(item => item.Identity);
        WorkflowChainDefinition[] chains = CanonicalWorkflowDeclarations.CreateChains()
            .Select(chain => chain with
            {
                Workflows = chain.Workflows.Select(item => byIdentity[item.Identity]).ToArray(),
            }).ToArray();
        string canonical = CatalogCanonicalJson.Serialize(new { semanticVersion = SemanticVersion, workflows, chains });
        string identity = Hash(canonical);
        CatalogObligation[] obligations = CatalogObligationEnumerator.Enumerate(workflows, chains).ToArray();
        var snapshot = new CanonicalWorkflowCatalogSnapshot(identity, SemanticVersion, workflows, chains, obligations);
        WorkflowCatalogValidationResult validation = WorkflowCatalogValidator.Validate(snapshot);
        if (!validation.IsValid)
            throw new InvalidOperationException("Canonical workflow catalog is invalid:\n" + string.Join("\n", validation.Errors));
        return snapshot;
    }

    private static WorkflowDefinition DeriveTransitionContracts(WorkflowDefinition workflow) => workflow with
    {
        Transitions = workflow.Transitions.Select(transition => DeriveTransition(workflow, transition)).ToArray(),
    };

    private static WorkflowTransitionDefinition DeriveTransition(
        WorkflowDefinition workflow, WorkflowTransitionDefinition transition)
    {
        ValidatorReference[] validators = transition.Validators
            .Select(identity => new ValidatorReference(new ValidatorIdentity(identity), WorkflowCatalogOwnerRegistry.ValidatorOwner))
            .DistinctBy(item => item.Identity).ToArray();
        bool publish = transition.Effects.Any(effect => effect.Category is EffectCategory.Publication or EffectCategory.Git);
        OutputSurfaceDefinition[] surfaces = transition.ProducedProducts
            .Select(product => SurfaceFor(product, validators, publish)).DistinctBy(item => item.Path, StringComparer.Ordinal).ToArray();
        InputSurfaceDefinition[] inputs = transition.RequiredInputProducts
            .Select(requirement => InputFor(requirement.Product, validators))
            .Concat(transition.InputGate.Requirements.Where(item => item.InputSurface is not null)
                .Select(item => InputFor(item.InputSurface!, validators)))
            .DistinctBy(item => item.Path, StringComparer.Ordinal).ToArray();
        EffectDefinition[] domainEffects = transition.Effects
            .Where(effect => effect.Category is not (EffectCategory.Publication or EffectCategory.Git)).ToArray();
        var derived = new List<EffectDefinition>(domainEffects);
        int order = derived.Count == 0 ? 0 : derived.Max(item => item.Order) + 1;
        foreach (OutputSurfaceDefinition surface in surfaces)
        {
            if (surface.CommitPolicy == CommitPolicy.BlockingLocal)
            {
                derived.Add(DerivedEffect(workflow, transition, surface, "commit", order++));
            }
            if (surface.PushPolicy == PushPolicy.RequiredAsync)
            {
                derived.Add(DerivedEffect(workflow, transition, surface, "push", order++));
            }
        }
        RuntimeCapabilityIdentity capability = new(transition.ExecutionPosture.Kind switch
        {
            ExecutionPostureKind.WarmSession => "agent.warm-session",
            ExecutionPostureKind.PersistentSession => "agent.persistent-session",
            ExecutionPostureKind.DecisionSession => "agent.decision-session",
            ExecutionPostureKind.ScopedArtifactOperation => "artifact.scoped-mutation",
            ExecutionPostureKind.ReadOnlyPrompt => "agent.read-only-prompt",
            _ => "agent.one-shot-prompt",
        });
        string templateVersion = CanonicalPromptAssetCatalog.Assets
            .SingleOrDefault(item => item.PromptIdentity == transition.PromptIdentity)?.SourceHash ?? "catalog-inline-v1";
        return transition with
        {
            Effects = derived,
            OutputSurfaces = surfaces,
            ValidatorReferences = validators,
            PromptContract = new PromptContractDefinition(transition.PromptIdentity, templateVersion,
                "resolved-canonical-prompt-policy", ["permissions", "sandbox", "network", "model-profile"],
                [capability]),
            InteractionCategories = ["DirtyInputCommitOffer", "RecoveryAmbiguity", "CompletionAmbiguity"],
            RecoveryStrategies = transition.Recovery.SupportedActions.Order(StringComparer.Ordinal).ToArray(),
            InputSurfaces = inputs,
        };
    }

    private static InputSurfaceDefinition InputFor(
        ProductIdentity product, IReadOnlyList<ValidatorReference> validators) =>
        InputFor(ProductSurfacePath(product), validators);

    private static InputSurfaceDefinition InputFor(
        string path, IReadOnlyList<ValidatorReference> validators)
    {
        ValidatorReference validator = validators.FirstOrDefault()
            ?? new ValidatorReference(new ValidatorIdentity("CanonicalInputSurfaceValidator"),
                WorkflowCatalogOwnerRegistry.ValidatorOwner);
        return new InputSurfaceDefinition(path, TargetFor(path), "repository-owned orchestration input", validator);
    }

    private static OutputSurfaceDefinition SurfaceFor(
        ProductDefinition product, IReadOnlyList<ValidatorReference> validators, bool publish)
    {
        string path = ProductSurfacePath(product.Identity);
        RepositoryTarget target = TargetFor(path);
        ValidatorReference validator = validators.FirstOrDefault(item =>
            item.Identity.Value.StartsWith(product.Identity.Value, StringComparison.Ordinal))
            ?? new ValidatorReference(new ValidatorIdentity($"{product.Identity}Validator"),
                WorkflowCatalogOwnerRegistry.ValidatorOwner);
        bool directory = path.EndsWith("/", StringComparison.Ordinal) ||
            path is ".agents/specs" or ".agents/milestones" or ".agents/evidence" or ".agents/archive/epics";
        return new OutputSurfaceDefinition(path.TrimEnd('/'), target,
            product.Identity == ProductIdentity.RepositoryChanges ? SurfaceMutationKind.RepositoryDelta :
            directory ? SurfaceMutationKind.ReplaceDirectory : SurfaceMutationKind.CreateOrReplaceFile,
            product.RepositoryOwnership, validator,
            publish ? CommitPolicy.BlockingLocal : CommitPolicy.None,
            publish ? PushPolicy.RequiredAsync : PushPolicy.None);
    }

    private static RepositoryTarget TargetFor(string path) => path == "." ? RepositoryTarget.Workspace :
        path.TrimEnd('/') == OrchestrationArtifactPaths.AgentsDirectory ? RepositoryTarget.ParentGitlink :
        OrchestrationArtifactPaths.IsAgentsPath(path) ? RepositoryTarget.NestedAgents : RepositoryTarget.Workspace;

    private static string ProductSurfacePath(ProductIdentity identity) => identity.Value switch
    {
        "EvaluationIntent" => EvaluationArtifactPaths.SelectedEvaluation,
        "RoadmapCompletionContext" => ".agents/roadmap-completion-context.md",
        "StrategicInitiativeSelection" => ".agents/selected-initiative.md",
        "EpicPreparationAudit" => ".agents/epic-preparation-audit.md",
        "DependencyInventory" => EvaluationArtifactPaths.DependencyInventory,
        "HypothesisInventory" => EvaluationArtifactPaths.HypothesisInventory,
        "ArchitecturalCatalog" => EvaluationArtifactPaths.ArchitecturalCatalog,
        "EvalDag" => EvaluationArtifactPaths.EvalDag,
        "NextEpicRoadmap" => EvaluationArtifactPaths.NextEpicRoadmap,
        "PreparedEpic" => EvaluationArtifactPaths.PreparedEpic,
        "MilestoneSpecificationSet" => EvaluationArtifactPaths.MilestoneSpecificationDirectory,
        "ExecutablePlan" => OrchestrationArtifactPaths.Plan,
        "AdversarialProjection" => PlanPromptContext.AdversarialPlanReviewProjectionPath,
        "AdversarialReview" => ".agents/projections/adversarial-plan-review.md",
        "OperationalContext" => OrchestrationArtifactPaths.OperationalContext,
        "ExecutionDetails" => OrchestrationArtifactPaths.Details,
        "ExecutionMilestoneSet" => OrchestrationArtifactPaths.MilestonesDirectory,
        "ExecutionReadiness" => ".agents/execution-readiness.json",
        "DecisionSet" => OrchestrationArtifactPaths.Decisions,
        "ImplementationSlice" => ".agents/evidence/implementation-slices",
        "ExecutionHandoff" => OrchestrationArtifactPaths.LiveHandoff,
        "OperationalDelta" => OrchestrationArtifactPaths.OperationalDelta,
        "RepositoryChanges" => ".",
        "CompletionEvidence" => OrchestrationArtifactPaths.EvidenceDirectory,
        "CompletionRoute" => ".agents/completion-route.json",
        "CertifiedCompletion" => ".agents/archive/epics",
        _ => throw new InvalidOperationException($"No output surface is registered for product '{identity}'."),
    };

    private static EffectDefinition DerivedEffect(WorkflowDefinition workflow,
        WorkflowTransitionDefinition transition, OutputSurfaceDefinition surface, string operation, int order) =>
        new(new EffectIdentity($"derived-git-{operation}:{workflow.Identity}:{transition.Identity}:{surface.Path}"),
            EffectCategory.Git, $"Structurally derived {operation} for declared output surface '{surface.Path}'.",
            transition.RequiredInputProducts.Select(item => item.Product).ToArray(),
            transition.ProducedProducts.Select(item => item.Identity).ToArray(), order,
            operation == "commit" ? "Local publication blocks transition completion."
                : "Required push remains durable asynchronous effect work until settled.");

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal static class CatalogCanonicalJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value)
    {
        JsonNode root = JsonSerializer.SerializeToNode(value, Options)!;
        var builder = new StringBuilder();
        Write(root, builder);
        return builder.ToString();
    }

    private static void Write(JsonNode? node, StringBuilder output)
    {
        switch (node)
        {
            case JsonObject obj:
                output.Append('{');
                bool first = true;
                foreach ((string key, JsonNode? value) in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    if (!first) output.Append(','); first = false;
                    output.Append(JsonSerializer.Serialize(key.Normalize(NormalizationForm.FormC))).Append(':');
                    Write(value, output);
                }
                output.Append('}');
                break;
            case JsonArray array:
                output.Append('[');
                for (int index = 0; index < array.Count; index++)
                {
                    if (index > 0) output.Append(',');
                    Write(array[index], output);
                }
                output.Append(']');
                break;
            case JsonValue value when value.TryGetValue(out string? text):
                output.Append(JsonSerializer.Serialize((text ?? string.Empty).Normalize(NormalizationForm.FormC)));
                break;
            default:
                output.Append(node?.ToJsonString() ?? "null");
                break;
        }
    }
}

public static class CatalogObligationEnumerator
{
    public static IReadOnlyList<CatalogObligation> Enumerate(
        IReadOnlyList<WorkflowDefinition> workflows, IReadOnlyList<WorkflowChainDefinition> chains)
    {
        var obligations = new List<CatalogObligation>();
        foreach (WorkflowDefinition workflow in workflows)
        {
            Add("WorkflowCatalog", "workflow", workflow.Identity.Value, new
            {
                identity = workflow.Identity.Value,
                workflow.Purpose,
                entryGate = workflow.EntryGate.Identity.Value,
                exitGate = workflow.ExitGate.Identity.Value,
                downstream = workflow.DownstreamWorkflow?.Value,
                completion = workflow.Completion.Identity,
            });
            foreach (WorkflowStageDefinition stage in workflow.Stages)
                Add("WorkflowCatalog", "stage", $"{workflow.Identity}/{stage.Identity}", stage);
            foreach (WorkflowTransitionDefinition transition in workflow.Transitions)
            {
                string path = $"{workflow.Identity}/{transition.Identity}";
                Add("WorkflowCatalog", "transition", path, transition);
                Add("PromptAuthority", "prompt-template", $"{path}/{transition.PromptContract!.TemplateIdentity}",
                    new { transition.PromptContract.TemplateIdentity, transition.PromptContract.TemplateVersion });
                Add("PolicyAuthority", "prompt-policy-profile", $"{path}/{transition.PromptContract.RequiredPolicyProfile}",
                    new { transition.PromptContract.RequiredPolicyProfile, transition.PromptContract.ResolvedPolicyRequirements });
                foreach (RuntimeCapabilityIdentity capability in transition.PromptContract.RuntimeCapabilities)
                    Add("RuntimeCapabilityRegistry", "runtime-capability", $"{path}/{capability}", capability);
                foreach (ValidatorReference validator in transition.ValidatorReferences ?? [])
                    Add(validator.Owner, "validator", $"{path}/{validator.Identity}", validator);
                foreach (string interaction in transition.InteractionCategories ?? [])
                    Add("InteractionBroker", "interaction-category", $"{path}/{interaction}", interaction);
                foreach (string strategy in transition.RecoveryStrategies ?? [])
                    Add("RecoveryCoordinator", "recovery-strategy", $"{path}/{strategy}", strategy);
                foreach (OutputSurfaceDefinition surface in transition.OutputSurfaces ?? [])
                    Add("WorkflowCatalog", "output-surface", $"{path}/{surface.Path}", surface);
                foreach (InputSurfaceDefinition surface in transition.InputSurfaces ?? [])
                    Add("WorkflowCatalog", "input-surface", $"{path}/{surface.Path}", surface);
                foreach (EffectDefinition effect in transition.Effects)
                    Add("EffectCoordinator", "effect", $"{path}/{effect.Identity}", effect);
                foreach (ProductDefinition product in transition.ProducedProducts)
                    Add("WorkflowCatalog", "product", $"{path}/{product.Identity}", product);
            }
        }
        foreach (WorkflowChainDefinition chain in chains)
            Add("WorkflowCatalog", "chain", chain.Identity, new { chain.Identity, chain.Purpose,
                workflows = chain.Workflows.Select(item => item.Identity.Value).ToArray() });
        return obligations.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();

        void Add<T>(string owner, string kind, string identity, T value)
        {
            string key = $"{owner}/{kind}/{identity}";
            string hash = Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(CatalogCanonicalJson.Serialize(value))));
            obligations.Add(new CatalogObligation(key, owner, kind, identity, hash));
        }
    }
}
