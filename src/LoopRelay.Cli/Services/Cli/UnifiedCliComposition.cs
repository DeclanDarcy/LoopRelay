using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Primitives;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Primitives;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Cli.Services.Cli;

internal sealed class UnifiedCliComposition : IAsyncDisposable
{
    private readonly ServiceProvider? provider;
    private readonly IAsyncDisposable? promptExecutorLifetime;

    private UnifiedCliComposition(
        Repository repository,
        IStorageVerifier storageVerifier,
        RepositoryObserver repositoryObserver,
        WorkflowResolver workflowResolver,
        ITransitionRuntime transitionRuntime,
        WorkflowController workflowController,
        WorkflowChainRunner workflowChainRunner,
        WorkflowBoundaryEvidenceWriter boundaryEvidenceWriter,
        IReadOnlyList<WorkflowDefinition> workflowDefinitions,
        IReadOnlyList<WorkflowChainDefinition> workflowChains,
        CanonicalWorkflowPersistenceStore persistence,
        ResolvedOperationalPolicy policy,
        ServiceProvider? provider = null,
        IAsyncDisposable? promptExecutorLifetime = null)
    {
        Repository = repository;
        Policy = policy;
        StorageVerifier = storageVerifier;
        RepositoryObserver = repositoryObserver;
        WorkflowResolver = workflowResolver;
        TransitionRuntime = transitionRuntime;
        WorkflowController = workflowController;
        WorkflowChainRunner = workflowChainRunner;
        BoundaryEvidenceWriter = boundaryEvidenceWriter;
        WorkflowDefinitions = workflowDefinitions;
        WorkflowChains = workflowChains;
        Persistence = persistence;
        this.provider = provider;
        this.promptExecutorLifetime = promptExecutorLifetime;
    }

    public Repository Repository { get; }

    /// <summary>
    /// The single resolved operational policy this invocation executes under. Every consumer
    /// observes this one instance; no production code re-reads settings or environment ad hoc.
    /// </summary>
    public ResolvedOperationalPolicy Policy { get; }

    public IStorageVerifier StorageVerifier { get; }

    public RepositoryObserver RepositoryObserver { get; }

    public WorkflowResolver WorkflowResolver { get; }

    public ITransitionRuntime TransitionRuntime { get; }

    public WorkflowController WorkflowController { get; }

    public WorkflowChainRunner WorkflowChainRunner { get; }

    public WorkflowBoundaryEvidenceWriter BoundaryEvidenceWriter { get; }

    public IReadOnlyList<WorkflowDefinition> WorkflowDefinitions { get; }

    public IReadOnlyList<WorkflowChainDefinition> WorkflowChains { get; }

    internal CanonicalWorkflowPersistenceStore Persistence { get; }

    /// <summary>True only for the production composition, which launches the real provider and
    /// therefore has runtime prerequisites to inspect; injected runtimes have none. Settable
    /// only by <see cref="CreateProduction"/> and tests exercising the prerequisite gate.</summary>
    internal bool ProductionRuntime { get; set; }

    /// <summary>The prerequisite inspector, replaceable by tests with one reading a fake
    /// environment — the default reads the real environment and filesystem.</summary>
    internal RuntimePrerequisiteDoctor RuntimePrerequisiteDoctor { get; set; } = new();

    public static UnifiedCliComposition Create(Repository repository) =>
        CreateCore(repository, agentRuntime: null, processRunner: new ProcessRunner(), provider: null);

    public static UnifiedCliComposition CreateProduction(
        Repository repository,
        IReadOnlyList<PolicyOverride>? policyOverrides = null)
    {
        var settings = CliSettingsLoader.Load();
        ResolvedOperationalPolicy policy = OperationalPolicyResolver.Resolve(
            settings.Policy,
            settings.IsDefaultTemplate
                ? $"settings:{settings.Path} (default template)"
                : $"settings:{settings.Path}",
            CombineInvocationOverrides(policyOverrides),
            settings.Permissions);
        var services = new ServiceCollection();
        services.AddAgents(settings.Permissions);
        ServiceProvider provider = services.BuildServiceProvider();
        return CreateCore(
            repository,
            provider.GetRequiredService<IAgentRuntime>(),
            provider.GetRequiredService<IProcessRunner>(),
            provider,
            policy,
            productionRuntime: true);
    }

    /// <summary>M7: inspect the production provider's runtime prerequisites and append the
    /// inspection as an append-only fact (run identity does not exist yet at this point; the
    /// fact is ordered against runs by its ledger position). Non-production compositions have
    /// no provider prerequisites and return no diagnostics.</summary>
    internal async Task<IReadOnlyList<RuntimeDiagnostic>> InspectRuntimePrerequisitesAsync(
        CancellationToken cancellationToken)
    {
        if (!ProductionRuntime)
        {
            return [];
        }

        IReadOnlyList<RuntimeDiagnostic> diagnostics = RuntimePrerequisiteDoctor.Inspect();
        try
        {
            await Persistence.AppendRuntimePrerequisiteAsync(
                new CanonicalRuntimePrerequisiteRecord(
                    CausalUlid.NewId("pre"),
                    RunId: null,
                    DateTimeOffset.UtcNow,
                    JsonSerializer.Serialize(
                        diagnostics
                            .Select(diagnostic => new RuntimePrerequisiteDiagnosticJson(
                                diagnostic.Id,
                                diagnostic.Severity.ToString(),
                                diagnostic.Message))
                            .ToArray(),
                        PrerequisiteJsonOptions)),
                CancellationToken.None);
        }
        catch
        {
            // Prerequisite evidence is best-effort; the console rendering still happens.
        }

        return diagnostics;
    }

    private static readonly JsonSerializerOptions PrerequisiteJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record RuntimePrerequisiteDiagnosticJson(string Id, string Severity, string Message);

    // Recognized environment variables are ambient invocation-layer inputs; explicit --policy
    // flags beat them for the same key. The decision-resume kill switch stays functional
    // (LoopRelay_DECISION_RESUME=0 or =false disables resume) but its value now flows through
    // the resolver's validation like any other policy input: a garbage value is rejected
    // loudly instead of silently enabling resume. LoopRelay_SESSION_LOG gets the same M7
    // treatment for the reconnected telemetry wrapper: it was previously "anything but 0/false
    // enables", and now a non-boolean value rejects resolution instead of silently enabling.
    internal static IReadOnlyList<PolicyOverride> CombineInvocationOverrides(
        IReadOnlyList<PolicyOverride>? flagOverrides,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        List<PolicyOverride> overrides = [];
        string? decisionResume = getEnvironmentVariable("LoopRelay_DECISION_RESUME");
        if (decisionResume is not null)
        {
            overrides.Add(new PolicyOverride(
                OperationalPolicyResolver.DecisionSessionResumeKey,
                decisionResume,
                "env:LoopRelay_DECISION_RESUME",
                IsExplicit: false));
        }

        string? sessionTelemetry = getEnvironmentVariable("LoopRelay_SESSION_LOG");
        if (sessionTelemetry is not null)
        {
            overrides.Add(new PolicyOverride(
                OperationalPolicyResolver.SessionTelemetryKey,
                sessionTelemetry,
                "env:LoopRelay_SESSION_LOG",
                IsExplicit: false));
        }

        if (flagOverrides is not null)
        {
            overrides.AddRange(flagOverrides);
        }

        return overrides;
    }

    internal static UnifiedCliComposition Create(
        Repository repository,
        IAgentRuntime agentRuntime,
        ResolvedOperationalPolicy? policy = null) =>
        CreateCore(repository, agentRuntime, processRunner: new ProcessRunner(), provider: null, policy);

    internal static UnifiedCliComposition Create(
        Repository repository,
        IAgentRuntime agentRuntime,
        IProcessRunner processRunner,
        ResolvedOperationalPolicy? policy = null) =>
        CreateCore(repository, agentRuntime, processRunner, provider: null, policy);

    private static UnifiedCliComposition CreateCore(
        Repository repository,
        IAgentRuntime? agentRuntime,
        IProcessRunner processRunner,
        ServiceProvider? provider,
        ResolvedOperationalPolicy? policy = null,
        bool productionRuntime = false)
    {
        // Non-production compositions execute under the built-in defaults so every attempt
        // still records one resolved policy identity.
        policy ??= OperationalPolicyResolver.Resolve(
            CliPolicyDocument.Empty,
            "built-in",
            [],
            PermissionPolicyFactory.Minimum);
        IStorageVerifier storageVerifier = new FileSystemStorageVerifier();
        var repositoryObserver = new RepositoryObserver(storageVerifier);
        var workflowResolver = new WorkflowResolver();
        IReadOnlyList<WorkflowDefinition> workflowDefinitions = CanonicalWorkflowDefinitionSketches.CreateAll();
        IReadOnlyList<WorkflowChainDefinition> workflowChains = CanonicalWorkflowDefinitionSketches.CreateChains();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var promptExecutor = new UnifiedPromptExecutor(
            repository, agentRuntime, processRunner, persistence, policy, productionRuntime);
        ITransitionRuntime transitionRuntime = new TransitionRuntime(
            new UnifiedTransitionDefinitionResolver(workflowDefinitions),
            new RepositoryObservationProductResolver(repositoryObserver, repository),
            new UnifiedGateEvaluator(processRunner, repository),
            new UnifiedPromptContextBuilder(repository),
            new UnifiedPromptRenderer(),
            promptExecutor,
            new UnifiedOutputInterpreter(repository),
            new UnifiedProductValidator(repository),
            new UnifiedEffectExecutor(repository, persistence, workflowDefinitions),
            new CanonicalTransitionRunStore(persistence),
            new CanonicalTransitionEvidenceStore(persistence),
            policy.PolicyId,
            new CanonicalTransitionWarningStore(persistence),
            new CanonicalTransitionRecoveryStore(persistence),
            new CanonicalTransitionGateEvaluationStore(persistence),
            new CanonicalTransitionEffectStore(persistence),
            new CanonicalAttemptStore(persistence),
            new CanonicalReadReceiptStore(persistence, processRunner, repository));
        var workflowController = new WorkflowController(workflowResolver, transitionRuntime);
        var boundaryEvidenceWriter = new WorkflowBoundaryEvidenceWriter(
            new CanonicalChainBoundaryEvidenceStore(persistence));
        var workflowChainRunner = new WorkflowChainRunner(
            workflowResolver,
            workflowController,
            new WorkflowEntryGateEvaluator(),
            new WorkflowExitGateEvaluator(),
            new ProductTransferEvaluator(),
            boundaryEvidenceWriter,
            new CanonicalWorkflowInstanceRecorder(persistence));
        var composition = new UnifiedCliComposition(
            repository,
            storageVerifier,
            repositoryObserver,
            workflowResolver,
            transitionRuntime,
            workflowController,
            workflowChainRunner,
            boundaryEvidenceWriter,
            workflowDefinitions,
            workflowChains,
            persistence,
            policy,
            provider,
            promptExecutor);
        composition.ProductionRuntime = productionRuntime;
        return composition;
    }

    public async ValueTask DisposeAsync()
    {
        if (promptExecutorLifetime is not null)
        {
            await promptExecutorLifetime.DisposeAsync();
        }

        if (provider?.GetService<AgentSessionRegistry>() is { } registry)
        {
            await registry.DisposeAsync();
        }

        if (provider is not null)
        {
            await provider.DisposeAsync();
        }
    }

    public Task<RepositoryObservation> ObserveAsync(CancellationToken cancellationToken) =>
        RepositoryObserver.ObserveAsync(Repository.Path, cancellationToken);

    public WorkflowResolutionResult Resolve(
        WorkflowInvocation invocation,
        RepositoryObservation observation) =>
        WorkflowResolver.Resolve(invocation, observation, WorkflowDefinitions);

    public WorkflowChainDefinition SelectChain(
        WorkflowInvocation invocation,
        RepositoryObservation observation)
    {
        WorkflowSelectionResult selection = InvocationModeResolver.Resolve(invocation, observation);
        WorkflowChainDefinition? chain = WorkflowChains.FirstOrDefault(item => item.InitialWorkflow == selection.SelectedWorkflow);
        if (chain is not null)
        {
            return chain;
        }

        WorkflowDefinition definition = WorkflowDefinitions.Single(item => item.Identity == selection.SelectedWorkflow);
        return new WorkflowChainDefinition(
            $"Bounded{selection.SelectedWorkflow}",
            $"Bounded invocation for {selection.SelectedWorkflow}.",
            selection.SelectedWorkflow,
            [definition]);
    }

    private sealed class UnifiedTransitionDefinitionResolver(
        IReadOnlyList<WorkflowDefinition> _definitions) : ITransitionDefinitionResolver
    {
        public Task<WorkflowTransitionDefinition> ResolveAsync(
            TransitionRuntimeRequest request,
            CancellationToken cancellationToken)
        {
            WorkflowDefinition definition = _definitions.Single(item => item.Identity == request.Workflow);
            return Task.FromResult(definition.Transitions.Single(item => item.Identity == request.Transition));
        }

        public Task<IReadOnlyList<WorkflowTransitionIdentity>> ResolveEligibleSuccessorsAsync(
            WorkflowTransitionDefinition definition,
            IReadOnlyList<ProductRecord> validatedProducts,
            CancellationToken cancellationToken) =>
            Task.FromResult(definition.EligibleSuccessors);
    }

    private sealed class RepositoryObservationProductResolver(
        RepositoryObserver _observer,
        Repository _repository) : IProductResolver
    {
        public async Task<ProductResolutionResult> ResolveAsync(
            IReadOnlyList<ProductRequirement> requirements,
            CancellationToken cancellationToken)
        {
            RepositoryObservation observation = await _observer.ObserveAsync(_repository.Path, cancellationToken);
            var products = new List<ProductRecord>();
            var missing = new List<ProductRequirement>();
            var stale = new List<ProductRecord>();
            var invalid = new List<ProductRecord>();
            var ambiguous = new List<ProductRecord>();

            foreach (ProductRequirement requirement in requirements)
            {
                ObservedProduct? observed = observation.Products.FirstOrDefault(item => item.Product.Identity == requirement.Product);
                if (observed is null)
                {
                    missing.Add(requirement);
                    continue;
                }

                products.Add(observed.Product);
                if (requirement.RequiresFreshness && observed.Product.Freshness == ProductFreshness.Stale)
                {
                    stale.Add(observed.Product);
                }

                if (!observed.GateUsable ||
                    observed.Product.ValidationState is ProductValidationState.Invalid or ProductValidationState.Stale)
                {
                    invalid.Add(observed.Product);
                }

                if (observed.Product.ValidationState == ProductValidationState.Ambiguous)
                {
                    ambiguous.Add(observed.Product);
                }
            }

            return new ProductResolutionResult(products, missing, stale, invalid, ambiguous);
        }
    }

    // Every requirement is evaluated individually into its own GateRequirementResult; the gate status
    // is the worst-of aggregation Invalid > Unsatisfied > Ambiguous > Waiting > Satisfied. Requirement
    // kinds are implicit by shape: Product != null is a product requirement, InputSurface != null is a
    // clean-input requirement (scoped git-porcelain cleanliness), neither is an explainable declaration.
    internal sealed class UnifiedGateEvaluator(
        IProcessRunner _processRunner,
        Repository _repository) : IGateEvaluator
    {
        public async Task<GateResult> EvaluateInputGateAsync(
            GateDefinition gate,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            var requirements = new List<GateRequirementResult>(gate.Requirements.Count);
            foreach (GateRequirementDefinition requirement in gate.Requirements)
            {
                requirements.Add(await EvaluateRequirementAsync(
                    requirement,
                    product => EvaluateInputProduct(requirement, product, inputs)));
            }

            GateResult result = Aggregate(
                gate,
                requirements,
                "Input gate satisfied by repository-owned products.");
            return result.Status == GateStatus.Satisfied
                ? result
                : result with { Explanation = $"Input gate {Describe(result.Status)}: {result.Explanation}" };
        }

        public async Task<GateResult> EvaluateOutputGateAsync(
            GateDefinition gate,
            ProductValidationResult validation,
            CancellationToken cancellationToken)
        {
            var requirements = new List<GateRequirementResult>(gate.Requirements.Count);
            foreach (GateRequirementDefinition requirement in gate.Requirements)
            {
                requirements.Add(await EvaluateRequirementAsync(
                    requirement,
                    product => EvaluateOutputProduct(requirement, product, validation)));
            }

            return Aggregate(gate, requirements, validation.Explanation);
        }

        // Worst-of ordering: Invalid > Unsatisfied > Ambiguous > Waiting > Satisfied.
        internal static GateStatus WorstOf(IEnumerable<GateStatus> statuses)
        {
            GateStatus worst = GateStatus.Satisfied;
            foreach (GateStatus status in statuses)
            {
                if (Severity(status) > Severity(worst))
                {
                    worst = status;
                }
            }

            return worst;
        }

        // Repo-relative porcelain paths are in scope when they equal the surface or fall under it as a
        // path prefix; a collapsed entry for the surface directory itself (".agents" gitlink or
        // "?? .agents/" untracked directory) also counts.
        internal static bool IsWithinSurface(string path, string surface)
        {
            string normalizedSurface = Normalize(surface);
            if (normalizedSurface.Length == 0)
            {
                return true;
            }

            string normalizedPath = Normalize(path);
            return normalizedPath == normalizedSurface ||
                normalizedPath.StartsWith(normalizedSurface + "/", StringComparison.Ordinal);
        }

        private async Task<GateRequirementResult> EvaluateRequirementAsync(
            GateRequirementDefinition requirement,
            Func<ProductIdentity, GateRequirementResult> productEvaluation)
        {
            if (requirement.Product is { } product)
            {
                return productEvaluation(product);
            }

            if (requirement.InputSurface is { } surface)
            {
                return await EvaluateCleanInputAsync(requirement, surface);
            }

            return new GateRequirementResult(
                requirement.Identity,
                GateStatus.Satisfied,
                "Requirement declares no product or input surface; it is satisfied as an explainable declaration.",
                [requirement.Description]);
        }

        private static GateRequirementResult EvaluateInputProduct(
            GateRequirementDefinition requirement,
            ProductIdentity product,
            ProductResolutionResult inputs)
        {
            // Product failures declare the rank-0 cannot-proceed outcome so a missing product
            // always outranks a surface problem in the runtime's worst-of selection.
            if (inputs.Missing.Any(missing => missing.Product == product) ||
                inputs.Products.All(resolved => resolved.Identity != product))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Required input product '{product}' is missing; produce it before rerunning.",
                    [product.Value],
                    UnsatisfiedOutcome: RuntimeOutcomeKind.MissingRequiredInput);
            }

