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
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Cli.Services.Decisions.Recovery;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Effects;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Cli.Services.Storage;
using LoopRelay.Cli.Services.Import;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Primitives;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Primitives;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;
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

internal sealed partial class LoopRelayCompositionRoot : IAsyncDisposable
{
    private readonly ServiceProvider? provider;
    private readonly IAsyncDisposable? promptExecutorLifetime;

    private LoopRelayCompositionRoot(
        Repository repository,
        IStorageVerifier storageVerifier,
        RepositoryObserver repositoryObserver,
        WorkflowResolver workflowResolver,
        ITransitionRuntime transitionRuntime,
        ITransitionEffectCoordinator effectCoordinator,
        LoopRelay.Orchestration.Effects.EffectWorker effectWorker,
        WorkflowController workflowController,
        WorkflowChainRunner workflowChainRunner,
        OrchestrationKernel orchestrationKernel,
        WorkflowBoundaryEvidenceWriter boundaryEvidenceWriter,
        CanonicalWorkflowCatalogSnapshot workflowCatalog,
        IReadOnlyList<WorkflowDefinition> workflowDefinitions,
        IReadOnlyList<WorkflowChainDefinition> workflowChains,
        CanonicalWorkflowPersistenceStore persistence,
        ResolvedOperationalPolicy policy,
        RuntimeProfileIdentity runtimeProfile,
        PromptPolicyProfileIdentity promptPolicyProfile,
        ResolvedAgentRolePolicy agentRolePolicy,
        IRecoveryInspectUseCase recoveryInspect,
        IRecoveryPlanUseCase recoveryPlan,
        IRecoveryExecuteUseCase recoveryExecute,
        IInteractionBroker interactionBroker,
        WorkspaceStorageApplicationService storageAuthority,
        CanonicalImportGateway importGateway,
        ServiceProvider? provider = null,
        IAsyncDisposable? promptExecutorLifetime = null)
    {
        Repository = repository;
        Policy = policy;
        RuntimeProfile = runtimeProfile;
        PromptPolicyProfile = promptPolicyProfile;
        AgentRolePolicy = agentRolePolicy;
        StorageVerifier = storageVerifier;
        RepositoryObserver = repositoryObserver;
        WorkflowResolver = workflowResolver;
        TransitionRuntime = transitionRuntime;
        EffectCoordinator = effectCoordinator;
        EffectWorker = effectWorker;
        WorkflowController = workflowController;
        WorkflowChainRunner = workflowChainRunner;
        OrchestrationKernel = orchestrationKernel;
        BoundaryEvidenceWriter = boundaryEvidenceWriter;
        WorkflowCatalog = workflowCatalog;
        WorkflowDefinitions = workflowDefinitions;
        WorkflowChains = workflowChains;
        Persistence = persistence;
        RecoveryInspect = recoveryInspect;
        RecoveryPlan = recoveryPlan;
        RecoveryExecute = recoveryExecute;
        InteractionBroker = interactionBroker;
        StorageAuthority = storageAuthority;
        ImportGateway = importGateway;
        this.provider = provider;
        this.promptExecutorLifetime = promptExecutorLifetime;
    }

    public Repository Repository { get; }

    /// <summary>
    /// The single resolved operational policy this invocation executes under. Every consumer
    /// observes this one instance; no production code re-reads settings or environment ad hoc.
    /// </summary>
    public ResolvedOperationalPolicy Policy { get; }

    public RuntimeProfileIdentity RuntimeProfile { get; }

    public PromptPolicyProfileIdentity PromptPolicyProfile { get; }

    public ResolvedAgentRolePolicy AgentRolePolicy { get; }

    public IStorageVerifier StorageVerifier { get; }

    public RepositoryObserver RepositoryObserver { get; }

    public WorkflowResolver WorkflowResolver { get; }

    public ITransitionRuntime TransitionRuntime { get; }

    internal ITransitionEffectCoordinator EffectCoordinator { get; }

    internal LoopRelay.Orchestration.Effects.EffectWorker EffectWorker { get; }

    public WorkflowController WorkflowController { get; }

    public WorkflowChainRunner WorkflowChainRunner { get; }

    public OrchestrationKernel OrchestrationKernel { get; }

    public WorkflowBoundaryEvidenceWriter BoundaryEvidenceWriter { get; }

    public CanonicalWorkflowCatalogSnapshot WorkflowCatalog { get; }

