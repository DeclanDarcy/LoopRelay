using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Cli.Services.Application;
using LoopRelay.Cli.Surface;
using LoopRelay.Application.Contracts;
using LoopRelay.Application.ReadModel;
using LoopRelay.Cli.Services.Effects;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Primitives.Diagnostics;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Import;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Cli.Services.Cli;

internal sealed class CanonicalCliApplicationService(LoopRelayCompositionRoot _composition)
    : IApplicationUseCaseDispatcher
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private CanonicalCliStatusSnapshot? lastStatus;
    private WorkflowStopReason? lastStopReason;
    private static readonly JsonSerializerOptions ProvenanceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<LoopRelayResult> DispatchAsync(
        LoopRelayRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await DispatchCoreAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new LoopRelayResult(request.Context.Correlation, ApplicationOutcomeKind.Cancelled,
                "Application request was cancelled.", 130, [], [], new Dictionary<string, string>(),
                [], [], [], [], [], []);
        }
        catch (KeyNotFoundException exception)
        {
            return CannotProceed(request, exception.Message);
        }
    }

    private async Task<LoopRelayResult> DispatchCoreAsync(
        LoopRelayRequest request,
        CancellationToken cancellationToken)
    {
        if (request is CompletionOperationRequest completion)
            return await DispatchCompletionAsync(completion, cancellationToken);
        if (request is RecoveryOperationRequest recovery)
            return await DispatchRecoveryAsync(recovery, cancellationToken);
        if (request is ImportOperationRequest import)
            return await DispatchImportAsync(import, cancellationToken);
        if (request is InteractionOperationRequest interaction)
            return interaction.Operation == InteractionOperationKind.Cancel
                ? await DispatchInteractionCancellationAsync(interaction, cancellationToken)
                : ToPublicResult(request, await ExecuteInteractionAsync(interaction, cancellationToken));
        if (request is CapabilityDiagnosticsRequest diagnostics)
            return await DispatchCapabilityDiagnosticsAsync(diagnostics, cancellationToken);
        ApplicationCommandResult result = request switch
        {
            RunWorkflowRequest run => await ExecuteInternalAsync(run, cancellationToken),
            CanonicalStatusRequest status => await ExecuteStatusAsync(status, cancellationToken),
            StorageOperationRequest storage => await ExecuteStorageAsync(storage, cancellationToken),
            _ => throw new NotSupportedException(
                $"Application request {request.GetType().Name} has no registered use-case owner."),
        };
        return ToPublicResult(request, result);
    }

    private static LoopRelayResult ToPublicResult(LoopRelayRequest request, ApplicationCommandResult result)
    {
        IReadOnlyList<string> messages = result.Status is null
            ? result.Messages
            : result.Messages.Concat([UnifiedCliStatusFormatter.Format(result.Status)]).ToArray();
        return new LoopRelayResult(request.Context.Correlation, result.Outcome,
            result.Errors.FirstOrDefault() ?? messages.LastOrDefault() ?? result.Outcome.ToString(),
            result.SuggestedExitCode, messages, result.Errors, CausalIdentities(result.Status),
            result.Evidence, result.Warnings, result.PendingEffects, [], [], result.RequiredActions,
            result.Status?.WorkspaceSnapshot?.SnapshotIdentity,
            (object?)result.Status?.WorkspaceSnapshot ?? result.Status);
    }

    private async Task<LoopRelayResult> DispatchRecoveryAsync(
        RecoveryOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecoveryIdentity))
            return CannotProceed(request, "Recovery operations require a recovery identity.");
        object payload;
        string identity;
        switch (request.Operation)
        {
            case RecoveryOperationKind.Inspect:
                var inspected = await _composition.RecoveryInspect.InspectAsync(
                    new RecoveryInspectRequest(new RecoveryCaseIdentity(request.RecoveryIdentity)), cancellationToken);
                payload = inspected;
                identity = inspected.Case.Identity.Value;
                break;
            case RecoveryOperationKind.Plan:
                CanonicalRecoveryPlan plan = await _composition.RecoveryPlan.PlanAsync(
                    new RecoveryPlanRequest(
                        new RecoveryCaseIdentity(request.RecoveryIdentity),
                        new RecoveryPlanningAuthority(
                            _composition.Policy.PolicyId,
                            _composition.RuntimeProfile.Value,
                            ExactProfileSupported: true,
                            CertifiedReconstructionAvailable: false,
                            RetryAllowed: true,
                            Enum.GetValues<CanonicalRecoveryAction>().ToHashSet(),
                            [_composition.AgentRolePolicy.Identity])),
                    cancellationToken);
                payload = plan;
                identity = plan.Identity.Value;
                break;
            case RecoveryOperationKind.Execute:
                CanonicalRecoveryActionEvent action = await _composition.RecoveryExecute.ExecuteAsync(
                    new RecoveryExecuteRequest(new RecoveryPlanIdentity(request.RecoveryIdentity)), cancellationToken);
                payload = action;
                identity = action.Identity.Value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
        return new LoopRelayResult(request.Context.Correlation, ApplicationOutcomeKind.Completed,
            $"Recovery {request.Operation} completed.", 0, [$"Recovery {request.Operation}: {identity}"], [],
            new Dictionary<string, string> { ["recovery"] = identity }, [identity], [], [], [identity], [], [],
            Payload: payload);
    }

    private async Task<LoopRelayResult> DispatchInteractionCancellationAsync(
        InteractionOperationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestIdentity))
            return CannotProceed(request, "Interaction cancellation requires a request identity.");
        var identity = new InteractionRequestIdentity(request.RequestIdentity);
        InteractionAggregate aggregate = await _composition.InteractionBroker.ShowAsync(
            new ShowInteractionQuery(identity), cancellationToken);
        aggregate = await _composition.InteractionBroker.CancelAsync(
            new CancelInteractionCommand(identity, request.ResponseDocument ?? "Cancelled by application request.",
                aggregate.RowVersion), cancellationToken);
        return new LoopRelayResult(request.Context.Correlation, ApplicationOutcomeKind.Cancelled,
            "Interaction request was cancelled.", 0, [$"Interaction cancelled: {identity.Value}"], [],
            new Dictionary<string, string> { ["interaction"] = identity.Value }, [identity.Value], [], [], [],
            [identity.Value], [], Payload: aggregate);
    }

    private async Task<LoopRelayResult> DispatchCapabilityDiagnosticsAsync(
        CapabilityDiagnosticsRequest request,
        CancellationToken cancellationToken)
    {
        RuntimePrerequisiteApplicationResult result = request.IncludePrerequisites
            ? await _composition.InspectRuntimePrerequisitesAsync(cancellationToken)
            : RuntimePrerequisiteApplicationResult.NotRequired;
        ApplicationOutcomeKind outcome = result.StopReason is null
            ? ApplicationOutcomeKind.Completed : ApplicationOutcomeKind.UnsupportedProviderCapability;
        return new LoopRelayResult(request.Context.Correlation, outcome,
            result.StopReason?.ToString() ?? "Capability prerequisites are satisfied or not required.",
            result.StopReason is null ? 0 : 4,
            [result.StopReason?.ToString() ?? "Capabilities available."], [],
            new Dictionary<string, string>
            {
                ["runtimeProfile"] = _composition.RuntimeProfile.Value,
                ["rolePolicy"] = _composition.AgentRolePolicy.Identity,
            },
            result.Evidence?.Findings.Select(item => $"{item.Id}:{item.Code}").ToArray() ?? [],
            [], [], [], [], result.StopReason is null ? [] : ["Satisfy the reported runtime prerequisite."],
            Payload: result);
    }

    private async Task<LoopRelayResult> DispatchImportAsync(
        ImportOperationRequest request,
        CancellationToken cancellationToken)
    {
        ImportResult imported = request.Operation switch
        {
            ImportOperationKind.Detect => await _composition.ImportGateway.DetectAsync(
                _composition.Repository.Path, cancellationToken),
            ImportOperationKind.Preview when !string.IsNullOrWhiteSpace(request.ImportIdentity) =>
                await _composition.ImportGateway.PreviewAsync(
                    new ImportDetectionIdentity(request.ImportIdentity), cancellationToken),
            ImportOperationKind.Execute when !string.IsNullOrWhiteSpace(request.ImportIdentity) =>
                await _composition.ImportGateway.ExecuteAsync(
                    new ImportPreviewIdentity(request.ImportIdentity), cancellationToken),
            ImportOperationKind.Verify when !string.IsNullOrWhiteSpace(request.ImportIdentity) =>
                await _composition.ImportGateway.VerifyAsync(request.ImportIdentity, cancellationToken),
            _ => new ImportResult(ImportLifecycle.Refused, null, null, null, null,
                $"Import {request.Operation} requires an import identity.", []),
        };
        (ApplicationOutcomeKind outcome, int exitCode) = imported.Lifecycle switch
        {
            ImportLifecycle.EffectsPending => (ApplicationOutcomeKind.EffectsPending, 3),
            ImportLifecycle.RecoveryRequired => (ApplicationOutcomeKind.RecoveryRequired, 4),
            ImportLifecycle.ApprovalRequired => (ApplicationOutcomeKind.HumanDecisionRequired, 4),
            ImportLifecycle.Refused => (ApplicationOutcomeKind.SpecificCannotProceed, 4),
            _ => (ApplicationOutcomeKind.Completed, 0),
        };
        var causal = new Dictionary<string, string>(StringComparer.Ordinal);
        if (imported.Detection is { } detection) causal["importDetection"] = detection.Identity.Value;
        if (imported.Preview is { } preview) causal["importPreview"] = preview.Identity.Value;
        if (imported.Operation is { } operation) causal["importOperation"] = operation.Value;
        if (imported.Receipt is { } receipt) causal["importReceipt"] = receipt.Identity.Value;
        string[] actions = imported.Lifecycle switch
        {
            ImportLifecycle.ApprovalRequired => ["Resolve the import approval interaction before execution."],
            ImportLifecycle.RecoveryRequired => ["Reconcile the persisted import promotion effect through Recovery Authority."],
            ImportLifecycle.Refused => [imported.Explanation],
            _ => [],
        };
        return new LoopRelayResult(request.Context.Correlation, outcome, imported.Explanation, exitCode,
            [imported.Explanation], [], causal, imported.Evidence, [], [],
            outcome == ApplicationOutcomeKind.RecoveryRequired && imported.Operation is { } recovery
                ? [recovery.Value] : [], [], actions, Payload: imported);
    }

    private static LoopRelayResult CannotProceed(LoopRelayRequest request, string reason) =>
        new(request.Context.Correlation, ApplicationOutcomeKind.SpecificCannotProceed, reason, 4, [], [reason],
            new Dictionary<string, string>(), [], [], [], [], [], [reason]);

    private async Task<LoopRelayResult> DispatchCompletionAsync(
        CompletionOperationRequest request,
        CancellationToken cancellationToken)
    {
        CompletionAuthorityProjectionSnapshot projection = await new CompletionApplicationProjection(
            _composition.Repository).ProjectAsync(cancellationToken);
        (ApplicationOutcomeKind outcome, int exitCode, string reason) = CompletionOutcome(projection);
        var causal = new Dictionary<string, string>(StringComparer.Ordinal);
        if (projection.LatestDecision is { } decision) causal["completionDecision"] = decision.Identity.Value;
        if (projection.Certificate is { } certificate) causal["completionCertificate"] = certificate.Identity.Value;
        if (projection.ClosurePlan is { } plan) causal["completionClosurePlan"] = plan.Identity.Value;
        if (projection.LatestSettlement is { } settlement) causal["completionSettlement"] = settlement.Identity.Value;
        if (projection.TerminalFact is { } terminal) causal["certifiedTerminal"] = terminal.Identity.Value;
        string[] evidence = projection.LatestDecision?.EvidenceIdentities
            .Concat(projection.LatestSettlement?.EvidenceIdentities ?? [])
            .Distinct(StringComparer.Ordinal).ToArray() ?? [];
        string[] actions = outcome switch
        {
            ApplicationOutcomeKind.EffectsPending => ["Resume the persisted completion closure plan."],
            ApplicationOutcomeKind.RecoveryRequired => ["Reconcile the unknown completion effect through Recovery Authority."],
            ApplicationOutcomeKind.SpecificCannotProceed => ["Resolve the typed completion cannot-proceed reason and start a new attempt."],
            _ => [],
        };
        return new LoopRelayResult(
            request.Context.Correlation,
            outcome,
            reason,
            exitCode,
            [reason],
            outcome == ApplicationOutcomeKind.Failed ? [reason] : [],
            causal,
            evidence,
            [],
            projection.PendingOperations,
            outcome == ApplicationOutcomeKind.RecoveryRequired && projection.LatestSettlement is { } recovery
                ? [recovery.Identity.Value] : [],
            [],
            actions,
            projection.Watermark,
            projection);
    }

    private static (ApplicationOutcomeKind Outcome, int ExitCode, string Reason) CompletionOutcome(
        CompletionAuthorityProjectionSnapshot projection)
    {
        if (projection.TerminalFact is not null)
            return (ApplicationOutcomeKind.Completed, 0, "Completion closure is certified terminal.");
        if (projection.LatestSettlement is { } settlement)
            return settlement.Kind switch
            {
                CompletionSettlementKind.EffectsPending =>
                    (ApplicationOutcomeKind.EffectsPending, 3, "Completion closure effects remain pending."),
                CompletionSettlementKind.RecoveryRequired =>
                    (ApplicationOutcomeKind.RecoveryRequired, 4, "Completion closure requires recovery reconciliation."),
                CompletionSettlementKind.Cancelled =>
                    (ApplicationOutcomeKind.Cancelled, 130, "Completion closure was cancelled."),
                CompletionSettlementKind.SpecificCannotProceed =>
                    (ApplicationOutcomeKind.SpecificCannotProceed, 4, "Completion closure cannot proceed for a typed reason."),
                _ => (ApplicationOutcomeKind.Failed, 4, "Completion closure failed."),
            };
        return projection.LatestDecision?.Kind switch
        {
            CompletionDecisionKind.CertifiedCandidate =>
                (ApplicationOutcomeKind.EffectsPending, 3, "Completion candidate awaits closure effects."),
            CompletionDecisionKind.Continue =>
                (ApplicationOutcomeKind.Waiting, 3, "Completion decision requires another execution slice."),
            CompletionDecisionKind.Waiting =>
                (ApplicationOutcomeKind.Waiting, 3, "Completion decision is waiting for current evidence."),
            CompletionDecisionKind.Failed =>
                (ApplicationOutcomeKind.Failed, 4, "Completion decision failed."),
            CompletionDecisionKind.Cancelled =>
                (ApplicationOutcomeKind.Cancelled, 130, "Completion decision was cancelled."),
            CompletionDecisionKind.SpecificCannotProceed =>
                (ApplicationOutcomeKind.SpecificCannotProceed, 4,
                    $"Completion cannot proceed: {projection.LatestDecision.CannotProceedReason}."),
            _ => (ApplicationOutcomeKind.Waiting, 3, "No completion decision has been recorded."),
        };
    }

    private async Task<ApplicationCommandResult> ExecuteInternalAsync(
        RunWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        ResetOutput();
        int exitCode = await RunCoreAsync(request, cancellationToken);
        return FinishInternal(exitCode);
    }

    private ApplicationCommandResult FinishInternal(int exitCode)
    {
        IReadOnlyList<string> evidence = lastStatus?.Observation.Evidence
            .Select(item => $"{item.Authority}:{item.Location}")
            .ToArray() ?? [];
        IReadOnlyList<string> warnings = lastStatus?.Resolution.Explanation.Warnings
            .Select(warning => $"{warning.Category}: {warning.Concern}")
            .ToArray() ?? [];
        IReadOnlyList<string> pendingEffects = lastStatus?.PendingEffects ?? [];
        IReadOnlyList<string> requiredActions = lastStatus?.RequiredActions ?? [];
        return new ApplicationCommandResult(
            OutcomeFor(exitCode, lastStopReason, lastStatus),
            exitCode,
            Lines(_output.ToString()),
            Lines(_error.ToString()),
            evidence,
            warnings,
            pendingEffects,
            requiredActions,
            lastStatus);
    }

    private void ResetOutput()
    {
        lastStatus = null;
        lastStopReason = null;
        _output.GetStringBuilder().Clear();
        _error.GetStringBuilder().Clear();
    }

    private static IReadOnlyDictionary<string, string> CausalIdentities(CanonicalCliStatusSnapshot? status) =>
        status is null ? new Dictionary<string, string>() : status.Observation.TransitionRuns
            .SelectMany(run => new[]
            {
                new KeyValuePair<string, string>($"transition:{run.Transition.Value}", run.State.ToString()),
            }).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private async Task<int> RunCoreAsync(
        RunWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryObservation observation = await _composition.ObserveAsync(cancellationToken);
        if (observation.StorageVerification.IsUnusable)
        {
            PrintStorageVerification(observation);
            lastStopReason = WorkflowStopReason.StorageUnusable;
            return 4;
        }

        await _composition.EffectWorker.RunOnceAsync(cancellationToken);
        observation = await _composition.ObserveAsync(cancellationToken);

        // M7: runtime prerequisites are inspected before any agent launches — an Error aborts
        // with the typed MissingRuntimePrerequisite outcome instead of the raw resolver
        // exception the first send would otherwise throw. Only the production composition
        // inspects: injected runtimes have no provider prerequisites.
        RuntimePrerequisiteApplicationResult runtimePrerequisites =
            await _composition.InspectRuntimePrerequisitesAsync(cancellationToken);
        foreach (RuntimePrerequisiteFinding finding in runtimePrerequisites.Evidence?.Findings ?? [])
        {
            TextWriter target = finding.Severity == RuntimePrerequisiteFindingSeverity.Error ? _error : _output;
            target.WriteLine($"Runtime prerequisite [{finding.Severity}] {finding.Id}: {finding.Message}");
        }

        if (runtimePrerequisites.StopReason is { } prerequisiteStopReason)
        {
            lastStopReason = prerequisiteStopReason;
            _output.WriteLine($"Stop reason: {prerequisiteStopReason}");
            return ExitCodeFor(prerequisiteStopReason);
        }

        return await RunWorkflowAsync(ToWorkflowInvocation(request.Mode, request.Workflow),
            request.Context.Interactive, observation, cancellationToken);
    }

    public static int ExitCodeFor(WorkflowStopReason stopReason) =>
        stopReason switch
        {
            WorkflowStopReason.ChainCompleted => 0,
            WorkflowStopReason.BoundedWorkflowCompleted => 0,
            WorkflowStopReason.Waiting => 0,
            WorkflowStopReason.TransitionCompleted => 0,
            WorkflowStopReason.Failed => 1,
            WorkflowStopReason.Stalled => 3,
            WorkflowStopReason.MissingRequiredInput => 4,
            WorkflowStopReason.DirtyInputSurface => 4,
            WorkflowStopReason.UnversionedInputSurface => 4,
            WorkflowStopReason.StorageUnusable => 4,
            WorkflowStopReason.MissingRuntimePrerequisite => 4,
            WorkflowStopReason.Ambiguous => 4,
            WorkflowStopReason.NoEligibleTransition => 4,
            WorkflowStopReason.RecoveryRequired => 4,
            WorkflowStopReason.RequiredEffectsPending => 4,
            WorkflowStopReason.WaitingForInteraction => 4,
            WorkflowStopReason.CompatibilityImportRequired => 4,
            WorkflowStopReason.UnsupportedProviderCapability => 4,
            WorkflowStopReason.ConcurrentStateConflict => 4,
            WorkflowStopReason.InputInvalidated => 4,
            WorkflowStopReason.Cancelled => 130,
            _ => 1,
        };

    private static WorkflowInvocation ToWorkflowInvocation(RunInvocationMode mode, string? workflow) =>
        new(mode switch
        {
            RunInvocationMode.ForcedTraditional => InvocationModeKind.ForcedTraditionalChain,
            RunInvocationMode.ForcedEval => InvocationModeKind.ForcedEvalChain,
            RunInvocationMode.BoundedWorkflow when workflow == "Execute" => InvocationModeKind.BoundedExecute,
            RunInvocationMode.BoundedWorkflow when workflow == "EvalRoadmap" => InvocationModeKind.BoundedEval,
            RunInvocationMode.BoundedWorkflow when workflow == "TraditionalRoadmap" => InvocationModeKind.BoundedTraditional,
            RunInvocationMode.BoundedWorkflow => InvocationModeKind.BoundedPlan,
            _ => InvocationModeKind.DefaultChained,
        });

    private async Task<ApplicationCommandResult> ExecuteStatusAsync(
        CanonicalStatusRequest request,
        CancellationToken cancellationToken)
    {
        ResetOutput();
        RepositoryObservation observation = await _composition.ObserveAsync(cancellationToken);
        WorkflowInvocation invocation = ToWorkflowInvocation(request.Mode, request.Workflow);
        WorkflowResolutionResult resolution = _composition.Resolve(invocation, observation);
        lastStatus = await CanonicalStatusSnapshotComposer.ProjectStatusAsync(
            _composition, observation, resolution, cancellationToken);
        return FinishInternal(observation.StorageVerification.IsUnusable ? 4 : 0);
    }

    private async Task<ApplicationCommandResult> ExecuteStorageAsync(
        StorageOperationRequest request,
        CancellationToken cancellationToken)
    {
        ResetOutput();
        if (request.Operation == LoopRelay.Application.Contracts.StorageOperationKind.Verify)
        {
            StorageInspection verified = await _composition.StorageAuthority.VerifyAsync(cancellationToken);
            PrintStorageInspection(verified);
            return FinishInternal(verified.Health == StorageHealth.Healthy ? 0 : 4);
        }
        StorageOperationResult result = request.Operation switch
        {
            LoopRelay.Application.Contracts.StorageOperationKind.Initialize => await _composition.StorageAuthority.InitializeAsync(cancellationToken),
            LoopRelay.Application.Contracts.StorageOperationKind.Migrate => await _composition.StorageAuthority.MigrateAsync(cancellationToken),
            LoopRelay.Application.Contracts.StorageOperationKind.Export => await _composition.StorageAuthority.ExportAsync(
                request.Target ?? ".LoopRelay/exports/workspace.canonical.json", cancellationToken),
            LoopRelay.Application.Contracts.StorageOperationKind.Sync => await _composition.StorageAuthority.SyncAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
        _output.WriteLine($"Storage operation: {request.Operation}");
        _output.WriteLine($"Operation identity: {result.Operation?.Value ?? "(none)"}");
        _output.WriteLine($"Lifecycle: {result.Lifecycle}");
        _output.WriteLine($"Explanation: {result.Explanation}");
        PrintStorageInspection(result.Inspection);
        return FinishInternal(result.Lifecycle == StorageOperationLifecycle.Completed ? 0 : 4);
    }

    private async Task<ApplicationCommandResult> ExecuteInteractionAsync(
        InteractionOperationRequest request,
        CancellationToken cancellationToken)
    {
        ResetOutput();
        if (request.Operation == InteractionOperationKind.List)
        {
            IReadOnlyList<InteractionAggregate> outstanding = await _composition.InteractionBroker.ListAsync(
                new ListInteractionsQuery(), cancellationToken);
            if (outstanding.Count == 0)
            {
                _output.WriteLine("Outstanding interactions: (none)");
                return FinishInternal(0);
            }
            foreach (InteractionAggregate interaction in outstanding)
            {
                _output.WriteLine($"{interaction.Request.Identity.Value} {interaction.Request.Category} {interaction.State} v{interaction.RowVersion}");
                _output.WriteLine($"Question: {interaction.Request.Question}");
            }
            return FinishInternal(0);
        }

        if (string.IsNullOrWhiteSpace(request.RequestIdentity))
        {
            _error.WriteLine($"Interaction {request.Operation} requires a request identity.");
            return FinishInternal(4);
        }
        var identity = new InteractionRequestIdentity(request.RequestIdentity);
        InteractionAggregate aggregate;
        try
        {
            aggregate = await _composition.InteractionBroker.ShowAsync(
                new ShowInteractionQuery(identity), cancellationToken);
        }
        catch (KeyNotFoundException exception)
        {
            _error.WriteLine(exception.Message);
            return FinishInternal(4);
        }

        if (request.Operation == InteractionOperationKind.Show)
        {
            PrintInteraction(aggregate);
            return FinishInternal(0);
        }

        if (string.IsNullOrWhiteSpace(request.ResponseDocument))
        {
            _error.WriteLine("Interaction response requires a response document.");
            return FinishInternal(4);
        }
        string responseJson = request.ResponseDocument;
        InteractionResponseResult response = await _composition.InteractionBroker.RespondAsync(
            new RespondInteractionCommand(
                identity,
                responseJson,
                $"local-cli:{identity.Value}:{Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(responseJson)))}",
                $"local-cli-process:{Environment.ProcessId}",
                ["responder-authentication", "mutation-authorization", "decision-authorization"],
                aggregate.RowVersion),
            cancellationToken);
        if (!response.Accepted || response.Aggregate is null)
        {
            _error.WriteLine(response.Explanation);
            return FinishInternal(4);
        }

        InteractionAggregate updated = response.Aggregate;
        string? effectIdentity = null;
        if (!updated.ResumeAuthorized)
        {
            using JsonDocument document = JsonDocument.Parse(response.Response!.ResponseJson);
            bool accepted = document.RootElement.TryGetProperty("accept", out JsonElement accept) && accept.GetBoolean();
            if (accepted)
            {
                EffectIntent effect = await new DirtyInputCommitEffectPlanner(_composition.Repository)
                    .ScheduleAsync(updated, cancellationToken);
                effectIdentity = effect.Identity.Value;
            }
            updated = await _composition.InteractionBroker.ResolveAsync(
                new ResolveInteractionCommand(identity, updated.RowVersion,
                    effectIdentity is null ? ["dirty-input-offer-declined"] : [$"effect:{effectIdentity}"]),
                cancellationToken);
        }

        _output.WriteLine($"Interaction resolved: {identity.Value}");
        _output.WriteLine($"State: {updated.State}");
        if (effectIdentity is not null) _output.WriteLine($"Commit effect planned: {effectIdentity}");
        return FinishInternal(0);
    }

    private void PrintInteraction(InteractionAggregate interaction)
    {
        _output.WriteLine($"Request: {interaction.Request.Identity.Value}");
        _output.WriteLine($"Category: {interaction.Request.Category}");
        _output.WriteLine($"State: {interaction.State}");
        _output.WriteLine($"Row version: {interaction.RowVersion}");
        _output.WriteLine($"Question: {interaction.Request.Question}");
        _output.WriteLine($"Presentation: {interaction.Request.PresentationJson}");
        _output.WriteLine($"Response schema: {interaction.Request.Policy.ResponseJsonSchema}");
        _output.WriteLine($"Schema hash: {interaction.Request.Policy.ResponseSchemaHash}");
        _output.WriteLine($"Deadline behavior: {interaction.Request.Policy.DeadlineBehavior}");
        _output.WriteLine($"Default response: {interaction.Request.Policy.DefaultResponseJson ?? "(none)"}");
        _output.WriteLine($"Required trust evidence: {string.Join(", ", interaction.Request.Policy.RequiredTrustEvidence)}");
        _output.WriteLine($"Resolver owner: {interaction.Request.Policy.ResolverOwner}");
        _output.WriteLine($"Creation evidence: {string.Join(", ", interaction.Request.CreationEvidence)}");
    }

    private async Task<int> RunWorkflowAsync(
        WorkflowInvocation invocation,
        bool interactive,
        RepositoryObservation observation,
        CancellationToken cancellationToken)
    {
        lastStopReason = null;
        bool certifiedTerminalState = observation.Products.Any(product =>
                product.Product.Identity == ProductIdentity.CertifiedCompletion && product.GateUsable) &&
            observation.WorkflowStates.Any(state =>
                state.Workflow == WorkflowIdentity.Execute &&
                state.State == WorkflowResolutionState.Completed &&
                state.CurrentStage is null);
        if (certifiedTerminalState)
        {
            lastStopReason = WorkflowStopReason.BoundedWorkflowCompleted;
            _output.WriteLine($"Stop reason: {WorkflowStopReason.BoundedWorkflowCompleted}");
            _output.WriteLine(
                "Explanation: CertifiedCompletion is already durable; no workflow, provider, or effect work is required.");
            return 0;
        }

        int budget = invocation.IsBounded
            ? 1
            : _composition.Policy.MaxUnboundedContinuationSteps;
        string chainIdentity = _composition.SelectChain(invocation, observation).Identity;
        string invocationMode = invocation.Mode.ToString();
        string workspaceId = await _composition.Persistence.ReadWorkspaceIdentityAsync(cancellationToken);
        WorkflowChainDefinition chain = _composition.SelectChain(invocation, observation);
        KernelRootEntry entry = await new CanonicalKernelRootRunCoordinator(_composition.Persistence).EnterAsync(
            workspaceId, chainIdentity, invocationMode, _composition.WorkflowCatalog, cancellationToken);
        if (entry.Kind is KernelRootEntryKind.Ambiguous or KernelRootEntryKind.RecoveryRequired)
        {
            WorkflowStopReason reason = entry.Kind == KernelRootEntryKind.Ambiguous
                ? WorkflowStopReason.Ambiguous : WorkflowStopReason.RecoveryRequired;
            lastStopReason = reason;
            _output.WriteLine($"Stop reason: {reason}");
            _output.WriteLine($"Explanation: {entry.Explanation}");
            foreach (string evidence in entry.Evidence) _output.WriteLine($"Evidence: {evidence}");
            return 4;
        }
        RunIdentity run = entry.Run ?? throw new InvalidOperationException("Kernel root entry has no run identity.");
        DateTimeOffset runStartedAt = entry.StartedAt;
        if (entry.Kind == KernelRootEntryKind.Created)
        {
            await _composition.Persistence.AppendPolicyResolutionAsync(
                new CanonicalPolicyResolutionRecord(
                    CausalUlid.NewId("res"),
                    _composition.Policy.PolicyId,
                    _composition.Policy.SchemaVersion,
                    _composition.Policy.ResolvedJson,
                    JsonSerializer.Serialize(_composition.Policy.Provenance, ProvenanceJsonOptions),
                    _composition.Policy.SourceDescription,
                    runStartedAt),
                cancellationToken);
        }
        await _composition.Persistence.AppendAgentRolePolicyAsync(
            _composition.AgentRolePolicy, cancellationToken);
        var context = new WorkflowRunContext(new WorkspaceIdentity(workspaceId), run,
            new PolicyIdentity(_composition.Policy.PolicyId), _composition.RuntimeProfile,
            _composition.PromptPolicyProfile, _composition.AgentRolePolicy.Identity);
        try
        {
            KernelResult result = await _composition.OrchestrationKernel.RunAsync(new KernelCommand(
                invocation, observation, chain, _composition.WorkflowCatalog, context,
                budget, interactive), cancellationToken);
            lastStopReason = result.StopReason;
            if (result.ChainResult is not null) PrintRunResult(result.ChainResult);
            else
            {
                _output.WriteLine($"Stop reason: {result.StopReason}");
                _output.WriteLine($"Explanation: {result.Explanation}");
            }
            if (result.StopReason is WorkflowStopReason.ChainCompleted or WorkflowStopReason.BoundedWorkflowCompleted or
                WorkflowStopReason.Cancelled or WorkflowStopReason.Failed)
            {
                await _composition.Persistence.UpsertRunAsync(new RunRecord(run.Value, workspaceId, chainIdentity,
                    invocationMode, result.StopReason.ToString(), runStartedAt, DateTimeOffset.UtcNow,
                    result.StopReason.ToString(), result.Explanation, _composition.WorkflowCatalog.Identity,
                    _composition.WorkflowCatalog.SemanticVersion), CancellationToken.None);
            }
            return ExitCodeFor(result.StopReason);
        }
        catch (OperationCanceledException)
        {
            lastStopReason = WorkflowStopReason.Cancelled;
            await _composition.Persistence.UpsertRunAsync(new RunRecord(run.Value, workspaceId, chainIdentity,
                invocationMode, "Cancelled", runStartedAt, DateTimeOffset.UtcNow, "Cancelled",
                "Invocation cancellation is durable.", _composition.WorkflowCatalog.Identity,
                _composition.WorkflowCatalog.SemanticVersion), CancellationToken.None);
            throw;
        }
    }

    private void PrintRunResult(WorkflowChainRunResult result)
    {
        _output.WriteLine($"Workflow: {result.LastWorkflow}");
        _output.WriteLine($"Stop reason: {result.StopReason}");
        _output.WriteLine($"Explanation: {result.Explanation}");
        if (result.ControllerResult?.Transition is { } transition)
        {
            _output.WriteLine($"Transition: {transition.Transition}");
            _output.WriteLine($"Outcome: {transition.Outcome}");
            _output.WriteLine($"Durable state: {transition.DurableState}");
            PrintGateWarnings(transition.InputGate);
            PrintGateWarnings(transition.OutputGate);
        }
    }

    private void PrintGateWarnings(GateResult? gate)
    {
        if (gate is null || gate.IsSatisfied)
        {
            return;
        }

        foreach (GateRequirementResult requirement in gate.Requirements)
        {
            if (requirement.Status != GateStatus.Satisfied)
            {
                _output.WriteLine($"Warning: {requirement.RequirementIdentity}: {CollapseToSingleLine(requirement.Explanation)}");
            }
        }
    }

    // Warning lines are line-oriented output; explanation text that embeds process output (for example
    // multi-line git stderr) must not be able to break one warning across several lines.
    private static string CollapseToSingleLine(string text) =>
        string.Join(
            " ",
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private void PrintStorageVerification(RepositoryObservation observation)
    {
        StorageVerificationResult verification = observation.StorageVerification;
        _output.WriteLine($"Storage authority: {verification.Authority}");
        _output.WriteLine($"Usable authority: {verification.UsableAuthority}");
        _output.WriteLine($"Evidence: {FormatList(verification.Evidence)}");
        _output.WriteLine($"Stale exports: {FormatList(verification.StaleExports)}");
        _output.WriteLine($"Conflicts: {FormatList(verification.Conflicts)}");
        _output.WriteLine($"Corruption: {FormatList(verification.Corruption)}");
        _output.WriteLine($"Unsupported schema: {FormatList(verification.UnsupportedSchema)}");
        _output.WriteLine($"Unresolved references: {FormatList(verification.UnresolvedReferences)}");
        _output.WriteLine($"Partial transactions: {FormatList(verification.PartialTransactions)}");
        _output.WriteLine($"Warnings: {FormatList(verification.BlockingConditions.Select(warning => warning.Concern).ToArray())}");
    }

    private void PrintStorageInspection(StorageInspection inspection)
    {
        _output.WriteLine($"Storage health: {inspection.Health}");
        _output.WriteLine($"Authority exists: {inspection.Exists}");
        _output.WriteLine($"Byte length: {inspection.ByteLength?.ToString() ?? "(none)"}");
        _output.WriteLine($"Byte SHA-256: {inspection.ByteSha256 ?? "(none)"}");
        _output.WriteLine($"Schema identity: {inspection.Schema?.SchemaIdentity ?? "(none)"}");
        _output.WriteLine($"Schema family: {inspection.Schema?.Family.ToString() ?? "(none)"}");
        _output.WriteLine($"Schema version: {inspection.Schema?.Version?.ToString() ?? "(none)"}");
        _output.WriteLine($"Physical shape: {inspection.Schema?.Shape.ToString() ?? "(none)"}");
        _output.WriteLine($"Shape fingerprint: {inspection.Schema?.ShapeFingerprint ?? "(none)"}");
        _output.WriteLine($"Unresolved references: {FormatList(inspection.UnresolvedReferences)}");
        _output.WriteLine($"Interrupted operations: {FormatList(inspection.InterruptedOperations)}");
        _output.WriteLine($"Required actions: {FormatList(inspection.RequiredActions)}");
    }

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);

    private static IReadOnlyList<string> Lines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToArray();

    private static ApplicationOutcomeKind OutcomeFor(
        int exitCode,
        WorkflowStopReason? stopReason,
        CanonicalCliStatusSnapshot? status) => stopReason switch
    {
        WorkflowStopReason.ChainCompleted or WorkflowStopReason.BoundedWorkflowCompleted or
            WorkflowStopReason.TransitionCompleted => ApplicationOutcomeKind.Completed,
        WorkflowStopReason.Waiting => ApplicationOutcomeKind.Waiting,
        WorkflowStopReason.RequiredEffectsPending => ApplicationOutcomeKind.EffectsPending,
        WorkflowStopReason.RecoveryRequired => ApplicationOutcomeKind.RecoveryRequired,
        WorkflowStopReason.WaitingForInteraction => ApplicationOutcomeKind.HumanDecisionRequired,
        WorkflowStopReason.MissingRequiredInput => ApplicationOutcomeKind.MissingRequiredInput,
        WorkflowStopReason.DirtyInputSurface => ApplicationOutcomeKind.DirtyInputSurface,
        WorkflowStopReason.UnversionedInputSurface => ApplicationOutcomeKind.UnversionedInputSurface,
        WorkflowStopReason.StorageUnusable => ApplicationOutcomeKind.StorageUnusable,
        WorkflowStopReason.MissingRuntimePrerequisite => ApplicationOutcomeKind.MissingRuntimePrerequisite,
        WorkflowStopReason.UnsupportedProviderCapability => ApplicationOutcomeKind.UnsupportedProviderCapability,
        WorkflowStopReason.CompatibilityImportRequired => ApplicationOutcomeKind.CompatibilityImportRequired,
        WorkflowStopReason.ConcurrentStateConflict => ApplicationOutcomeKind.ConcurrentStateConflict,
        WorkflowStopReason.InputInvalidated => ApplicationOutcomeKind.InputInvalidated,
        WorkflowStopReason.NoEligibleTransition => ApplicationOutcomeKind.NoEligibleTransition,
        WorkflowStopReason.Ambiguous => ApplicationOutcomeKind.Ambiguous,
        WorkflowStopReason.Stalled => ApplicationOutcomeKind.Stalled,
        WorkflowStopReason.Cancelled => ApplicationOutcomeKind.Cancelled,
        WorkflowStopReason.Failed => ApplicationOutcomeKind.Failed,
        _ when status?.Observation.StorageVerification.IsUnusable == true =>
            ApplicationOutcomeKind.StorageUnusable,
        _ => exitCode switch
        {
            0 => ApplicationOutcomeKind.Completed,
            3 => ApplicationOutcomeKind.Stalled,
            4 => ApplicationOutcomeKind.SpecificCannotProceed,
            130 => ApplicationOutcomeKind.Cancelled,
            _ => ApplicationOutcomeKind.Failed,
        },
    };
}

internal sealed class UnifiedCliRunner(
    LoopRelay.Application.Contracts.ILoopRelayApplication _application,
    TextWriter _output,
    TextWriter _error)
{
    internal UnifiedCliRunner(
        LoopRelayCompositionRoot composition,
        TextWriter output,
        TextWriter error)
        : this(new LoopRelayApplication(new CanonicalCliApplicationService(composition)), output, error)
    {
    }

    public static int ExitCodeFor(WorkflowStopReason stopReason) =>
        CanonicalCliApplicationService.ExitCodeFor(stopReason);

    public async Task<int> RunAsync(
        LoopRelayRequest request,
        CancellationToken cancellationToken)
    {
        LoopRelayResult result = await _application.ExecuteAsync(
            request,
            cancellationToken);
        RenderedCliResult rendered = CliResultRenderer.Render(result);
        foreach (string line in rendered.Output) _output.WriteLine(line);
        foreach (string line in rendered.Errors) _error.WriteLine(line);

        return result.SuggestedExitCode;
    }
}
