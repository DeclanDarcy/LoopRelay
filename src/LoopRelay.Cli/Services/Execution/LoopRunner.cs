using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Primitives;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Primitives;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// The LoopStart state machine. Runs serially until the epic completes, a step fails, or cancellation
/// is requested. Owns the warm DecisionSession and disposes it on exit.
/// </summary>
internal sealed class LoopRunner(
    MilestoneGate gate,
    LoopArtifacts artifacts,
    ExecutionStep execution,
    DecisionSession decision,
    AgentsSubmodulePublisher submodulePublisher,
    CommitGate commitGate,
    IDecisionSessionResumeStore resumeStore,
    ICompletionCertificationService completionCertification,
    INonImplementationPostExecutionReviewService postExecutionReview,
    INonImplementationCompletionReviewService completionReview,
    ILoopConsole console) : IAsyncDisposable
{
    private readonly MilestoneGate _gate = gate;
    private readonly LoopArtifacts _artifacts = artifacts;
    private readonly ExecutionStep _execution = execution;
    private readonly DecisionSession _decision = decision;
    private readonly AgentsSubmodulePublisher _submodulePublisher = submodulePublisher;
    private readonly CommitGate _commitGate = commitGate;
    private readonly IDecisionSessionResumeStore _resumeStore = resumeStore;
    private readonly ICompletionCertificationService _completionCertification = completionCertification;
    private readonly INonImplementationPostExecutionReviewService _postExecutionReview = postExecutionReview;
    private readonly INonImplementationCompletionReviewService _completionReview = completionReview;
    private readonly ILoopConsole _console = console;
    public async Task<LoopOutcome> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ---- LoopStart ----
                // A finished epic needs no Codex work, so return immediately. Usage-limit handling runs
                // per-turn inside GatedAgentRuntime, so a completed epic never opens a session and thus never
                // waits on a (potentially multi-day) quota reset.
                if (await _gate.IsEpicCompleteAsync())
                {
                    NonImplementationCompletionReviewResult review =
                        await _completionReview.ReviewAsync(cancellationToken);
                    if (review.IsBlocked)
                    {
                        await _submodulePublisher.PublishAsync(
                            AgentsSubmodulePublisher.CompletionCertificationMessage,
                            cancellationToken);
                        _console.Warn(
                            "Non-implementation completion review blocked final evaluation. " +
                            $"Evidence: {FormatEvidence(review.EvidencePaths)}");
                        return LoopOutcome.CompletionBlocked;
                    }

                    if (review.AppliedDeletePaths.Count > 0)
                    {
                        await _commitGate.CommitPushIfChangedAsync(cancellationToken);
                    }

                    CompletionCertificationResult completion = await _completionCertification.CertifyPlanCompletionAsync(
                        new CompletionCertificationRequest(
                            _artifacts.Repository,
                            NonImplementationReviewEvidencePaths: review.EvidencePaths),
                        cancellationToken);
                    await _submodulePublisher.PublishAsync(
                        AgentsSubmodulePublisher.CompletionCertificationMessage,
                        cancellationToken);

                    if (completion.Outcome == CompletionCertificationServiceOutcome.Blocked)
                    {
                        _console.Warn($"Completion certification blocked: {completion.Message} Evidence: {FormatEvidence(completion.EvidencePaths)}");
                        return LoopOutcome.CompletionBlocked;
                    }

                    if (completion.Outcome == CompletionCertificationServiceOutcome.Failed)
                    {
                        _console.Error($"Completion certification failed: {completion.Message} Evidence: {FormatEvidence(completion.EvidencePaths)}");
                        return LoopOutcome.Failed;
                    }

                    // A finished epic obsoletes the persisted decision-session resume state — the next epic must start
                    // from a fresh decision process primed with its own operational context. Idempotent by design: this
                    // fires again on every re-run against a completed epic, and deleting nothing is a no-op.
                    await _resumeStore.ClearAsync(cancellationToken);
                    return LoopOutcome.EpicCompleted;
                }

                await _artifacts.EnsureOperationalContextAsync();

                // ---- Sequence this slice: execution-first, decision-first, or skip-to-execution ----
                // The decision session only earns its keep once there is a handoff to fold into the next agent's
                // system prompt. So the sequencing depends on state:
                //
                //  * A pending (unrotated) decisions.md — a prior slice produced it and was interrupted before its
                //    execution consumed+retired it — is executed directly; the decision session is skipped (same
                //    flow, just this portion skipped). Handoff rotation is skipped too. A live handoff can linger
                //    here (the prior slice crashed before rotating the handoff it consumed, or after execution wrote
                //    a fresh one); the skip path intentionally leaves it for execution's turn-2 write to overwrite
                //    rather than archiving it. That costs nothing: the only handoff consumer reads the LATEST
                //    handoff, a consumed handoff's content already lives in the decisions snapshot it was folded
                //    into (and in git), and the numbered handoff sequence tolerates gaps — a missing entry is
                //    cosmetic, not lost state.
                //  * No handoff yet (the first pass over a fresh plan) runs EXECUTION FIRST, straight from the plan
                //    via StartExecution — the self-contained plan is context enough to get going, and there is no
                //    prior session to adapt from, so no decision session runs and no decisions.md is produced.
                //  * Otherwise (a handoff exists) the decision session folds that handoff into decisions.md — the
                //    next agent's system prompt — and then execution continues from it.
                if (await _artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
                {
                    _console.Info("Found an unrotated decisions.md — skipping the decision session and executing it directly.");
                }
                else if ((await _artifacts.ReadLatestHandoffAsync()).Content is null)
                {
                    _console.Info("No handoff yet — starting the first execution directly from the plan.");
                }
                else
                {
                    await _decision.RunAsync(cancellationToken);

                    // Archive the handoff the decision just consumed so execution writes onto a clean slate.
                    await _artifacts.RotateLiveHandoffAsync();
                }

                // ---- Persist context BEFORE invoking codex ----
                // `.agents/` is a git submodule, so the parent repo cannot commit its contents. Commit and
                // push the submodule now, before the execution turn opens, so the exact context codex is
                // about to consume (operational_context, decisions, the rotated handoff) is persisted and
                // shared to the submodule's own remote first.
                await _submodulePublisher.PublishAsync(
                    AgentsSubmodulePublisher.ContextUpdateMessage, cancellationToken);

                // ---- Execution ----
                // Snapshot the unchecked-milestone-item count before codex runs (nothing between here and
                // the execution turn writes milestone files), so the stall gate can credit an iteration whose
                // only progress is ticking milestone boxes — those live inside the ignored `.agents/`, so the
                // working-tree probe alone would misread pure box-ticking as a stall.
                int uncheckedMilestonesBefore = (await _gate.GetUntickedItemsAsync()).Count;
                RepositorySliceBaseline reviewBaseline =
                    await _postExecutionReview.CapturePreSliceBaselineAsync(cancellationToken);

                // Consume decisions.md, do the work, and write the next handoff.
                await _execution.RunAsync(cancellationToken);
                NonImplementationPostExecutionReviewResult reviewResult =
                    await _postExecutionReview.ReviewAfterExecutionAsync(reviewBaseline, cancellationToken);
                _console.Info(
                    $"Non-implementation review completed for {reviewResult.ExecutionSliceId}: " +
                    $"{reviewResult.Summary.ChangedFileCount} changed, " +
                    $"{reviewResult.Summary.SemanticCandidateCount} semantic candidate(s), " +
                    $"{reviewResult.Summary.ConfirmedCount} confirmed, " +
                    $"{reviewResult.Summary.ReusedSemanticDispositionCount} reused. " +
                    $"Evidence: {FormatEvidence(reviewResult.EvidencePaths)}");

                // ---- Retire the consumed decisions ----
                // Execution has consumed decisions.md, so drop the live file. This is what makes the skip above
                // correct: the next slice sees no pending decisions.md and runs a fresh decision, and a restart
                // mid-slice (after this point) re-runs a decision rather than re-executing stale decisions. The
                // numbered snapshot written at persist time is the retained history. Retired BEFORE the post-codex
                // publish so the submodule commit captures the consumed state.
                await _artifacts.RetireLiveDecisionsAsync();

                // ---- Persist codex's writes ----
                // Publish codex's `.agents/` output (the new handoff, milestone box-checks, final decisions) to
                // the submodule. CommitGate ignores `.agents/`, and the epic-complete check short-circuits at
                // the TOP of the next iteration BEFORE its pre-codex publish — so without this the completing
                // (and stalling) iteration's state would never reach the submodule remote.
                await _submodulePublisher.PublishAsync(
                    AgentsSubmodulePublisher.ExecutionHandoffMessage, cancellationToken);

                // decision.RunAsync verifies decisions.md and execution.RunAsync verifies handoff.md (each throws
                // if its artifact is missing). Commit and push the working tree, then apply the stall gate: stop
                // if no substantive change recurs. A drop in unchecked milestone items across the execution turn
                // counts as substantive progress even when no real repository file changed.
                int uncheckedMilestonesAfter = (await _gate.GetUntickedItemsAsync()).Count;
                if (await _commitGate.CommitPushAndEvaluateAsync(
                        uncheckedMilestonesBefore, uncheckedMilestonesAfter, cancellationToken))
                {
                    return LoopOutcome.Stalled;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _console.Warn("Cancellation requested — stopping the loop.");
            await SalvageSubmoduleOnExitAsync();
            return LoopOutcome.Cancelled;
        }
        catch (NonImplementationPostExecutionReviewException ex)
        {
            _console.Error(
                $"Post-execution non-implementation review failed: {ex.Message} " +
                $"Evidence: {FormatEvidence(ex.EvidencePaths)}");
            await SalvageSubmoduleOnExitAsync();
            return LoopOutcome.Failed;
        }
        catch (LoopStepException ex)
        {
            _console.Error(ex.Message);
            await SalvageSubmoduleOnExitAsync();
            return LoopOutcome.Failed;
        }
    }

    // The loop is exiting abnormally (a failed step or cancellation), so the current iteration's `.agents/`
    // writes were never published. Salvage them to the submodule remote rather than stranding them until the
    // next run's pre-codex publish. Best-effort: uses CancellationToken.None so a cancelled run still flushes,
    // and swallows any failure so this never masks the real Failed/Cancelled outcome.
    private async Task SalvageSubmoduleOnExitAsync()
    {
        try
        {
            await _submodulePublisher.PublishAsync(AgentsSubmodulePublisher.PartialExitMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _console.Warn($"Could not publish partial .agents state on exit: {ex.Message}");
        }
    }

    private static string FormatEvidence(IReadOnlyList<string> evidencePaths) =>
        evidencePaths.Count == 0 ? "None" : string.Join(", ", evidencePaths);

    public ValueTask DisposeAsync() => _decision.DisposeAsync();
}
