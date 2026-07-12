using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Decisions.Recovery;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Primitives;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Primitives;

namespace LoopRelay.Cli.Services.Decisions;

/// <summary>
/// The decision-making codex session, routed by the SessionRouter. Mirrors RepositoryOrchestrator's
/// RunDecisionAsync + the auto-submit half of BeginSubmitDecisionsAsync (a fully-automated CLI submits
/// the agent's proposal verbatim). Owns ONE warm read-only process reused across iterations. A FRESH process
/// (first pass, or the recycle after a Transfer) is primed with the operational context inline in its first
/// proposal turn — there is NO separate seed turn (the legacy StartDecisionSession* overseer seed, which framed
/// the agent as an executor "waiting for the first session report", is not used in this loop). It proposes the
/// execution agent's system prompt — GenerateSystemPromptForFirstExecutionAgent on the first pass (no handoff
/// yet), else GenerateSystemPromptForNextExecutionAgent(latestHandoff) (a post-Transfer process still has a
/// handoff, so it is a NEXT proposal, not a first) — persists the proposal to decisions.{N:0000}.md AND
/// canonical decisions.md; verifies decisions.md exists. Across CLI runs, the warm process is resumable: the
/// codex thread id + router accounting persist to the runtime SQLite database after every successful proposal
/// (see OpenOrResumeSessionAsync).
/// </summary>
internal sealed class DecisionSession(
    IAgentRuntime _runtime,
    IDecisionSessionRouter _router,
    LoopArtifacts _artifacts,
    ILoopConsole _console,
    Repository _repository,
    BrainConfiguration _brainConfiguration,
    IDecisionCostModel? _costModel = null,
    IDecisionSessionResumeStore? _resumeStore = null,
    IProjectContextProjectionService? _projectionService = null,
    bool _resumeEnabled = true,
    string? _promptPolicy = null,
    ExplicitHitlNonImplementationRequestCaptureService? _hitlRequestCapture = null,
    IAgentSessionContinuityRuntime? _continuityRuntime = null,
    SessionContinuityProfile? _continuityProfile = null,
    IRecoveryStore? _recoveryStore = null,
    IRecoveryRuntime? _recoveryRuntime = null,
    string _recoveryPolicyVersion = "decision-recovery-resume-only.v1",
    int _operationalContextGrowthStreakWarningThreshold =
        OperationalPolicyResolver.DefaultOperationalContextGrowthWarningStreak,
    IDecisionPromptTurnDispatcher? _promptDispatcher = null) : IAsyncDisposable
{
    private const string TemplateOwnedPromptPolicy = "template-owned-implementation-first.v1";
    private IAgentSession? session;
    private bool seeded;
    private bool resumeAttempted;
    private DecisionExecutionContext? decisionExecutionContext;
    private DecisionSessionActiveState? durableActiveState;
    private DecisionSessionLineageNode? durableLineage;
    private SessionContinuityProfile? durableProfile;

    // Operational-context size-health state (Stage 2, mirrors RepositoryOrchestrator). Single-threaded, no lock.
    private int? previousOperationalContextSize;
    private int operationalContextGrowthStreak;

    // Cost-aware routing accounting (mirrors RepositoryOrchestrator). PER-PROCESS fields reset on recycle;
    // transferCost persists across recycles. Single-threaded — RunAsync is called sequentially — so no lock.
    private int occupancyTokens;            // O
    private double reuseCost;               // R
    private int reuseCycles;                // n
    private double lastCycleCost;           // e_last
    private double prevCycleCost;           // e_prev
    private double transferCost = 250_000d; // C: seed -> measured -> running average
    private int transferCount;

    public Task RunAsync(CancellationToken cancellationToken) => RunAsync(context: null, cancellationToken);

    public async Task RunAsync(DecisionExecutionContext? context, CancellationToken cancellationToken)
    {
        if (context is not null)
        {
            if (decisionExecutionContext is not null
                && decisionExecutionContext.Scope.ScopeId != context.Scope.ScopeId)
            {
                throw new LoopStepException("A warm DecisionSession cannot cross an Execute scope boundary.");
            }

            decisionExecutionContext = context;
            _console.Info(
                $"Decision continuity scope {context.Scope.ScopeId} " +
                $"(workspace {context.Scope.WorkspaceId}, run {context.PromptExecution.RunId}, " +
                $"{context.PromptExecution.Workflow}/{context.PromptExecution.Stage}/{context.PromptExecution.Transition}, " +
                $"invocation {context.PromptExecution.RootInvocation.Mode}).");

            if (_recoveryStore is not null && await TryRehydrateCommittedDecisionAsync(context, cancellationToken))
            {
                return;
            }
        }

        DecisionRoute route = _router.Evaluate(BuildRouterInputs());
        // Eligibility downgrade: a Transfer needs a primed warm process to extract a delta from.
        if (route == DecisionRoute.Transfer && !seeded)
        {
            route = DecisionRoute.Continue;
        }

        _console.Phase($"Decision (route={route})");

        if (route == DecisionRoute.Transfer)
        {
            await TransferAsync(cancellationToken);
        }

        session ??= await OpenOrResumeSessionAsync(cancellationToken);

        (string? handoff, string? handoffPath) = await _artifacts.ReadLatestHandoffAsync();
        ProposalPrompt proposal = await BuildProposalPromptAsync(handoff, handoffPath, cancellationToken);

        // Own phase header so post-transfer proposal output no longer prints under the last
        // "Decision: Transfer/…" header.
        _console.Phase("Decision: Propose");
        var proposalRenderer = new ConsoleTurnRenderer(_console);
        DurableTurnProgressObserver? durableTurn = await BeginDurableTurnAsync(cancellationToken);
        DecisionPromptTurnResult proposedTurn;
        try
        {
            using IDisposable progressScope = AgentTurnProgress.Use(durableTurn);
            proposedTurn = await DispatchPromptAsync(
                session,
                proposal.PromptIdentity,
                proposal.TemplateSourceHash,
                proposal.Prompt,
                proposal.ConsumedInputs,
                proposalRenderer.Stream,
                cancellationToken);
        }
        catch
        {
            durableTurn?.MarkUnknown();
            throw;
        }
        AgentTurnResult proposed = proposedTurn.Result;

        if (proposed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException(WithDiagnostics(
                $"Decision turn ended in state {proposed.State}.", proposed.Diagnostics));
        }

        // Any completed proposal supersedes the previous live recommendation. Invalidating first ensures an
        // empty proposal cannot leave the previous pair launchable after this transition has failed.
        await _artifacts.InvalidateExecutionRecommendationProjectionAsync();

        if (string.IsNullOrWhiteSpace(proposed.Output))
        {
            await CloseAsync();
            throw new LoopStepException("Decision turn returned no execution system prompt.");
        }

        // The process now holds the operational context (delivered inline in this turn), so subsequent proposals
        // on it are cheap handoff-only deltas.
        seeded = true;
        proposalRenderer.EchoIfSilent(proposed.Output);

        _console.Phase("Decision: Recommend execution configuration");
        var recommendationRenderer = new ConsoleTurnRenderer(_console);
        DecisionPromptTurnResult recommendedTurn = await DispatchPromptAsync(
            session,
            "ExecutionRecommendation",
            templateSourceHash: null,
            ExecutionRecommendationContract.RenderPrompt(proposed.Output),
            [],
            recommendationRenderer.Stream,
            cancellationToken);
        AgentTurnResult recommended = recommendedTurn.Result;
        if (recommended.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException(WithDiagnostics(
                $"Execution recommendation turn ended in state {recommended.State}.",
                recommended.Diagnostics));
        }

        recommendationRenderer.EchoIfSilent(recommended.Output);
        ExecutionRecommendation recommendation;
        try
        {
            recommendation = ExecutionRecommendationContract.ParseAgentOutput(recommended.Output);
        }
        catch (InvalidDataException exception)
        {
            await CloseAsync();
            throw new LoopStepException($"Execution recommendation was invalid: {exception.Message}", exception);
        }

        RecordProposalCost(proposed.Usage.Add(recommended.Usage));
        if (durableTurn is not null)
        {
            durableTurn.RecordTerminalResult(proposed);
            await CommitDurableDecisionOutputAsync(
                durableTurn.Record,
                proposed.Output,
                recommendation,
                recommendedTurn,
                cancellationToken);
        }
        else
        {
            await PersistResumeStateAsync(cancellationToken);
            // Auto-submit: persist the exact prompt and its correlated, validated recommendation as the live pair.
            await _artifacts.PersistDecisionsAsync(
                proposed.Output,
                recommendation,
                recommendedTurn.Causality,
                recommendedTurn.Session,
                recommendedTurn.Turn,
                "Decision agent advisory model/effort recommendation.",
                new HistoryEvidenceAttachments(
                    Provider: new HistoryProviderEvidence(
                        "codex",
                        recommendedTurn.Session.Value,
                        recommended.ProviderTurnId ?? recommendedTurn.Turn.Value)),
                cancellationToken);
        }
        if (_hitlRequestCapture is not null)
        {
            await _hitlRequestCapture.CaptureFromSourceAsync(OrchestrationArtifactPaths.Decisions, proposed.Output);
        }

        if (!await _artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
        {
            throw new LoopStepException(".agents/decisions/decisions.md was not written.");
        }

        _console.Info("New decisions.md verified.");
    }

    // decisions.md IS the execution agent's system prompt. The first pass (no prior handoff) authors the FIRST
    // agent's system prompt; any pass with a handoff (including the first proposal on a post-Transfer process)
    // authors the NEXT agent's, folding in the previous session's handoff. A FRESH process is also primed with
    // the operational context in this same turn (there is no separate seed) — a WARM process already carries it
    // from its first proposal, so its later proposals send only the handoff delta.
    // One composed proposal prompt plus the identity evidence needed to append its
    // rendered-prompt fact: the generated template it came from (whose build-time source hash is
    // the policy-complete prompt version) and the collaboration files whose content fed the holes.
    private sealed record ProposalPrompt(
        string Prompt,
        string PromptIdentity,
        string TemplateSourceHash,
        IReadOnlyList<ConsumedInputFile> ConsumedInputs);

    private async Task<ProposalPrompt> BuildProposalPromptAsync(
        string? handoff,
        string? handoffPath,
        CancellationToken cancellationToken)
    {
        // Warm process: the projection and operational context are already in this process's history,
        // so both declared template holes render empty and the turn carries only the handoff delta.
        string decisionSessionProjection = seeded
            ? string.Empty
            : (await EnsureDecisionProjectionAsync(cancellationToken)).Content;
        string operationalContext = string.Empty;
        List<ConsumedInputFile> consumedInputs = [];
        if (!seeded)
        {
            await _artifacts.EnsureOperationalContextAsync();
            operationalContext = await _artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;
            consumedInputs.Add(ConsumedInputFile.FromContent(OrchestrationArtifactPaths.OperationalContext, operationalContext));

            if (string.IsNullOrWhiteSpace(operationalContext))
            {
                _console.Warn("Operational context is empty — the decision agent has no context to work from.");
            }
        }

        if (handoff is not null)
        {
            consumedInputs.Add(ConsumedInputFile.FromContent(handoffPath ?? OrchestrationArtifactPaths.LiveHandoff, handoff));
        }

        return handoff is null
            ? new ProposalPrompt(
                GenerateSystemPromptForFirstExecutionAgent.Render(operationalContext, decisionSessionProjection),
                "GenerateSystemPromptForFirstExecutionAgent",
                GenerateSystemPromptForFirstExecutionAgent.SourceHash,
                consumedInputs)
            : new ProposalPrompt(
                GenerateSystemPromptForNextExecutionAgent.Render(operationalContext, decisionSessionProjection, handoff),
                "GenerateSystemPromptForNextExecutionAgent",
                GenerateSystemPromptForNextExecutionAgent.SourceHash,
                consumedInputs);
    }

    private Task<DecisionPromptTurnResult> DispatchPromptAsync(
        IAgentSession targetSession,
        string promptIdentity,
        string? templateSourceHash,
        string renderedText,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        Func<AgentStreamChunk, Task>? onChunk,
        CancellationToken cancellationToken)
    {
        IDecisionPromptTurnDispatcher dispatcher = _promptDispatcher
            ?? throw new LoopStepException(
                "Decision prompt dispatch is not configured; provider dispatch is blocked before submission.");
        return dispatcher.DispatchAsync(
            targetSession,
            promptIdentity,
            templateSourceHash,
            renderedText,
            consumedInputs,
            onChunk,
            cancellationToken);
    }

    /// <summary>
    /// The FIRST open of this CLI process attempts to resume the persisted decision session (if any); every
    /// later open — the post-Transfer recycle, the reopen after a failed turn — starts fresh, because the
    /// persisted state describes a thread this process has already moved past. Restored accounting is applied
    /// only HERE, at a successful resume: the router's route evaluation runs before the open, so the first
    /// route of a run always sees pre-restore (zeroed) inputs and the existing !seeded downgrade guards it.
    /// </summary>
    private async Task<IAgentSession> OpenOrResumeSessionAsync(CancellationToken cancellationToken)
    {
        bool firstOpen = !resumeAttempted;
        resumeAttempted = true;

        if (_recoveryStore is not null && decisionExecutionContext is not null)
        {
            return await OpenOrResumeDurableSessionAsync(firstOpen, cancellationToken);
        }

        DecisionSessionResumeState? state = firstOpen && _resumeEnabled
            ? await (_resumeStore ?? new NullDecisionSessionResumeStore()).ReadAsync(cancellationToken)
            : null;
        if (state is not null && _projectionService is not null)
        {
            ProjectionFreshness freshness = await EvaluateDecisionProjectionFreshnessAsync(cancellationToken);
            if (!freshness.IsFresh)
            {
                _console.Warn(
                    "Decision session projection is stale or missing; clearing persisted decision session and starting fresh.");
                await (_resumeStore ?? new NullDecisionSessionResumeStore()).ClearAsync(cancellationToken);
                state = null;
            }
        }

        if (state is null)
        {
            return await _runtime.OpenSessionAsync(
                AgentSpecs.Decision(_repository, _brainConfiguration),
                cancellationToken);
        }

        IAgentSessionContinuityRuntime continuityRuntime = _continuityRuntime
            ?? _runtime as IAgentSessionContinuityRuntime
            ?? throw new LoopStepException(
                "Decision-session continuity is not available; the active thread was preserved and no replacement was started.");
        SessionContinuityProfile profile = _continuityProfile
            ?? (await continuityRuntime.NegotiateAsync(
                ProductionNegotiationRequest(), cancellationToken)).Profile;

        SessionResumeResult resume = await continuityRuntime.ResumeSessionAsync(
            new SessionResumeRequest(
                AgentSpecs.Decision(_repository, _brainConfiguration, state.ThreadId),
                new ProviderSessionReference("codex", state.ThreadId),
                profile),
            cancellationToken);
        if (resume.Outcome == SessionResumeOutcome.SuccessfulResume && resume.Session is { } resumed)
        {
            // The resumed thread already holds the operational context (its first proposal primed it), and
            // the router accounting it accrued — restore both so priming and transfer economics continue
            // where the previous run left off.
            seeded = true;
            occupancyTokens = state.OccupancyTokens;
            reuseCost = state.ReuseCost;
            reuseCycles = state.ReuseCycles;
            lastCycleCost = state.LastCycleCost;
            prevCycleCost = state.PrevCycleCost;
            transferCost = state.TransferCost;
            transferCount = state.TransferCount;
            previousOperationalContextSize = state.PreviousOperationalContextSize;
            operationalContextGrowthStreak = state.OperationalContextGrowthStreak;
            _console.Info($"Resumed decision session (thread {state.ThreadId}).");
            return resumed;
        }

        string failure = resume.Outcome == SessionResumeOutcome.DeterministicProtocolFailure
            ? "Decision-session resume requires a protocol repair"
            : $"Decision-session resume stopped with {resume.Outcome}";
        _console.Warn($"{failure} (thread {state.ThreadId}); the active thread was preserved and no replacement was started.");
        throw new LoopStepException($"{failure}. The active thread was preserved; no replacement was started.");
    }

    private async Task<IAgentSession> OpenOrResumeDurableSessionAsync(
        bool firstOpen,
        CancellationToken cancellationToken)
    {
        DecisionSessionScope scope = decisionExecutionContext!.Scope;
        ActiveStateReadResult read = await _recoveryStore!.ReadActiveAsync(scope.ScopeId.Value, cancellationToken);
        if (read.Status is ActiveStateReadStatus.Corrupt or ActiveStateReadStatus.Conflict)
        {
            throw new LoopStepException(
                $"Decision-session active state is {read.Status}: {read.Diagnostic}. No provider operation was started.");
        }

        if (!firstOpen || read.Status == ActiveStateReadStatus.Absent)
        {
            if (!firstOpen)
            {
                return await CreatePlannedTransferSuccessorAsync(cancellationToken);
            }

            return await CreateAndActivateFreshDurableSessionAsync(scope, cancellationToken);
        }

        if (!_resumeEnabled)
        {
            throw new LoopStepException(
                "ContinuityDisabled: decision continuity is disabled while an active session exists; the active thread was preserved and no replacement was started.");
        }

        durableActiveState = read.Active!;
        durableLineage = read.Lineage!;
        IAgentSessionContinuityRuntime continuityRuntime = _continuityRuntime
            ?? _runtime as IAgentSessionContinuityRuntime
            ?? throw new LoopStepException("Decision-session continuity runtime is unavailable.");
        durableProfile = _continuityProfile
            ?? (await continuityRuntime.NegotiateAsync(ProductionNegotiationRequest(), cancellationToken)).Profile;
        if (!string.Equals(durableLineage.ProfileDigest, durableProfile.Digest, StringComparison.Ordinal))
        {
            throw new LoopStepException(
                "The active decision session continuity profile does not match the installed provider profile; no provider operation was started.");
        }

        if (_recoveryRuntime is not null)
        {
            int contextBudget = durableProfile.MaximumRecoverableContext is { } maximum
                ? Math.Max(0, (int)(maximum * 0.80) - 8_000)
                : 0;
            var coordinator = new DecisionSessionRecoveryCoordinator(_recoveryRuntime, _recoveryStore);
            DecisionSessionRecoveryResult coordinated;
            try
            {
                coordinated = await coordinator.OpenAsync(
                    scope.ScopeId.Value,
                    decisionExecutionContext.PromptExecution.RunId,
                    AgentSpecs.Decision(_repository, _brainConfiguration, durableLineage.ProviderSessionId),
                    AgentSpecs.Decision(_repository, _brainConfiguration),
                    durableProfile,
                    new Dictionary<string, string>
                    {
                        ["policy-version"] = _recoveryPolicyVersion,
                        ["rank:ThreadReadReconstruction@1"] = "100",
                        ["rank:RolloutReconstruction@1"] = "200",
                        ["rank:RepositoryReconstruction@1"] = "300",
                    },
                    contextBudget,
                    cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                throw new LoopStepException(exception.Message, exception);
            }

            durableActiveState = coordinated.Active;
            durableLineage = coordinated.Lineage;
            RestoreAccounting(durableActiveState.Accounting);
            seeded = coordinated.Seeded;
            _console.Info(ContinuityMessage(coordinated.Recovery, durableLineage.ProviderSessionId));
            return coordinated.Session;
        }

        SessionResumeResult resume = await continuityRuntime.ResumeSessionAsync(
            new SessionResumeRequest(
                AgentSpecs.Decision(_repository, _brainConfiguration, durableLineage.ProviderSessionId),
                new ProviderSessionReference(durableLineage.Provider, durableLineage.ProviderSessionId),
                durableProfile),
            cancellationToken);
        if (resume.Outcome != SessionResumeOutcome.SuccessfulResume || resume.Session is null)
        {
            string failure = resume.Outcome == SessionResumeOutcome.DeterministicProtocolFailure
                ? "Decision-session resume requires a protocol repair"
                : $"Decision-session resume stopped with {resume.Outcome}";
            _console.Warn($"{failure}; the active thread was preserved and no replacement was started.");
            throw new LoopStepException($"{failure}. The active thread was preserved; no replacement was started.");
        }

        RestoreAccounting(durableActiveState.Accounting);
        seeded = true;
        _console.Info($"Resumed decision session (thread {durableLineage.ProviderSessionId}).");
        return resume.Session;
    }

    private async Task<IAgentSession> CreateAndActivateFreshDurableSessionAsync(
        DecisionSessionScope scope,
        CancellationToken cancellationToken)
    {
        IAgentSessionContinuityRuntime continuityRuntime = _continuityRuntime
            ?? _runtime as IAgentSessionContinuityRuntime
            ?? throw new LoopStepException("Decision-session continuity runtime is unavailable for durable creation.");
        durableProfile = _continuityProfile
            ?? (await continuityRuntime.NegotiateAsync(ProductionNegotiationRequest(), cancellationToken)).Profile;
        SessionCreateResult create = await continuityRuntime.CreateSessionAsync(
            new SessionCreateRequest(
                AgentSpecs.Decision(_repository, _brainConfiguration),
                durableProfile,
                $"fresh:{scope.ScopeId.Value}"),
            cancellationToken);
        if (!create.Succeeded || create.Session is null || create.Created is null)
        {
            throw new LoopStepException(
                $"Failed to eagerly create the durable decision session ({create.Failure?.Classification ?? "Unknown"}).");
        }

        string lineageId = Guid.NewGuid().ToString("N");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var scopeRecord = new DecisionSessionScopeRecord(
            scope.ScopeId.Value, scope.WorkspaceId, scope.PreparedEpic.CausalIdentity,
            scope.ExecutablePlan.CausalIdentity, "Decision", scope.ContractVersion, "Active", now, null);
        durableLineage = new DecisionSessionLineageNode(
            lineageId, scope.ScopeId.Value, create.Created.Provider, create.Created.ThreadId, null, lineageId,
            "Fresh", RecoveryCompleteness.Full, null, durableProfile.Digest, null, now, now, null, "Authoritative");
        durableActiveState = new DecisionSessionActiveState(
            scope.ScopeId.Value, lineageId, CurrentAccounting(),
            Hash(_promptPolicy ?? TemplateOwnedPromptPolicy),
            null, 0, now);
        RecoveryStoreWriteResult persisted = await _recoveryStore!.CreateScopeAndActivateAsync(
            scopeRecord, durableLineage, durableActiveState, durableProfile, cancellationToken);
        if (!persisted.Succeeded)
        {
            await _runtime.CloseSessionAsync(create.Session);
            throw new LoopStepException($"Failed to activate the fresh decision session: {persisted.Diagnostic}");
        }

        return create.Session;
    }

    private async Task<IAgentSession> CreatePlannedTransferSuccessorAsync(
        CancellationToken cancellationToken)
    {
        if (durableActiveState is null || durableLineage is null || durableProfile is null)
        {
            throw new LoopStepException(
                "A planned transfer requires the original durable active pointer, lineage, and continuity profile.");
        }

        IAgentSessionContinuityRuntime continuityRuntime = _continuityRuntime
            ?? _runtime as IAgentSessionContinuityRuntime
            ?? throw new LoopStepException("Decision-session continuity runtime is unavailable for planned transfer.");
        SessionCreateResult create = await continuityRuntime.CreateSessionAsync(
            new SessionCreateRequest(
                AgentSpecs.Decision(_repository, _brainConfiguration),
                durableProfile,
                $"planned-transfer:{durableActiveState.ScopeId}:{durableActiveState.RowVersion}"),
            cancellationToken);
        if (!create.Succeeded || create.Session is null || create.Created is null)
        {
            throw new LoopStepException(
                $"Planned transfer replacement creation failed ({create.Failure?.Classification ?? "Unknown"}); " +
                "the original active lineage was preserved.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var successor = new DecisionSessionLineageNode(
            Guid.NewGuid().ToString("N"),
            durableActiveState.ScopeId,
            create.Created.Provider,
            create.Created.ThreadId,
            durableActiveState.LineageId,
            durableLineage.RootLineageId,
            "PlannedTransfer",
            RecoveryCompleteness.Full,
            null,
            durableProfile.Digest,
            null,
            now,
            null,
            null,
            "Inactive");
        RecoveryStoreWriteResult recorded = await _recoveryStore!.RecordPlannedSuccessorAsync(
            durableActiveState, successor, cancellationToken);
        if (!recorded.Succeeded)
        {
            await _runtime.CloseSessionAsync(create.Session);
            throw new LoopStepException(
                $"Planned transfer successor was not persisted: {recorded.Diagnostic}. The original remains active.");
        }

        durableLineage = successor;
        return create.Session;
    }

    private async Task<bool> TryRehydrateCommittedDecisionAsync(
        DecisionExecutionContext context,
        CancellationToken cancellationToken)
    {
        DecisionSessionTurnRecord? turn = await _recoveryStore!.ReadDecisionTurnAsync(
            context.PromptExecution.RunId,
            context.PromptExecution.InputSnapshotHash,
            cancellationToken);
        if (turn is null)
        {
            return false;
        }

        if ((turn.State is DecisionTurnState.Committed or DecisionTurnState.Materialized)
            && turn.OutputBody is { } output)
        {
            await _artifacts.WriteAsync(OrchestrationArtifactPaths.Decisions, output);
            if (turn.HistorySequence is { } sequence)
            {
                await _artifacts.WriteAsync($".agents/decisions/decisions.{sequence:0000}.md", output);
            }
            if (turn.State == DecisionTurnState.Committed)
            {
                RecoveryStoreWriteResult materialized = await _recoveryStore.MarkDecisionArtifactMaterializedAsync(
                    turn, cancellationToken);
                if (!materialized.Succeeded)
                {
                    throw new LoopStepException($"Failed to repair committed decisions.md: {materialized.Diagnostic}");
                }
            }

            _console.Info("Rehydrated committed decision output; no provider turn was submitted.");
            return true;
        }

        if (turn.State == DecisionTurnState.Pending && !turn.WriteStarted)
        {
            return false;
        }

        throw new LoopStepException(
            $"Decision turn {turn.TurnRecordId} is {turn.State}; its provider outcome must be reconciled before another turn can start.");
    }

    private async Task<DurableTurnProgressObserver?> BeginDurableTurnAsync(CancellationToken cancellationToken)
    {
        if (_recoveryStore is null || decisionExecutionContext is null || durableActiveState is null || durableLineage is null)
        {
            return null;
        }

        PromptExecutionRequest prompt = decisionExecutionContext.PromptExecution;
        DecisionSessionTurnRecord? existing = await _recoveryStore.ReadDecisionTurnAsync(
            prompt.RunId, prompt.InputSnapshotHash, cancellationToken);
        DecisionSessionTurnRecord turn;
        if (existing is null)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            turn = new DecisionSessionTurnRecord(
                Guid.NewGuid().ToString("N"), durableActiveState.ScopeId, durableLineage.LineageId,
                prompt.RunId, prompt.InputSnapshotHash, durableLineage.ProviderSessionId,
                null, null, DecisionTurnState.Pending, false, false, false, false,
                null, null, null, null, false, null, 0, now, now);
            RecoveryStoreWriteResult begun = await _recoveryStore.BeginDecisionTurnAsync(turn, cancellationToken);
            if (!begun.Succeeded)
            {
                throw new LoopStepException($"Failed to persist decision turn intent: {begun.Diagnostic}");
            }
        }
        else if (existing.State == DecisionTurnState.Pending && !existing.WriteStarted)
        {
            turn = existing;
        }
        else
        {
            throw new LoopStepException(
                $"Decision turn {existing.TurnRecordId} is {existing.State}; duplicate submission is blocked.");
        }

        return new DurableTurnProgressObserver(_recoveryStore, turn);
    }

    private async Task CommitDurableDecisionOutputAsync(
        DecisionSessionTurnRecord turn,
        string output,
        ExecutionRecommendation recommendation,
        DecisionPromptTurnResult recommendationTurn,
        CancellationToken cancellationToken)
    {
        DecisionTurnCommitResult commit = await _recoveryStore!.CommitDecisionOutputAsync(
            turn,
            durableActiveState!,
            CurrentAccounting(),
            output,
            Hash(_promptPolicy ?? TemplateOwnedPromptPolicy),
            cancellationToken);
        if (!commit.Write.Succeeded || commit.Turn is null || commit.Active is null)
        {
            throw new LoopStepException($"Failed to atomically commit decision output: {commit.Write.Diagnostic}");
        }

        durableActiveState = commit.Active;
        await _artifacts.WriteAsync(OrchestrationArtifactPaths.Decisions, output);
        if (commit.HistoryRelativePath is { } historyPath)
        {
            await _artifacts.WriteAsync(historyPath, output);
        }
        await _artifacts.PersistRecommendationEvidenceAsync(
            new DecisionProductVersionIdentity(commit.Turn.TurnRecordId),
            output,
            recommendation,
            recommendationTurn.Causality,
            recommendationTurn.Session,
            recommendationTurn.Turn,
            "Decision agent advisory model/effort recommendation.",
            cancellationToken);
        RecoveryStoreWriteResult materialized = await _recoveryStore.MarkDecisionArtifactMaterializedAsync(
            commit.Turn, cancellationToken);
        if (!materialized.Succeeded)
        {
            throw new LoopStepException($"Decision output committed but live artifact materialization was not recorded: {materialized.Diagnostic}");
        }
    }

    private static SessionContinuityNegotiationRequest ProductionNegotiationRequest()
    {
        CodexInstalledCompatibilityIdentity identity = CodexCompatibilityIdentityProbe.Resolve();
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("{}");
        return new SessionContinuityNegotiationRequest(
            "codex",
            CodexAppServerProtocol.ClientVersion,
            Environment.GetEnvironmentVariable("LOOPRELAY_CODEX_VERSION") ?? identity.ServerVersion,
            identity.ExecutableIdentity,
            "app-server-v2",
            Environment.GetEnvironmentVariable("LOOPRELAY_CODEX_SCHEMA_DIGEST") ?? identity.SchemaDigest,
            document.RootElement.Clone(),
            OfferExperimentalApi: true);
    }

    /// <summary>
    /// The state is only ever written after a SUCCESSFUL proposal turn, so its existence implies the thread is
    /// primed (no seeded field in the schema). One small SQLite upsert per decision step; the store is fail-open.
    /// </summary>
    private async Task PersistResumeStateAsync(CancellationToken cancellationToken)
    {
        if (session?.ThreadId is not { Length: > 0 } threadId)
        {
            return; // no codex thread id (legacy/one-shot shapes) — nothing a later run could resume
        }

        if (_recoveryStore is not null && decisionExecutionContext is not null)
        {
            await PersistDurableActiveStateAsync(threadId, cancellationToken);
            return;
        }

        await (_resumeStore ?? new NullDecisionSessionResumeStore()).WriteAsync(new DecisionSessionResumeState(
            threadId, occupancyTokens, reuseCost, reuseCycles, lastCycleCost, prevCycleCost,
            transferCost, transferCount, previousOperationalContextSize, operationalContextGrowthStreak),
            cancellationToken);
    }

    private async Task PersistDurableActiveStateAsync(string threadId, CancellationToken cancellationToken)
    {
        DecisionSessionScope scope = decisionExecutionContext!.Scope;
        DecisionSessionAccounting accounting = CurrentAccounting();
        string policyDigest = Hash(_promptPolicy ?? TemplateOwnedPromptPolicy);
        if (durableActiveState is null)
        {
            IAgentSessionContinuityRuntime continuityRuntime = _continuityRuntime
                ?? _runtime as IAgentSessionContinuityRuntime
                ?? throw new LoopStepException("Decision-session continuity runtime is unavailable for durable activation.");
            durableProfile ??= _continuityProfile
                ?? (await continuityRuntime.NegotiateAsync(ProductionNegotiationRequest(), cancellationToken)).Profile;
            string lineageId = Guid.NewGuid().ToString("N");
            var scopeRecord = new DecisionSessionScopeRecord(
                scope.ScopeId.Value,
                scope.WorkspaceId,
                scope.PreparedEpic.CausalIdentity,
                scope.ExecutablePlan.CausalIdentity,
                "Decision",
                scope.ContractVersion,
                "Active",
                DateTimeOffset.UtcNow,
                null);
            durableLineage = new DecisionSessionLineageNode(
                lineageId,
                scope.ScopeId.Value,
                durableProfile.Provider,
                threadId,
                null,
                lineageId,
                "Fresh",
                RecoveryCompleteness.Full,
                null,
                durableProfile.Digest,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                "Authoritative");
            durableActiveState = new DecisionSessionActiveState(
                scope.ScopeId.Value,
                lineageId,
                accounting,
                policyDigest,
                null,
                0,
                DateTimeOffset.UtcNow);
            RecoveryStoreWriteResult created = await _recoveryStore!.CreateScopeAndActivateAsync(
                scopeRecord, durableLineage, durableActiveState, durableProfile, cancellationToken);
            if (!created.Succeeded)
            {
                throw new LoopStepException($"Failed to durably activate the fresh decision session: {created.Diagnostic}");
            }

            return;
        }

        DecisionSessionActiveState updated = durableActiveState with
        {
            Accounting = accounting,
            PolicyDigest = policyDigest,
            RowVersion = durableActiveState.RowVersion + 1,
        };
        RecoveryStoreWriteResult result = await _recoveryStore!.UpdateActiveAccountingAsync(
            durableActiveState, updated, cancellationToken);
        if (!result.Succeeded)
        {
            throw new LoopStepException($"Failed to commit decision session accounting: {result.Diagnostic}");
        }

        durableActiveState = updated;
    }

    private DecisionSessionAccounting CurrentAccounting() => new(
        occupancyTokens,
        reuseCost,
        reuseCycles,
        lastCycleCost,
        prevCycleCost,
        transferCost,
        transferCount,
        previousOperationalContextSize,
        operationalContextGrowthStreak);

    private void RestoreAccounting(DecisionSessionAccounting accounting)
    {
        occupancyTokens = accounting.OccupancyTokens;
        reuseCost = accounting.ReuseCost;
        reuseCycles = accounting.ReuseCycles;
        lastCycleCost = accounting.LastCycleCost;
        prevCycleCost = accounting.PreviousCycleCost;
        transferCost = accounting.TransferCost;
        transferCount = accounting.TransferCount;
        previousOperationalContextSize = accounting.PreviousContextSize;
        operationalContextGrowthStreak = accounting.ContextGrowthStreak;
    }

    /// <summary>
    /// Transfer recycle, mirroring RepositoryOrchestrator.PrepareTransferAsync: extract an operational delta
    /// from the warm process, close it, rewrite operational_context.md via a scoped artifact operation, then
    /// optimize the operational documents (plan/details/context) via a second scoped artifact operation (CLI-only;
    /// the backend transfer does not run the optimization — see technical-debt.md). The fresh process is NOT
    /// reseeded here — RunAsync reopens it and its next proposal primes it with the just-rewritten context
    /// inline (no legacy StartDecisionSession* turn), so this leaves the process closed.
    /// </summary>
    private async Task TransferAsync(CancellationToken cancellationToken)
    {
        _console.Phase("Decision: Transfer/ProduceOperationalDelta");
        var deltaRenderer = new ConsoleTurnRenderer(_console);
        DecisionPromptTurnResult deltaTurn = await DispatchPromptAsync(
            session!,
            "ProduceOperationalDelta",
            ProduceOperationalDelta.SourceHash,
            ProduceOperationalDelta.Text,
            [],
            deltaRenderer.Stream,
            cancellationToken);
        AgentTurnResult delta = deltaTurn.Result;
        if (delta.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException(WithDiagnostics(
                $"Operational-delta turn ended in state {delta.State}.", delta.Diagnostics));
        }

        deltaRenderer.EchoIfSilent(delta.Output);

        await _artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, delta.Output);

        // Close the old process (resets seeded + token pressure).
        await CloseAsync();

        _console.Phase("Decision: Transfer/UpdateOperationalContext");
        AgentTurnResult update = await EvolveOperationalContextAsync(delta.Output, cancellationToken);

        _console.Phase("Decision: Transfer/OptimizeOperationalDocuments");
        AgentTurnResult optimize = await OptimizeOperationalDocumentsAsync(cancellationToken);

        // Archive the consumed operational delta now that operational_context.md is successfully updated: rotate
        // .agents/operational_delta.md into a numbered .agents/deltas/ copy and remove the live file. Hard step —
        // a missing delta or a failed rotation fails the transfer (the old process is already closed above; no
        // session is open to tear down here).
        _console.Phase("Decision: Transfer/ArchiveOperationalDelta");
        string? archived;
        try
        {
            archived = await _artifacts.RotateOperationalDeltaAsync(
                deltaTurn.Causality);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new LoopStepException("Transfer failed to archive operational_delta.md.", exception);
        }

        if (archived is null)
        {
            throw new LoopStepException("Transfer produced no operational_delta.md to archive.");
        }

        // The fresh process is left CLOSED (seeded stays false): RunAsync reopens it and its next proposal primes
        // it with the just-rewritten operational context inline (see BuildProposalPromptAsync). No reseed turn.

        // Cost-aware accounting: record the MEASURED transfer cost (delta + evolution + optimization) so the
        // router's transfer-cost estimate (C) self-calibrates off reality. CloseAsync above reset the per-process
        // reuse accounting; the fresh process's context-priming cost is captured by the next proposal's
        // RecordProposalCost. transferCost persists across recycles.
        RecordTransferCost(
            (_costModel ?? new EffectiveTokenCostModel()).Measure(delta.Usage) + (_costModel ?? new EffectiveTokenCostModel()).Measure(update.Usage) + (_costModel ?? new EffectiveTokenCostModel()).Measure(optimize.Usage));
    }

    // Evolves the operational context through a fresh app-server session scoped to the context and delta artifacts.
    // Direct repository writes are wrapped in a rollback transaction so a failed turn/gate preserves inputs.
    private async Task<AgentTurnResult> EvolveOperationalContextAsync(
        string deltaOutput, CancellationToken cancellationToken)
    {
        await _artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, deltaOutput);

        var operation = new DecisionArtifactOperation(
            Label: "operational-context-evolution",
            PromptIdentity: "UpdateOperationalContext",
            TemplateSourceHash: UpdateOperationalContext.SourceHash,
            Prompt: UpdateOperationalContext.Text,
            Profile: new OperationPermissionProfile(
                "operational-context-evolution",
                _repository.Path,
                [OrchestrationArtifactPaths.OperationalContext, OrchestrationArtifactPaths.OperationalDelta],
                [],
                [OrchestrationArtifactPaths.OperationalContext],
                []),
            RequiredOutputs: [OrchestrationArtifactPaths.OperationalContext],
            ChangedGuard: OrchestrationArtifactPaths.OperationalContext);

        return await RunArtifactOperationAsync(
            operation,
            "Transfer left no operational_context.md to seed the next decision session from.",
            "evolution left operational_context.md unchanged — the operational delta was not applied",
            cancellationToken);
    }

    // The operational documents the post-evolution optimization operation is scoped to.
    private static readonly string[] OptimizationDocuments =
    [
        OrchestrationArtifactPaths.Plan,
        OrchestrationArtifactPaths.Details,
        OrchestrationArtifactPaths.OperationalContext,
    ];

    // Immediately after the context evolution, optimize the operational documents in a second scoped app-server
    // session. Optional documents may be touched only when they existed before this operation.
    private async Task<AgentTurnResult> OptimizeOperationalDocumentsAsync(CancellationToken cancellationToken)
    {
        var existingDocuments = new List<string>();
        foreach (string document in OptimizationDocuments)
        {
            if (await _artifacts.ExistsAsync(document))
            {
                existingDocuments.Add(document);
            }
        }

        var operation = new DecisionArtifactOperation(
            Label: "operational-documents-optimization",
            PromptIdentity: "OptimizeOperationalDocuments",
            TemplateSourceHash: OptimizeOperationalDocuments.SourceHash,
            Prompt: OptimizeOperationalDocuments.Text,
            Profile: new OperationPermissionProfile(
                "operational-documents-optimization",
                _repository.Path,
                existingDocuments,
                [],
                existingDocuments,
                []),
            RequiredOutputs: [OrchestrationArtifactPaths.OperationalContext],
            ChangedGuard: null);

        AgentTurnResult optimize = await RunArtifactOperationAsync(
            operation,
            "Optimization left no operational_context.md to seed the next decision session from.",
            unchangedGuardFailure: null,
            cancellationToken);

        string optimizedContext = await _artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;
        RecordOperationalContextHealth(optimizedContext.Length);
        return optimize;
    }

    private async Task<AgentTurnResult> RunArtifactOperationAsync(
        DecisionArtifactOperation operation,
        string missingRequiredMessage,
        string? unchangedGuardFailure,
        CancellationToken cancellationToken)
    {
        string? changedGuardSnapshot = null;
        foreach (string read in operation.Profile.AllowedReads)
        {
            string? content = await _artifacts.ReadAsync(read);
            if (content is null)
            {
                throw new LoopStepException($"{operation.Label}: required input {read} was not found.");
            }

            if (operation.ChangedGuard is not null && string.Equals(read, operation.ChangedGuard, StringComparison.Ordinal))
            {
                changedGuardSnapshot = content;
            }
        }

        ArtifactMutationTransaction transaction =
            await ArtifactMutationTransaction.CaptureAsync(_artifacts.Store, operation.Profile);

        IAgentSession? scopedSession = null;
        bool keepChanges = false;
        try
        {
            var renderer = new ConsoleTurnRenderer(_console);
            scopedSession = await _runtime.OpenSessionAsync(
                AgentSpecs.ScopedArtifactOperation(
                    _repository,
                    _brainConfiguration,
                    operation.Profile),
                cancellationToken);
            AgentTurnResult result = (await DispatchPromptAsync(
                scopedSession,
                operation.PromptIdentity,
                operation.TemplateSourceHash,
                operation.Prompt,
                [],
                renderer.Stream,
                cancellationToken)).Result;
            if (result.State != AgentTurnState.Completed)
            {
                throw new LoopStepException(WithDiagnostics(
                    $"{operation.Label} turn ended in state {result.State}.", result.Diagnostics));
            }

            renderer.EchoIfSilent(result.Output);

            IReadOnlyList<string> deleted = await transaction.DeletedSnapshotFilesAsync();
            if (deleted.Count > 0)
            {
                throw new LoopStepException(
                    $"{operation.Label} deleted declared artifact(s): {string.Join(", ", deleted)}.");
            }

            foreach (string requiredOutput in operation.RequiredOutputs)
            {
                if (!await _artifacts.ExistsAsync(requiredOutput))
                {
                    throw new LoopStepException(missingRequiredMessage);
                }
            }

            if (operation.ChangedGuard is { } changedGuard)
            {
                string changedContent = await _artifacts.ReadAsync(changedGuard) ?? string.Empty;
                if (string.Equals(changedContent, changedGuardSnapshot ?? string.Empty, StringComparison.Ordinal))
                {
                    throw new LoopStepException(unchangedGuardFailure ?? $"{operation.Label} left {changedGuard} unchanged.");
                }
            }

            keepChanges = true;
            return result;
        }
        catch
        {
            if (!keepChanges)
            {
                await transaction.RestoreAsync();
            }

            throw;
        }
        finally
        {
            if (scopedSession is not null)
            {
                await _runtime.CloseSessionAsync(scopedSession);
            }
        }
    }

    private sealed record DecisionArtifactOperation(
        string Label,
        string PromptIdentity,
        string? TemplateSourceHash,
        string Prompt,
        OperationPermissionProfile Profile,
        IReadOnlyList<string> RequiredOutputs,
        string? ChangedGuard);

    private sealed class DurableTurnProgressObserver(
        IRecoveryStore _store,
        DecisionSessionTurnRecord initial) : ICriticalAgentTurnProgressObserver
    {
        public DecisionSessionTurnRecord Record { get; private set; } = initial;

        public void RequestWriteStarted() => Advance(
            DecisionTurnState.WriteStarted,
            writeStarted: true);

        public void RequestSubmitted() => Advance(
            DecisionTurnState.Submitted,
            writeStarted: true,
            submitted: true);

        public void RequestAccepted() => Advance(
            DecisionTurnState.Accepted,
            writeStarted: true,
            submitted: true,
            accepted: true);

        public void FirstProtocolEvent() { }

        public void FirstOutput() { }

        public void ProviderTurnIdentified(string providerTurnId) => Advance(
            Record.State,
            Record.WriteStarted,
            Record.Submitted,
            Record.Accepted,
            Record.Terminal,
            providerTurnId);

        public void Terminal()
        {
            if (Record.State != DecisionTurnState.Terminal)
            {
                Advance(
                    DecisionTurnState.Terminal,
                    Record.WriteStarted,
                    Record.Submitted,
                    Record.Accepted,
                    terminal: true,
                    Record.ProviderTurnId);
            }
        }

        public void Unknown() => MarkUnknown();

        public void MarkUnknown()
        {
            if (Record.State is DecisionTurnState.Committed or DecisionTurnState.Materialized)
            {
                return;
            }

            Advance(
                DecisionTurnState.Unknown,
                Record.WriteStarted,
                Record.Submitted,
                Record.Accepted,
                Record.Terminal,
                Record.ProviderTurnId);
        }

        public void RecordTerminalResult(AgentTurnResult result)
        {
            if (result.ProviderTurnId is { Length: > 0 } providerTurnId
                && !string.Equals(providerTurnId, Record.ProviderTurnId, StringComparison.Ordinal))
            {
                ProviderTurnIdentified(providerTurnId);
            }

            Terminal();
        }

        private void Advance(
            DecisionTurnState state,
            bool writeStarted = false,
            bool submitted = false,
            bool accepted = false,
            bool terminal = false,
            string? providerTurnId = null)
        {
            DecisionSessionTurnRecord updated = Record with
            {
                State = state,
                WriteStarted = writeStarted,
                Submitted = submitted,
                Accepted = accepted,
                Terminal = terminal,
                ProviderTurnId = providerTurnId ?? Record.ProviderTurnId,
                RowVersion = Record.RowVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            RecoveryStoreWriteResult write = _store.CompareAndSwapDecisionTurnAsync(Record, updated)
                .GetAwaiter().GetResult();
            if (!write.Succeeded)
            {
                throw new InvalidOperationException($"Decision turn progress persistence failed: {write.Diagnostic}");
            }

            Record = updated;
        }
    }

    // Size-health guard: warn on a sustained upward ratchet of the operational-context size across consecutive
    // transfers. Kept local so the CLI does not depend on the legacy orchestration host's monitor service.
    private void RecordOperationalContextHealth(int newSize)
    {
        if (previousOperationalContextSize is null)
        {
            previousOperationalContextSize = newSize;
            operationalContextGrowthStreak = 0;
            return;
        }

        operationalContextGrowthStreak = newSize > previousOperationalContextSize.Value
            ? operationalContextGrowthStreak + 1
            : 0;
        previousOperationalContextSize = newSize;
        if (operationalContextGrowthStreak >= _operationalContextGrowthStreakWarningThreshold)
        {
            _console.Warn($"Operational context has grown for {operationalContextGrowthStreak} consecutive transfers (now {newSize} chars) — check for bloat.");
        }
    }

    private async Task<ProjectContextProjectionResult> EnsureDecisionProjectionAsync(CancellationToken cancellationToken)
    {
        if (_projectionService is null)
        {
            return new ProjectContextProjectionResult(
                new ProjectionDefinition(
                    ProjectionRuntimePromptNames.DecisionSession,
                    "ProjectionForDecisionSession",
                    ProjectionArtifactPaths.ProjectionPaths[ProjectionRuntimePromptNames.DecisionSession],
                    "# Execution Agent System Prompt Projection",
                    ProjectionRuntimePromptNames.DecisionSession),
                string.Empty,
                Generated: false,
                ProjectionStaleStatus.UnknownProvenance,
                []);
        }

        try
        {
            return await _projectionService.EnsureFreshAsync(
                ProjectionRuntimePromptNames.DecisionSession,
                cancellationToken);
        }
        catch (ProjectionException ex)
        {
            throw new LoopStepException(ex.Message, ex);
        }
    }

    private async Task<ProjectionFreshness> EvaluateDecisionProjectionFreshnessAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _projectionService!.EvaluateFreshnessAsync(
                ProjectionRuntimePromptNames.DecisionSession,
                cancellationToken);
        }
        catch (ProjectionException ex)
        {
            throw new LoopStepException(ex.Message, ex);
        }
    }

    // The router's unit-blind signals (mirrors RepositoryOrchestrator.SnapshotRouterInputs). Before any cycle is
    // observed (n == 0), occupancy is 0 so only the capacity guard could fire (it won't on a fresh process).
    private RouterInputs BuildRouterInputs()
    {
        if (reuseCycles == 0)
        {
            return new RouterInputs(0, 0d, 0, 0d, transferCost);
        }

        double predictedNext = (_costModel ?? new EffectiveTokenCostModel()).EstimateNextCycle(
            new DecisionCostForecast(lastCycleCost, prevCycleCost, occupancyTokens, 0));
        return new RouterInputs(occupancyTokens, reuseCost, reuseCycles, predictedNext, transferCost);
    }

    private void RecordProposalCost(AgentTokenUsage usage)
    {
        double cost = (_costModel ?? new EffectiveTokenCostModel()).Measure(usage);
        occupancyTokens = usage.PromptTokens + usage.OutputTokens;
        reuseCost += cost;
        reuseCycles += 1;
        prevCycleCost = lastCycleCost;
        lastCycleCost = cost;
    }

    private void RecordTransferCost(double measuredCost)
    {
        transferCount += 1;
        transferCost = transferCount == 1
            ? measuredCost
            : transferCost + ((measuredCost - transferCost) / transferCount);
    }

    // A failed turn's Diagnostics (the codex process's retained stderr tail) rides along in the thrown
    // message so the actual refusal/error text reaches the operator instead of a bare turn state.
    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";

    private static string Hash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ContinuityMessage(RecoveryRuntimeResult result, string threadId) => result.Outcome switch
    {
        RecoveryRuntimeOutcome.ResumedOriginal =>
            $"ResumedOriginal: resumed decision session (thread {threadId}).",
        RecoveryRuntimeOutcome.ReplacementNativeFork =>
            $"ReplacementNativeFork: activated certified fork (thread {threadId}).",
        RecoveryRuntimeOutcome.ReplacementRecoveredFull =>
            $"ReplacementRecoveredFull: activated full public-context replacement (thread {threadId}).",
        RecoveryRuntimeOutcome.ReplacementRecoveredPartial =>
            $"ReplacementRecoveredPartial: activated selective replacement (thread {threadId}, completeness {result.Completeness}).",
        RecoveryRuntimeOutcome.ReplacementRepositoryOnly =>
            $"ReplacementRepositoryOnly: the original conversation was unavailable; activated repository-only replacement (thread {threadId}).",
        _ => $"Decision continuity {result.Outcome} (thread {threadId}).",
    };

    // clearResumeState: a Transfer recycle or a failed turn ends the thread's useful life — the persisted
    // resume state must die with it (the recycled process re-persists after its first successful turn).
    // Disposal (loop exit) KEEPS the state: it is precisely the next run's resume payload, and no turn can
    // mutate the thread between the last persist and disposal.
    private async Task CloseAsync(bool clearResumeState = true)
    {
        if (session is not null)
        {
            await _runtime.CloseSessionAsync(session);
            session = null;
            seeded = false;
            // Per-process accounting resets for the fresh process; transferCost/transferCount persist.
            occupancyTokens = 0;
            reuseCost = 0d;
            reuseCycles = 0;
            lastCycleCost = 0d;
            prevCycleCost = 0d;

            if (clearResumeState && _recoveryStore is null)
            {
                await (_resumeStore ?? new NullDecisionSessionResumeStore()).ClearAsync(CancellationToken.None);
            }
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync(clearResumeState: false);
}