            if (inputs.Invalid.FirstOrDefault(invalid => invalid.Identity == product) is { } invalidRecord)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Required input product '{product}' is invalid or unusable; repair it before rerunning.",
                    ProductEvidence(product, invalidRecord),
                    UnsatisfiedOutcome: RuntimeOutcomeKind.MissingRequiredInput);
            }

            if (inputs.Stale.FirstOrDefault(stale => stale.Identity == product) is { } staleRecord)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Required input product '{product}' is stale; refresh it before rerunning.",
                    ProductEvidence(product, staleRecord),
                    UnsatisfiedOutcome: RuntimeOutcomeKind.MissingRequiredInput);
            }

            if (inputs.Ambiguous.FirstOrDefault(ambiguous => ambiguous.Identity == product) is { } ambiguousRecord)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Ambiguous,
                    $"Required input product '{product}' has ambiguous validation state.",
                    ProductEvidence(product, ambiguousRecord));
            }

            ProductRecord satisfied = inputs.Products.First(resolved => resolved.Identity == product);
            return new GateRequirementResult(
                requirement.Identity,
                GateStatus.Satisfied,
                $"Required input product '{product}' is resolved and usable.",
                satisfied.EvidenceLocations.Count > 0 ? satisfied.EvidenceLocations : [product.Value]);
        }

        private static GateRequirementResult EvaluateOutputProduct(
            GateRequirementDefinition requirement,
            ProductIdentity product,
            ProductValidationResult validation)
        {
            if (validation.MissingProducts.Contains(product) ||
                validation.InvalidProducts.Contains(product) ||
                validation.StaleProducts.Contains(product))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Output product '{product}' failed validation: {validation.Explanation}",
                    [product.Value]);
            }

            if (validation.AmbiguousProducts.Contains(product))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Ambiguous,
                    $"Output product '{product}' has ambiguous validation state: {validation.Explanation}",
                    [product.Value]);
            }

            ProductRecord? validated = validation.Products.FirstOrDefault(item => item.Identity == product);
            if (validated is not null)
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Satisfied,
                    $"Output product '{product}' passed validation.",
                    validated.EvidenceLocations.Count > 0 ? validated.EvidenceLocations : [product.Value]);
            }

            return validation.Status == ProductValidationStatus.Valid
                ? new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Satisfied,
                    $"Output product '{product}' is accepted by a valid output validation.",
                    validation.Evidence)
                : new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Output product '{product}' was not produced by a valid output validation: {validation.Explanation}",
                    [product.Value]);
        }

        private async Task<GateRequirementResult> EvaluateCleanInputAsync(
            GateRequirementDefinition requirement,
            string surface)
        {
            // Read-at-use resolves every consumed input to a commit (M3); a workspace without a
            // git working tree cannot honor that, so a declared input surface cannot proceed here.
            string gitMarker = Path.Combine(_repository.Path, ".git");
            if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker))
            {
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Input surface '{surface}' has no git working tree; consumed inputs cannot resolve to a commit. Initialize git and commit the surface before rerunning.",
                    [surface],
                    UnsatisfiedOutcome: RuntimeOutcomeKind.UnversionedInputSurface);
            }

            ProcessRunResult status = await _processRunner.RunAsync("git", ["status", "--porcelain"], _repository.Path);
            if (status.ExitCode != 0)
            {
                // Process stderr can span lines; the concern text feeds line-oriented warning output,
                // so it is collapsed to a single line before it is composed into the explanation.
                string standardError = string.Join(
                    " ",
                    status.StandardError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Invalid,
                    $"Cleanliness of input surface '{surface}' could not be evaluated: git status failed: {standardError}",
                    [surface]);
            }

            IReadOnlyList<string> dirty = Git.GitPorcelain.ChangedPaths(status.StandardOutput)
                .Where(path => IsWithinSurface(path, surface))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return dirty.Count == 0
                ? new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Satisfied,
                    $"Input surface '{surface}' is clean in the git working tree.",
                    [surface])
                : new GateRequirementResult(
                    requirement.Identity,
                    GateStatus.Unsatisfied,
                    $"Input surface '{surface}' has uncommitted changes; commit the listed files under '{surface}' before rerunning.",
                    dirty,
                    UnsatisfiedOutcome: RuntimeOutcomeKind.DirtyInputSurface);
        }

        private static GateResult Aggregate(
            GateDefinition gate,
            IReadOnlyList<GateRequirementResult> requirements,
            string satisfiedExplanation)
        {
            if (requirements.Count == 0)
            {
                // No requirement, no decision: a gate with zero requirements is satisfied with an
                // explainable requirement result naming why.
                var explainable = new GateRequirementResult(
                    $"{gate.Identity}.Explainable",
                    GateStatus.Satisfied,
                    $"Gate '{gate.Identity}' declares no requirements; it is satisfied by definition.",
                    [gate.Purpose]);
                return new GateResult(
                    GateStatus.Satisfied,
                    [explainable],
                    explainable.Explanation,
                    explainable.Evidence);
            }

            GateStatus status = WorstOf(requirements.Select(requirement => requirement.Status));
            string explanation = status == GateStatus.Satisfied
                ? satisfiedExplanation
                : requirements.First(requirement => requirement.Status == status).Explanation;
            IReadOnlyList<string> evidence = requirements
                .SelectMany(requirement => requirement.Evidence)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new GateResult(status, requirements, explanation, evidence);
        }

        private static IReadOnlyList<string> ProductEvidence(ProductIdentity product, ProductRecord record) =>
            new[] { product.Value }
                .Concat(record.EvidenceLocations)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

#pragma warning disable CS8524
        private static int Severity(GateStatus status) =>
            status switch
            {
                GateStatus.Satisfied => 0,
                GateStatus.Waiting => 1,
                GateStatus.Ambiguous => 2,
                GateStatus.Unsatisfied => 3,
                GateStatus.Invalid => 4,
            };

        private static string Describe(GateStatus status) =>
            status switch
            {
                GateStatus.Satisfied => "satisfied",
                GateStatus.Unsatisfied => "unsatisfied",
                GateStatus.Waiting => "waiting",
                GateStatus.Ambiguous => "ambiguous",
                GateStatus.Invalid => "invalid",
            };
