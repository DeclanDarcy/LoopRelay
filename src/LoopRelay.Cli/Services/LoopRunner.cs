using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Primitives;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models;
using LoopRelay.Completion.Primitives;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services;

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
                if (await gate.IsEpicCompleteAsync())
                {
                    NonImplementationCompletionReviewResult review =
                        await completionReview.ReviewAsync(cancellationToken);
                    if (review.IsBlocked)
                    {
                        await submodulePublisher.PublishAsync(
                            AgentsSubmodulePublisher.CompletionCertificationMessage,
                            cancellationToken);
                        console.Warn(
                            "Non-implementation completion review blocked final evaluation. " +
                            $"Evidence: {FormatEvidence(review.EvidencePaths)}");
                        return LoopOutcome.CompletionBlocked;
                    }

                    if (review.AppliedDeletePaths.Count > 0)
                    {
                        await commitGate.CommitPushIfChangedAsync(cancellationToken);
                    }

                    CompletionCertificationResult completion = await completionCertification.CertifyPlanCompletionAsync(
                        new CompletionCertificationRequest(
                            artifacts.Repository,
                            NonImplementationReviewEvidencePaths: review.EvidencePaths),
                        cancellationToken);
                    await submodulePublisher.PublishAsync(
                        AgentsSubmodulePublisher.CompletionCertificationMessage,
                        cancellationToken);

                    if (completion.Outcome == CompletionCertificationServiceOutcome.Blocked)
                    {
                        console.Warn($"Completion certification blocked: {completion.Message} Evidence: {FormatEvidence(completion.EvidencePaths)}");
                        return LoopOutcome.CompletionBlocked;
                    }

                    if (completion.Outcome == CompletionCertificationServiceOutcome.Failed)
                    {
                        console.Error($"Completion certification failed: {completion.Message} Evidence: {FormatEvidence(completion.EvidencePaths)}");
                        return LoopOutcome.Failed;
                    }

                    // A finished epic obsoletes the persisted decision-session resume state — the next epic must start
                    // from a fresh decision process primed with its own operational context. Idempotent by design: this
                    // fires again on every re-run against a completed epic, and deleting nothing is a no-op.
                    await resumeStore.ClearAsync(cancellationToken);
                    return LoopOutcome.EpicCompleted;
                }

                await artifacts.EnsureOperationalContextAsync();

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
                if (await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
                {
                    console.Info("Found an unrotated decisions.md — skipping the decision session and executing it directly.");
                }
                else if ((await artifacts.ReadLatestHandoffAsync()).Content is null)
                {
                    console.Info("No handoff yet — starting the first execution directly from the plan.");
                }
                else
                {
                    await decision.RunAsync(cancellationToken);

                    // Archive the handoff the decision just consumed so execution writes onto a clean slate.
                    await artifacts.RotateLiveHandoffAsync();
                }

                // ---- Persist context BEFORE invoking codex ----
                // `.agents/` is a git submodule, so the parent repo cannot commit its contents. Commit and
                // push the submodule now, before the execution turn opens, so the exact context codex is
                // about to consume (operational_context, decisions, the rotated handoff) is persisted and
                // shared to the submodule's own remote first.
                await submodulePublisher.PublishAsync(
                    AgentsSubmodulePublisher.ContextUpdateMessage, cancellationToken);

                // ---- Execution ----
                // Snapshot the unchecked-milestone-item count before codex runs (nothing between here and
                // the execution turn writes milestone files), so the stall gate can credit an iteration whose
                // only progress is ticking milestone boxes — those live inside the ignored `.agents/`, so the
                // working-tree probe alone would misread pure box-ticking as a stall.
                int uncheckedMilestonesBefore = (await gate.GetUntickedItemsAsync()).Count;
                RepositorySliceBaseline reviewBaseline =
                    await postExecutionReview.CapturePreSliceBaselineAsync(cancellationToken);

                // Consume decisions.md, do the work, and write the next handoff.
                await execution.RunAsync(cancellationToken);
                NonImplementationPostExecutionReviewResult reviewResult =
                    await postExecutionReview.ReviewAfterExecutionAsync(reviewBaseline, cancellationToken);
                console.Info(
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
                await artifacts.RetireLiveDecisionsAsync();

                // ---- Persist codex's writes ----
                // Publish codex's `.agents/` output (the new handoff, milestone box-checks, final decisions) to
                // the submodule. CommitGate ignores `.agents/`, and the epic-complete check short-circuits at
                // the TOP of the next iteration BEFORE its pre-codex publish — so without this the completing
                // (and stalling) iteration's state would never reach the submodule remote.
                await submodulePublisher.PublishAsync(
                    AgentsSubmodulePublisher.ExecutionHandoffMessage, cancellationToken);

                // decision.RunAsync verifies decisions.md and execution.RunAsync verifies handoff.md (each throws
                // if its artifact is missing). Commit and push the working tree, then apply the stall gate: stop
                // if no substantive change recurs. A drop in unchecked milestone items across the execution turn
                // counts as substantive progress even when no real repository file changed.
                int uncheckedMilestonesAfter = (await gate.GetUntickedItemsAsync()).Count;
                if (await commitGate.CommitPushAndEvaluateAsync(
                        uncheckedMilestonesBefore, uncheckedMilestonesAfter, cancellationToken))
                {
                    return LoopOutcome.Stalled;
                }
            }
        }
        catch (OperationCanceledException)
        {
            console.Warn("Cancellation requested — stopping the loop.");
            await SalvageSubmoduleOnExitAsync();
            return LoopOutcome.Cancelled;
        }
        catch (NonImplementationPostExecutionReviewException ex)
        {
            console.Error(
                $"Post-execution non-implementation review failed: {ex.Message} " +
                $"Evidence: {FormatEvidence(ex.EvidencePaths)}");
            await SalvageSubmoduleOnExitAsync();
            return LoopOutcome.Failed;
        }
        catch (LoopStepException ex)
        {
            console.Error(ex.Message);
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
            await submodulePublisher.PublishAsync(AgentsSubmodulePublisher.PartialExitMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            console.Warn($"Could not publish partial .agents state on exit: {ex.Message}");
        }
    }

    private static string FormatEvidence(IReadOnlyList<string> evidencePaths) =>
        evidencePaths.Count == 0 ? "None" : string.Join(", ", evidencePaths);

    public ValueTask DisposeAsync() => decision.DisposeAsync();
}