    public IReadOnlyList<WorkflowDefinition> WorkflowDefinitions { get; }

    public IReadOnlyList<WorkflowChainDefinition> WorkflowChains { get; }

    internal CanonicalWorkflowPersistenceStore Persistence { get; }

    internal IRecoveryInspectUseCase RecoveryInspect { get; }
    internal IRecoveryPlanUseCase RecoveryPlan { get; }
    internal IRecoveryExecuteUseCase RecoveryExecute { get; }
    internal IInteractionBroker InteractionBroker { get; }
    internal WorkspaceStorageApplicationService StorageAuthority { get; }
    internal CanonicalImportGateway ImportGateway { get; }

    /// <summary>True only for the production composition, which launches the real provider and
    /// therefore has runtime prerequisites to inspect; injected runtimes have none. Settable
    /// only by <see cref="CreateProduction"/> and tests exercising the prerequisite gate.</summary>
    internal bool ProductionRuntime { get; set; }

    /// <summary>The selected runtime profile projected to the host facts needed by the
    /// provider-specific prerequisite inspector.</summary>
    internal ResolvedRuntimeHostProfile? RuntimePrerequisiteProfile { get; set; }

    /// <summary>The prerequisite inspector, replaceable by tests with one reading a fake
    /// environment — the default reads the real environment and filesystem.</summary>
    internal RuntimePrerequisiteDoctor RuntimePrerequisiteDoctor { get; set; } = new();

    internal static LoopRelayCompositionRoot CreateForTests(Repository repository) =>
        CreateCore(
            repository,
            agentRuntime: null,
            processRunner: new ProcessRunner(),
            RequireBrain(CliSettingsLoader.Load()),
            provider: null);

    public static LoopRelayCompositionRoot CreateProduction(
        Repository repository,
        IReadOnlyList<PolicyOverride>? policyOverrides = null,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        var settings = CliSettingsLoader.Load();
        ResolvedOperationalPolicy policy = OperationalPolicyResolver.Resolve(
            settings.PolicyInputs,
            settings.IsDefaultTemplate
                ? $"settings:{settings.Path} (default template)"
                : $"settings:{settings.Path}",
            CombineInvocationOverrides(policyOverrides),
            settings.PermissionInputs);
        var services = new ServiceCollection();
        services.AddAgents(settings.PermissionInputs);
        ServiceProvider provider = services.BuildServiceProvider();
        IAgentRuntime rawRuntime = provider.GetRequiredService<IAgentRuntime>();
        return CreateCore(
            repository,
            rawRuntime,
            provider.GetRequiredService<IProcessRunner>(),
            RequireBrain(settings),
            provider,
            output ?? System.Console.Out,
            error ?? System.Console.Error,
            rawRuntime as IAgentSessionContinuityRuntime,
            policy,
            productionRuntime: true);
    }

    /// <summary>M7: inspect the production provider's runtime prerequisites and append the
    /// inspection as an append-only fact (run identity does not exist yet at this point; the
    /// fact is ordered against runs by its ledger position). Non-production compositions have
    /// no provider prerequisites and return no diagnostics.</summary>
    internal async Task<RuntimePrerequisiteApplicationResult> InspectRuntimePrerequisitesAsync(
        CancellationToken cancellationToken)
    {
        if (!ProductionRuntime)
        {
            return RuntimePrerequisiteApplicationResult.NotRequired;
        }

        ResolvedRuntimeHostProfile profile = RuntimePrerequisiteProfile
            ?? throw new InvalidOperationException(
                "Production runtime prerequisite inspection requires a resolved runtime host profile.");
        RuntimePrerequisiteInspection inspection = RuntimePrerequisiteDoctor.Inspect(
            profile,
            DateTimeOffset.UtcNow);
        await Persistence.AppendRuntimePrerequisiteAsync(
            new CanonicalRuntimePrerequisiteRecord(
                CausalUlid.NewId("pre"),
                RunId: null,
                inspection.InspectedAt,
                JsonSerializer.Serialize(inspection, PrerequisiteJsonOptions)),
            CancellationToken.None);

        WorkflowStopReason? stopReason = inspection.OverallStatus == RuntimePrerequisiteOverallStatus.Unsatisfied
            ? WorkflowStopReason.MissingRuntimePrerequisite
            : null;
        return new RuntimePrerequisiteApplicationResult(inspection, stopReason);
    }

    private static readonly JsonSerializerOptions PrerequisiteJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

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

