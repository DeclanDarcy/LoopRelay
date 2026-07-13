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

internal sealed partial class LoopRelayCompositionRoot
{
    private sealed record PromptExecutionContext(
        string? RunId,
        string? TransitionRunId,
        string? AttemptId,
        WorkflowIdentity? Workflow,
        WorkflowTransitionIdentity? Transition,
        IReadOnlyList<ConsumedInputFile>? ConsumedFiles)
    {
        public static PromptExecutionContext Empty { get; } = new(null, null, null, null, null, []);
    }

    private sealed class UnifiedPromptExecutor(
        Repository _repository,
        IAgentRuntime? _agentRuntime,
        IProcessRunner _processRunner,
        ILoopConsole console,
        IAgentSessionContinuityRuntime? _continuityRuntime,
        ResolvedAgentRolePolicy _rolePolicy,
        CanonicalWorkflowPersistenceStore _persistence,
        ResolvedOperationalPolicy _policy,
        ProviderEnvironmentConfiguration _providerEnvironment,
        string _catalogIdentity,
        PromptPolicyProfileIdentity _promptPolicyProfile,
        IReadOnlyList<WorkflowDefinition> _workflowDefinitions,
        ICanonicalRecoveryCaseRecorder _recoveryCases,
        bool _productionRuntime = false) : IProviderPromptTransport, IAsyncDisposable
    {
        private sealed class PromptContextBlockedException(
            string message,
            IReadOnlyList<string> evidence) : InvalidOperationException(message)
        {
            public IReadOnlyList<string> Evidence { get; } = evidence;
        }

        private readonly IArtifactStore artifactStore = new RepositoryArtifactStore(
            new FileSystemArtifactStore(),
            _repository);
        private readonly SessionSpineRecorder sessionRecorder = new(_persistence);
        private IAgentRuntime? recordingRuntime;
        private IAgentSession? planAuthoringSession;
        private DecisionSession? executeDecisionSession;
        private IRecoveryStore? executeRecoveryStore;
        private IAgentSession? executionSession;
        private RepositorySliceBaseline? executionSliceBaseline;
        private IReadOnlyList<string> changedPathsAfterExecution = [];
        private int uncheckedMilestonesBeforeExecution;
        private int uncheckedMilestonesAfterExecution;
        private IReadOnlyList<string> nonImplementationReviewEvidencePaths = [];
        private IReadOnlyList<string> completionRecoveryEvidencePaths = [];
        private CompletionCertificationResult? completionCertificationResult;

        private PromptExecutionContext CurrentExecutionContext { get; set; } = PromptExecutionContext.Empty;
        private PromptDispatchAuthorization? CurrentAuthorization { get; set; }
        private RenderedPromptFact? CurrentPromptFact { get; set; }

        private static IReadOnlyList<ConsumedInputFile> ConsumedFiles(
            params (string Path, string? Content)[] files) =>
            files
                .Where(file => file.Content is not null)
                .Select(file => ConsumedInputFile.FromContent(file.Path, file.Content!))
                .ToArray();

        /// <summary>The agent runtime gateway (M7): the D3 operational wrappers composed once at the
        /// runtime boundary under policy, with best-effort causal-spine recording outermost — one
        /// agent_sessions row per underlying session open (one-shot or persistent, including opens
        /// made inside DecisionSession and the helper prompt runners), one agent_turns row per
        /// completed AgentTurnResult. Prompt facts are owned exclusively by PromptDispatchGateway
        /// and are durably persisted and authorized before this runtime boundary is entered.</summary>
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
                    runtime, _policy, _repository, _processRunner, console, _providerEnvironment)
                : runtime;

        public async Task<PromptExecutionResult> DispatchAsync(
            PersistedRenderedPromptFact persisted,
            AuthorizedPromptDispatch dispatch,
            CancellationToken cancellationToken)
        {
            RenderedPromptFact fact = persisted.Fact;
            CurrentPromptFact = fact;
            CurrentAuthorization = dispatch.Authorization;
            WorkflowDefinition workflow = _workflowDefinitions.First(candidate =>
                candidate.Transitions.Any(transition =>
                    transition.Identity == dispatch.Authorization.Transition));
            WorkflowTransitionDefinition definition = workflow.Transitions.Single(transition =>
                transition.Identity == dispatch.Authorization.Transition);
            WorkflowStageDefinition stage = workflow.Stages.Single(candidate =>
                candidate.Transitions.Contains(definition.Identity));
            var prompt = new RenderedPrompt(
                fact.TemplateIdentity,
                fact.PolicyProfileIdentity,
                fact.RenderedContent,
                $"rendered-prompt:{fact.Identity.Value}",
                fact.TemplateSourceHash);
            CurrentExecutionContext = new PromptExecutionContext(
                dispatch.Authorization.Causality.Run.Value,
                dispatch.Authorization.Causality.TransitionRun.Value,
                dispatch.Authorization.Causality.Attempt.Value,
                workflow.Identity,
                definition.Identity,
                fact.ConsumedInputs);
            var request = new PromptExecutionRequest(
                dispatch.Authorization.Causality.Run.Value,
                workflow.Identity,
                stage.Identity,
                definition.Identity,
                definition,
                prompt,
                dispatch.Authorization.InputSnapshotHash,
                new WorkflowInvocation(InvocationModeKind.DefaultChained),
                new Dictionary<string, string>());
            return await ExecuteAsync(request, cancellationToken);
        }

        private async Task<PromptExecutionResult> ExecuteAsync(
            PromptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = request.Definition;
            RenderedPrompt prompt = request.RenderedPrompt;
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
                return await ExecutePlanWarmSessionAsync(request, cancellationToken);
            }

            if (ExecuteDecisionSessionTransitions.Supports(definition))
            {
                return await ExecuteDecisionSessionAsync(request, cancellationToken);
            }

            if (ExecuteImplementationTransitions.Supports(definition))
            {
                return await ExecuteImplementationTransitionAsync(request, cancellationToken);
            }

            if (ExecuteRepositoryStateTransitions.Supports(definition))
            {
                return await ExecuteRepositoryStateTransitionAsync(definition, prompt, cancellationToken);
            }