#pragma warning restore CS8524

        private static string Normalize(string value)
        {
            string normalized = value.Replace('\\', '/').Trim().Trim('/');
            return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
        }
    }

    private sealed class UnifiedPromptContextBuilder(Repository _repository) : IPromptContextBuilder
    {
        public Task<PromptContext> BuildAsync(
            TransitionRuntimeRequest request,
            WorkflowTransitionDefinition definition,
            ProductResolutionResult inputs,
            CancellationToken cancellationToken)
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string key, string value) in request.Metadata ?? new Dictionary<string, string>())
            {
                metadata[key] = value;
            }

            var sections = new List<PromptContextSection>();
            var consumedFiles = new List<ConsumedInputFile>();
            if (request.Workflow == WorkflowIdentity.EvalRoadmap &&
                request.Transition == EvalRoadmapMilestonePromptContext.Transition)
            {
                EvalRoadmapMilestonePromptContextResult result =
                    EvalRoadmapMilestonePromptContext.Build(_repository.Path);
                if (!result.IsUsable)
                {
                    throw new PromptContextUnavailableException(result.Explanation, result.Evidence, result.ConsumedFiles);
                }

                foreach ((string key, string value) in result.Metadata)
                {
                    metadata[key] = value;
                }

                sections.AddRange(result.Sections);
                consumedFiles.AddRange(result.ConsumedFiles);
            }
            else if (request.Workflow == WorkflowIdentity.Plan &&
                PlanPromptContext.Supports(definition.Identity))
            {
                PlanPromptContextResult result =
                    PlanPromptContext.Build(_repository.Path, definition, inputs);
                if (!result.IsUsable)
                {
                    throw new PromptContextUnavailableException(result.Explanation, result.Evidence, result.ConsumedFiles);
                }

                foreach ((string key, string value) in result.Metadata)
                {
                    metadata[key] = value;
                }

                sections.AddRange(result.Sections);
                consumedFiles.AddRange(result.ConsumedFiles);
            }

            return Task.FromResult(new PromptContext(
                definition,
                inputs,
                TransitionInputSnapshotHasher.Create(definition, inputs.Products, metadata, sections),
                metadata,
                sections,
                consumedFiles));
        }
    }

    // Persists each chain-boundary decision as an append-only history fact so chain progression
    // never exists only in memory or console output.
    internal sealed class CanonicalChainBoundaryEvidenceStore(
        CanonicalWorkflowPersistenceStore _persistence) : IChainBoundaryEvidenceStore
    {
        private static readonly JsonSerializerOptions BoundaryJsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        public Task AppendAsync(ChainBoundaryEvidenceCapture capture, CancellationToken cancellationToken) =>
            _persistence.AppendChainBoundaryEventAsync(
                new CanonicalChainBoundaryEventRecord(
                    CausalUlid.NewId("bnd"),
                    capture.RunId,
                    capture.ChainIdentity,
                    capture.Evaluation.SourceWorkflow,
                    capture.Evaluation.TargetWorkflow,
                    capture.Evaluation.ExitGate.Status,
                    capture.Evaluation.EntryGate?.Status,
                    capture.Evaluation.ProductTransfer?.Gate.Status,
                    capture.Evaluation.CanAdvance ? "Advanced" : "StoppedAtBoundary",
                    capture.Evaluation.Explanation,
                    capture.Evaluation.ExitGate.Evidence
                        .Concat(capture.Evaluation.EntryGate?.Evidence ?? [])
                        .Concat(capture.Evaluation.ProductTransfer?.Gate.Evidence ?? [])
                        .ToArray(),
                    JsonSerializer.Serialize(capture.Evaluation, BoundaryJsonOptions),
                    capture.RecordedAt),
                cancellationToken);
    }

    // Enriches a consumption capture with git provenance — the commit every read resolves to and
    // per-surface tree hashes at that commit — then appends the receipt. Enrichment failures
    // degrade to null fields; the receipt still records exactly what was read.
    internal sealed class CanonicalReadReceiptStore(
        CanonicalWorkflowPersistenceStore _persistence,
        IProcessRunner _processRunner,
        Repository _repository) : IReadReceiptStore
    {
        public async Task AppendAsync(ReadReceiptCapture capture, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> surfaces = capture.Definition.InputGate.Requirements
                .Where(requirement => requirement.InputSurface is not null)
                .Select(requirement => requirement.InputSurface!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string? commitHash = await GitScalarAsync("rev-parse", "HEAD");
            Dictionary<string, string?>? surfaceTreeHashes = null;
            if (surfaces.Count > 0)
            {
                surfaceTreeHashes = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (string surface in surfaces)
                {
                    surfaceTreeHashes[surface] = commitHash is null
                        ? null
                        : await GitScalarAsync("rev-parse", $"HEAD:{surface.TrimEnd('/')}");
                }
            }

            await _persistence.AppendReadReceiptAsync(
                new CanonicalReadReceiptRecord(
                    CausalUlid.NewId("rcpt"),
                    capture.Request.Run?.Value ?? string.Empty,
                    capture.Request.Workflow.Value,
                    capture.Definition.Identity.Value,
                    capture.AttemptId,
                    commitHash,
                    surfaces,
                    surfaceTreeHashes,
                    capture.ConsumedFiles.Select(file => new CanonicalReadReceiptFile(file.Path, file.Sha256)).ToArray(),
                    capture.ConsumedProducts.Select(product => new CanonicalReadReceiptProduct(
                        product.Identity.Value,
                        product.CausalIdentity,
                        product.ValidationState.ToString())).ToArray(),
                    capture.Validation,
                    capture.ConsumedAt,
                    capture.TransitionRunId),
                cancellationToken);
        }

        private async Task<string?> GitScalarAsync(params string[] arguments)
        {
            try
            {
                ProcessRunResult result = await _processRunner.RunAsync("git", arguments, _repository.Path);
                if (result.ExitCode != 0)
                {
                    return null;
                }

                string value = result.StandardOutput.Trim();
                return value.Length == 0 ? null : value;
            }
            catch
            {
                return null;
            }
        }
    }

    // Appends the rendered-prompt fact: the exact agent-bound text, the template source hash it
    // was rendered from, the consumed input manifest, and the policy identity in effect.
    internal sealed class CanonicalRenderedPromptStore(
        CanonicalWorkflowPersistenceStore _persistence) : IRenderedPromptStore
    {
        public async Task AppendAsync(RenderedPromptCapture capture, CancellationToken cancellationToken) =>
            await _persistence.AppendRenderedPromptAsync(
                new CanonicalRenderedPromptRecord(
                    CausalUlid.NewId("rp"),
                    capture.TransitionRunId,
                    capture.AttemptId,
                    capture.PromptIdentity,
                    capture.TemplateSourceHash,
                    ConsumedInputFile.HashContent(capture.RenderedText),
                    capture.RenderedText,
                    capture.ConsumedInputs.Select(file => new CanonicalReadReceiptFile(file.Path, file.Sha256)).ToArray(),
                    capture.PolicyId,
                    capture.RenderedAt,
                    capture.SessionId,
                    capture.TurnId),
                cancellationToken);
    }

    private static class LocalVerificationTransitions
    {
        private static readonly HashSet<string> Supported =
        [
            "SelectEvaluationIntent",
            "VerifyPlanEntryContract",
            "VerifyExecuteEntryContract",
            "VerifyExecutionReadiness",
            "VerifyWorkflowExitGate",
        ];

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            Supported.Contains(definition.Identity.Value);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/local-verification/{definition.Identity}.md"];
    }

    private static class LocalArtifactTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "GenerateOperationalContext";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/local-artifacts/{definition.Identity}.md"];
    }

    private static class PlanProjectionTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "GenerateAdversarialProjection";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-projection/{definition.Identity}.md"];
    }

    private static class EvalPromptTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out _);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/eval-prompt/{definition.Identity}.md"];
    }

    private static class TraditionalRoadmapPromptTransitions
    {
        private const string RoadmapCompletionContext = ".agents/core/roadmap-completion-context.md";
        private const string Selection = ".agents/selection.md";
        private const string ActiveEpic = ".agents/epic.md";

        private static readonly IReadOnlyDictionary<string, string> PrimaryOutputs =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BootstrapRoadmapCompletionContext"] = RoadmapCompletionContext,
                ["UpdateRoadmapCompletionContext"] = RoadmapCompletionContext,
                ["SelectStrategicInitiative"] = Selection,
                ["AuditExistingEpic"] = ActiveEpic,
                ["CreateEpic"] = ActiveEpic,
                ["SplitEpic"] = ActiveEpic,
                ["RealignEpic"] = ActiveEpic,
                ["ReimagineEpic"] = ActiveEpic,
                ["RetireEpic"] = RoadmapCompletionContext,
            };

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            PrimaryOutputs.ContainsKey(definition.Identity.Value);

        public static bool TryGetPrimaryOutput(
            WorkflowTransitionDefinition definition,
            out string primaryOutput) =>
            PrimaryOutputs.TryGetValue(definition.Identity.Value, out primaryOutput!);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/traditional-roadmap-prompt/{definition.Identity}.md"];

        public static IReadOnlyList<string> ValidatePrimaryOutput(
            WorkflowTransitionDefinition definition,
            string content) =>
            definition.Identity.Value switch
            {
                "BootstrapRoadmapCompletionContext" or "UpdateRoadmapCompletionContext" or "RetireEpic" =>
                    ContainsHeading(content, "# Roadmap Completion Context")
                        ? []
                        : ["roadmap completion context output is missing `# Roadmap Completion Context`."],
                "SelectStrategicInitiative" =>
                    ContainsAnyHeading(
                        content,
                        "# Strategic Initiative Selection",
                        "# Next Strategic Initiative Selection")
                        ? []
                        : ["strategic initiative selection output is missing a recognized selection heading."],
                "AuditExistingEpic" or "CreateEpic" or "SplitEpic" or "RealignEpic" or "ReimagineEpic" =>
                    ValidatePreparedEpic(content),
                _ => [],
            };

        private static bool ContainsAnyHeading(string content, params string[] headings) =>
            headings.Any(heading => ContainsHeading(content, heading));

        private static bool ContainsHeading(string content, string heading) =>
            content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Any(line => string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));

        private static IReadOnlyList<string> ValidatePreparedEpic(string content)
        {
            string[] lines = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            var issues = new List<string>();
            int epicHeadings = lines.Count(line => line.TrimStart().StartsWith("# Epic:", StringComparison.Ordinal));
            if (epicHeadings == 0)
            {
                issues.Add("prepared epic is missing top-level `# Epic:` heading.");
            }
            else if (epicHeadings > 1)
            {
                issues.Add("prepared epic contains multiple top-level `# Epic:` headings.");
            }

            RequireHeading(lines, "## Epic Metadata", issues);
            if (!HasHeading(lines, "## Strategic Purpose") && !HasHeading(lines, "## Strategic Continuity"))
            {
                issues.Add("prepared epic is missing `## Strategic Purpose` or `## Strategic Continuity` section.");
            }

            RequireHeading(lines, "## Desired Capability", issues);
            RequireHeading(lines, "## Acceptance Criteria", issues);
            RequireHeading(lines, "## Milestone Roadmap", issues);
            if (!HasMilestoneRoadmapTable(lines))
            {
                issues.Add("prepared epic is missing the required milestone roadmap table header.");
            }

            return issues;
        }

        private static void RequireHeading(string[] lines, string heading, List<string> issues)
        {
            if (!HasHeading(lines, heading))
            {
                issues.Add($"prepared epic is missing `{heading}` section.");
            }
        }

        private static bool HasHeading(string[] lines, string heading) =>
            lines.Any(line => string.Equals(line.Trim(), heading, StringComparison.Ordinal));

        private static bool HasMilestoneRoadmapTable(string[] lines)
        {
            const string requiredHeader = "|MilestoneID|MilestoneName|Purpose|Outcome|DependsOn|CompletionSignal|";
            return lines
                .Select(line => line.Replace(" ", string.Empty, StringComparison.Ordinal).Trim())
                .Any(line => string.Equals(line, requiredHeader, StringComparison.Ordinal));
        }
    }

    private static class MilestoneDeepDiveTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "GenerateMilestoneDeepDivesForEpic";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/milestone-deep-dive/{definition.Identity}.md"];
    }

    private static class PlanReadOnlyReviewTransitions
    {
        public const string ReviewOutputPath = ".LoopRelay/evidence/plan/adversarial-review.md";

        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value == "RunAdversarialReview" &&
            definition.ExecutionPosture.Kind == ExecutionPostureKind.ReadOnlyPrompt;

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-read-only-review/{definition.Identity}.md"];
    }

    private static class PlanScopedArtifactTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.ExecutionPosture.Kind == ExecutionPostureKind.ScopedArtifactOperation &&
            PlanScopedArtifactOperationCatalog.Supports(definition.Identity);

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-scoped-artifact/{definition.Identity}.md"];
    }

    private static class PlanWarmSessionTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "WriteExecutablePlan" or "RevisePlan" &&
            definition.ExecutionPosture.Kind == ExecutionPostureKind.WarmSession;

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/plan-warm-session/{definition.Identity}.md"];
    }

    private static class ExecuteDecisionSessionTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" &&
            definition.ExecutionPosture.Kind == ExecutionPostureKind.DecisionSession;

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-decision-session/{definition.Identity}.md"];
    }

    private static class ExecuteImplementationTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "ExecuteImplementationSlice" or "GenerateHandoff";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-implementation/{definition.Identity}.md"];
    }

    private static class ExecuteRepositoryStateTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "UpdateOperationalContext" or "PublishRepositoryState" or
                "EvaluateCommit" or "EvaluateMilestoneCompletion";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-repository-state/{definition.Identity}.md"];
    }

    private static class ExecuteReviewTransitions
    {
        public static bool Supports(WorkflowTransitionDefinition definition) =>
            definition.Identity.Value is "RunNonImplementationReview" or "RunCompletionCertification" or
                "InterpretCompletionRoute";

        public static IReadOnlyList<string> Evidence(WorkflowTransitionDefinition definition) =>
            [$".LoopRelay/evidence/execute-review/{definition.Identity}.md"];
    }

    private sealed class UnifiedPromptRenderer : IPromptRenderer
    {
        public Task<RenderedPrompt> RenderAsync(
            WorkflowTransitionDefinition definition,
            PromptContext context,
            CancellationToken cancellationToken)
        {
            string text;
            string evidence;
            string? templateSourceHash;
            if (LocalVerificationTransitions.Supports(definition))
            {
                text = $"Local verification transition `{definition.Identity}` validates already-observed canonical products.";
                evidence = LocalVerificationTransitions.Evidence(definition)[0];
                templateSourceHash = null;
            }
            else if (LocalArtifactTransitions.Supports(definition))
            {
                text = $"Local artifact transition `{definition.Identity}` materializes deterministic repository-owned artifacts.";
                evidence = LocalArtifactTransitions.Evidence(definition)[0];
                templateSourceHash = null;
            }
            else if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset asset))
            {
                text = RenderEvalPromptAsset(asset, context);
                evidence = $"unified-cli/prompts/eval/{asset.PromptAssetName}@{asset.SourceHash}";
                templateSourceHash = asset.SourceHash;
            }
            else if (CanonicalPromptAssetCatalog.TryGetByPromptIdentity(definition.PromptIdentity, out CanonicalPromptAsset canonicalAsset))
            {
                text = definition.Identity.Value == "RunAdversarialReview"
                    ? RenderAdversarialPlanReviewPrompt(context)
                    : RenderCanonicalPromptAsset(canonicalAsset, context);
                evidence = $"unified-cli/prompts/core/{canonicalAsset.PromptAssetName}@{canonicalAsset.SourceHash}";
                templateSourceHash = canonicalAsset.SourceHash;
            }
            else
            {
                // No catalog owns this prompt identity: the placeholder never reaches an agent —
                // the executor fails closed with a typed not-wired result before any send — and
                // the evidence names the unwired state instead of fabricating an asset path.
                text = $"Prompt `{definition.PromptIdentity}` is registered but execution integration is not wired.";
                evidence = $"unified-cli/prompts/unwired/{definition.PromptIdentity}";
                templateSourceHash = null;
            }

            return Task.FromResult(new RenderedPrompt(definition.PromptIdentity, text, evidence, templateSourceHash));
        }

        private static string RenderEvalPromptAsset(
            EvalPromptAsset asset,
            PromptContext context)
        {
            string sections = context.Sections.Count == 0
                ? "No additional prompt context sections were provided."
                : string.Join(
                    Environment.NewLine + Environment.NewLine,
                    context.Sections.Select(section =>
                        $"""
                        ## {section.Title}

                        Source: {section.SourcePath}
                        Evidence: {string.Join(", ", section.Evidence)}

                        {section.Content}
                        """));
            return $"""
            {asset.PromptTemplate}

            ---

            # Canonical Runtime Context

            Prompt asset: {asset.PromptAssetName}
            Source hash: {asset.SourceHash}
            Primary output: {asset.PrimaryOutput}
            Primary output path: {asset.PrimaryOutputPath}

            {sections}
            """;
        }

        private static string RenderCanonicalPromptAsset(
            CanonicalPromptAsset asset,
            PromptContext context)
        {
            string products = context.Inputs.Products.Count == 0
                ? "No input products were resolved."
                : string.Join(
                    Environment.NewLine,
                    context.Inputs.Products.Select(product =>
                        $"- {product.Identity}: {string.Join(", ", product.StorageRepresentations)}"));
            string sections = context.Sections.Count == 0
                ? "No additional prompt context sections were provided."
                : string.Join(
                    Environment.NewLine + Environment.NewLine,
                    context.Sections.Select(section =>
                        $"""
                        ## {section.Title}

                        Source: {section.SourcePath}
                        Evidence: {string.Join(", ", section.Evidence)}

                        {section.Content}
                        """));
            return $"""
            {asset.PromptTemplate}

            ---

            # Canonical Runtime Context

            Prompt asset: {asset.PromptAssetName}
            Prompt identity: {asset.PromptIdentity}
            Source hash: {asset.SourceHash}

            ## Input Products

            {products}

            ## Context Sections

            {sections}
            """;
        }

        private static string RenderAdversarialPlanReviewPrompt(PromptContext context)
        {
            // All instruction text is template-owned (covered by the asset SourceHash); this branch only
            // routes the two declared inputs into their positional holes.
            string projection = RequiredSection(context, "Adversarial Projection");
            string plan = RequiredSection(context, "Executable Plan");
            return AdversarialPlanReview.Render(projection, plan);
        }

        private static string RequiredSection(PromptContext context, string title) =>
            context.Sections.FirstOrDefault(section => string.Equals(section.Title, title, StringComparison.Ordinal))?.Content
            ?? throw new InvalidOperationException($"Plan prompt context did not include `{title}`.");
    }

    private sealed class UnifiedPromptExecutor(
        Repository _repository,
        IAgentRuntime? _agentRuntime,
        IProcessRunner _processRunner,
        CanonicalWorkflowPersistenceStore _persistence,
        ResolvedOperationalPolicy _policy,
        bool _productionRuntime = false) : IPromptExecutor, IAsyncDisposable
    {
        private readonly IArtifactStore artifactStore = new FileSystemArtifactStore();
        private readonly ILoopConsole console = new ConsoleLoopConsole(TextWriter.Null, TextWriter.Null);
        // Real console for the D3 operational wrappers: usage-limit waits and input-wait progress
        // are user-facing reporting, unlike this class's silent internal console.
        private readonly ILoopConsole operationalConsole = new ConsoleLoopConsole();
        private readonly SessionSpineRecorder sessionRecorder = new(_persistence);
        private IAgentRuntime? recordingRuntime;
        // The gateway's deposit slot: the semantic capture for the NEXT transport send. Sends are
        // sequential within a run (concurrency is a deferred program stance), so one slot suffices.
        private RenderedPromptCapture? pendingRenderedPrompt;
        private IAgentSession? planAuthoringSession;
        private DecisionSession? executeDecisionSession;
        private IAgentSession? executionSession;
        private RepositorySliceBaseline? executionSliceBaseline;
        private IReadOnlyList<string> changedPathsAfterExecution = [];
        private int uncheckedMilestonesBeforeExecution;
        private int uncheckedMilestonesAfterExecution;
        private IReadOnlyList<string> nonImplementationReviewEvidencePaths = [];
        private IReadOnlyList<string> completionRecoveryEvidencePaths = [];
        private CompletionCertificationResult? completionCertificationResult;

        private PromptExecutionContext CurrentExecutionContext { get; set; } = PromptExecutionContext.Empty;

        // Rendered-prompt facts are minted at the SEND site — the point where text actually goes
        // to an agent — never at the render seam. Since M7 the send sites DEPOSIT the semantic
        // capture (identity, template hash, consumed inputs) with the gateway, and the recording
        // gateway appends the one fact at the actual transport moment, enriched with session/turn
        // identity and the transport-normalized text. A deposit whose send never happens mints no
        // fact; a deposit displaced by a later one was never sent and is dropped for the same
        // reason. This overload covers sends of the runtime-rendered text verbatim; the manifest
        // comes from the execution context the runtime handed over.
        private void DepositRenderedPrompt(RenderedPrompt prompt) =>
            DepositRenderedPrompt(
                prompt.PromptIdentity,
                prompt.TemplateSourceHash,
                CurrentExecutionContext.ConsumedFiles ?? []);

        private void DepositRenderedPrompt(
            string promptIdentity,
            string? templateSourceHash,
            IReadOnlyList<ConsumedInputFile> consumedInputs) =>
            pendingRenderedPrompt = new RenderedPromptCapture(
                CurrentExecutionContext.TransitionRunId ?? string.Empty,
                CurrentExecutionContext.AttemptId,
                promptIdentity,
                templateSourceHash,
                string.Empty,
                consumedInputs,
                _policy.PolicyId,
                DateTimeOffset.UtcNow);

        // Called by the recording gateway at the transport moment: consume the deposited semantic
        // capture (or synthesize an honest unattributed one) and append the ONE rendered-prompt
        // fact carrying session/turn identity and the exact transport text. Best-effort: rendered
        // evidence must never fail execution.
        private async Task TryAppendGatewayRenderedPromptAsync(
            string transportText,
            string role,
            string? sessionId,
            string turnId)
        {
            RenderedPromptCapture? deposit = pendingRenderedPrompt;
            pendingRenderedPrompt = null;
            RenderedPromptCapture capture = deposit ?? new RenderedPromptCapture(
                CurrentExecutionContext.TransitionRunId ?? string.Empty,
                CurrentExecutionContext.AttemptId,
                $"gateway/unattributed:{role}",
                null,
                string.Empty,
                [],
                _policy.PolicyId,
                DateTimeOffset.UtcNow);
            try
            {
                await new CanonicalRenderedPromptStore(_persistence).AppendAsync(
                    capture with
                    {
                        RenderedText = transportText,
                        RenderedAt = DateTimeOffset.UtcNow,
                        SessionId = sessionId,
                        TurnId = turnId,
                    },
                    CancellationToken.None);
            }
            catch
            {
                // Rendered-prompt persistence is supporting evidence; failing to append must not fail execution.
            }
        }

        private static IReadOnlyList<ConsumedInputFile> ConsumedFiles(
            params (string Path, string? Content)[] files) =>
            files
                .Where(file => file.Content is not null)
                .Select(file => ConsumedInputFile.FromContent(file.Path, file.Content!))
                .ToArray();

        // The runner-facing IRenderedPromptStore handed to DecisionSession and the helper prompt
        // runners: an append is a DEPOSIT the recording gateway consumes at the actual send it
        // precedes, enriching it with session/turn identity and the transport text. Depositors and
        // the gateway share one executor, and their sends are sequential within a run.
        private sealed class GatewayRenderedPromptDeposit(
            UnifiedPromptExecutor _executor) : IRenderedPromptStore
        {
            public Task AppendAsync(RenderedPromptCapture capture, CancellationToken cancellationToken)
            {
                _executor.pendingRenderedPrompt = capture;
                return Task.CompletedTask;
            }
        }

        /// <summary>The agent runtime gateway (M7): the D3 operational wrappers composed once at the
        /// runtime boundary under policy, with best-effort causal-spine recording outermost — one
        /// agent_sessions row per underlying session open (one-shot or persistent, including opens
        /// made inside DecisionSession and the helper prompt runners), one agent_turns row per
        /// completed AgentTurnResult, and one rendered-prompt fact per transport send.</summary>
        private IAgentRuntime? Runtime =>
            _agentRuntime is null
                ? null
                : recordingRuntime ??= new RecordingAgentRuntime(ComposeOperationalRuntime(_agentRuntime), this);

        // D3 (M7): telemetry, usage-limit wait/retry, and input-wait reporting are product
        // intent, reconnected once at the runtime boundary under the resolved policy — but only
        // around the real provider: injected runtimes have no provider to operate, so wrapping
        // them would record telemetry about nothing and render progress no one asked for.
        private IAgentRuntime ComposeOperationalRuntime(IAgentRuntime runtime) =>
            _productionRuntime
                ? OperationalRuntimeComposition.Compose(
                    runtime, _policy, _repository, _processRunner, operationalConsole)
                : runtime;

        public async Task<PromptExecutionResult> ExecuteAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            PromptExecutionContext context,
            CancellationToken cancellationToken)
        {
            CurrentExecutionContext = context;
            // A deposit stranded by an exception between deposit and send must not survive into
            // the next transition and mis-attribute its first send's fact.
            pendingRenderedPrompt = null;

            if (LocalVerificationTransitions.Supports(definition))
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    prompt.Text,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["local-verification"] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }

            if (LocalArtifactTransitions.Supports(definition))
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    prompt.Text,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["local-artifact"] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }

            if (PlanProjectionTransitions.Supports(definition))
            {
                return await ExecutePlanProjectionAsync(definition, prompt, cancellationToken);
            }

            if (EvalPromptTransitions.Supports(definition))
            {
                return await ExecuteOneShotAgentPromptAsync(
                    definition,
                    prompt,
                    "eval-prompt",
                    cancellationToken);
            }

            if (TraditionalRoadmapPromptTransitions.Supports(definition))
            {
                return await ExecuteOneShotAgentPromptAsync(
                    definition,
                    prompt,
                    "traditional-roadmap-prompt",
                    cancellationToken);
            }

            if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                return await ExecuteOneShotAgentPromptAsync(
                    definition,
                    prompt,
                    "milestone-deep-dive",
                    cancellationToken);
            }

            if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                return await ExecutePlanReadOnlyReviewAsync(definition, prompt, cancellationToken);
            }

            if (PlanScopedArtifactTransitions.Supports(definition))
            {
                return await ExecutePlanScopedArtifactOperationAsync(definition, prompt, cancellationToken);
            }

            if (PlanWarmSessionTransitions.Supports(definition))
            {
                return await ExecutePlanWarmSessionAsync(definition, prompt, cancellationToken);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition))
            {
                return await ExecuteDecisionSessionAsync(definition, prompt, cancellationToken);
            }

            if (ExecuteImplementationTransitions.Supports(definition))
            {
                return await ExecuteImplementationTransitionAsync(definition, prompt, cancellationToken);
            }

            if (ExecuteRepositoryStateTransitions.Supports(definition))
            {
                return await ExecuteRepositoryStateTransitionAsync(definition, prompt, cancellationToken);
            }

            if (ExecuteReviewTransitions.Supports(definition))
            {
                return await ExecuteReviewTransitionAsync(definition, prompt, cancellationToken);
            }

            return new PromptExecutionResult(
                PromptExecutionStatus.Failed,
                string.Empty,
                TimeSpan.Zero,
                new Dictionary<string, string>(),
                $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");
        }

        private async Task<PromptExecutionResult> ExecuteOneShotAgentPromptAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            string metadataKey,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");
            }

            try
            {
                DepositRenderedPrompt(prompt);
                AgentTurnResult result = await Runtime!.RunOneShotAsync(
                    AgentSpecs.Operational(_repository, AgentEffortLevel.High, "xhigh"),
                    prompt.Text,
                    onChunk: null,
                    cancellationToken);
                if (result.State != AgentTurnState.Completed)
                {
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        result.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        WithDiagnostics($"{definition.Identity} turn ended in state {result.State}.", result.Diagnostics));
                }

                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    result.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        [metadataKey] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Cancelled,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>());
            }
        }

        private async Task<PromptExecutionResult> ExecutePlanProjectionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");
            }

            try
            {
                ProjectContextProjectionService service = CreateProjectionService();
                ProjectContextProjectionResult projection = await service.EnsureFreshAsync(
                    ProjectionRuntimePromptNames.AdversarialPlanReview,
                    cancellationToken);
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    projection.Content,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["plan-projection"] = definition.Identity.Value,
                        ["projection-generated"] = projection.Generated.ToString(),
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Cancelled,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>());
            }
            catch (Exception exception)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    exception.Message);
            }
        }

        private async Task<PromptExecutionResult> ExecutePlanReadOnlyReviewAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");
            }

            IAgentSession? session = null;
            try
            {
                session = await Runtime!.OpenSessionAsync(AgentSpecs.Review(_repository), cancellationToken);
                DepositRenderedPrompt(prompt);
                AgentTurnResult result = await session.RunTurnAsync(
                    prompt.Text,
                    onChunk: null,
                    cancellationToken);
                if (result.State != AgentTurnState.Completed)
                {
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        result.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        WithDiagnostics($"Adversarial review turn ended in state {result.State}.", result.Diagnostics));
                }

                if (string.IsNullOrWhiteSpace(result.Output))
                {
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        result.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        "adversarial review returned no output");
                }

                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    result.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["plan-read-only-review"] = definition.Identity.Value,
                        ["thread-id"] = session.ThreadId ?? string.Empty,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Cancelled,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>());
            }
            finally
            {
                if (session is not null)
                {
                    await Runtime!.CloseSessionAsync(session);
                }
            }
        }

        private ProjectContextProjectionService CreateProjectionService()
        {
            var artifacts = new ProjectionArtifacts(artifactStore, _repository);
            ProjectionDefinitionRegistry registry = ProjectionDefinitionRegistry.CreateDefault();
            return new ProjectContextProjectionService(
                artifacts,
                registry,
                new ProjectionManifestStore(artifacts),
                new ProjectionValidator(registry),
                new ProjectionPromptRunner(
                    Runtime ?? throw new InvalidOperationException("Projection prompt runner requires an agent runtime."),
                    _repository,
                    new ConsoleLoopConsole(TextWriter.Null, TextWriter.Null),
                    new GatewayRenderedPromptDeposit(this),
                    _policy.PolicyId,
                    CurrentExecutionContext));
        }

        private async Task<PromptExecutionResult> ExecutePlanScopedArtifactOperationAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");
            }

            PlanScopedArtifactOperationSpec operation =
                PlanScopedArtifactOperationCatalog.Get(definition.Identity);
            (bool inputsValid, string? inputFailure, string? changedGuardSnapshot) =
                await VerifyPlanScopedInputsAsync(operation);
            if (!inputsValid)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    inputFailure);
            }

            OperationPermissionProfile profile = ToPermissionProfile(operation);
            ArtifactMutationTransaction transaction =
                await ArtifactMutationTransaction.CaptureAsync(artifactStore, _repository, profile);
            IAgentSession? session = null;
            bool keepChanges = false;

            try
            {
                session = await Runtime!.OpenSessionAsync(
                    AgentSpecs.ScopedArtifactOperation(_repository, AgentEffortLevel.High, "xhigh", profile),
                    cancellationToken);
                DepositRenderedPrompt(prompt);
                AgentTurnResult result = await session.RunTurnAsync(
                    prompt.Text,
                    onChunk: null,
                    cancellationToken);
                if (result.State != AgentTurnState.Completed)
                {
                    await transaction.RestoreAsync();
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        result.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        WithDiagnostics($"{operation.Label} turn ended in state {result.State}.", result.Diagnostics));
                }

                string? outputFailure = await VerifyPlanScopedOutputsAsync(
                    operation,
                    transaction,
                    changedGuardSnapshot);
                if (outputFailure is not null)
                {
                    await transaction.RestoreAsync();
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        result.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        outputFailure);
                }

                keepChanges = true;
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    result.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["plan-scoped-artifact"] = definition.Identity.Value,
                        ["operation"] = operation.Label,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                if (!keepChanges)
                {
                    await transaction.RestoreAsync();
                }

                return new PromptExecutionResult(
                    PromptExecutionStatus.Cancelled,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>());
            }
            catch (Exception exception)
            {
                if (!keepChanges)
                {
                    await transaction.RestoreAsync();
                }

                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    exception.Message);
            }
            finally
            {
                if (session is not null)
                {
                    await Runtime!.CloseSessionAsync(session);
                }
            }
        }

        private async Task<(bool IsValid, string? Failure, string? ChangedGuardSnapshot)> VerifyPlanScopedInputsAsync(
            PlanScopedArtifactOperationSpec operation)
        {
            string? changedGuardSnapshot = null;
            foreach (string read in operation.AllowedReads)
            {
                string absolutePath = ResolveRepositoryPath(_repository, read);
                string? content = await artifactStore.ReadAsync(absolutePath);
                if (content is null)
                {
                    return (
                        false,
                        $"{operation.Label}: required input {read} was not found in the repository.",
                        null);
                }

                if (operation.ChangedGuard is not null &&
                    string.Equals(read, operation.ChangedGuard, StringComparison.Ordinal))
                {
                    changedGuardSnapshot = content;
                }
            }

            if (operation.ChangedGuard is not null && changedGuardSnapshot is null)
            {
                return (
                    false,
                    $"{operation.Label} is misconfigured: ChangedGuard {operation.ChangedGuard} is not among the operation's AllowedReads, so there is no pre-turn snapshot to compare against.",
                    null);
            }

            return (true, null, changedGuardSnapshot);
        }

        private async Task<string?> VerifyPlanScopedOutputsAsync(
            PlanScopedArtifactOperationSpec operation,
            ArtifactMutationTransaction transaction,
            string? changedGuardSnapshot)
        {
            IReadOnlyList<string> deleted = await transaction.DeletedSnapshotFilesAsync();
            if (deleted.Count > 0)
            {
                return $"{operation.Label} deleted declared artifact(s): {string.Join(", ", deleted)}.";
            }

            foreach (string requiredOutput in operation.RequiredOutputs)
            {
                if (!await artifactStore.ExistsAsync(ResolveRepositoryPath(_repository, requiredOutput)))
                {
                    return $"{operation.Label} did not produce {requiredOutput}.";
                }
            }

            if (operation.RequiredOutputGlob is { } requiredGlob)
            {
                IReadOnlyList<string> matches = await ListAbsoluteAsync(requiredGlob);
                if (matches.Count == 0)
                {
                    return $"{operation.Label} produced no files matching {requiredGlob.Directory}/{requiredGlob.Pattern}.";
                }

                if (operation.RequireChecklistInGlob)
                {
                    int total = 0;
                    foreach (string match in matches)
                    {
                        string content = await artifactStore.ReadAsync(match) ?? string.Empty;
                        (int matchTotal, _, _) = ExecutionMilestoneGate.CountCheckboxes(content);
                        total += matchTotal;
                    }

                    if (total == 0)
                    {
                        return "extracted milestones contain no trackable checkboxes";
                    }
                }
            }

            if (operation.ChangedGuard is { } changedGuard)
            {
                string absolutePath = ResolveRepositoryPath(_repository, changedGuard);
                if (!await artifactStore.ExistsAsync(absolutePath))
                {
                    return $"{operation.Label} left {changedGuard} missing - it must remain present.";
                }

                string changedContent = await artifactStore.ReadAsync(absolutePath) ?? string.Empty;
                if (string.Equals(changedContent, changedGuardSnapshot ?? string.Empty, StringComparison.Ordinal))
                {
                    return $"{operation.Label} left {changedGuard} unchanged - the expected rewrite did not happen.";
                }
            }

            return null;
        }

        private async Task<IReadOnlyList<string>> ListAbsoluteAsync(OperationPathGlob glob) =>
            await artifactStore.ListAsync(ResolveRepositoryPath(_repository, glob.Directory), glob.Pattern);

        private OperationPermissionProfile ToPermissionProfile(PlanScopedArtifactOperationSpec operation) =>
            new(
                operation.Label,
                _repository.Path,
                operation.AllowedReads,
                operation.AllowedReadGlobs,
                operation.AllowedWrites,
                operation.AllowedWriteGlobs);

        private async Task<PromptExecutionResult> ExecutePlanWarmSessionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return new PromptExecutionResult(
                    PromptExecutionStatus.Failed,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>(),
                    $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");
            }

            try
            {
                if (definition.Identity.Value == "WriteExecutablePlan")
                {
                    if (planAuthoringSession is not null)
                    {
                        await ClosePlanSessionAsync();
                    }

                    planAuthoringSession = await Runtime!.OpenSessionAsync(
                        AgentSpecs.PlanAuthoring(_repository),
                        cancellationToken);
                }
                else if (planAuthoringSession is null)
                {
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        string.Empty,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        "RevisePlan requires the warm planning session opened by WriteExecutablePlan.");
                }

                DepositRenderedPrompt(prompt);
                AgentTurnResult result = await planAuthoringSession.RunTurnAsync(
                    prompt.Text,
                    onChunk: null,
                    cancellationToken);
                if (result.State != AgentTurnState.Completed)
                {
                    await ClosePlanSessionAsync();
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        result.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        WithDiagnostics($"{definition.Identity} turn ended in state {result.State}.", result.Diagnostics));
                }

                string? threadId = planAuthoringSession.ThreadId;
                if (definition.Identity.Value == "RevisePlan")
                {
                    await ClosePlanSessionAsync();
                }

                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    result.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["plan-warm-session"] = definition.Identity.Value,
                        ["thread-id"] = threadId ?? string.Empty,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                await ClosePlanSessionAsync();
                return new PromptExecutionResult(
                    PromptExecutionStatus.Cancelled,
                    string.Empty,
                    TimeSpan.Zero,
                    new Dictionary<string, string>());
            }
        }

        private async Task<PromptExecutionResult> ExecuteDecisionSessionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return NotWired(definition);
            }

            try
            {
                executeDecisionSession ??= new DecisionSession(
                    Runtime!,
                    new DecisionSessionRouter(),
                    CreateLoopArtifacts(),
                    console,
                    _repository,
                    _costModel: null,
                    _resumeStore: new SqliteDecisionSessionResumeStore(_repository),
                    _projectionService: null,
                    _resumeEnabled: _policy.DecisionSessionResume,
                    _operationalContextGrowthStreakWarningThreshold: _policy.OperationalContextGrowthWarningStreak,
                    _renderedPromptStore: new GatewayRenderedPromptDeposit(this),
                    _policyIdentity: _policy.PolicyId);
                await executeDecisionSession.RunAsync(
                    cancellationToken,
                    new LoopHistoryLineage(
                        CurrentExecutionContext?.RunId,
                        CurrentExecutionContext?.TransitionRunId,
                        CurrentExecutionContext?.AttemptId));
                string decisions = await ReadRequiredAsync(OrchestrationArtifactPaths.Decisions, cancellationToken);
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    decisions,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-decision-session"] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                return Cancelled();
            }
            catch (Exception exception)
            {
                return Failed(exception.Message);
            }
        }

        private async Task<PromptExecutionResult> ExecuteImplementationTransitionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return NotWired(definition);
            }

            return definition.Identity.Value switch
            {
                "ExecuteImplementationSlice" => await ExecuteImplementationSliceAsync(definition, prompt, cancellationToken),
                "GenerateHandoff" => await ExecuteHandoffAsync(definition, prompt, cancellationToken),
                _ => NotWired(definition),
            };
        }

        private async Task<PromptExecutionResult> ExecuteImplementationSliceAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            try
            {
                if (executionSession is not null)
                {
                    await CloseExecutionSessionAsync();
                }

                LoopArtifacts artifacts = CreateLoopArtifacts();
                string? plan = await artifacts.ReadPlanAsync();
                string? details = await artifacts.ReadDetailsAsync();
                (string? decisions, string? decisionsPath) = await artifacts.ReadLatestDecisionsAsync();
                if (string.IsNullOrWhiteSpace(decisions))
                {
                    return Failed($"{OrchestrationArtifactPaths.Decisions} was not available for execution.");
                }

                MilestoneGate milestones = CreateMilestoneGate();
                uncheckedMilestonesBeforeExecution = (await milestones.GetUntickedItemsAsync()).Count;
                executionSliceBaseline = await CreateBaselineStore().CapturePreSliceAsync();

                string executionPrompt = ContinueExecution.Render(plan, details, decisions);
                DepositRenderedPrompt(
                    "ContinueExecution",
                    ContinueExecution.SourceHash,
                    ConsumedFiles(
                        (OrchestrationArtifactPaths.Plan, plan),
                        (OrchestrationArtifactPaths.Details, details),
                        (decisionsPath ?? OrchestrationArtifactPaths.Decisions, decisions)));
                executionSession = await Runtime!.OpenSessionAsync(
                    AgentSpecs.Operational(
                        _repository,
                        AgentEffortLevel.Medium,
                        identifier: null,
                        sandboxIdentifier: "danger-full-access"),
                    cancellationToken);

                AgentTurnResult work = await executionSession.RunTurnAsync(
                    executionPrompt,
                    onChunk: null,
                    cancellationToken);
                if (work.State != AgentTurnState.Completed)
                {
                    await CloseExecutionSessionAsync();
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        work.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        WithDiagnostics($"Execution turn ended in state {work.State}.", work.Diagnostics));
                }

                changedPathsAfterExecution = await CreateChangeDetector().GetRealChangedPathsAsync();
                uncheckedMilestonesAfterExecution = (await milestones.GetUntickedItemsAsync()).Count;
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    work.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-implementation"] = definition.Identity.Value,
                        ["changed-paths"] = string.Join("|", changedPathsAfterExecution),
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                await CloseExecutionSessionAsync();
                return Cancelled();
            }
            catch (Exception exception)
            {
                await CloseExecutionSessionAsync();
                return Failed(exception.Message);
            }
        }

        private async Task<PromptExecutionResult> ExecuteHandoffAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (executionSession is null)
            {
                return Failed("GenerateHandoff requires the held-open execution session created by ExecuteImplementationSlice.");
            }

            try
            {
                string handoffPrompt;
                if (changedPathsAfterExecution.Count > 0)
                {
                    handoffPrompt = GenerateHandoff.Text;
                    DepositRenderedPrompt("GenerateHandoff", GenerateHandoff.SourceHash, []);
                }
                else
                {
                    IReadOnlyList<string> unticked = await CreateMilestoneGate().GetUntickedItemsAsync();
                    handoffPrompt = GenerateNoChangesHandoff.Render(string.Join("\n", unticked));
                    DepositRenderedPrompt("GenerateNoChangesHandoff", GenerateNoChangesHandoff.SourceHash, []);
                }

                AgentTurnResult handoff = await executionSession.RunTurnAsync(
                    handoffPrompt,
                    onChunk: null,
                    cancellationToken);
                if (handoff.State != AgentTurnState.Completed)
                {
                    await CloseExecutionSessionAsync();
                    return new PromptExecutionResult(
                        PromptExecutionStatus.Failed,
                        handoff.Output,
                        TimeSpan.Zero,
                        new Dictionary<string, string>(),
                        WithDiagnostics($"Handoff turn ended in state {handoff.State}.", handoff.Diagnostics));
                }

                if (!await artifactStore.ExistsAsync(ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.LiveHandoff)))
                {
                    await CloseExecutionSessionAsync();
                    return Failed($"Execution completed but {OrchestrationArtifactPaths.LiveHandoff} was not written.");
                }

                await CloseExecutionSessionAsync();
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    handoff.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-implementation"] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                await CloseExecutionSessionAsync();
                return Cancelled();
            }
            catch (Exception exception)
            {
                await CloseExecutionSessionAsync();
                return Failed(exception.Message);
            }
        }

        private async Task<PromptExecutionResult> ExecuteRepositoryStateTransitionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            try
            {
                string output = definition.Identity.Value switch
                {
                    "UpdateOperationalContext" => await ExecuteOperationalContextUpdateAsync(cancellationToken),
                    "PublishRepositoryState" => await ExecuteRepositoryPublicationAsync(cancellationToken),
                    "EvaluateCommit" => await ExecuteCommitEvaluationAsync(cancellationToken),
                    "EvaluateMilestoneCompletion" => await ExecuteMilestoneCompletionEvaluationAsync(cancellationToken),
                    _ => throw new InvalidOperationException($"Unsupported Execute repository-state transition `{definition.Identity}`."),
                };
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-repository-state"] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                return Cancelled();
            }
            catch (Exception exception)
            {
                return Failed(exception.Message);
            }
        }

        private async Task<string> ExecuteOperationalContextUpdateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoopArtifacts artifacts = CreateLoopArtifacts();
            if (await artifacts.ExistsAsync(OrchestrationArtifactPaths.OperationalDelta))
            {
                return "Live operational delta is ready for effect-phase archival.";
            }

            return "No live operational delta was present; operational context remains current.";
        }

        private async Task<string> ExecuteRepositoryPublicationAsync(CancellationToken cancellationToken)
        {
            var publisher = new AgentsSubmodulePublisher(_processRunner, _repository, console);
            bool committed = await publisher.PublishAsync(
                AgentsSubmodulePublisher.ExecutionHandoffMessage,
                cancellationToken);
            return committed
                ? "Published .agents repository state and recorded parent gitlink."
                : "Repository state publication found no .agents changes to commit.";
        }

        private async Task<string> ExecuteCommitEvaluationAsync(CancellationToken cancellationToken)
        {
            MilestoneGate milestones = CreateMilestoneGate();
            uncheckedMilestonesAfterExecution = (await milestones.GetUntickedItemsAsync()).Count;
            var commitGate = new CommitGate(
                CreateChangeDetector(),
                _processRunner,
                _repository,
                console,
                _policy.MaxNoChangesCommits);
            bool stalled = await commitGate.CommitPushAndEvaluateAsync(
                uncheckedMilestonesBeforeExecution,
                uncheckedMilestonesAfterExecution,
                cancellationToken);
            bool substantiveProgress = changedPathsAfterExecution.Count > 0 ||
                uncheckedMilestonesAfterExecution < uncheckedMilestonesBeforeExecution;
            int previousNoProgressCount = await ReadExecuteNoProgressCountAsync(cancellationToken);
            int noProgressCount = substantiveProgress ? 0 : previousNoProgressCount + 1;
            stalled = stalled || noProgressCount > _policy.MaxNoChangesCommits;
            string stallEvidence = await WriteExecuteStallEvidenceAsync(
                previousNoProgressCount,
                noProgressCount,
                substantiveProgress,
                stalled,
                cancellationToken);
            return $"""
                # Commit Evaluation

                | Field | Value |
                |---|---|
                | Unchecked Before | {uncheckedMilestonesBeforeExecution} |
                | Unchecked After | {uncheckedMilestonesAfterExecution} |
                | Changed Paths | {string.Join(", ", changedPathsAfterExecution)} |
                | Substantive Progress | {substantiveProgress} |
                | Previous No-Progress Count | {previousNoProgressCount} |
                | Consecutive No-Progress Count | {noProgressCount} |
                | Stalled | {stalled} |
                | Stall Evidence | {stallEvidence} |
                """;
        }

        private async Task<int> ReadExecuteNoProgressCountAsync(CancellationToken cancellationToken)
        {
            string path = ResolveRepositoryPath(_repository, ".LoopRelay/evidence/execute-stall/state.md");
            if (!File.Exists(path))
            {
                return 0;
            }

            string content = await File.ReadAllTextAsync(path, cancellationToken);
            foreach (string line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("| Consecutive No-Progress Count |", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] cells = trimmed
                    .Trim('|')
                    .Split('|', StringSplitOptions.TrimEntries);
                if (cells.Length >= 2 &&
                    int.TryParse(cells[1], out int count))
                {
                    return count;
                }
            }

            return 0;
        }

        private async Task<string> WriteExecuteStallEvidenceAsync(
            int previousNoProgressCount,
            int noProgressCount,
            bool substantiveProgress,
            bool stalled,
            CancellationToken cancellationToken)
        {
            const string relativePath = ".LoopRelay/evidence/execute-stall/state.md";
            string path = ResolveRepositoryPath(_repository, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                $"""
                # Execute Stall State

                | Field | Value |
                |---|---|
                | Previous No-Progress Count | {previousNoProgressCount} |
                | Consecutive No-Progress Count | {noProgressCount} |
                | Max No-Progress Count | {_policy.MaxNoChangesCommits} |
                | Substantive Progress | {substantiveProgress} |
                | Stalled | {stalled} |
                | Updated At | {DateTimeOffset.UtcNow:O} |

                The canonical Execute workflow persists the no-progress count because transition executions may
                happen across separate CLI invocations. Stalled execution requires explicit recovery before
                silently retrying the same no-progress loop.
                """,
                cancellationToken);
            return relativePath;
        }

        private async Task<string> ExecuteMilestoneCompletionEvaluationAsync(CancellationToken cancellationToken)
        {
            bool complete = await CreateMilestoneGate().IsEpicCompleteAsync();
            return $"""
                # Milestone Completion Evaluation

                | Field | Value |
                |---|---|
                | Complete | {complete} |
                | Unchecked Before | {uncheckedMilestonesBeforeExecution} |
                | Unchecked After | {uncheckedMilestonesAfterExecution} |
                """;
        }

        private async Task<PromptExecutionResult> ExecuteReviewTransitionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            if (_agentRuntime is null)
            {
                return NotWired(definition);
            }

            try
            {
                string output = definition.Identity.Value switch
                {
                    "RunNonImplementationReview" => await ExecuteNonImplementationReviewAsync(cancellationToken),
                    "RunCompletionCertification" => await ExecuteCompletionCertificationAsync(cancellationToken),
                    "InterpretCompletionRoute" => ExecuteCompletionRouteInterpretation(),
                    _ => throw new InvalidOperationException($"Unsupported Execute review transition `{definition.Identity}`."),
                };
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-review"] = definition.Identity.Value,
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                return Cancelled();
            }
            catch (Exception exception)
            {
                return Failed(exception.Message);
            }
        }

        private async Task<string> ExecuteNonImplementationReviewAsync(CancellationToken cancellationToken)
        {
            if (executionSliceBaseline is null)
            {
                executionSliceBaseline = await CreateBaselineStore().CapturePreSliceAsync();
            }

            INonImplementationPostExecutionReviewService service = CreateNonImplementationPostExecutionReviewService();
            NonImplementationPostExecutionReviewResult result =
                await service.ReviewAfterExecutionAsync(executionSliceBaseline, cancellationToken);
            nonImplementationReviewEvidencePaths = result.EvidencePaths;
            return $"""
                # Non-Implementation Review

                | Field | Value |
                |---|---|
                | Execution Slice ID | {result.ExecutionSliceId} |
                | Changed Files | {result.Summary.ChangedFileCount} |
                | Semantic Candidates | {result.Summary.SemanticCandidateCount} |
                | Confirmed | {result.Summary.ConfirmedCount} |
                | Evidence | {string.Join(", ", result.EvidencePaths)} |
                """;
        }

        private async Task<string> ExecuteCompletionCertificationAsync(CancellationToken cancellationToken)
        {
            var completionObserver = new CompletionPhaseEvidenceObserver(_repository, console);
            completionObserver.Phase("Completion review");
            NonImplementationCompletionReviewResult completionReview =
                await CreateNonImplementationCompletionReviewService().ReviewAsync(cancellationToken);
            if (completionReview.IsBlocked)
            {
                completionRecoveryEvidencePaths = completionObserver.EvidencePaths;
                throw new InvalidOperationException(
                    "Non-implementation completion review stopped final evaluation: " +
                    string.Join("; ", completionReview.UnresolvedMessages));
            }

            if (completionReview.AppliedDeletePaths.Count > 0)
            {
                var commitGate = new CommitGate(
                    CreateChangeDetector(),
                    _processRunner,
                    _repository,
                    console,
                    _policy.MaxNoChangesCommits);
                await commitGate.CommitPushIfChangedAsync(cancellationToken);
            }

            nonImplementationReviewEvidencePaths = nonImplementationReviewEvidencePaths
                .Concat(completionReview.EvidencePaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var promptRunner = new AgentCompletionPromptRunner(
                Runtime!,
                _repository,
                new GatewayRenderedPromptDeposit(this),
                _policy.PolicyId,
                CurrentExecutionContext);
            var archiveService = new CompletedEpicArchiveService(artifactStore, promptRunner, completionObserver);
            var service = new CompletionCertificationService(
                artifactStore,
                CreateProjectionService(),
                promptRunner,
                archiveService,
                _observer: completionObserver);
            completionCertificationResult = await service.CertifyPlanCompletionAsync(
                new CompletionCertificationRequest(
                    _repository,
                    NonImplementationReviewEvidencePaths: nonImplementationReviewEvidencePaths),
                cancellationToken);
            completionRecoveryEvidencePaths = completionObserver.EvidencePaths;
            if (completionCertificationResult.Outcome != CompletionCertificationServiceOutcome.Completed)
            {
                throw new InvalidOperationException(
                    $"Completion certification {completionCertificationResult.Outcome}: {completionCertificationResult.Message}");
            }

            return RenderCompletionCertificationResult(completionCertificationResult, completionRecoveryEvidencePaths);
        }

        private string ExecuteCompletionRouteInterpretation()
        {
            if (completionCertificationResult is null)
            {
                throw new InvalidOperationException("InterpretCompletionRoute requires RunCompletionCertification evidence.");
            }

            return $"""
                # Completion Route

                | Field | Value |
                |---|---|
                | Outcome | {completionCertificationResult.Outcome} |
                | Should Close Epic | {completionCertificationResult.Route?.ShouldCloseEpic} |
                | Message | {completionCertificationResult.Message} |
                | Evidence | {string.Join(", ", completionCertificationResult.EvidencePaths)} |
                | Recovery Phase Evidence | {string.Join(", ", completionRecoveryEvidencePaths)} |
                """;
        }

        private static string RenderCompletionCertificationResult(
            CompletionCertificationResult result,
            IReadOnlyList<string> recoveryPhaseEvidence) =>
            $"""
            # Completion Certification

            | Field | Value |
            |---|---|
            | Outcome | {result.Outcome} |
            | Message | {result.Message} |
            | Evaluation Evidence | {result.EvaluationEvidencePath} |
            | Completed Epic Archive | {result.CompletedEpicArchiveIndex} |
            | Synthesis | {result.CompletedEpicSynthesisPath} |
            | Roadmap Context Changed | {result.RoadmapCompletionContextChanged} |
            | Evidence | {string.Join(", ", result.EvidencePaths)} |
            | Recovery Phase Evidence | {string.Join(", ", recoveryPhaseEvidence)} |
            """;

        private sealed class CompletionPhaseEvidenceObserver(
            Repository _repository,
            ILoopConsole _console) : ICompletionObserver
        {
            private readonly object gate = new();
            private readonly List<string> evidencePaths = [];
            private int sequence;

            public IReadOnlyList<string> EvidencePaths
            {
                get
                {
                    lock (gate)
                    {
                        return evidencePaths.ToArray();
                    }
                }
            }

            public void Phase(string phase)
            {
                _console.Phase(phase);
                Record("Phase", phase);
            }

            public void Info(string text)
            {
                _console.Info(text);
                Record("Info", text);
            }

            public void Warn(string text)
            {
                _console.Warn(text);
                Record("Warn", text);
            }

            private void Record(string kind, string message)
            {
                lock (gate)
                {
                    sequence++;
                    string relativePath =
                        $".LoopRelay/evidence/execute-completion-recovery/{sequence:0000}-{Slug(kind + "-" + message)}.md";
                    string path = ResolveRepositoryPath(relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(
                        path,
                        $"""
                        # Execute Completion Recovery Phase

                        | Field | Value |
                        |---|---|
                        | Event Kind | {kind} |
                        | Message | {Escape(message)} |
                        | Sequence | {sequence} |
                        | Created At | {DateTimeOffset.UtcNow:O} |

                        This marker records progress through canonical Execute completion. Recovery must inspect
                        these phase markers together with transition runs, products, warnings, archive records,
                        and completion certification evidence before retrying interrupted closure work.
                        """);
                    evidencePaths.Add(relativePath);
                }
            }

            private string ResolveRepositoryPath(string relativePath)
            {
                string root = Path.GetFullPath(_repository.Path);
                string path = Path.GetFullPath(Path.Combine(
                    root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                string relative = Path.GetRelativePath(root, path);
                if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                {
                    throw new InvalidOperationException("Completion phase evidence path escaped the repository root.");
                }

                return path;
            }

            private static string Slug(string value)
            {
                var builder = new StringBuilder();
                foreach (char c in value.ToLowerInvariant())
                {
                    if (char.IsAsciiLetterOrDigit(c))
                    {
                        builder.Append(c);
                    }
                    else if (builder.Length == 0 || builder[^1] != '-')
                    {
                        builder.Append('-');
                    }
                }

                string slug = builder.ToString().Trim('-');
                return string.IsNullOrWhiteSpace(slug) ? "event" : slug;
            }

            private static string Escape(string value) =>
                value
                    .Replace("|", "\\|", StringComparison.Ordinal)
                    .Replace("\r", " ", StringComparison.Ordinal)
                    .Replace("\n", " ", StringComparison.Ordinal)
                    .Trim();
        }

        // The ledger-backed history store makes decision/handoff/delta history ledger-authoritative
        // on the canonical path; numbered files remain as derived projections only.
        private LoopArtifacts CreateLoopArtifacts() =>
            new(artifactStore, _repository, new LedgerLoopHistoryStore(artifactStore, _repository));

        private MilestoneGate CreateMilestoneGate() =>
            new(artifactStore, _repository);

        private WorkingTreeChangeDetector CreateChangeDetector() =>
            new(_processRunner, _repository);

        private RepositorySliceBaselineStore CreateBaselineStore() =>
            new(new RepositoryChangeSetDetector(_processRunner, _repository), artifactStore);

        private INonImplementationPostExecutionReviewService CreateNonImplementationPostExecutionReviewService()
        {
            var ledger = new NonImplementationReviewLedgerStore(artifactStore);
            var runner = new AgentNonImplementationReviewRunner(
                Runtime!,
                _repository,
                new GatewayRenderedPromptDeposit(this),
                _policy.PolicyId,
                CurrentExecutionContext);
            return new NonImplementationPostExecutionReviewService(
                CreateBaselineStore(),
                new NonImplementationArtifactClassifier(),
                new NonImplementationSemanticConfirmer(ledger, runner),
                artifactStore);
        }

        private INonImplementationCompletionReviewService CreateNonImplementationCompletionReviewService()
        {
            var ledger = new NonImplementationReviewLedgerStore(artifactStore);
            var runner = new AgentNonImplementationReviewRunner(
                Runtime!,
                _repository,
                new GatewayRenderedPromptDeposit(this),
                _policy.PolicyId,
                CurrentExecutionContext);
            return new NonImplementationCompletionReviewService(
                new RepositoryChangeSetDetector(_processRunner, _repository),
                new NonImplementationArtifactClassifier(),
                new NonImplementationSemanticConfirmer(ledger, runner),
                ledger,
                artifactStore,
                _repository.Path);
        }

        private async Task<string> ReadRequiredAsync(string relativePath, CancellationToken cancellationToken)
        {
            string path = ResolveRepositoryPath(_repository, relativePath);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"{relativePath} was not written.");
            }

            string content = await File.ReadAllTextAsync(path, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException($"{relativePath} is empty.");
            }

            return content;
        }

        private PromptExecutionResult NotWired(WorkflowTransitionDefinition definition) =>
            new(
                PromptExecutionStatus.Failed,
                string.Empty,
                TimeSpan.Zero,
                new Dictionary<string, string>(),
                $"Prompt execution integration is not wired for `{definition.PromptIdentity}`.");

        private static PromptExecutionResult Cancelled() =>
            new(
                PromptExecutionStatus.Cancelled,
                string.Empty,
                TimeSpan.Zero,
                new Dictionary<string, string>());

        private static PromptExecutionResult Failed(string? message) =>
            new(
                PromptExecutionStatus.Failed,
                string.Empty,
                TimeSpan.Zero,
                new Dictionary<string, string>(),
                message ?? "Prompt execution failed.");

        private async ValueTask ClosePlanSessionAsync()
        {
            if (planAuthoringSession is null || _agentRuntime is null)
            {
                planAuthoringSession = null;
                return;
            }

            IAgentSession session = planAuthoringSession;
            planAuthoringSession = null;
            await Runtime!.CloseSessionAsync(session);
        }

        private async ValueTask CloseExecutionSessionAsync()
        {
            if (executionSession is null || _agentRuntime is null)
            {
                executionSession = null;
                return;
            }

            IAgentSession session = executionSession;
            executionSession = null;
            await Runtime!.CloseSessionAsync(session);
        }

        private static string WithDiagnostics(string message, string? diagnostics) =>
            string.IsNullOrWhiteSpace(diagnostics)
                ? message
                : $"{message} Agent stderr (tail):\n{diagnostics}";

        public async ValueTask DisposeAsync()
        {
            await ClosePlanSessionAsync();
            await CloseExecutionSessionAsync();
            if (executeDecisionSession is not null)
            {
                await executeDecisionSession.DisposeAsync();
                executeDecisionSession = null;
            }
        }

        /// <summary>Best-effort agent_sessions/agent_turns spine writes. Every method swallows store failures:
        /// prompt execution must never fail because session evidence recording failed.</summary>
        private sealed class SessionSpineRecorder(CanonicalWorkflowPersistenceStore _store)
        {
            public async Task<AgentSessionRecord?> TryBeginSessionAsync(
                AgentSessionSpec spec,
                string provider,
                PromptExecutionContext context,
                CancellationToken cancellationToken)
            {
                try
                {
                    var record = new AgentSessionRecord(
                        AgentSessionIdentity.New().Value,
                        context.AttemptId,
                        null,
                        provider,
                        spec.ResumeThreadId,
                        spec.Role.ToString(),
                        spec.SessionId.Value.ToString("D"),
                        DateTimeOffset.UtcNow,
                        null,
                        spec.Effort.Identifier ?? spec.Effort.Level.ToString(),
                        spec.Sandbox.Identifier);
                    await _store.UpsertAgentSessionAsync(record, cancellationToken);
                    return record;
                }
                catch
                {
                    return null;
                }
            }

            public async Task TryRecordTurnAsync(
                AgentSessionRecord? session,
                string turnId,
                AgentTurnResult result,
                string promptSha256,
                CancellationToken cancellationToken)
            {
                if (session is null)
                {
                    return;
                }

                try
                {
                    // Terminal turn evidence is written even when the caller's token has fired.
                    await _store.AppendAgentTurnAsync(
                        new AgentTurnRecord(
                            turnId,
                            session.SessionId,
                            result.TurnIndex,
                            DateTimeOffset.UtcNow,
                            result.State.ToString(),
                            promptSha256,
                            result.Usage.PromptTokens,
                            result.Usage.OutputTokens,
                            result.Usage.CachedInputTokens,
                            DiagnosisKind(result),
                            result.Diagnostics),
                        CancellationToken.None);
                }
                catch
                {
                    // Duplicate turn indexes (resume edge) and store failures are tolerated silently.
                }
            }

            // A turn that ended by exception instead of a provider result (caller cancellation,
            // transport failure) still records terminal evidence — with CancellationToken.None,
            // because the caller's token has typically already fired. Index collisions with a
            // later retry of the same logical turn fall into the same tolerated-duplicate posture
            // as the resume edge.
            public async Task TryRecordThrownTurnAsync(
                AgentSessionRecord? session,
                string turnId,
                int turnIndex,
                bool cancelled,
                string promptSha256,
                string exceptionMessage)
            {
                if (session is null)
                {
                    return;
                }

                try
                {
                    await _store.AppendAgentTurnAsync(
                        new AgentTurnRecord(
                            turnId,
                            session.SessionId,
                            turnIndex,
                            DateTimeOffset.UtcNow,
                            cancelled ? nameof(AgentTurnState.Canceled) : nameof(AgentTurnState.Failed),
                            promptSha256,
                            null,
                            null,
                            null,
                            cancelled ? "Cancelled" : "ProviderFailure",
                            exceptionMessage),
                        CancellationToken.None);
                }
                catch
                {
                    // Duplicate turn indexes and store failures are tolerated silently.
                }
            }

            // The typed diagnosis the domain may consume (M7): provider diagnostic strings are
            // retained verbatim in the diagnostics column as evidence, never as the classifier.
            // The usage-limit predicate is shared with the retry seam so the recorded kind and
            // the wait/retry behavior cannot drift apart.
            private static string? DiagnosisKind(AgentTurnResult result) =>
                result.State switch
                {
                    AgentTurnState.Canceled => "Cancelled",
                    AgentTurnState.Failed => UsageLimitDetector.IsUsageLimitFailure(result)
                        ? "UsageLimit"
                        : "ProviderFailure",
                    _ => null,
                };

            public async Task TryCompleteSessionAsync(
                AgentSessionRecord? session,
                string? providerThreadId)
            {
                if (session is null)
                {
                    return;
                }

                try
                {
                    await _store.UpsertAgentSessionAsync(
                        session with
                        {
                            ProviderThreadId = providerThreadId ?? session.ProviderThreadId,
                            CompletedAt = DateTimeOffset.UtcNow,
                        },
                        CancellationToken.None);
                }
                catch
                {
                    // Session completion is supporting evidence only.
                }
            }
        }

        /// <summary>Wraps the real agent runtime so every underlying session open (including opens made by
        /// DecisionSession and the helper prompt runners) records one agent_sessions row tied to the ambient
        /// PromptExecutionContext, and every completed turn records one agent_turns row. A resumed session gets
        /// a NEW row whose provider_thread_id is the resumed thread id.</summary>
        private sealed class RecordingAgentRuntime(
            IAgentRuntime _inner,
            UnifiedPromptExecutor _executor) : IAgentRuntime
        {
            public AgentRuntimeCapabilities Capabilities => _inner.Capabilities;

            public async Task<IAgentSession> OpenSessionAsync(
                AgentSessionSpec spec,
                CancellationToken cancellationToken = default)
            {
                // Capability negotiation happens at the gateway, before launch (D5/M7): a spec
                // requiring an undeclared capability is a typed outcome, never a silent fallback.
                AgentCapabilityNegotiation.EnsureCanOpenSession(_inner.Capabilities, spec);

                // The effective specification is recorded BEFORE the provider launches (the M7
                // verification brief) — a spawn failure still leaves the attempted launch as
                // evidence, completed immediately so the record does not read as live.
                AgentSessionRecord? record = await _executor.sessionRecorder.TryBeginSessionAsync(
                    spec,
                    _inner.Capabilities.Provider,
                    _executor.CurrentExecutionContext,
                    cancellationToken);
                try
                {
                    IAgentSession session = await _inner.OpenSessionAsync(spec, cancellationToken);
                    return new RecordingAgentSession(session, _executor, record);
                }
                catch
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, providerThreadId: null);
                    throw;
                }
            }

            public async Task<AgentTurnResult> RunOneShotAsync(
                AgentSessionSpec spec,
                string prompt,
                Func<AgentStreamChunk, Task>? onChunk = null,
                CancellationToken cancellationToken = default)
            {
                AgentCapabilityNegotiation.EnsureCanRunOneShot(_inner.Capabilities);

                // One-shot transport normalization happens HERE, before the fact is recorded, so
                // the rendered-prompt fact holds exactly the bytes that go on the wire (the
                // session's own normalization is then an idempotent no-op).
                string transportPrompt = AgentPromptTransport.EnsureTrailingNewline(prompt);
                AgentSessionRecord? record = await _executor.sessionRecorder.TryBeginSessionAsync(
                    spec,
                    _inner.Capabilities.Provider,
                    _executor.CurrentExecutionContext,
                    cancellationToken);
                string turnId = TurnIdentity.New().Value;
                await _executor.TryAppendGatewayRenderedPromptAsync(
                    transportPrompt,
                    spec.Role.ToString(),
                    record?.SessionId,
                    turnId);
                try
                {
                    AgentTurnResult result = await _inner.RunOneShotAsync(
                        spec, transportPrompt, onChunk, cancellationToken);
                    await _executor.sessionRecorder.TryRecordTurnAsync(
                        record,
                        turnId,
                        result,
                        ConsumedInputFile.HashContent(transportPrompt),
                        cancellationToken);
                    return result;
                }
                catch (Exception exception)
                {
                    // A thrown turn (caller cancellation, transport failure) still leaves turn
                    // evidence — otherwise cancelled work vanishes from the spine while its
                    // rendered fact claims a send happened.
                    await _executor.sessionRecorder.TryRecordThrownTurnAsync(
                        record,
                        turnId,
                        turnIndex: 0,
                        cancelled: exception is OperationCanceledException,
                        ConsumedInputFile.HashContent(transportPrompt),
                        exception.Message);
                    throw;
                }
                finally
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, providerThreadId: null);
                }
            }

            public async ValueTask CloseSessionAsync(IAgentSession session)
            {
                if (session is RecordingAgentSession recording)
                {
                    await recording.CompleteAsync();
                    await _inner.CloseSessionAsync(recording.Inner);
                    return;
                }

                await _inner.CloseSessionAsync(session);
            }
        }

        private sealed class RecordingAgentSession(
            IAgentSession _inner,
            UnifiedPromptExecutor _executor,
            AgentSessionRecord? _record) : IAgentSession
        {
            public IAgentSession Inner => _inner;

            public SessionIdentity SessionId => _inner.SessionId;

            public string RepositoryId => _inner.RepositoryId;

            public SessionRole Role => _inner.Role;

            public AgentSessionMode Mode => _inner.Mode;

            public AgentProcessState State => _inner.State;

            public int CompletedTurns => _inner.CompletedTurns;

            public AgentTokenUsage TotalUsage => _inner.TotalUsage;

            public string? ThreadId => _inner.ThreadId;

            public async Task<AgentTurnResult> RunTurnAsync(
                string prompt,
                Func<AgentStreamChunk, Task>? onChunk = null,
                CancellationToken cancellationToken = default)
            {
                // Persistent turns are framed as JSON-RPC — the prompt is never mutated in
                // transport, so the fact records it verbatim. The turn id is minted before the
                // send so the rendered fact and the turn row share identity even when the turn
                // ends by exception.
                string turnId = TurnIdentity.New().Value;
                // The in-flight turn's index: CompletedTurns only increments after completion.
                int turnIndex = _inner.CompletedTurns;
                await _executor.TryAppendGatewayRenderedPromptAsync(
                    prompt,
                    _inner.Role.ToString(),
                    _record?.SessionId,
                    turnId);
                try
                {
                    AgentTurnResult result = await _inner.RunTurnAsync(prompt, onChunk, cancellationToken);
                    await _executor.sessionRecorder.TryRecordTurnAsync(
                        _record,
                        turnId,
                        result,
                        ConsumedInputFile.HashContent(prompt),
                        cancellationToken);
                    return result;
                }
                catch (Exception exception)
                {
                    // A thrown turn (caller cancellation, transport failure) still leaves turn
                    // evidence — otherwise cancelled work vanishes from the spine while its
                    // rendered fact claims a send happened.
                    await _executor.sessionRecorder.TryRecordThrownTurnAsync(
                        _record,
                        turnId,
                        turnIndex,
                        cancelled: exception is OperationCanceledException,
                        ConsumedInputFile.HashContent(prompt),
                        exception.Message);
                    throw;
                }
            }

            public Task CancelAsync(CancellationToken cancellationToken = default) =>
                _inner.CancelAsync(cancellationToken);

            public Task CompleteAsync() =>
                _executor.sessionRecorder.TryCompleteSessionAsync(_record, _inner.ThreadId);

            public async ValueTask DisposeAsync()
            {
                await CompleteAsync();
                await _inner.DisposeAsync();
            }
        }
    }

    private sealed class UnifiedOutputInterpreter(Repository _repository) : IOutputInterpreter
    {
        public async Task<InterpretedTransitionOutput> InterpretAsync(
            WorkflowTransitionDefinition definition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            if (PlanWarmSessionTransitions.Supports(definition))
            {
                IReadOnlyList<string> evidence = PlanWarmSessionTransitions.Evidence(definition)
                    .Concat([OrchestrationArtifactPaths.Plan])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan warm-session output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (PlanProjectionTransitions.Supports(definition))
            {
                IReadOnlyList<string> evidence = PlanProjectionTransitions.Evidence(definition)
                    .Concat([PlanPromptContext.AdversarialPlanReviewProjectionPath])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan projection output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset evalAsset))
            {
                if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
                {
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Unavailable,
                        [],
                        $"Eval prompt `{definition.Identity}` returned no output.",
                        []);
                }

                string path = ResolveRepositoryPath(_repository, evalAsset.PrimaryOutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, executionResult.RawOutput, cancellationToken);
                IReadOnlyList<string> evidence = EvalPromptTransitions.Evidence(definition)
                    .Concat([evalAsset.PrimaryOutputPath])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Eval prompt output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (TraditionalRoadmapPromptTransitions.TryGetPrimaryOutput(definition, out string roadmapOutput))
            {
                if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
                {
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Unavailable,
                        [],
                        $"Traditional roadmap prompt `{definition.Identity}` returned no output.",
                        []);
                }

                string path = ResolveRepositoryPath(_repository, roadmapOutput);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, executionResult.RawOutput, cancellationToken);
                IReadOnlyList<string> evidence = TraditionalRoadmapPromptTransitions.Evidence(definition)
                    .Concat([roadmapOutput])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Traditional roadmap prompt output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                IReadOnlyList<string> evidence = MilestoneDeepDiveTransitions.Evidence(definition)
                    .Concat([$"{OrchestrationArtifactPaths.SpecsDirectory}/*.md"])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Milestone deep-dive output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
                {
                    return new InterpretedTransitionOutput(
                        OutputInterpretationStatus.Unavailable,
                        [],
                        "Adversarial review output was empty.",
                        []);
                }

                string path = ResolveRepositoryPath(_repository, PlanReadOnlyReviewTransitions.ReviewOutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, executionResult.RawOutput, cancellationToken);
                IReadOnlyList<string> evidence = PlanReadOnlyReviewTransitions.Evidence(definition)
                    .Concat([PlanReadOnlyReviewTransitions.ReviewOutputPath])
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan read-only review output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (PlanScopedArtifactTransitions.Supports(definition))
            {
                PlanScopedArtifactOperationSpec operation =
                    PlanScopedArtifactOperationCatalog.Get(definition.Identity);
                IReadOnlyList<string> evidence = PlanScopedArtifactTransitions.Evidence(definition)
                    .Concat(operation.RequiredOutputs)
                    .Concat(operation.RequiredOutputGlob is { } glob
                        ? [$"{glob.Directory}/{glob.Pattern}"]
                        : Array.Empty<string>())
                    .ToArray();
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Valid,
                    definition.ProducedProducts
                        .Select(product => ProductRecord(product, definition.Identity, evidence))
                        .ToArray(),
                    $"Plan scoped artifact output interpreted for `{definition.Identity}`.",
                    evidence);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition) ||
                ExecuteImplementationTransitions.Supports(definition) ||
                ExecuteRepositoryStateTransitions.Supports(definition) ||
                ExecuteReviewTransitions.Supports(definition))
            {
                return await InterpretExecuteOutputAsync(definition, executionResult, cancellationToken);
            }

            if (!LocalVerificationTransitions.Supports(definition) &&
                !LocalArtifactTransitions.Supports(definition))
            {
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Unavailable,
                    [],
                    "Output interpretation is not wired because prompt execution did not run.",
                    []);
            }

            IReadOnlyList<string> localEvidence = LocalVerificationTransitions.Supports(definition)
                ? LocalVerificationTransitions.Evidence(definition)
                : LocalArtifactTransitions.Evidence(definition);
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(product, definition.Identity, localEvidence))
                .ToArray();
            return new InterpretedTransitionOutput(
                OutputInterpretationStatus.Valid,
                products,
                $"Local verification interpreted `{definition.Identity}` successfully.",
                localEvidence);
        }

        private async Task<InterpretedTransitionOutput> InterpretExecuteOutputAsync(
            WorkflowTransitionDefinition definition,
            PromptExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(executionResult.RawOutput))
            {
                return new InterpretedTransitionOutput(
                    OutputInterpretationStatus.Unavailable,
                    [],
                    $"Execute transition `{definition.Identity}` returned no output.",
                    []);
            }

            IReadOnlyList<string> evidence = ExecuteEvidence(definition);
            foreach (string evidencePath in evidence)
            {
                string path = ResolveRepositoryPath(_repository, evidencePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # Execute Transition Output

                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}

                    {executionResult.RawOutput}
                    """,
                    cancellationToken);
            }

            IReadOnlyList<string> productEvidence = evidence
                .Concat(ExecuteArtifactEvidence(definition))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string causalIdentity = Hash(executionResult.RawOutput);
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    productEvidence,
                    causalIdentity,
                    ExecuteStorageRepresentations(product, definition, productEvidence)))
                .ToArray();
            return new InterpretedTransitionOutput(
                OutputInterpretationStatus.Valid,
                products,
                $"Execute transition output interpreted for `{definition.Identity}`.",
                productEvidence);
        }
    }

    private sealed class UnifiedProductValidator(Repository _repository) : IProductValidator
    {
        public async Task<ProductValidationResult> ValidateAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            CancellationToken cancellationToken)
        {
            if (PlanWarmSessionTransitions.Supports(definition))
            {
                return ValidatePlanWarmSessionOutput(definition, output);
            }

            if (PlanProjectionTransitions.Supports(definition))
            {
                return await ValidateSingleArtifactOutputAsync(
                    definition,
                    output,
                    PlanPromptContext.AdversarialPlanReviewProjectionPath,
                    "Plan projection",
                    validateProjection: true);
            }

            if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset evalAsset))
            {
                return await ValidateSingleArtifactOutputAsync(
                    definition,
                    output,
                    evalAsset.PrimaryOutputPath,
                    "Eval prompt",
                    validateProjection: false);
            }

            if (TraditionalRoadmapPromptTransitions.TryGetPrimaryOutput(definition, out string roadmapOutput))
            {
                return await ValidateTraditionalRoadmapOutputAsync(
                    definition,
                    output,
                    roadmapOutput,
                    "Traditional roadmap prompt");
            }

            if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                return await ValidateMilestoneDeepDiveOutputAsync(definition, output);
            }

            if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                return await ValidateSingleArtifactOutputAsync(
                    definition,
                    output,
                    PlanReadOnlyReviewTransitions.ReviewOutputPath,
                    "Plan read-only review",
                    validateProjection: false);
            }

            if (PlanScopedArtifactTransitions.Supports(definition))
            {
                return await ValidatePlanScopedArtifactOutputAsync(definition, output);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition) ||
                ExecuteImplementationTransitions.Supports(definition) ||
                ExecuteRepositoryStateTransitions.Supports(definition) ||
                ExecuteReviewTransitions.Supports(definition))
            {
                return await ValidateExecuteOutputAsync(definition, output);
            }

            if (!LocalVerificationTransitions.Supports(definition) &&
                !LocalArtifactTransitions.Supports(definition))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    "Product validation is not wired because prompt execution did not run.",
                    []);
            }

            HashSet<ProductIdentity> actual = output.CandidateProducts
                .Select(product => product.Identity)
                .ToHashSet();
            ProductIdentity[] missing = definition.ProducedProducts
                .Select(product => product.Identity)
                .Where(identity => !actual.Contains(identity))
                .ToArray();
            ProductValidationStatus status = missing.Length == 0
                ? ProductValidationStatus.Valid
                : ProductValidationStatus.Missing;
            return new ProductValidationResult(
                status,
                output.CandidateProducts,
                missing,
                [],
                [],
                [],
                missing.Length == 0
                    ? $"Local verification validated `{definition.Identity}` products."
                    : $"Local verification did not produce all declared products for `{definition.Identity}`.",
                output.Evidence);
        }

        private async Task<ProductValidationResult> ValidateMilestoneDeepDiveOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            string directory = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.SpecsDirectory);
            string[] matches = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.md")
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            IReadOnlyList<string> evidence = output.Evidence;
            if (matches.Length == 0)
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{OrchestrationArtifactPaths.SpecsDirectory}/*.md was not written.",
                    evidence);
            }

            var relativePaths = new List<string>();
            var builder = new StringBuilder();
            foreach (string match in matches)
            {
                string relativePath = ArtifactPath.ToRepositoryRelativePath(_repository, match);
                string content = await File.ReadAllTextAsync(match);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                        [],
                        [],
                        $"{relativePath} is empty.",
                        evidence.Concat([relativePath]).ToArray());
                }

                relativePaths.Add(relativePath);
                builder
                    .AppendLine($"--- {relativePath} ---")
                    .AppendLine(content);
            }

            IReadOnlyList<string> productEvidence = evidence
                .Concat(relativePaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    productEvidence,
                    Hash(builder.ToString()),
                    relativePaths))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Milestone deep-dive transition `{definition.Identity}` produced valid milestone specifications.",
                productEvidence);
        }

        private ProductValidationResult ValidatePlanWarmSessionOutput(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            string planPath = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Plan);
            IReadOnlyList<string> evidence = output.Evidence;
            if (!File.Exists(planPath))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{OrchestrationArtifactPaths.Plan} was not written.",
                    evidence);
            }

            string plan = File.ReadAllText(planPath);
            if (string.IsNullOrWhiteSpace(plan))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    $"{OrchestrationArtifactPaths.Plan} is empty.",
                    evidence);
            }

            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence,
                    Hash(plan)))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Plan warm-session transition `{definition.Identity}` produced a valid executable plan.",
                evidence);
        }

        private async Task<ProductValidationResult> ValidateSingleArtifactOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            string relativePath,
            string title,
            bool validateProjection)
        {
            IReadOnlyList<string> evidence = output.Evidence.Count == 0
                ? [relativePath]
                : output.Evidence;
            string path = ResolveRepositoryPath(_repository, relativePath);
            if (!File.Exists(path))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{relativePath} was not written.",
                    evidence);
            }

            string content = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    $"{relativePath} is empty.",
                    evidence);
            }

            if (validateProjection)
            {
                ProjectionDefinitionRegistry registry = ProjectionDefinitionRegistry.CreateDefault();
                ProjectionValidationResult validation =
                    new ProjectionValidator(registry).Validate(ProjectionRuntimePromptNames.AdversarialPlanReview, content);
                if (!validation.IsValid)
                {
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                        [],
                        [],
                        validation.Error ?? "Projection validation failed.",
                        evidence);
                }
            }

            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence,
                    Hash(content),
                    [relativePath]))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"{title} transition `{definition.Identity}` produced a valid artifact.",
                evidence);
        }

        private async Task<ProductValidationResult> ValidatePlanScopedArtifactOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            PlanScopedArtifactOperationSpec operation =
                PlanScopedArtifactOperationCatalog.Get(definition.Identity);
            List<string> evidence = [..PlanScopedArtifactTransitions.Evidence(definition)];
            var invalid = new List<ProductIdentity>();
            var missing = new List<ProductIdentity>();
            var artifactPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (string requiredOutput in operation.RequiredOutputs)
            {
                string path = ResolveRepositoryPath(_repository, requiredOutput);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{requiredOutput} was not written.",
                        evidence);
                }

                string content = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(content))
                {
                    invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        invalid.Distinct().ToArray(),
                        [],
                        [],
                        $"{requiredOutput} is empty.",
                        evidence.Concat([requiredOutput]).ToArray());
                }

                artifactPaths.Add(requiredOutput);
                evidence.Add(requiredOutput);
            }

            if (operation.RequiredOutputGlob is { } requiredGlob)
            {
                string directory = ResolveRepositoryPath(_repository, requiredGlob.Directory);
                string[] matches = Directory.Exists(directory)
                    ? Directory.GetFiles(directory, requiredGlob.Pattern)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : [];
                if (matches.Length == 0)
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{requiredGlob.Directory}/{requiredGlob.Pattern} was not written.",
                        evidence);
                }

                string[] relativeMatches = matches
                    .Select(path => ArtifactPath.ToRepositoryRelativePath(_repository, path))
                    .ToArray();
                foreach (string relativeMatch in relativeMatches)
                {
                    artifactPaths.Add(relativeMatch);
                    evidence.Add(relativeMatch);
                }

                if (operation.RequireChecklistInGlob)
                {
                    ExecutionMilestoneGateResult gate =
                        ExecutionMilestoneGate.Evaluate(_repository.Path, relativeMatches);
                    if (!gate.ReadinessSatisfied)
                    {
                        invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                        return new ProductValidationResult(
                            ProductValidationStatus.Invalid,
                            [],
                            [],
                            invalid.Distinct().ToArray(),
                            [],
                            [],
                            "extracted milestones contain no trackable checkboxes.",
                            evidence);
                    }
                }
            }

            if (operation.ChangedGuard is { } changedGuard)
            {
                string path = ResolveRepositoryPath(_repository, changedGuard);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{changedGuard} was not written.",
                        evidence);
                }

                artifactPaths.Add(changedGuard);
                evidence.Add(changedGuard);
            }

            string causalIdentity = await HashArtifactsAsync(artifactPaths);
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence.Distinct(StringComparer.Ordinal).ToArray(),
                    causalIdentity,
                    PlanScopedStorageRepresentations(product, artifactPaths)))
                .ToArray();

            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Plan scoped artifact transition `{definition.Identity}` produced valid repository artifacts.",
                evidence.Distinct(StringComparer.Ordinal).ToArray());
        }

        private async Task<ProductValidationResult> ValidateTraditionalRoadmapOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output,
            string relativePath,
            string title)
        {
            IReadOnlyList<string> evidence = output.Evidence.Count == 0
                ? [relativePath]
                : output.Evidence;
            string path = ResolveRepositoryPath(_repository, relativePath);
            if (!File.Exists(path))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Missing,
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    [],
                    $"{relativePath} was not written.",
                    evidence);
            }

            string content = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    $"{relativePath} is empty.",
                    evidence.Concat([relativePath]).ToArray());
            }

            IReadOnlyList<string> validationIssues =
                TraditionalRoadmapPromptTransitions.ValidatePrimaryOutput(definition, content);
            if (validationIssues.Count > 0)
            {
                return new ProductValidationResult(
                    ProductValidationStatus.Invalid,
                    [],
                    [],
                    definition.ProducedProducts.Select(product => product.Identity).ToArray(),
                    [],
                    [],
                    string.Join("; ", validationIssues),
                    evidence.Concat([relativePath]).ToArray());
            }

            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    evidence.Concat([relativePath]).Distinct(StringComparer.Ordinal).ToArray(),
                    Hash(content),
                    [relativePath]))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"{title} transition `{definition.Identity}` produced a parsed and validated artifact.",
                evidence.Concat([relativePath]).Distinct(StringComparer.Ordinal).ToArray());
        }

        private async Task<ProductValidationResult> ValidateExecuteOutputAsync(
            WorkflowTransitionDefinition definition,
            InterpretedTransitionOutput output)
        {
            IReadOnlyList<string> evidence = output.Evidence.Count == 0
                ? ExecuteEvidence(definition)
                : output.Evidence;
            var missing = new List<ProductIdentity>();
            var invalid = new List<ProductIdentity>();
            var artifactPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (string relativePath in ExecuteRequiredArtifacts(definition))
            {
                string path = ResolveRepositoryPath(_repository, relativePath);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{relativePath} was not written.",
                        evidence);
                }

                string content = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(content))
                {
                    invalid.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Invalid,
                        [],
                        [],
                        invalid.Distinct().ToArray(),
                        [],
                        [],
                        $"{relativePath} is empty.",
                        evidence.Concat([relativePath]).ToArray());
                }

                artifactPaths.Add(relativePath);
            }

            foreach (string evidencePath in ExecuteEvidence(definition))
            {
                string path = ResolveRepositoryPath(_repository, evidencePath);
                if (!File.Exists(path))
                {
                    missing.AddRange(definition.ProducedProducts.Select(product => product.Identity));
                    return new ProductValidationResult(
                        ProductValidationStatus.Missing,
                        [],
                        missing.Distinct().ToArray(),
                        [],
                        [],
                        [],
                        $"{evidencePath} was not written.",
                        evidence);
                }

                artifactPaths.Add(evidencePath);
            }

            string causalIdentity = await HashArtifactsAsync(artifactPaths);
            IReadOnlyList<string> productEvidence = evidence
                .Concat(artifactPaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            ProductRecord[] products = definition.ProducedProducts
                .Select(product => ProductRecord(
                    product,
                    definition.Identity,
                    productEvidence,
                    causalIdentity,
                    ExecuteStorageRepresentations(product, definition, productEvidence)))
                .ToArray();
            return new ProductValidationResult(
                ProductValidationStatus.Valid,
                products,
                [],
                [],
                [],
                [],
                $"Execute transition `{definition.Identity}` produced valid repository artifacts.",
                productEvidence);
        }

        private async Task<string> HashArtifactsAsync(IEnumerable<string> relativePaths)
        {
            var builder = new StringBuilder();
            foreach (string relativePath in relativePaths
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal))
            {
                string path = ResolveRepositoryPath(_repository, relativePath);
                if (!File.Exists(path))
                {
                    continue;
                }

                builder
                    .AppendLine($"--- {relativePath} ---")
                    .AppendLine(await File.ReadAllTextAsync(path));
            }

            return Hash(builder.ToString());
        }
    }

    private sealed class UnifiedEffectExecutor(
        Repository _repository,
        CanonicalWorkflowPersistenceStore _store,
        IReadOnlyList<WorkflowDefinition> _definitions) : IEffectExecutor
    {
        public async Task<EffectExecutionResult> ExecuteAsync(
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            EffectExecutionContext context,
            CancellationToken cancellationToken)
        {
            if (LocalArtifactTransitions.Supports(definition))
            {
                return await ExecuteLocalArtifactAsync(definition, validation, cancellationToken);
            }

            if (PlanWarmSessionTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? PlanWarmSessionTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Plan warm-session",
                    context,
                    cancellationToken);
            }

            if (PlanProjectionTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? PlanProjectionTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Plan projection",
                    context,
                    cancellationToken);
            }

            if (EvalPromptTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? EvalPromptTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Eval prompt",
                    context,
                    cancellationToken);
            }

            if (TraditionalRoadmapPromptTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? TraditionalRoadmapPromptTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Traditional roadmap prompt",
                    context,
                    cancellationToken);
            }

            if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? MilestoneDeepDiveTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Milestone deep-dive",
                    context,
                    cancellationToken);
            }

            if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? PlanReadOnlyReviewTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Plan read-only review",
                    context,
                    cancellationToken);
            }

            if (PlanScopedArtifactTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? PlanScopedArtifactTransitions.Evidence(definition)
                        : validation.Evidence,
                    "Plan scoped artifact",
                    context,
                    cancellationToken);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition) ||
                ExecuteImplementationTransitions.Supports(definition) ||
                ExecuteRepositoryStateTransitions.Supports(definition) ||
                ExecuteReviewTransitions.Supports(definition))
            {
                return await ExecuteSuccessfulProductEffectsAsync(
                    definition,
                    validation,
                    validation.Evidence.Count == 0
                        ? ExecuteEvidence(definition)
                        : validation.Evidence,
                    "Execute transition",
                    context,
                    cancellationToken);
            }

            if (!LocalVerificationTransitions.Supports(definition))
            {
                return new EffectExecutionResult(
                    EffectExecutionStatus.Failed,
                    [],
                    "Effect execution is not wired because product validation did not run.",
                    []);
            }

            WorkflowDefinition workflow = WorkflowFor(definition);
            WorkflowStageDefinition stage = StageFor(workflow, definition);
            IReadOnlyList<string> evidence = validation.Evidence.Count == 0
                ? LocalVerificationTransitions.Evidence(definition)
                : validation.Evidence;
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                evidence,
                "Local verification",
                context,
                cancellationToken);
        }

        private async Task<EffectExecutionResult> ExecuteSuccessfulProductEffectsAsync(
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            string evidenceTitle,
            EffectExecutionContext context,
            CancellationToken cancellationToken)
        {
            WorkflowDefinition workflow = WorkflowFor(definition);
            WorkflowStageDefinition stage = StageFor(workflow, definition);
            bool executeCommitStalled = await IsExecuteCommitStalledAsync(definition, cancellationToken);
            IReadOnlyList<string> effectEvidence = AddExecuteStallStateEvidence(definition, evidence);
            effectEvidence = await AddFinalClosedStatePersistenceEvidenceAsync(
                definition,
                effectEvidence,
                cancellationToken);
            effectEvidence = await AddTraditionalRoadmapRigorEvidenceAsync(
                workflow,
                stage,
                definition,
                validation,
                effectEvidence,
                cancellationToken);
            if (PlanWarmSessionTransitions.Supports(definition))
            {
                await MaterializePlanWarmSessionEvidenceAsync(workflow, definition, validation, effectEvidence, cancellationToken);
            }
            else if (PlanProjectionTransitions.Supports(definition))
            {
                await MaterializePlanTransitionEvidenceAsync(
                    workflow,
                    definition,
                    validation,
                    effectEvidence,
                    PlanProjectionTransitions.Evidence(definition),
                    "Plan Projection Evidence",
                    cancellationToken);
            }
            else if (EvalPromptTransitions.Supports(definition))
            {
                await MaterializePlanTransitionEvidenceAsync(
                    workflow,
                    definition,
                    validation,
                    effectEvidence,
                    EvalPromptTransitions.Evidence(definition),
                    "Eval Prompt Evidence",
                    cancellationToken);
            }
            else if (TraditionalRoadmapPromptTransitions.Supports(definition))
            {
                await MaterializePlanTransitionEvidenceAsync(
                    workflow,
                    definition,
                    validation,
                    effectEvidence,
                    TraditionalRoadmapPromptTransitions.Evidence(definition),
                    "Traditional Roadmap Prompt Evidence",
                    cancellationToken);
            }
            else if (MilestoneDeepDiveTransitions.Supports(definition))
            {
                await MaterializePlanTransitionEvidenceAsync(
                    workflow,
                    definition,
                    validation,
                    effectEvidence,
                    MilestoneDeepDiveTransitions.Evidence(definition),
                    "Milestone Deep-Dive Evidence",
                    cancellationToken);
            }
            else if (PlanReadOnlyReviewTransitions.Supports(definition))
            {
                await MaterializePlanTransitionEvidenceAsync(
                    workflow,
                    definition,
                    validation,
                    effectEvidence,
                    PlanReadOnlyReviewTransitions.Evidence(definition),
                    "Plan Read-Only Review Evidence",
                    cancellationToken);
            }
            else if (PlanScopedArtifactTransitions.Supports(definition))
            {
                await MaterializePlanScopedArtifactEvidenceAsync(workflow, definition, validation, effectEvidence, cancellationToken);
            }
            else if (ExecuteDecisionSessionTransitions.Supports(definition) ||
                ExecuteImplementationTransitions.Supports(definition) ||
                ExecuteRepositoryStateTransitions.Supports(definition) ||
                ExecuteReviewTransitions.Supports(definition))
            {
                await MaterializeExecuteTransitionEvidenceAsync(workflow, definition, validation, effectEvidence, context, cancellationToken);
            }
            else
            {
                await MaterializeLocalVerificationEvidenceAsync(workflow, definition, validation, effectEvidence, cancellationToken);
            }

            foreach (ProductRecord product in validation.Products)
            {
                await _store.UpsertProductAsync(product, cancellationToken);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (executeCommitStalled)
            {
                await PersistExecuteStallStateAsync(workflow, stage, definition, effectEvidence, now, cancellationToken);
                EffectExecutionRecord[] stalledRecords = definition.Effects
                    .Select(effect => new EffectExecutionRecord(
                        effect.Identity,
                        EffectExecutionStatus.Stalled,
                        $"Execute commit evaluation stalled at `{effect.Identity}`.",
                        effectEvidence))
                    .ToArray();
                return new EffectExecutionResult(
                    EffectExecutionStatus.Stalled,
                    stalledRecords,
                    "Execute commit evaluation detected repeated no-substantive-change iterations.",
                    effectEvidence);
            }

            await _store.UpsertStageStateAsync(
                new CanonicalStageStateRecord(
                    workflow.Identity,
                    stage.Identity,
                    WorkflowResolutionState.Completed,
                    now,
                    effectEvidence),
                cancellationToken);

            if (stage.AllowedSuccessors.Count == 0)
            {
                await _store.UpsertWorkflowStateAsync(
                    new CanonicalWorkflowStateRecord(
                        workflow.Identity,
                        WorkflowResolutionState.Completed,
                        null,
                        RuntimeOutcomeKind.Completed,
                        now,
                        effectEvidence),
                    cancellationToken);
            }
            else
            {
                WorkflowStageIdentity nextStage = stage.AllowedSuccessors[0];
                await _store.UpsertStageStateAsync(
                    new CanonicalStageStateRecord(
                        workflow.Identity,
                        nextStage,
                        WorkflowResolutionState.Active,
                        now,
                        effectEvidence),
                    cancellationToken);
                await _store.UpsertWorkflowStateAsync(
                    new CanonicalWorkflowStateRecord(
                        workflow.Identity,
                        WorkflowResolutionState.Resumable,
                        nextStage,
                        RuntimeOutcomeKind.Waiting,
                        now,
                        effectEvidence),
                    cancellationToken);
            }

            var records = new List<EffectExecutionRecord>();
            foreach (EffectDefinition effect in definition.Effects)
            {
                records.Add(new EffectExecutionRecord(
                    effect.Identity,
                    EffectExecutionStatus.Succeeded,
                    $"{evidenceTitle} applied `{effect.Identity}`.",
                    effectEvidence));
            }

            return new EffectExecutionResult(
                EffectExecutionStatus.Succeeded,
                records,
                $"{evidenceTitle} effects applied for `{definition.Identity}`.",
                effectEvidence);
        }

        private async Task<bool> IsExecuteCommitStalledAsync(
            WorkflowTransitionDefinition definition,
            CancellationToken cancellationToken)
        {
            if (definition.Identity.Value != "EvaluateCommit")
            {
                return false;
            }

            foreach (string relativePath in ExecuteEvidence(definition))
            {
                string path = ResolveRepositoryPath(relativePath);
                if (!File.Exists(path))
                {
                    continue;
                }

                string content = await File.ReadAllTextAsync(path, cancellationToken);
                if (content
                    .Split(["\r\n", "\n"], StringSplitOptions.None)
                    .Any(line => line.Trim().StartsWith("| Stalled |", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains("true", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] AddExecuteStallStateEvidence(
            WorkflowTransitionDefinition definition,
            IReadOnlyList<string> evidence)
        {
            const string relativePath = ".LoopRelay/evidence/execute-stall/state.md";
            if (definition.Identity.Value != "EvaluateCommit" ||
                !File.Exists(ResolveRepositoryPath(relativePath)))
            {
                return evidence.ToArray();
            }

            return evidence
                .Concat([relativePath])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private async Task<IReadOnlyList<string>> AddFinalClosedStatePersistenceEvidenceAsync(
            WorkflowTransitionDefinition definition,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            if (definition.Identity.Value != "VerifyWorkflowExitGate")
            {
                return evidence;
            }

            string relativePath = ".LoopRelay/evidence/execute-completion-recovery/final-closed-state-persistence.md";
            string path = ResolveRepositoryPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                $"""
                # Execute Completion Recovery Phase

                | Field | Value |
                |---|---|
                | Phase | Final closed-state persistence |
                | Transition | {definition.Identity} |
                | Created At | {DateTimeOffset.UtcNow:O} |

                Execute is persisting the canonical completed workflow state, terminal stage state, certified
                completion product, and effect evidence. If interruption occurs here, recovery must inspect
                canonical transition runs, products, effect records, and this phase marker before rerunning.
                """,
                cancellationToken);
            return evidence
                .Concat([relativePath])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private async Task<IReadOnlyList<string>> AddTraditionalRoadmapRigorEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowStageDefinition stage,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            if (workflow.Identity != WorkflowIdentity.TraditionalRoadmap)
            {
                return evidence;
            }

            string relativePath = $".LoopRelay/evidence/traditional-roadmap-effects/{definition.Identity}.md";
            string path = ResolveRepositoryPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string products = string.Join(", ", validation.Products.Select(product => product.Identity.Value));
            string storage = string.Join(
                ", ",
                validation.Products.SelectMany(product => product.StorageRepresentations).Distinct(StringComparer.Ordinal));
            await File.WriteAllTextAsync(
                path,
                $"""
                # TraditionalRoadmap Canonical Runtime Evidence

                | Field | Value |
                |---|---|
                | Workflow | {workflow.Identity} |
                | Stage | {stage.Identity} |
                | Transition | {definition.Identity} |
                | Prompt Identity | {definition.PromptIdentity} |
                | Products | {products} |
                | Storage | {storage} |
                | Parser | TraditionalRoadmap transition-specific parser validated the primary output artifact. |
                | Output Validator | {validation.Explanation} |
                | Effects | Product, stage, workflow, effect-record, and evidence persistence are owned by the canonical effect executor. |
                | Warning and Recovery Metadata | Failed, cannot-proceed, cancelled, and partial effect outcomes are recorded through canonical warning and recovery stores. |
                | Transition Ordering | Declared by `{stage.Identity}` stage transition order in the workflow definition. |
                | Prompt Execution Sequencing | Owned by `TransitionRuntime`; prompt success alone does not advance workflow state. |
                | Transition Persistence Sequencing | Started, raw output, interpretation, validation, effects, and completion are persisted by canonical transition stores. |
                | Lifecycle Advancement | Canonical stage and product lifecycle records advance only after output validation and effects. |
                | Next Transition Decisions | Resolved by canonical workflow resolver from products, dependencies, and completed transition runs. |
                | Projection Freshness | Prompt rendering uses generated prompt/catalog source-hash evidence where registered. |
                | Prompt Contract Snapshot | Prompt identity, source-hash evidence, input gate, output gate, and validators are persisted with the transition run. |
                | Input Snapshots | Transition input snapshot hash is persisted by the canonical runtime before prompt execution. |
                | Selection Provenance | Product causal identity and storage representations preserve selected roadmap artifact provenance. |
                | Artifact Promotion Validation | Primary output validation must pass before `PreparedEpic`, `StrategicInitiativeSelection`, or context products become gate-usable. |
                | Decision Ledger | Canonical transition/effect records replace legacy decision-ledger authority for active orchestration. |
                | Split Lineage | Split transition output is represented as canonical product/evidence lineage instead of state-machine control flow. |
                | Warning Evidence | Canonical failed/unsatisfied transition evidence remains repository-owned and resolvable. |
                | Recovery Intent | Canonical recovery markers identify rerun/resume paths without silent repair. |
                | Created At | {DateTimeOffset.UtcNow:O} |
                """,
                cancellationToken);
            return evidence
                .Concat([relativePath])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private async Task PersistExecuteStallStateAsync(
            WorkflowDefinition workflow,
            WorkflowStageDefinition stage,
            WorkflowTransitionDefinition definition,
            IReadOnlyList<string> evidence,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            // Stalls are derived, never latched: the stall evidence is appended as a warning and the
            // next invocation re-evaluates gates without any manual clearing step.
            await _store.AppendWarningAsync(
                new CanonicalWarningRecord(
                    CausalUlid.NewId("warn"),
                    workflow.Identity,
                    stage.Identity,
                    definition.Identity,
                    WarningCategory.Repository,
                    "Execute commit evaluation detected repeated no-substantive-change iterations.",
                    "Execute commit gate",
                    "Make substantive repository or milestone progress, or rerun after inspecting stall evidence.",
                    evidence,
                    recordedAt),
                cancellationToken);
        }

        private async Task<EffectExecutionResult> ExecuteLocalArtifactAsync(
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> evidence = validation.Evidence.Count == 0
                ? LocalArtifactTransitions.Evidence(definition)
                : validation.Evidence;
            try
            {
                await MaterializeLocalArtifactEffectsAsync(definition, validation, evidence, cancellationToken);
            }
            catch (Exception exception)
            {
                return new EffectExecutionResult(
                    EffectExecutionStatus.Failed,
                    definition.Effects.Select(effect => new EffectExecutionRecord(
                        effect.Identity,
                        EffectExecutionStatus.Failed,
                        exception.Message,
                        evidence)).ToArray(),
                    exception.Message,
                    evidence);
            }

            foreach (ProductRecord product in validation.Products)
            {
                await _store.UpsertProductAsync(product, cancellationToken);
            }

            WorkflowDefinition workflow = WorkflowFor(definition);
            WorkflowStageDefinition stage = StageFor(workflow, definition);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await _store.UpsertStageStateAsync(
                new CanonicalStageStateRecord(
                    workflow.Identity,
                    stage.Identity,
                    WorkflowResolutionState.Active,
                    now,
                    evidence),
                cancellationToken);
            await _store.UpsertWorkflowStateAsync(
                new CanonicalWorkflowStateRecord(
                    workflow.Identity,
                    WorkflowResolutionState.Resumable,
                    stage.Identity,
                    RuntimeOutcomeKind.Waiting,
                    now,
                    evidence),
                cancellationToken);

            var records = new List<EffectExecutionRecord>();
            foreach (EffectDefinition effect in definition.Effects)
            {
                records.Add(new EffectExecutionRecord(
                    effect.Identity,
                    EffectExecutionStatus.Succeeded,
                    $"Local artifact effect applied `{effect.Identity}`.",
                    evidence));
            }

            return new EffectExecutionResult(
                EffectExecutionStatus.Succeeded,
                records,
                $"Local artifact effects applied for `{definition.Identity}`.",
                evidence);
        }

        private WorkflowDefinition WorkflowFor(WorkflowTransitionDefinition definition) =>
            _definitions.Single(workflow =>
                workflow.Transitions.Any(transition => ReferenceEquals(transition, definition) || transition.Equals(definition)));

        private static WorkflowStageDefinition StageFor(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition) =>
            workflow.Stages.Single(stage => stage.Transitions.Contains(definition.Identity));

        private async Task MaterializeLocalVerificationEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            foreach (string relativePath in evidence)
            {
                string path = ResolveRepositoryPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # Local Verification Evidence

                    Workflow: {workflow.Identity}
                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}
                    Status: {validation.Status}
                    Explanation: {validation.Explanation}
                    Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                    """,
                    cancellationToken);
            }
        }

        private async Task MaterializePlanWarmSessionEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            foreach (string relativePath in PlanWarmSessionTransitions.Evidence(definition))
            {
                string path = ResolveRepositoryPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # Plan Warm Session Evidence

                    Workflow: {workflow.Identity}
                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}
                    Status: {validation.Status}
                    Explanation: {validation.Explanation}
                    Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                    Evidence: {string.Join(", ", evidence)}
                    """,
                    cancellationToken);
            }
        }

        private async Task MaterializePlanTransitionEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            IReadOnlyList<string> evidencePaths,
            string title,
            CancellationToken cancellationToken)
        {
            foreach (string relativePath in evidencePaths)
            {
                string path = ResolveRepositoryPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # {title}

                    Workflow: {workflow.Identity}
                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}
                    Status: {validation.Status}
                    Explanation: {validation.Explanation}
                    Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                    Evidence: {string.Join(", ", evidence)}
                    """,
                    cancellationToken);
            }
        }

        private async Task MaterializePlanScopedArtifactEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            foreach (string relativePath in PlanScopedArtifactTransitions.Evidence(definition))
            {
                string path = ResolveRepositoryPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # Plan Scoped Artifact Evidence

                    Workflow: {workflow.Identity}
                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}
                    Status: {validation.Status}
                    Explanation: {validation.Explanation}
                    Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                    Evidence: {string.Join(", ", evidence)}
                    """,
                    cancellationToken);
            }
        }

        private async Task MaterializeExecuteTransitionEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            EffectExecutionContext context,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> loopArtifactEffects =
                await ApplyExecuteLoopArtifactEffectsAsync(definition, context, cancellationToken);
            foreach (string relativePath in ExecuteEvidence(definition))
            {
                string path = ResolveRepositoryPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # Execute Transition Evidence

                    Workflow: {workflow.Identity}
                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}
                    Status: {validation.Status}
                    Explanation: {validation.Explanation}
                    Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                    Evidence: {string.Join(", ", evidence)}
                    Loop Artifact Effects:
                    {RenderLines(loopArtifactEffects)}
                    """,
                    cancellationToken);
            }
        }

        private async Task<IReadOnlyList<string>> ApplyExecuteLoopArtifactEffectsAsync(
            WorkflowTransitionDefinition definition,
            EffectExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileStore = new FileSystemArtifactStore();
            var artifacts = new LoopArtifacts(
                fileStore,
                _repository,
                new LedgerLoopHistoryStore(fileStore, _repository));
            var lineage = new LoopHistoryLineage(context.RunId, context.TransitionRunId, context.AttemptId);
            return definition.Identity.Value switch
            {
                "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" =>
                    await RotateLiveHandoffEffectAsync(artifacts, lineage),
                "GenerateHandoff" => await RetireLiveDecisionsEffectAsync(artifacts),
                "UpdateOperationalContext" => await RotateOperationalDeltaEffectAsync(artifacts, lineage),
                _ => [],
            };
        }

        private static async Task<IReadOnlyList<string>> RotateLiveHandoffEffectAsync(
            LoopArtifacts artifacts,
            LoopHistoryLineage lineage)
        {
            string? content = await artifacts.RotateLiveHandoffAsync(lineage);
            return content is null
                ? ["No live handoff was present to rotate."]
                : ["Rotated live handoff into handoff history."];
        }

        private static async Task<IReadOnlyList<string>> RetireLiveDecisionsEffectAsync(LoopArtifacts artifacts)
        {
            bool retired = await artifacts.RetireLiveDecisionsAsync();
            return retired
                ? ["Retired live decisions after implementation handoff generation."]
                : ["No live decisions were present to retire."];
        }

        private static async Task<IReadOnlyList<string>> RotateOperationalDeltaEffectAsync(
            LoopArtifacts artifacts,
            LoopHistoryLineage lineage)
        {
            string? content = await artifacts.RotateOperationalDeltaAsync(lineage);
            return content is null
                ? ["No live operational delta was present to rotate."]
                : ["Rotated live operational delta into delta history."];
        }

        private static string RenderLines(IReadOnlyList<string> lines) =>
            lines.Count == 0
                ? "- None"
                : string.Join(Environment.NewLine, lines.Select(line => $"- {line}"));

        private async Task MaterializeLocalArtifactEffectsAsync(
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            if (definition.Identity.Value != "GenerateOperationalContext")
            {
                throw new InvalidOperationException($"Unsupported local artifact transition `{definition.Identity}`.");
            }

            string planPath = ResolveRepositoryPath(OrchestrationArtifactPaths.Plan);
            if (!File.Exists(planPath))
            {
                throw new InvalidOperationException($"{OrchestrationArtifactPaths.Plan} was not found.");
            }

            string plan = await File.ReadAllTextAsync(planPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(plan))
            {
                throw new InvalidOperationException($"{OrchestrationArtifactPaths.Plan} is empty.");
            }

            string operationalContextPath = ResolveRepositoryPath(OrchestrationArtifactPaths.OperationalContext);
            Directory.CreateDirectory(Path.GetDirectoryName(operationalContextPath)!);
            await File.WriteAllTextAsync(operationalContextPath, plan, cancellationToken);

            WorkflowDefinition workflow = WorkflowFor(definition);
            await MaterializeLocalArtifactEvidenceAsync(workflow, definition, validation, evidence, cancellationToken);
        }

        private async Task MaterializeLocalArtifactEvidenceAsync(
            WorkflowDefinition workflow,
            WorkflowTransitionDefinition definition,
            ProductValidationResult validation,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken)
        {
            foreach (string relativePath in evidence)
            {
                string path = ResolveRepositoryPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    $"""
                    # Local Artifact Evidence

                    Workflow: {workflow.Identity}
                    Transition: {definition.Identity}
                    Prompt: {definition.PromptIdentity}
                    Status: {validation.Status}
                    Explanation: {validation.Explanation}
                    Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                    """,
                    cancellationToken);
            }
        }

        private string ResolveRepositoryPath(string relativePath)
        {
            string root = Path.GetFullPath(_repository.Path);
            string path = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string relative = Path.GetRelativePath(root, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                throw new InvalidOperationException("Local verification evidence path escaped the repository root.");
            }

            return path;
        }
    }

    private static ProductRecord ProductRecord(
        ProductDefinition product,
        WorkflowTransitionIdentity transition,
        IReadOnlyList<string> evidence,
        string? causalIdentity = null,
        IReadOnlyList<string>? storageRepresentations = null) =>
        new(
            product.Identity,
            product.ProducerWorkflow,
            transition,
            product.IntendedConsumers,
            product.RepositoryOwnership,
            product.Authority,
            storageRepresentations ?? StorageRepresentations(product),
            causalIdentity ?? $"local-verification:{transition}:{product.Identity}:{string.Join("|", evidence)}",
            product.Freshness,
            product.ValidationState,
            product.Lifecycle,
            evidence,
            product.SchemaVersion);

    private static IReadOnlyList<string> PlanScopedStorageRepresentations(
        ProductDefinition product,
        IEnumerable<string> artifactPaths)
    {
        string[] paths = artifactPaths
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return product.Identity.Value switch
        {
            "ExecutionMilestoneSet" => paths
                .Where(path => path.StartsWith(
                    OrchestrationArtifactPaths.MilestonesDirectory + "/",
                    StringComparison.Ordinal))
                .ToArray(),
            "ExecutionDetails" => paths.Contains(OrchestrationArtifactPaths.Details, StringComparer.Ordinal)
                ? [OrchestrationArtifactPaths.Details]
                : StorageRepresentations(product),
            _ => paths.Length == 0 ? StorageRepresentations(product) : paths,
        };
    }

    private static IReadOnlyList<string> ExecuteEvidence(WorkflowTransitionDefinition definition) =>
        ExecuteDecisionSessionTransitions.Supports(definition)
            ? ExecuteDecisionSessionTransitions.Evidence(definition)
            : ExecuteImplementationTransitions.Supports(definition)
                ? ExecuteImplementationTransitions.Evidence(definition)
                : ExecuteRepositoryStateTransitions.Supports(definition)
                    ? ExecuteRepositoryStateTransitions.Evidence(definition)
                    : ExecuteReviewTransitions.Evidence(definition);

    private static IReadOnlyList<string> ExecuteArtifactEvidence(WorkflowTransitionDefinition definition) =>
        definition.Identity.Value switch
        {
            "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" =>
                [OrchestrationArtifactPaths.Decisions],
            "GenerateHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            "UpdateOperationalContext" => [OrchestrationArtifactPaths.OperationalContext],
            "RunNonImplementationReview" =>
                [OrchestrationArtifactPaths.NonImplementationReview, OrchestrationArtifactPaths.NonImplementationLedger],
            _ => [],
        };

    private static IReadOnlyList<string> ExecuteRequiredArtifacts(WorkflowTransitionDefinition definition) =>
        definition.Identity.Value switch
        {
            "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" =>
                [OrchestrationArtifactPaths.Decisions],
            "GenerateHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            _ => [],
        };

    private static IReadOnlyList<string> ExecuteStorageRepresentations(
        ProductDefinition product,
        WorkflowTransitionDefinition definition,
        IReadOnlyList<string> evidence) =>
        product.Identity.Value switch
        {
            "DecisionSet" => [OrchestrationArtifactPaths.Decisions],
            "ExecutionHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            "ImplementationSlice" => ExecuteEvidence(definition),
            "RepositoryChanges" => ExecuteEvidence(definition),
            "OperationalDelta" => ExecuteEvidence(definition),
            "CompletionEvidence" => evidence.Count == 0 ? ExecuteEvidence(definition) : evidence,
            "CompletionRoute" => ExecuteEvidence(definition),
            _ => StorageRepresentations(product),
        };

    private static IReadOnlyList<string> StorageRepresentations(ProductDefinition product) =>
        product.Identity.Value switch
        {
            "PreparedEpic" => [OrchestrationArtifactPaths.AgentsDirectory + "/epic.md"],
            "MilestoneSpecificationSet" => [OrchestrationArtifactPaths.SpecsDirectory],
            "ExecutablePlan" => [OrchestrationArtifactPaths.Plan],
            "OperationalContext" => [OrchestrationArtifactPaths.OperationalContext],
            "ExecutionDetails" => [OrchestrationArtifactPaths.Details],
            "ExecutionMilestoneSet" => [OrchestrationArtifactPaths.MilestonesDirectory],
            "ExecutionReadiness" => [".LoopRelay/evidence/local-verification/VerifyExecuteEntryContract.md"],
            "DecisionSet" => [OrchestrationArtifactPaths.Decisions],
            "ImplementationSlice" => [".LoopRelay/evidence/execute-implementation/ExecuteImplementationSlice.md"],
            "RepositoryChanges" => [".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md"],
            "ExecutionHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            "OperationalDelta" => [".LoopRelay/evidence/execute-repository-state/UpdateOperationalContext.md"],
            "CompletionEvidence" => [".LoopRelay/evidence/execute-review/RunCompletionCertification.md"],
            "CompletionRoute" => [".LoopRelay/evidence/execute-review/InterpretCompletionRoute.md"],
            "CertifiedCompletion" => [".LoopRelay/evidence/local-verification/VerifyWorkflowExitGate.md"],
            _ => product.StorageRepresentations,
        };

    private static string ResolveRepositoryPath(Repository repository, string relativePath)
    {
        string root = Path.GetFullPath(repository.Path);
        string path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Repository path escaped the repository root.");
        }

        return path;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