        string? recoveryPolicy = getEnvironmentVariable("LoopRelay_DECISION_RECOVERY_POLICY");
        if (recoveryPolicy is not null)
        {
            overrides.Add(new PolicyOverride(
                OperationalPolicyResolver.DecisionRecoveryPolicyKey,
                recoveryPolicy,
                "env:LoopRelay_DECISION_RECOVERY_POLICY",
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

    internal static LoopRelayCompositionRoot CreateForTests(
        Repository repository,
        IAgentRuntime agentRuntime,
        ResolvedOperationalPolicy? policy = null) =>
        CreateCore(repository, agentRuntime, processRunner: new ProcessRunner(),
            RequireBrain(CliSettingsLoader.Load()), provider: null, policy: policy);

    internal static LoopRelayCompositionRoot CreateForTests(
        Repository repository,
        IAgentRuntime agentRuntime,
        IProcessRunner processRunner,
        ResolvedOperationalPolicy? policy = null) =>
        CreateCore(repository, agentRuntime, processRunner,
            RequireBrain(CliSettingsLoader.Load()), provider: null, policy: policy);

    private static BrainConfiguration RequireBrain(CliSettingsLoadResult settings)
    {
        ConfiguredBrainFacts configured = settings.Runtime.Brain;
        if (configured.Model is null || configured.Effort is null)
        {
            throw new CliSettingsException(
                "runtime.brain.model and runtime.brain.effort are required application composition inputs.");
        }

        return new BrainConfiguration(configured.Model.Value, configured.Effort.Value);
    }

    private static LoopRelayCompositionRoot CreateCore(
        Repository repository,
        IAgentRuntime? agentRuntime,
        IProcessRunner processRunner,
        BrainConfiguration brainConfiguration,
        ServiceProvider? provider,
        TextWriter? output = null,
        TextWriter? error = null,
        IAgentSessionContinuityRuntime? continuityRuntime = null,
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
        IStorageVerifier storageVerifier = productionRuntime
            ? new WorkspaceStorageVerifierAdapter()
            : new FileSystemStorageVerifier();
        var repositoryObserver = new RepositoryObserver(storageVerifier);
        var workflowResolver = new WorkflowResolver();
        CanonicalWorkflowCatalogSnapshot workflowCatalog = CanonicalWorkflowCatalog.Current;
        IReadOnlyList<WorkflowDefinition> workflowDefinitions = workflowCatalog.Workflows;
        IReadOnlyList<WorkflowChainDefinition> workflowChains = workflowCatalog.Chains;
        string capabilityMaterial = agentRuntime is null ? "no-provider-runtime"
            : JsonSerializer.Serialize(agentRuntime.Capabilities);
        string runtimeHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{workflowCatalog.Identity}\n{policy.PolicyId}\n{capabilityMaterial}\n{brainConfiguration.Model}\n{brainConfiguration.Effort}")));
        var runtimeProfile = new RuntimeProfileIdentity($"runtime_{runtimeHash[..32]}");
        string promptHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{workflowCatalog.Identity}\n{policy.PolicyId}\n{policy.ResolvedJson}")));
        var promptPolicyProfile = new PromptPolicyProfileIdentity($"prompt_policy_{promptHash[..32]}");
        ResolvedAgentRolePolicy agentRolePolicy = ResolvedAgentRolePolicy.Create(
            new PolicyIdentity(policy.PolicyId), runtimeProfile, brainConfiguration,
            $"resolved-policy:{policy.SourceDescription}");
        string[] catalogErrors = workflowDefinitions
            .SelectMany(definition => WorkflowDefinitionValidator.Validate(definition).Errors
                .Select(error => $"{definition.Identity}: {error}"))
            .ToArray();
        if (catalogErrors.Length > 0)
        {
            throw new InvalidOperationException(
                $"Canonical workflow catalog validation failed: {string.Join("; ", catalogErrors)}");
        }

        if (workflowDefinitions.SelectMany(definition => definition.Transitions)
            .Any(transition => string.IsNullOrWhiteSpace(transition.PromptIdentity)))
        {
            throw new InvalidOperationException("Canonical prompt catalog contains an empty prompt identity.");
        }
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        ProviderEnvironmentConfiguration providerEnvironment = ProviderEnvironmentConfiguration.Resolve();
        var recoveryStore = new CanonicalRecoveryStore(repository);
        var recoveryCases = new CanonicalRecoveryCaseRecorder(recoveryStore);
        var loopConsole = new ConsoleLoopConsole(output ?? TextWriter.Null, error ?? TextWriter.Null);
        var promptExecutor = new UnifiedPromptExecutor(
            repository, agentRuntime, processRunner,
            loopConsole,
            continuityRuntime ?? agentRuntime as IAgentSessionContinuityRuntime,
            agentRolePolicy,
            persistence,
            policy,
            providerEnvironment,
            workflowCatalog.Identity,
            promptPolicyProfile,
            workflowDefinitions,
            recoveryCases,
            productionRuntime);
        var transitionEvidenceStore = new CanonicalTransitionEvidenceStore(persistence);
        var transitionBoundaryJournal = new CanonicalTransitionBoundaryJournal(persistence);
        var promptStore = new CanonicalRenderedPromptFactStore(persistence);
        var promptGateway = new PromptDispatchGateway(
            promptStore,
            new CanonicalPromptDispatchLifecycleStore(persistence),
            new LoadingPromptRuntimeDispatcher(promptStore, promptExecutor));
        var productResolver = new RepositoryObservationProductResolver(repositoryObserver, repository);
        var observationSource = new UnifiedRepositoryObservationSource(repositoryObserver, repository);
        var promptContextBuilder = new UnifiedPromptContextBuilder(repository);
        var interactionBroker = new InteractionBroker(new CanonicalInteractionStore(repository));
        ITransitionRuntime transitionRuntime = new TransitionRuntime(
            new UnifiedTransitionDefinitionResolver(workflowDefinitions),
            productResolver,
            new UnifiedGateEvaluator(processRunner, repository, interactionBroker, policy.PolicyId),
            promptContextBuilder,
            new UnifiedPromptRenderer(),
            promptGateway,
            new UnifiedOutputInterpreter(repository),
            new CanonicalCandidateProductStore(persistence),
            new UnifiedProductValidator(repository),
            new SnapshotInputFreshnessValidator(productResolver, promptContextBuilder),
            new CanonicalTransitionRunStore(persistence),
            new CanonicalAttemptStore(persistence),
            new CanonicalReadReceiptStore(persistence, processRunner, repository),
            transitionEvidenceStore,
            new CanonicalTransitionGateEvaluationStore(persistence),
            new CanonicalTransitionCommitStore(persistence),
            transitionBoundaryJournal,
            recoveryCases);
        var effectExecutor = new CanonicalFeatureEffectExecutor(
            repository, persistence, workflowDefinitions);
        var effectWorkStore = new CanonicalEffectWorkStore(repository);
        TransitionEffectExecutorAdapter[] typedEffectExecutors = workflowDefinitions
            .SelectMany(workflow => workflow.Transitions)
            .SelectMany(transition => transition.Effects)
            .GroupBy(effect => effect.Identity)
            .Select(group => group.First())
            .Select(effect => new TransitionEffectExecutorAdapter(
                effectExecutor,
                TransitionalFeatureEffectExecutorKeys.For(effect.Identity.Value),
                effect.Identity))
            .ToArray();
        var nestedCommitExecutor = new NestedRepositoryCommitEffectExecutor(repository, processRunner);
        var nestedPushExecutor = new GitPushEffectExecutor(
            repository, processRunner, LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.NestedRepositoryPush);
        var parentCommitExecutor = new ParentGitlinkCommitEffectExecutor(repository, processRunner);
        var parentWorktreeCommitExecutor = new ParentWorkingTreeCommitEffectExecutor(repository, processRunner);
        var parentPushExecutor = new GitPushEffectExecutor(
            repository, processRunner, LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.ParentRepositoryPush);
        var checkpointCleanupExecutor = new WorkspaceCheckpointCleanupEffectExecutor(repository);
        var checkpointCleanupReconciler = new WorkspaceCheckpointCleanupReconciler(repository);
        var decisionContinuityCleanupExecutor = new DecisionContinuityCleanupEffectExecutor(repository);
        var decisionContinuityCleanupReconciler = new DecisionContinuityCleanupReconciler(repository);
        var filesystemWriteExecutor = new FilesystemWriteEffectExecutor(repository);
        var filesystemWriteReconciler = new FilesystemWriteEffectReconciler(repository);
        var surfaceRestoreExecutor = new SurfaceRestoreEffectExecutor(repository);
        var surfaceRestoreReconciler = new SurfaceRestoreEffectReconciler(repository);
        var exportPackageExecutor = new ExportPackageEffectExecutor(repository);
        var exportPackageReconciler = new ExportPackageEffectReconciler(repository);
        var deferredCompletionArchiveExecutor = new DeferredCompletionArchiveEffectExecutor();
        var completionArchiveReconciler = new CompletionArchiveEffectReconciler(
            repository, new FileSystemArtifactStore());
        var durableLoopArtifacts = new LoopArtifacts(
            new RepositoryArtifactStore(new FileSystemArtifactStore(), repository),
            repository,
            new LedgerLoopHistoryStore(repository),
            new CanonicalExecutionRecommendationEvidenceStore(persistence));
        var liveHandoffRotationExecutor = new LiveHandoffRotationEffectExecutor(durableLoopArtifacts);
        var liveDecisionRetirementExecutor = new LiveDecisionRetirementEffectExecutor(durableLoopArtifacts);
        var operationalDeltaRotationExecutor = new OperationalDeltaRotationEffectExecutor(durableLoopArtifacts);
        var loopArtifactRotationReconciler = new LoopArtifactRotationEffectReconciler(
            repository, durableLoopArtifacts);
        var gitReconciler = new GitEffectReconciler(repository, processRunner);
        var reconcilerByExecutor = new Dictionary<EffectExecutorKey, LoopRelay.Orchestration.Effects.IEffectReconciler>
            {
                [LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.NestedRepositoryCommit] = gitReconciler,
                [LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.NestedRepositoryPush] = gitReconciler,
                [LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.ParentGitlinkCommit] = gitReconciler,
                [LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.ParentWorkingTreeCommit] = gitReconciler,
                [LoopRelay.Orchestration.Effects.GitEffectExecutorKeys.ParentRepositoryPush] = gitReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.CheckpointCleanup] = checkpointCleanupReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.DecisionContinuityCleanup] = decisionContinuityCleanupReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.FilesystemWrite] = filesystemWriteReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.SurfaceRestore] = surfaceRestoreReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.ExportPackageWrite] = exportPackageReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.CompletionArchive] = completionArchiveReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.RotateLiveHandoff] = loopArtifactRotationReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.RetireLiveDecisions] = loopArtifactRotationReconciler,
                [LoopRelay.Orchestration.Effects.WorkspaceEffectExecutorKeys.RotateOperationalDelta] = loopArtifactRotationReconciler,
            };
        var featureReconciler = new TransitionalFeatureEffectReconciler(effectWorkStore);
        foreach (TransitionEffectExecutorAdapter executor in typedEffectExecutors)
        {
            reconcilerByExecutor[executor.Key] = featureReconciler;
        }
        var effectReconciler = new LoopRelay.Orchestration.Effects.EffectReconcilerRegistry(
            reconcilerByExecutor,
            new HumanDecisionEffectReconciler());
        var effectWorker = new LoopRelay.Orchestration.Effects.EffectWorker(
            $"cli-{Environment.ProcessId}",
            effectWorkStore,
            new LoopRelay.Orchestration.Effects.EffectExecutorRegistry(
                [.. typedEffectExecutors, nestedCommitExecutor, nestedPushExecutor, parentCommitExecutor,
                  parentWorktreeCommitExecutor, parentPushExecutor, checkpointCleanupExecutor,
                  decisionContinuityCleanupExecutor,
                 filesystemWriteExecutor, surfaceRestoreExecutor, exportPackageExecutor, deferredCompletionArchiveExecutor,
                 liveHandoffRotationExecutor, liveDecisionRetirementExecutor,
                 operationalDeltaRotationExecutor]),
            effectReconciler,
            TimeSpan.FromMinutes(2),
            _recoveryCases: recoveryCases);
        var effectCoordinator = new TransitionEffectCoordinator(
            effectWorkStore,
            effectWorker,
            new CanonicalEffectPlanSettlementStore(repository, workflowDefinitions));
        var workflowController = new WorkflowController(
            workflowResolver,
            transitionRuntime,
            effectCoordinator,
            observationSource);
        var boundaryEvidenceWriter = new WorkflowBoundaryEvidenceWriter(
            new CanonicalChainBoundaryEvidenceStore(persistence));
        var workflowChainRunner = new WorkflowChainRunner(
            workflowResolver,
            workflowController,
            new WorkflowEntryGateEvaluator(),
            new WorkflowExitGateEvaluator(),
            new ProductTransferEvaluator(),
            boundaryEvidenceWriter,
            new CanonicalWorkflowInstanceRecorder(persistence, workflowCatalog),
            observationSource);
        var orchestrationKernel = new OrchestrationKernel(workflowChainRunner, observationSource,
            new DurableKernelAttemptAuthorizationSelector(), new CanonicalKernelDecisionStore(repository),
            [new CompletionKernelBoundaryObserver(repository)]);
        var recoveryCoordinator = new CanonicalRecoveryCoordinator(recoveryStore);
        var canonicalRecoveryRuntime = new CanonicalRecoveryRuntime(recoveryStore, []);
        var storageAuthority = new WorkspaceStorageApplicationService(repository, effectWorker, recoveryCases);
        var importGateway = new CanonicalImportGateway(repository);
        var composition = new LoopRelayCompositionRoot(
            repository,
            storageVerifier,
            repositoryObserver,
            workflowResolver,
            transitionRuntime,
            effectCoordinator,
            effectWorker,
            workflowController,
            workflowChainRunner,
            orchestrationKernel,
            boundaryEvidenceWriter,
            workflowCatalog,
            workflowDefinitions,
            workflowChains,
            persistence,
            policy,
            runtimeProfile,
            promptPolicyProfile,
            agentRolePolicy,
            recoveryCoordinator,
            recoveryCoordinator,
            canonicalRecoveryRuntime,
            interactionBroker,
            storageAuthority,
            importGateway,
            provider,
            promptExecutor);
        composition.ProductionRuntime = productionRuntime;
        composition.RuntimePrerequisiteProfile = agentRuntime is null
            ? null
            : new ResolvedRuntimeHostProfile(
                composition.RuntimeProfile,
                agentRuntime.Capabilities);
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

    internal static IReadOnlyList<string> ExecuteEvidence(WorkflowTransitionDefinition definition) =>
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
                [OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation],
            "GenerateHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            "UpdateOperationalContext" => [OrchestrationArtifactPaths.OperationalContext],
            "RunNonImplementationReview" =>
                [OrchestrationArtifactPaths.NonImplementationReview, OrchestrationArtifactPaths.NonImplementationLedger],
            "InterpretCompletionRoute" => [ExecuteReviewTransitions.CompletionRouteOutputPath],
            _ => [],
        };

    private static IReadOnlyList<string> ExecuteRequiredArtifacts(WorkflowTransitionDefinition definition) =>
        definition.Identity.Value switch
        {
            "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" =>
                [OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation],
            "GenerateHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            _ => [],
        };

    private static IReadOnlyList<string> ExecuteStorageRepresentations(
        ProductDefinition product,
        WorkflowTransitionDefinition definition,
        IReadOnlyList<string> evidence) =>
        product.Identity.Value switch
        {
            "DecisionSet" =>
                [OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation],
            "ExecutionHandoff" => [OrchestrationArtifactPaths.LiveHandoff],
            "ImplementationSlice" => ExecuteEvidence(definition),
            "RepositoryChanges" => ExecuteEvidence(definition),
            "OperationalDelta" => ExecuteEvidence(definition),
            "CompletionEvidence" => evidence.Count == 0 ? ExecuteEvidence(definition) : evidence,
            "CompletionRoute" => ExecuteArtifactEvidence(definition),
            _ => StorageRepresentations(product),
        };

    private static IReadOnlyList<string> StorageRepresentations(ProductDefinition product) =>
        product.Identity.Value switch
        {
            "EpicPreparationAudit" => [".LoopRelay/evidence/traditional-roadmap-prompt/AuditExistingEpic-output.md"],
            "PreparedEpic" => [OrchestrationArtifactPaths.AgentsDirectory + "/epic.md"],
            "MilestoneSpecificationSet" => [OrchestrationArtifactPaths.SpecsDirectory],
            "ExecutablePlan" => [OrchestrationArtifactPaths.Plan],
            "OperationalContext" => [OrchestrationArtifactPaths.OperationalContext],
            "ExecutionDetails" => [OrchestrationArtifactPaths.Details],
            "ExecutionMilestoneSet" => [OrchestrationArtifactPaths.MilestonesDirectory],
            "ExecutionReadiness" => [".LoopRelay/evidence/local-verification/VerifyExecuteEntryContract.md"],
            "DecisionSet" =>
                [OrchestrationArtifactPaths.Decisions, OrchestrationArtifactPaths.ExecutionRecommendation],
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