            if (ExecuteReviewTransitions.Supports(definition))
            {
                return await ExecuteReviewTransitionAsync(request, cancellationToken);
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
                string? primaryOutput = PrimaryOutputPath(definition);
                string? primaryOutputBefore = primaryOutput is null
                    ? null
                    : HashFileIfPresent(ResolveRepositoryPath(_repository, primaryOutput));
                AgentTurnResult result = await Runtime!.RunOneShotAsync(
                    AgentSpecs.BrainOperational(_repository, _rolePolicy.Brain),
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

                var metadata = new Dictionary<string, string>
                {
                    [metadataKey] = definition.Identity.Value,
                    ["evidence"] = prompt.EvidenceLocation,
                };
                if (primaryOutput is not null)
                {
                    string? after = HashFileIfPresent(ResolveRepositoryPath(_repository, primaryOutput));
                    metadata["primary-output-path"] = primaryOutput;
                    metadata["primary-output-mutated"] =
                        (after is not null && !string.Equals(after, primaryOutputBefore, StringComparison.Ordinal)).ToString();
                }
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    result.Output,
                    TimeSpan.Zero,
                    metadata);
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
                session = await Runtime!.OpenSessionAsync(
                    AgentSpecs.Review(_repository, _rolePolicy.Brain),
                    cancellationToken);
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
                    CreateOneShotPromptGateway(),
                    new CanonicalPromptComposer(),
                    CurrentPromptPolicyProfile(),
                    RequireCurrentAuthorization(),
                    ConsumedInputManifestIdentity.New(),
                    CurrentExecutionContext.ConsumedFiles ?? [],
                    console));
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
                await ArtifactMutationTransaction.CaptureAsync(artifactStore, profile);
            IAgentSession? session = null;
            bool keepChanges = false;

            try
            {
                session = await Runtime!.OpenSessionAsync(
                    AgentSpecs.ScopedArtifactOperation(_repository, _rolePolicy.Brain, profile),
                    cancellationToken);
                AgentTurnResult result = await session.RunTurnAsync(
                    prompt.Text,
                    onChunk: null,
                    cancellationToken);
                if (result.State != AgentTurnState.Completed)
                {
                    await RestorePlanSurfaceAsync(transaction, operation, cancellationToken);
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
                    await RestorePlanSurfaceAsync(transaction, operation, cancellationToken);
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
                    await RestorePlanSurfaceAsync(transaction, operation, CancellationToken.None);
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
                    await RestorePlanSurfaceAsync(transaction, operation, CancellationToken.None);
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
                string? content = await artifactStore.ReadAsync(read);
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
                if (!await artifactStore.ExistsAsync(requiredOutput))
                {
                    return $"{operation.Label} did not produce {requiredOutput}.";
                }
            }

            if (operation.RequiredOutputGlob is { } requiredGlob)
            {
                IReadOnlyList<string> matches = await ListRelativeAsync(requiredGlob);
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
                if (!await artifactStore.ExistsAsync(changedGuard))
                {
                    return $"{operation.Label} left {changedGuard} missing - it must remain present.";
                }

                string changedContent = await artifactStore.ReadAsync(changedGuard) ?? string.Empty;
                if (string.Equals(changedContent, changedGuardSnapshot ?? string.Empty, StringComparison.Ordinal))
                {
                    return $"{operation.Label} left {changedGuard} unchanged - the expected rewrite did not happen.";
                }
            }

            return null;
        }

        private async Task<IReadOnlyList<string>> ListRelativeAsync(OperationPathGlob glob) =>
            await artifactStore.ListAsync(glob.Directory, glob.Pattern);

        private OperationPermissionProfile ToPermissionProfile(PlanScopedArtifactOperationSpec operation) =>
            new(
                operation.Label,
                _repository.Path,
                operation.AllowedReads,
                operation.AllowedReadGlobs,
                operation.AllowedWrites,
                operation.AllowedWriteGlobs);

        private async Task<PromptExecutionResult> ExecutePlanWarmSessionAsync(
            PromptExecutionRequest executionRequest,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = executionRequest.Definition;
            RenderedPrompt prompt = executionRequest.RenderedPrompt;
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
                var continuityStore = new CanonicalCheckpointStore(_repository);
                bool resumedAfterRestart = false;
                if (definition.Identity.Value == "WriteExecutablePlan")
                {
                    if (planAuthoringSession is not null)
                    {
                        await ClosePlanSessionAsync();
                    }

                    planAuthoringSession = await Runtime!.OpenSessionAsync(
                        AgentSpecs.PlanAuthoring(_repository, _rolePolicy.Brain),
                        cancellationToken);
                }
                else if (planAuthoringSession is null)
                {
                    PlanWarmSessionContinuity continuity = await continuityStore.ReadAsync<PlanWarmSessionContinuity>(
                            CanonicalCheckpointKeys.PlanWarmSession, cancellationToken)
                        ?? throw new PromptContextBlockedException(
                            "RevisePlan cannot recover the authoring session because no durable WriteExecutablePlan continuity record exists.",
                            ["plan-warm-session:missing"]);
                    string planPath = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Plan);
                    if (!File.Exists(planPath) || !string.Equals(HashFile(planPath), continuity.PlanHash, StringComparison.Ordinal))
                    {
                        throw new PromptContextBlockedException(
                            "RevisePlan cannot resume because the executable plan changed after the warm-session checkpoint.",
                            ["plan-warm-session:plan-hash-mismatch"]);
                    }
                    if (!string.Equals(continuity.ExactRuntimeProfileIdentity, _rolePolicy.RuntimeProfile.Value,
                            StringComparison.Ordinal) ||
                        !string.Equals(continuity.CatalogIdentity, _catalogIdentity, StringComparison.Ordinal))
                    {
                        throw new PromptContextBlockedException(
                            "RevisePlan cannot resume under a different exact runtime profile or workflow catalog.",
                            ["plan-warm-session:profile-or-catalog-mismatch"]);
                    }

                    IAgentSessionContinuityRuntime continuityRuntime = Runtime as IAgentSessionContinuityRuntime
                        ?? _continuityRuntime
                        ?? throw new PromptContextBlockedException(
                            "RevisePlan requires a continuity-capable agent runtime after restart.",
                            ["plan-warm-session:runtime-incompatible"]);
                    CodexInstalledCompatibilityIdentity installed = CodexCompatibilityIdentityProbe.Resolve();
                    SessionContinuityNegotiationResult negotiation = await continuityRuntime.NegotiateAsync(
                        new SessionContinuityNegotiationRequest(
                            "codex",
                            CodexAppServerProtocol.ClientVersion,
                            installed.ServerVersion,
                            installed.ExecutableIdentity,
                            "app-server-v2",
                            installed.SchemaDigest,
                            default,
                            OfferExperimentalApi: true),
                        cancellationToken);
                    if (!negotiation.FromCertifiedManifest)
                    {
                        throw new PromptContextBlockedException(
                            "RevisePlan recovery is blocked because the installed provider profile is not exactly certified.",
                            ["plan-warm-session:profile-incompatible"]);
                    }

                    AgentSessionSpec fresh = AgentSpecs.PlanAuthoring(_repository, _rolePolicy.Brain);
                    var resumeSpec = new AgentSessionSpec(
                        fresh.SessionId,
                        fresh.RepositoryId,
                        fresh.Role,
                        fresh.Sandbox,
                        fresh.Model,
                        fresh.Effort,
                        fresh.ConfigurationAuthority,
                        fresh.WorkingDirectory,
                        fresh.StartupOptions,
                        continuity.ProviderThreadId,
                        fresh.OperationPermissionProfile);
                    await EnsureWarmRecoveryPlanAsync(
                        await ResolveCausalityAsync(cancellationToken),
                        $"plan:{continuity.ProviderThreadId}",
                        negotiation.Profile,
                        ["plan-warm-session:restart", $"thread:{continuity.ProviderThreadId}"],
                        cancellationToken);
                    SessionResumeResult resumed = await continuityRuntime.ResumeSessionAsync(
                        new SessionResumeRequest(
                            resumeSpec,
                            new ProviderSessionReference("codex", continuity.ProviderThreadId),
                            negotiation.Profile,
                            Timeout: TimeSpan.FromSeconds(30)),
                        cancellationToken);
                    if (resumed.Outcome != SessionResumeOutcome.SuccessfulResume || resumed.Session is null)
                    {
                        throw new PromptContextBlockedException(
                            $"RevisePlan could not resume the exact authoring thread ({resumed.Outcome}); repair or restart Plan authoring explicitly.",
                            ["plan-warm-session:resume-failed", $"resume-outcome:{resumed.Outcome}"]);
                    }

                    planAuthoringSession = resumed.Session;
                    resumedAfterRestart = true;
                }

                AgentTurnResult result = (await CreateDecisionPromptDispatcher().DispatchAsync(
                    planAuthoringSession,
                    prompt.TemplateIdentity.Value,
                    prompt.TemplateSourceHash,
                    prompt.Text,
                    CurrentExecutionContext.ConsumedFiles ?? [],
                    onChunk: null,
                    cancellationToken)).Result;
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

                bool materializedPlanFromOutput = false;
                if (definition.Identity.Value == "WriteExecutablePlan")
                {
                    materializedPlanFromOutput = await TryMaterializePlanFromOutputAsync(
                        result.Output,
                        cancellationToken);
                    string planPath = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Plan);
                    if (!File.Exists(planPath))
                    {
                        string headings = string.Join(
                            '|',
                            result.Output.Replace("\r\n", "\n", StringComparison.Ordinal)
                                .Split('\n')
                                .Select(line => line.Trim())
                                .Where(line => line.StartsWith('#'))
                                .Take(8));
                        await ClosePlanSessionAsync();
                        return new PromptExecutionResult(
                            PromptExecutionStatus.Failed,
                            result.Output,
                            TimeSpan.Zero,
                            new Dictionary<string, string>(),
                            $"WriteExecutablePlan completed without `.agents/plan.md`; returned-output-length={result.Output.Length}; " +
                            $"returned-headings={(headings.Length == 0 ? "none" : headings)}.");
                    }
                }

                string? threadId = planAuthoringSession.ThreadId;
                if (definition.Identity.Value == "WriteExecutablePlan" &&
                    threadId is { Length: > 0 })
                {
                    string planPath = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Plan);
                    if (File.Exists(planPath))
                    {
                        await continuityStore.WriteAsync(
                            CanonicalCheckpointKeys.PlanWarmSession,
                            new PlanWarmSessionContinuity(
                                threadId,
                                planAuthoringSession.SessionId.Value.ToString(),
                                result.ProviderTurnId ?? $"turn:{result.TurnIndex}",
                                _rolePolicy.RuntimeProfile.Value,
                                CurrentPromptFact?.Identity.Value
                                    ?? throw new InvalidOperationException("Plan prompt fact identity is unavailable."),
                                (CurrentExecutionContext.ConsumedFiles ?? [])
                                    .Select(file => $"input:{file.Path}:{file.Sha256}")
                                    .Order(StringComparer.Ordinal).ToArray(),
                                executionRequest.InputSnapshotHash,
                                HashFile(planPath),
                                definition.PromptIdentity,
                                _catalogIdentity,
                                CurrentExecutionContext.RunId
                                    ?? throw new InvalidOperationException("Plan run identity is unavailable."),
                                (await ResolveCausalityAsync(cancellationToken)).WorkflowInstance.Value,
                                CurrentExecutionContext.TransitionRunId
                                    ?? throw new InvalidOperationException("Plan transition-run identity is unavailable."),
                                CurrentExecutionContext.AttemptId
                                    ?? throw new InvalidOperationException("Plan attempt identity is unavailable."),
                                DateTimeOffset.UtcNow),
                            cancellationToken);
                    }
                }
                if (definition.Identity.Value == "RevisePlan")
                {
                    await ClosePlanSessionAsync();
                    await continuityStore.RetireAsync(
                        CanonicalCheckpointKeys.PlanWarmSession, cancellationToken);
                }

                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    result.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["plan-warm-session"] = definition.Identity.Value,
                        ["thread-id"] = threadId ?? string.Empty,
                        ["continuity"] = resumedAfterRestart ? "resumed-after-restart" : "warm-in-process",
                        ["plan-output-fallback"] = materializedPlanFromOutput ? "materialized" : "not-needed-or-invalid",
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

        private async Task<bool> TryMaterializePlanFromOutputAsync(
            string output,
            CancellationToken cancellationToken)
        {
            string planPath = ResolveRepositoryPath(_repository, OrchestrationArtifactPaths.Plan);
            if (File.Exists(planPath)) return false;

            string content = output.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
            if (content.StartsWith("```", StringComparison.Ordinal) &&
                content.EndsWith("```", StringComparison.Ordinal))
            {
                int firstNewline = content.IndexOf('\n');
                content = firstNewline >= 0 ? content[(firstNewline + 1)..^3].Trim() : string.Empty;
            }
            int heading = content.IndexOf("# ", StringComparison.Ordinal);
            if (heading > 0) content = content[heading..];
            bool structural = content.Length >= 100 &&
                content.StartsWith("# ", StringComparison.Ordinal) &&
                content.Contains("Milestone", StringComparison.OrdinalIgnoreCase) &&
                content.Contains("##", StringComparison.Ordinal);
            if (!structural) return false;

            await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                await ResolveCausalityAsync(cancellationToken),
                OrchestrationArtifactPaths.Plan,
                content.TrimEnd() + Environment.NewLine,
                cancellationToken);
            return true;
        }

        private static string? PrimaryOutputPath(WorkflowTransitionDefinition definition)
        {
            if (EvalPromptAssetCatalog.TryGetByTransition(definition.Identity, out EvalPromptAsset asset))
            {
                return asset.PrimaryOutputPath;
            }

            return TraditionalRoadmapPromptTransitions.TryGetPrimaryOutput(definition, out string output)
                ? output
                : null;
        }

        private static string? HashFileIfPresent(string path)
        {
            if (!File.Exists(path)) return null;
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        private static string HashFile(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        private async Task<PromptExecutionResult> ExecuteDecisionSessionAsync(
            PromptExecutionRequest executionRequest,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = executionRequest.Definition;
            RenderedPrompt prompt = executionRequest.RenderedPrompt;
            if (_agentRuntime is null)
            {
                return NotWired(definition);
            }

            try
            {
                executeRecoveryStore ??= new CanonicalDecisionRecoveryStore(
                    _repository,
                    new SqliteRecoveryStore(_repository));
                IAgentSessionContinuityRuntime continuityRuntime = Runtime as IAgentSessionContinuityRuntime
                    ?? _continuityRuntime
                    ?? throw new InvalidOperationException("The configured agent runtime does not support decision continuity.");
                string codexHome = _providerEnvironment.CodexHome;
                ResumeRecoveryStrategy recoveryPolicy = _policy.Resume.RecoveryStrategy;
                var recoveryMechanisms = new List<IRecoveryMechanism>();
                if (recoveryPolicy is ResumeRecoveryStrategy.Reconstructed or ResumeRecoveryStrategy.Certified)
                {
                    recoveryMechanisms.Add(new ThreadReadReconstructionMechanism());
                    recoveryMechanisms.Add(new RolloutReconstructionMechanism());
                    recoveryMechanisms.Add(new RepositoryReconstructionMechanism());
                }
                if (recoveryPolicy == ResumeRecoveryStrategy.Certified)
                {
                    recoveryMechanisms.Add(new NativeForkRecoveryMechanism());
                }
                var recoveryRuntime = new RecoveryRuntime(
                    executeRecoveryStore,
                    continuityRuntime,
                    new RecoverySourceCatalog(
                    [
                        new ThreadReadRecoverySource(continuityRuntime),
                        new RolloutSalvageRecoverySource(new CodexRolloutRepository(), codexHome),
                        new RepositoryContinuationRecoverySource(_repository),
                    ]),
                    new RecoveryPlanner(),
                    new RecoveryMechanismCatalog(recoveryMechanisms),
                    new CanonicalRecoveryEnvelopeFactory(),
                    _canonicalStore: new CanonicalRecoveryStore(_repository));
                LoopArtifacts decisionArtifacts = CreateLoopArtifacts();
                executeDecisionSession ??= new DecisionSession(
                    Runtime!,
                    new DecisionSessionRouter(),
                    decisionArtifacts,
                    console,
                    _repository,
                    _rolePolicy.Brain,
                    _costModel: null,
                    _resumeStore: null,
                    _projectionService: null,
                    _resumeEnabled: _policy.Resume.Enabled,
                    _continuityRuntime: continuityRuntime,
                    _recoveryStore: executeRecoveryStore,
                    _recoveryRuntime: recoveryRuntime,
                    _recoveryPolicyVersion: recoveryPolicy switch
                    {
                        ResumeRecoveryStrategy.ResumeOnly => "decision-recovery-resume-only.v1",
                        ResumeRecoveryStrategy.Reconstructed => "decision-recovery-reconstructed.v1",
                        _ => "decision-recovery-certified.v1",
                    },
                    _operationalContextGrowthStreakWarningThreshold: _policy.OperationalContextGrowthWarningStreak,
                    _promptDispatcher: CreateDecisionPromptDispatcher(),
                    _artifactEffects: new DurableLoopArtifactEffectCoordinator(_repository, decisionArtifacts));
                DecisionSessionScope scope = await new DecisionSessionScopeResolver(_repository)
                    .ResolveAsync(cancellationToken);
                await executeDecisionSession.RunAsync(
                    new DecisionExecutionContext(
                        scope,
                        executionRequest,
                        await ResolveCausalityAsync(cancellationToken)),
                    cancellationToken);
                string decisions = await ReadRequiredAsync(OrchestrationArtifactPaths.Decisions, cancellationToken);
                DecisionSessionTurnRecord? turn = await executeRecoveryStore.ReadDecisionTurnAsync(
                    executionRequest.RunId,
                    executionRequest.InputSnapshotHash,
                    cancellationToken);
                var metadata = new Dictionary<string, string>
                {
                    ["execute-decision-session"] = definition.Identity.Value,
                    ["evidence"] = prompt.EvidenceLocation,
                    ["run-id"] = executionRequest.RunId,
                    ["session-scope"] = scope.ScopeId.Value,
                    ["root-invocation"] = executionRequest.RootInvocation.Mode.ToString(),
                };
                if (turn is not null)
                {
                    metadata["lineage-id"] = turn.LineageId;
                    metadata["provider-thread-id"] = turn.ProviderThreadId;
                    metadata["provider-turn-id"] = turn.ProviderTurnId ?? string.Empty;
                    metadata["turn-record-id"] = turn.TurnRecordId;
                    metadata["history-sequence"] = turn.HistorySequence?.ToString() ?? string.Empty;
                }
                RecoveryAttempt? recoveryAttempt = await executeRecoveryStore.ReadLatestAttemptAsync(
                    scope.ScopeId.Value, cancellationToken);
                if (recoveryAttempt is not null)
                {
                    metadata["recovery-attempt-id"] = recoveryAttempt.AttemptId;
                    metadata["recovery-status"] = recoveryAttempt.Status.ToString();
                    metadata["continuity-profile-digest"] = recoveryAttempt.ProfileDigest;
                    if (recoveryAttempt.Mechanism is { } mechanism)
                    {
                        metadata["recovery-mechanism"] = $"{mechanism.Identity}@{mechanism.Version}";
                    }
                    if (recoveryAttempt.PlanDigest is { } planDigest)
                    {
                        metadata["recovery-plan-digest"] = planDigest;
                        RecoveryPlan? recoveryPlan = await executeRecoveryStore.ReadPlanAsync(
                            planDigest, cancellationToken);
                        if (recoveryPlan is not null)
                        {
                            metadata["recovery-completeness"] = recoveryPlan.ExpectedCompleteness.ToString();
                            metadata["recovery-source-count"] = recoveryPlan.Sources.Count.ToString();
                        }
                    }
                }
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    decisions,
                    TimeSpan.Zero,
                    metadata);
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
            PromptExecutionRequest executionRequest,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = executionRequest.Definition;
            RenderedPrompt prompt = executionRequest.RenderedPrompt;
            if (_agentRuntime is null)
            {
                return NotWired(definition);
            }

            return definition.Identity.Value switch
            {
                "ExecuteImplementationSlice" => await ExecuteImplementationSliceAsync(executionRequest, cancellationToken),
                "GenerateHandoff" => await ExecuteHandoffAsync(definition, prompt, cancellationToken),
                _ => NotWired(definition),
            };
        }

        private async Task<PromptExecutionResult> ExecuteImplementationSliceAsync(
            PromptExecutionRequest executionRequest,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = executionRequest.Definition;
            RenderedPrompt prompt = executionRequest.RenderedPrompt;
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

                ResolvedRuntimeProfile effectiveProfile =
                    await ResolveExecutionRuntimeProfileAsync(cancellationToken);
                string executionPrompt = prompt.Text;
                executionSession = await Runtime!.OpenSessionAsync(
                    AgentSpecs.Execution(_repository, effectiveProfile),
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
                string? threadId = executionSession.ThreadId;
                if (threadId is { Length: > 0 } && executionSliceBaseline is not null)
                {
                    await new CanonicalCheckpointStore(_repository).WriteAsync(
                        CanonicalCheckpointKeys.ExecutionWarmSession,
                        new ExecutionWarmSessionContinuity(
                            threadId,
                            executionRequest.InputSnapshotHash,
                            changedPathsAfterExecution,
                            uncheckedMilestonesBeforeExecution,
                            uncheckedMilestonesAfterExecution,
                            executionSliceBaseline,
                            HandoffCompleted: false,
                            DateTimeOffset.UtcNow),
                        cancellationToken);
                }
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    work.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-implementation"] = definition.Identity.Value,
                        ["changed-paths"] = string.Join("|", changedPathsAfterExecution),
                        ["thread-id"] = threadId ?? string.Empty,
                        ["continuity"] = "warm-in-process",
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

        private static string? AppendRepositoryReadmeContext(string? details, string? repositoryReadme)
        {
            if (string.IsNullOrWhiteSpace(repositoryReadme))
            {
                return details;
            }

            return $"""
                {details}

                # Repository README Context

                The following repository-owned README content is authoritative execution context. Use it directly when the plan or verifier refers to README-defined values; do not attempt to infer those values from hashes.
                When the README specifies exact required content and the verifier accepts multiple values, the README resolves the canonical target. A broader verifier allowlist is not ambiguity and must not block implementation solely because it contains multiple accepted values.

                <REPOSITORY_README>
                {repositoryReadme.Trim()}
                </REPOSITORY_README>
                """;
        }

        private async Task<PromptExecutionResult> ExecuteHandoffAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            bool resumedAfterRestart = false;
            ExecutionWarmSessionContinuity? continuity = null;
            if (executionSession is null)
            {
                continuity = await RestoreExecutionCheckpointAsync(cancellationToken)
                    ?? throw new PromptContextBlockedException(
                        "GenerateHandoff cannot recover because no durable ExecuteImplementationSlice checkpoint exists.",
                        ["execution-warm-session:missing"]);
                if (continuity.HandoffCompleted)
                {
                    throw new PromptContextBlockedException(
                        "GenerateHandoff checkpoint is already retired after a completed handoff.",
                        ["execution-warm-session:handoff-completed"]);
                }

                executionSession = await ResumeExecutionSessionAsync(continuity, cancellationToken);
                resumedAfterRestart = true;
            }

            try
            {
                string handoffPrompt;
                if (changedPathsAfterExecution.Count > 0)
                {
                    handoffPrompt = GenerateHandoff.Text;
                }
                else
                {
                    IReadOnlyList<string> unticked = await CreateMilestoneGate().GetUntickedItemsAsync();
                    handoffPrompt = GenerateNoChangesHandoff.Render(string.Join("\n", unticked));
                }

                string handoffIdentity = changedPathsAfterExecution.Count > 0
                    ? "GenerateHandoff"
                    : "GenerateNoChangesHandoff";
                string handoffHash = changedPathsAfterExecution.Count > 0
                    ? GenerateHandoff.SourceHash
                    : GenerateNoChangesHandoff.SourceHash;
                AgentTurnResult handoff = (await CreateDecisionPromptDispatcher().DispatchAsync(
                    executionSession,
                    handoffIdentity,
                    handoffHash,
                    handoffPrompt,
                    [],
                    onChunk: null,
                    cancellationToken)).Result;
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

                if (!await artifactStore.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff))
                {
                    await CloseExecutionSessionAsync();
                    return Failed($"Execution completed but {OrchestrationArtifactPaths.LiveHandoff} was not written.");
                }

                string? threadId = executionSession.ThreadId;
                await CloseExecutionSessionAsync();
                continuity ??= await new CanonicalCheckpointStore(_repository)
                    .ReadAsync<ExecutionWarmSessionContinuity>(
                        CanonicalCheckpointKeys.ExecutionWarmSession, cancellationToken);
                if (continuity is not null)
                {
                    await new CanonicalCheckpointStore(_repository).WriteAsync(
                        CanonicalCheckpointKeys.ExecutionWarmSession,
                        continuity with { HandoffCompleted = true, RecordedAt = DateTimeOffset.UtcNow },
                        cancellationToken);
                }
                return new PromptExecutionResult(
                    PromptExecutionStatus.Completed,
                    handoff.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["execute-implementation"] = definition.Identity.Value,
                        ["thread-id"] = threadId ?? string.Empty,
                        ["continuity"] = resumedAfterRestart ? "resumed-after-restart" : "warm-in-process",
                        ["evidence"] = prompt.EvidenceLocation,
                    });
            }
            catch (OperationCanceledException)
            {
                await CloseExecutionSessionAsync();
                return Cancelled();
            }
            catch (PromptContextBlockedException)
            {
                await CloseExecutionSessionAsync();
                throw;
            }
            catch (Exception exception)
            {
                await CloseExecutionSessionAsync();
                return Failed(exception.Message);
            }
        }

        private async Task<IAgentSession> ResumeExecutionSessionAsync(
            ExecutionWarmSessionContinuity continuity,
            CancellationToken cancellationToken)
        {
            IAgentSessionContinuityRuntime continuityRuntime = Runtime as IAgentSessionContinuityRuntime
                ?? _continuityRuntime
                ?? throw new PromptContextBlockedException(
                    "GenerateHandoff requires a continuity-capable agent runtime after restart.",
                    ["execution-warm-session:runtime-incompatible"]);
            CodexInstalledCompatibilityIdentity installed = CodexCompatibilityIdentityProbe.Resolve();
            SessionContinuityNegotiationResult negotiation = await continuityRuntime.NegotiateAsync(
                new SessionContinuityNegotiationRequest(
                    "codex",
                    CodexAppServerProtocol.ClientVersion,
                    installed.ServerVersion,
                    installed.ExecutableIdentity,
                    "app-server-v2",
                    installed.SchemaDigest,
                    default,
                    OfferExperimentalApi: true),
                cancellationToken);
            if (!negotiation.FromCertifiedManifest)
            {
                throw new PromptContextBlockedException(
                    "GenerateHandoff recovery is blocked because the installed provider profile is not exactly certified.",
                    ["execution-warm-session:profile-incompatible"]);
            }

            ResolvedRuntimeProfile effectiveProfile =
                await ResolveExecutionRuntimeProfileAsync(cancellationToken);
            AgentSessionSpec fresh = AgentSpecs.Execution(_repository, effectiveProfile);
            var resumeSpec = new AgentSessionSpec(
                fresh.SessionId,
                fresh.RepositoryId,
                fresh.Role,
                fresh.Sandbox,
                fresh.Model,
                fresh.Effort,
                fresh.ConfigurationAuthority,
                fresh.WorkingDirectory,
                fresh.StartupOptions,
                continuity.ProviderThreadId,
                fresh.OperationPermissionProfile);
            await EnsureWarmRecoveryPlanAsync(
                await ResolveCausalityAsync(cancellationToken),
                $"execute:{continuity.ProviderThreadId}",
                negotiation.Profile,
                ["execution-warm-session:restart", $"thread:{continuity.ProviderThreadId}"],
                cancellationToken);
            SessionResumeResult resumed = await continuityRuntime.ResumeSessionAsync(
                new SessionResumeRequest(
                    resumeSpec,
                    new ProviderSessionReference("codex", continuity.ProviderThreadId),
                    negotiation.Profile,
                    Timeout: TimeSpan.FromSeconds(30)),
                cancellationToken);
            if (resumed.Outcome != SessionResumeOutcome.SuccessfulResume || resumed.Session is null)
            {
                throw new PromptContextBlockedException(
                    $"GenerateHandoff could not resume the exact execution thread ({resumed.Outcome}); inspect the durable slice checkpoint before retrying.",
                    ["execution-warm-session:resume-failed", $"resume-outcome:{resumed.Outcome}"]);
            }

            return resumed.Session;
        }

        private async Task<ResolvedRuntimeProfile> ResolveExecutionRuntimeProfileAsync(
            CancellationToken cancellationToken)
        {
            var recommendationStore = new CanonicalExecutionRecommendationEvidenceStore(_persistence);
            ExecutionRecommendationEvidence recommendation =
                await recommendationStore.ReadLatestAsync(cancellationToken)
                ?? throw new InvalidOperationException("Canonical execution recommendation evidence was not found.");
            DecisionProductVersionIdentity decisionProduct = recommendation.DecisionProduct;
            var fallback = new ResolvedRuntimeProfile(
                new RuntimeProfileIdentity("runtime-fallback"),
                "codex",
                _rolePolicy.Brain.Model,
                _rolePolicy.Brain.Effort,
                "persistent-session",
                "danger-full-access",
                "execution-default",
                "never",
                "resume-or-recover",
                TimeSpan.FromMinutes(30),
                "default",
                "reconcile-before-retry");
            var evaluationStore = new CanonicalRuntimeProfileEvaluationStore(_persistence);
            var policyService = new ExecutionRecommendationPolicyService(
                new ExecutionRecommendationPolicyEvaluator(), evaluationStore);
            RenderedPromptFact currentPrompt = CurrentPromptFact
                ?? throw new InvalidOperationException("Execution prompt fact is unavailable.");
            PromptDispatchAuthorization currentAuthorization = CurrentAuthorization
                ?? throw new InvalidOperationException("Execution dispatch authorization is unavailable.");
            (_, ExecutionAuthorization authorization) = await policyService.AuthorizeAsync(
                new ExecutionRecommendationEvaluationRequest(
                    recommendation,
                    decisionProduct,
                    currentAuthorization.Policy,
                    new ProviderCapabilityEvidence(
                        ProviderCapabilityEvidenceIdentity.New(),
                        (_agentRuntime ?? throw new InvalidOperationException(
                            "Execution runtime capability evidence is unavailable.")).Capabilities.Provider,
                        [_rolePolicy.Brain.Model],
                        _rolePolicy.Brain.Effort,
                        DateTimeOffset.UtcNow),
                    fallback,
                    new ExecutionRecommendationPolicy(
                        true,
                        new HashSet<AgentModel>(Enum.GetValues<AgentModel>()),
                        AgentEffort.XHigh,
                        new HashSet<string> { ExecutionRecommendationEvidenceSchemas.Version1 })),
                _promptPolicyProfile,
                _catalogIdentity,
                CurrentExecutionContext.Workflow
                    ?? throw new InvalidOperationException("Execution catalog workflow identity is unavailable."),
                CurrentExecutionContext.Transition
                    ?? throw new InvalidOperationException("Execution catalog transition identity is unavailable."),
                $"sandbox:{fallback.SandboxProfile};permissions:{fallback.PermissionProfile};approval:{fallback.ApprovalPolicy};network:resolved-policy",
                currentPrompt.Identity,
                currentPrompt.ConsumedInputManifestIdentity,
                currentAuthorization.Causality,
                cancellationToken);
            return await new ExecutionAuthorizationResolver(evaluationStore, evaluationStore)
                .ResolveAsync(authorization, cancellationToken);
        }

        private async Task<ExecutionWarmSessionContinuity?> RestoreExecutionCheckpointAsync(
            CancellationToken cancellationToken)
        {
            ExecutionWarmSessionContinuity? continuity =
                await new CanonicalCheckpointStore(_repository).ReadAsync<ExecutionWarmSessionContinuity>(
                    CanonicalCheckpointKeys.ExecutionWarmSession, cancellationToken);
            if (continuity is null) return null;
            changedPathsAfterExecution = continuity.ChangedPaths;
            uncheckedMilestonesBeforeExecution = continuity.UncheckedMilestonesBefore;
            uncheckedMilestonesAfterExecution = continuity.UncheckedMilestonesAfter;
            executionSliceBaseline = continuity.SliceBaseline;
            return continuity;
        }

        private async Task<PromptExecutionResult> ExecuteRepositoryStateTransitionAsync(
            WorkflowTransitionDefinition definition,
            RenderedPrompt prompt,
            CancellationToken cancellationToken)
        {
            try
            {
                await RestoreExecutionCheckpointAsync(cancellationToken);
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

        private Task<string> ExecuteRepositoryPublicationAsync(CancellationToken cancellationToken)
        {
            var publisher = new AgentsSubmodulePublisher(_processRunner, _repository, console);
            publisher.EnsureSupportedTopology();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                "Repository publication topology is valid; publication is authorized only in the durable effect phase.");
        }

        private async Task<string> ExecuteCommitEvaluationAsync(CancellationToken cancellationToken)
        {
            MilestoneGate milestones = CreateMilestoneGate();
            uncheckedMilestonesAfterExecution = (await milestones.GetUntickedItemsAsync()).Count;
            bool substantiveProgress = changedPathsAfterExecution.Count > 0 ||
                uncheckedMilestonesAfterExecution < uncheckedMilestonesBeforeExecution;
            bool stalled = !substantiveProgress;
            string stallEvidence = await WriteExecuteStallEvidenceAsync(
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
                | Predicate | changed paths or fewer unchecked milestones |
                | Stalled | {stalled} |
                | Stall Evidence | {stallEvidence} |
                """;
        }

        private async Task<string> WriteExecuteStallEvidenceAsync(
            bool substantiveProgress,
            bool stalled,
            CancellationToken cancellationToken)
        {
            const string relativePath = ".LoopRelay/evidence/execute-stall/state.md";
            await new DurableFilesystemWriteEffectPlanner(_repository).WriteCandidateAsync(
                await ResolveCausalityAsync(cancellationToken),
                relativePath,
                $"""
                # Execute Stall State

                | Field | Value |
                |---|---|
                | Predicate | changed paths or fewer unchecked milestones |
                | Changed Paths | {string.Join(", ", changedPathsAfterExecution)} |
                | Unchecked Before | {uncheckedMilestonesBeforeExecution} |
                | Unchecked After | {uncheckedMilestonesAfterExecution} |
                | Substantive Progress | {substantiveProgress} |
                | Stalled | {stalled} |
                | Updated At | {DateTimeOffset.UtcNow:O} |

                Stall is derived from current repository and milestone evidence. No counter or manual latch is
                persisted; the next observation may proceed whenever the evidence changes.
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
            PromptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            WorkflowTransitionDefinition definition = request.Definition;
            RenderedPrompt prompt = request.RenderedPrompt;
            if (_agentRuntime is null)
            {
                return NotWired(definition);
            }

            try
            {
                await RestoreExecutionCheckpointAsync(cancellationToken);
                string output = definition.Identity.Value switch
                {
                    "RunNonImplementationReview" => await ExecuteNonImplementationReviewAsync(cancellationToken),
                    "RunCompletionCertification" => await ExecuteCompletionCertificationAsync(
                        await ResolveCausalityAsync(cancellationToken), cancellationToken),
                    "InterpretCompletionRoute" => await ExecuteCompletionRouteInterpretationAsync(cancellationToken),
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

        private async Task<string> ExecuteCompletionCertificationAsync(
            CanonicalCausalContext causality,
            CancellationToken cancellationToken)
        {
            var completionObserver = new CompletionPhaseEvidenceObserver(_repository, console);
            try
            {
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

                nonImplementationReviewEvidencePaths = nonImplementationReviewEvidencePaths
                    .Concat(completionReview.EvidencePaths)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var promptRunner = new AgentCompletionPromptRunner(
                CreateOneShotPromptGateway(),
                new CanonicalPromptComposer(),
                CurrentPromptPolicyProfile(),
                RequireCurrentAuthorization(),
                ConsumedInputManifestIdentity.New(),
                CurrentExecutionContext.ConsumedFiles ?? []);
                var executionEvidenceStore = new SqliteExecutionEvidenceStore(_repository);
                var archiveAdapter = new CompletedEpicArchiveService(
                artifactStore,
                promptRunner,
                completionObserver,
                new SqliteCompletedEpicArchiveMaterializer());
                var archiveService = new DurableCompletedEpicArchiveService(
                    _repository, causality, artifactStore, archiveAdapter);
                var candidateSink = new CanonicalCertifiedCompletionCandidateSink(_repository, causality);
                var contextMaterializer = new DurableCompletionContextMaterializer(
                    _repository, causality, artifactStore);
                var service = new CompletionCertificationService(
                artifactStore,
                CreateProjectionService(),
                promptRunner,
                archiveService,
                _observer: completionObserver,
                _executionEvidenceStore: executionEvidenceStore,
                _candidateSink: candidateSink,
                _contextMaterializer: contextMaterializer);
                completionCertificationResult = await service.CertifyPlanCompletionAsync(
                new CompletionCertificationRequest(
                    _repository,
                    NonImplementationReviewEvidencePaths: nonImplementationReviewEvidencePaths),
                cancellationToken);
                completionRecoveryEvidencePaths = completionObserver.EvidencePaths;
                IReadOnlyList<string> completionEvidence = completionCertificationResult.EvidencePaths
                    .Concat(completionRecoveryEvidencePaths)
                    .Append($"transition:{causality.TransitionRun.Value}")
                    .Distinct(StringComparer.Ordinal).ToArray();
                CompletionDecision completionDecision = candidateSink.Decision ??
                    new CompletionAuthority().Decide(new CompletionDecisionInput(
                        causality.Run, causality.Attempt,
                        Cancelled: false,
                        Failed: completionCertificationResult.Outcome == CompletionCertificationServiceOutcome.Failed,
                        Waiting: false,
                        ContinueExecution: false,
                        CannotProceedReason: completionCertificationResult.Outcome ==
                            CompletionCertificationServiceOutcome.SpecificCannotProceed
                                ? CompletionCannotProceedReason.ReviewRejected : null,
                        EvidenceIdentities: completionEvidence,
                        GateIdentities: [$"gate:milestone-completion:{causality.Attempt.Value}"],
                        ReviewIdentities: [$"review:completion-certification:{causality.Attempt.Value}"]),
                        DateTimeOffset.UtcNow);
                var completionStore = new CanonicalCompletionAuthorityStore(_repository);
                CompletionCertificate? completionCertificate = candidateSink.Certificate;
                CompletionClosurePlan? completionPlan = candidateSink.Plan;
                if (candidateSink.Decision is null)
                {
                    await completionStore.AppendDecisionAsync(completionDecision, cancellationToken);
                }
                await new CanonicalCheckpointStore(_repository).WriteAsync(
                CanonicalCheckpointKeys.CompletionCertification,
                new CompletionCertificationCheckpoint(
                    completionDecision.Identity.Value,
                    completionCertificate?.Identity.Value,
                    completionPlan?.Identity.Value,
                    completionCertificationResult,
                    completionRecoveryEvidencePaths,
                    DateTimeOffset.UtcNow),
                cancellationToken);
                if (completionCertificationResult.Outcome != CompletionCertificationServiceOutcome.Completed)
                {
                    throw new InvalidOperationException(
                        $"Completion certification {completionCertificationResult.Outcome}: {completionCertificationResult.Message}");
                }

                return RenderCompletionCertificationResult(completionCertificationResult, completionRecoveryEvidencePaths);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                var subject = new RecoveryCausalSubject(
                    causality,
                    CompletionPlanIdentity: causality.TransitionRun.Value);
                await _recoveryCases.RecordAsync(
                    RecoveryScopeKind.CompletionClosure,
                    subject,
                    new RecoveryDurableFacts(
                        RecoveryScopeKind.CompletionClosure, subject, EvidenceComplete: true, Corrupt: false,
                        Authorized: true, ValidInFlightCorrelation: false,
                        OutwardStarted: true, OutwardAccepted: true, ProviderOutcomeUnknown: false,
                        TerminalProviderResult: false, RawOutputDurable: false, OutputPromoted: false,
                        ExplicitFailure: exception is not OperationCanceledException,
                        ExplicitCancellation: exception is OperationCanceledException,
                        CancellationBoundary: exception is OperationCanceledException
                            ? RecoveryCancellationBoundary.DuringCompletionClosure
                            : RecoveryCancellationBoundary.None,
                        RequiredEffects: 1,
                        SucceededEffects: completionCertificationResult?.CompletedEpicArchiveIndex is null ? 0 : 1,
                        CompletionClosureStarted: true,
                        CompletionClosureSettled: false,
                        Evidence: completionObserver.EvidencePaths.Concat([exception.GetType().Name]).ToArray()),
                    CancellationToken.None);
                throw;
            }
            finally
            {
                await completionObserver.FlushAsync(causality, CancellationToken.None);
                completionRecoveryEvidencePaths = completionObserver.EvidencePaths;
            }
        }

        private async Task RestorePlanSurfaceAsync(
            ArtifactMutationTransaction transaction,
            PlanScopedArtifactOperationSpec operation,
            CancellationToken cancellationToken) =>
            await new DurableSurfaceRestoreEffectPlanner(_repository).RestoreAsync(
                await ResolveCausalityAsync(cancellationToken), transaction,
                operation.Transition.Value, cancellationToken);

        private async Task<CanonicalCausalContext> ResolveCausalityAsync(
            CancellationToken cancellationToken)
        {
            AttemptRecord attempt = (await _persistence.ReadAttemptsAsync(cancellationToken))
                .Single(item => item.AttemptId == CurrentExecutionContext.AttemptId &&
                    item.TransitionRunId == CurrentExecutionContext.TransitionRunId);
            return new CanonicalCausalContext(
                new WorkspaceIdentity(await _persistence.ReadWorkspaceIdentityAsync(cancellationToken)),
                new RunIdentity(attempt.RunId),
                new WorkflowInstanceIdentity(attempt.WorkflowInstanceId),
                new TransitionRunIdentity(attempt.TransitionRunId),
                new AttemptIdentity(attempt.AttemptId));
        }

        private async Task<CanonicalRecoveryPlan> EnsureWarmRecoveryPlanAsync(
            CanonicalCausalContext causality,
            string sessionIdentity,
            SessionContinuityProfile profile,
            IReadOnlyList<string> evidence,
            CancellationToken cancellationToken) =>
            await new WarmSessionRecoveryPlanningService(new CanonicalRecoveryStore(_repository)).PlanResumeAsync(
                causality,
                sessionIdentity,
                CurrentAuthorization?.Policy.Value ?? "resolved-policy",
                profile,
                evidence,
                cancellationToken);

        private async Task<string> ExecuteCompletionRouteInterpretationAsync(CancellationToken cancellationToken)
        {
            if (completionCertificationResult is null)
            {
                CompletionCertificationCheckpoint? checkpoint =
                    await new CanonicalCheckpointStore(_repository).ReadAsync<CompletionCertificationCheckpoint>(
                        CanonicalCheckpointKeys.CompletionCertification, cancellationToken);
                if (checkpoint is null)
                {
                    throw new InvalidOperationException(
                        "InterpretCompletionRoute requires durable RunCompletionCertification evidence.");
                }
                completionCertificationResult = checkpoint.Result;
                completionRecoveryEvidencePaths = checkpoint.RecoveryEvidencePaths;
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
            private readonly List<(string Path, string Content)> pending = [];
            private readonly HashSet<string> flushed = new(StringComparer.Ordinal);
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
                    string content = $"""
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
                        """;
                    evidencePaths.Add(relativePath);
                    pending.Add((relativePath, content));
                }
            }

            public async Task FlushAsync(
                CanonicalCausalContext causality,
                CancellationToken cancellationToken)
            {
                (string Path, string Content)[] snapshot;
                lock (gate)
                {
                    snapshot = pending.Where(item => !flushed.Contains(item.Path)).ToArray();
                }
                var writer = new DurableFilesystemWriteEffectPlanner(_repository);
                foreach ((string path, string content) in snapshot)
                {
                    await writer.WriteCandidateAsync(causality, path, content, cancellationToken);
                    lock (gate)
                    {
                        flushed.Add(path);
                    }
                }
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
            new(
                artifactStore,
                _repository,
                new LedgerLoopHistoryStore(_repository),
                new CanonicalExecutionRecommendationEvidenceStore(_persistence));

        private PromptDispatchAuthorization RequireCurrentAuthorization() =>
            CurrentAuthorization
            ?? throw new InvalidOperationException("Nested prompt authorization is unavailable.");

        private PromptPolicyProfile CurrentPromptPolicyProfile() =>
            new(
                RequireCurrentAuthorization().PolicyProfile,
                "## Resolved Prompt Policy\n\nFollow the resolved operational and artifact policy.");

        private IPromptDispatchGateway CreateOneShotPromptGateway()
        {
            var prompts = new CanonicalRenderedPromptFactStore(_persistence);
            return new PromptDispatchGateway(
                prompts,
                new CanonicalPromptDispatchLifecycleStore(_persistence),
                new LoadingPromptRuntimeDispatcher(
                    prompts,
                    new OneShotProviderTransport(
                        Runtime ?? throw new InvalidOperationException("Agent runtime is unavailable."),
                        _repository,
                        _rolePolicy)));
        }

        private IDecisionPromptTurnDispatcher CreateDecisionPromptDispatcher()
        {
            PromptDispatchAuthorization authorization = CurrentAuthorization
                ?? throw new InvalidOperationException("Decision prompt authorization is unavailable.");
            var prompts = new CanonicalRenderedPromptFactStore(_persistence);
            return new CanonicalDecisionPromptTurnDispatcher(
                prompts,
                prompts,
                new CanonicalPromptDispatchLifecycleStore(_persistence),
                new CanonicalPromptComposer(),
                new PromptPolicyProfile(
                    authorization.PolicyProfile,
                    "## Resolved Prompt Policy\n\nFollow the resolved operational and artifact policy."),
                authorization);
        }

        private sealed class OneShotProviderTransport(
            IAgentRuntime _runtime,
            Repository _repository,
            ResolvedAgentRolePolicy _rolePolicy) : IProviderPromptTransport
        {
            public async Task<PromptExecutionResult> DispatchAsync(
                PersistedRenderedPromptFact prompt,
                AuthorizedPromptDispatch dispatch,
                CancellationToken cancellationToken)
            {
                AgentTurnResult result = await _runtime.RunOneShotAsync(
                    AgentSpecs.BrainOperational(_repository, _rolePolicy.Brain),
                    prompt.Fact.RenderedContent,
                    onChunk: null,
                    cancellationToken);
                return new PromptExecutionResult(
                    result.State == AgentTurnState.Completed
                        ? PromptExecutionStatus.Completed
                        : result.State == AgentTurnState.Canceled
                            ? PromptExecutionStatus.Cancelled
                            : PromptExecutionStatus.Failed,
                    result.Output,
                    TimeSpan.Zero,
                    new Dictionary<string, string>
                    {
                        ["provider_turn_id"] = result.ProviderTurnId ?? string.Empty,
                    },
                    result.Diagnostics);
            }
        }

        /// <summary>Prompt-Authority adapter for read-only non-implementation review prompts.
        /// It receives no runtime settings or repository mutation capability.</summary>
        private sealed class CanonicalNonImplementationReviewRunner(
            IPromptDispatchGateway _prompts,
            IPromptComposer _composer,
            PromptPolicyProfile _policyProfile,
            PromptDispatchAuthorization _authorization,
            ConsumedInputManifestIdentity _consumedInputManifest,
            IReadOnlyList<ConsumedInputFile> _consumedInputs) : INonImplementationReviewRunner
        {
            public NonImplementationReviewRunnerConstraints Capabilities =>
                NonImplementationReviewRunnerConstraints.ReadOnly;

            public async Task<NonImplementationReviewRunnerResponse> RunAsync(
                NonImplementationReviewRunnerRequest request,
                CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(request);
                request.Constraints.EnsureReadOnly();
                string? sourceHash = request.PromptName switch
                {
                    "ConfirmNonImplementationCandidate" => ConfirmNonImplementationCandidate.SourceHash,
                    "SynthesizeNonImplementationInsights" => SynthesizeNonImplementationInsights.SourceHash,
                    _ => null,
                };
                PromptComposition composition = _composer.Compose(
                    new PromptTemplateIdentity(request.PromptName),
                    sourceHash,
                    _authorization.Policy,
                    _policyProfile,
                    _consumedInputManifest,
                    _consumedInputs,
                    new Dictionary<string, string>(),
                    request.PromptPayload);
                PreparedPromptDispatch prepared = await _prompts.PrepareAsync(
                    composition,
                    _authorization,
                    cancellationToken);
                PromptExecutionResult result = await _prompts.DispatchAsync(prepared, cancellationToken);
                if (result.Status != PromptExecutionStatus.Completed)
                {
                    throw new NonImplementationReviewRunnerException(
                        $"{request.PromptName} non-implementation review turn ended in state {result.Status}." +
                        (string.IsNullOrWhiteSpace(result.FailureMessage)
                            ? string.Empty
                            : $" Provider diagnostics: {result.FailureMessage}"));
                }

                return new NonImplementationReviewRunnerResponse(result.RawOutput);
            }
        }

        private MilestoneGate CreateMilestoneGate() =>
            new(artifactStore, _repository);

        private WorkingTreeChangeDetector CreateChangeDetector() =>
            new(_processRunner, _repository);

        private RepositorySliceBaselineStore CreateBaselineStore() =>
            new(new RepositoryChangeSetDetector(_processRunner, _repository), artifactStore);

        private INonImplementationPostExecutionReviewService CreateNonImplementationPostExecutionReviewService()
        {
            var ledger = new NonImplementationReviewLedgerStore(artifactStore);
            var runner = new CanonicalNonImplementationReviewRunner(
                CreateOneShotPromptGateway(),
                new CanonicalPromptComposer(),
                CurrentPromptPolicyProfile(),
                RequireCurrentAuthorization(),
                ConsumedInputManifestIdentity.New(),
                CurrentExecutionContext.ConsumedFiles ?? []);
            return new NonImplementationPostExecutionReviewService(
                CreateBaselineStore(),
                new NonImplementationArtifactClassifier(),
                new NonImplementationSemanticConfirmer(ledger, runner),
                artifactStore);
        }

        private INonImplementationCompletionReviewService CreateNonImplementationCompletionReviewService()
        {
            var ledger = new NonImplementationReviewLedgerStore(artifactStore);
            var runner = new CanonicalNonImplementationReviewRunner(
                CreateOneShotPromptGateway(),
                new CanonicalPromptComposer(),
                CurrentPromptPolicyProfile(),
                RequireCurrentAuthorization(),
                ConsumedInputManifestIdentity.New(),
                CurrentExecutionContext.ConsumedFiles ?? []);
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

        /// <summary>Durable Runtime Authority evidence. Session intent is written before launch;
        /// turn and completion evidence is written before the prompt gateway may report an observed
        /// dispatch. A persistence failure therefore propagates to the gateway's Unknown/recovery
        /// path instead of silently presenting uncertain provider effects as complete.</summary>
        private sealed class SessionSpineRecorder(CanonicalWorkflowPersistenceStore _store)
        {
            public async Task<AgentSessionRecord> TryBeginSessionAsync(
                AgentSessionSpec spec,
                string provider,
                PromptExecutionContext context,
                CancellationToken cancellationToken)
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
                    AgentConfigurationCatalog.Format(spec.Effort),
                    spec.Sandbox.Identifier);
                await _store.UpsertAgentSessionAsync(record, cancellationToken);
                return record;
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

                await _store.UpsertAgentSessionAsync(
                    session with
                    {
                        ProviderThreadId = providerThreadId ?? session.ProviderThreadId,
                        CompletedAt = DateTimeOffset.UtcNow,
                    },
                    CancellationToken.None);
            }
        }

        /// <summary>Wraps the real agent runtime so every underlying session open (including opens made by
        /// DecisionSession and the helper prompt runners) records one agent_sessions row tied to the ambient
        /// PromptExecutionContext, and every completed turn records one agent_turns row. A resumed session gets
        /// a NEW row whose provider_thread_id is the resumed thread id.</summary>
        private sealed class RecordingAgentRuntime(
            IAgentRuntime _inner,
            UnifiedPromptExecutor _executor) : IAgentRuntime, IAgentSessionContinuityRuntime
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

                AgentSessionRecord? record = await _executor.sessionRecorder.TryBeginSessionAsync(
                    spec,
                    _inner.Capabilities.Provider,
                    _executor.CurrentExecutionContext,
                    cancellationToken);
                string turnId = TurnIdentity.New().Value;
                try
                {
                    AgentTurnResult result = await _inner.RunOneShotAsync(
                        spec, prompt, onChunk, cancellationToken);
                    await _executor.sessionRecorder.TryRecordTurnAsync(
                        record,
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
                        record,
                        turnId,
                        turnIndex: 0,
                        cancelled: exception is OperationCanceledException,
                        ConsumedInputFile.HashContent(prompt),
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

            public Task<SessionContinuityNegotiationResult> NegotiateAsync(
                SessionContinuityNegotiationRequest request,
                CancellationToken cancellationToken = default) =>
                Continuity.NegotiateAsync(request, cancellationToken);

            public async Task<SessionCreateResult> CreateSessionAsync(
                SessionCreateRequest request,
                CancellationToken cancellationToken = default)
            {
                AgentSessionRecord? record = await BeginContinuitySessionAsync(
                    request.SessionSpec, cancellationToken);
                SessionCreateResult result;
                try
                {
                    result = await Continuity.CreateSessionAsync(request, cancellationToken);
                }
                catch
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, providerThreadId: null);
                    throw;
                }
                if (result.Session is null)
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, result.Created?.ThreadId);
                    return result;
                }

                return result with { Session = new RecordingAgentSession(result.Session, _executor, record) };
            }

            public async Task<SessionResumeResult> ResumeSessionAsync(
                SessionResumeRequest request,
                CancellationToken cancellationToken = default)
            {
                AgentSessionRecord? record = await BeginContinuitySessionAsync(
                    request.SessionSpec, cancellationToken);
                SessionResumeResult result;
                try
                {
                    result = await Continuity.ResumeSessionAsync(request, cancellationToken);
                }
                catch
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, request.Original.ThreadId);
                    throw;
                }
                if (result.Session is null)
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, result.Resolved?.ThreadId);
                    return result;
                }

                return result with { Session = new RecordingAgentSession(result.Session, _executor, record) };
            }

            public Task<SessionContentResult> ReadSessionAsync(
                SessionContentRequest request,
                CancellationToken cancellationToken = default) =>
                Continuity.ReadSessionAsync(request, cancellationToken);

            public Task<SessionSeedResult> SeedSessionAsync(
                SessionSeedRequest request,
                CancellationToken cancellationToken = default) =>
                Continuity.SeedSessionAsync(Unwrap(request), cancellationToken);

            public async Task<SessionForkResult> ForkSessionAsync(
                SessionForkRequest request,
                CancellationToken cancellationToken = default)
            {
                AgentSessionRecord? record = await BeginContinuitySessionAsync(
                    request.SessionSpec, cancellationToken);
                SessionForkResult result;
                try
                {
                    result = await Continuity.ForkSessionAsync(request, cancellationToken);
                }
                catch
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, providerThreadId: null);
                    throw;
                }
                if (result.Session is null)
                {
                    await _executor.sessionRecorder.TryCompleteSessionAsync(record, result.Child?.ThreadId);
                    return result;
                }

                return result with { Session = new RecordingAgentSession(result.Session, _executor, record) };
            }

            public Task<SessionReconcileResult> ReconcileAsync(
                SessionReconcileRequest request,
                CancellationToken cancellationToken = default) =>
                Continuity.ReconcileAsync(request, cancellationToken);

            private IAgentSessionContinuityRuntime Continuity => _inner as IAgentSessionContinuityRuntime
                ?? throw new InvalidOperationException(
                    "The inner agent runtime does not support continuity operations.");

            private Task<AgentSessionRecord> BeginContinuitySessionAsync(
                AgentSessionSpec spec,
                CancellationToken cancellationToken) =>
                _executor.sessionRecorder.TryBeginSessionAsync(
                    spec,
                    _inner.Capabilities.Provider,
                    _executor.CurrentExecutionContext,
                    cancellationToken);

            private static SessionSeedRequest Unwrap(SessionSeedRequest request) =>
                request.Target is RecordingAgentSession recording
                    ? request with { Target = recording.Inner }
                    : request;
        }

        private sealed class RecordingAgentSession(
            IAgentSession _inner,
            UnifiedPromptExecutor _executor,
            AgentSessionRecord? _record) : IAgentSession, ICanonicalPromptEvidenceSession
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

            public AgentSessionIdentity EvidenceSessionIdentity => _record is null
                ? new AgentSessionIdentity(_inner.SessionId.ToString())
                : new AgentSessionIdentity(_record.SessionId);

            public Task<AgentTurnResult> RunTurnAsync(
                string prompt,
                Func<AgentStreamChunk, Task>? onChunk = null,
                CancellationToken cancellationToken = default) =>
                RunRecordedTurnAsync(prompt, TurnIdentity.New(), onChunk, cancellationToken);

            public Task<AgentTurnResult> RunCanonicalTurnAsync(
                string prompt,
                RenderedPromptFactIdentity promptFact,
                PromptDispatchIdentity dispatch,
                TurnIdentity turn,
                Func<AgentStreamChunk, Task>? onChunk,
                CancellationToken cancellationToken)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(promptFact.Value);
                ArgumentException.ThrowIfNullOrWhiteSpace(dispatch.Value);
                return RunRecordedTurnAsync(prompt, turn, onChunk, cancellationToken);
            }

            private async Task<AgentTurnResult> RunRecordedTurnAsync(
                string prompt,
                TurnIdentity turn,
                Func<AgentStreamChunk, Task>? onChunk,
                CancellationToken cancellationToken)
            {
                // Runtime evidence records the hash of the immutable prompt bytes already owned
                // by Prompt Authority. It never creates or replaces a rendered-prompt fact.
                string turnId = turn.Value;
                // The in-flight turn's index: CompletedTurns only increments after completion.
                int turnIndex = _inner.CompletedTurns;
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

}
