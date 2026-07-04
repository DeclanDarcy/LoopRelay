using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// The sequencer for the pipeline steps: epic rollover (auto-archive a complete previous workspace via
/// new-epic, publishing the archive — submodule AND parent gitlink — immediately), preflight, write plan,
/// adversarial review, revise plan (eager session close, then operational_context.md seeded from the revised
/// plan), then the three sandboxed one-shots (collect details, extract milestones, extract details) — each
/// followed by a deterministic filesystem gate. Every artifact-mutating step is followed by a commit+push of
/// the .agents submodule (the adversarial review writes nothing, so it publishes nothing); the parent repo's
/// moved gitlink pointer is recorded ONCE at the end of a successful run. Mirrors the reference CLI's
/// <c>LoopRunner</c> in shape: a thin sequencer that owns no agent-facing logic itself, only orders the steps,
/// surfaces console phase markers, and translates step failures into a terminal <see cref="PlanOutcome"/>.
/// </summary>
internal sealed class PlanPipeline(
    EpicRolloverStep rollover,
    PreflightGate preflight,
    PlanSession planSession,
    ReviewStep review,
    SandboxedPromptStep oneShot,
    AgentsSubmodulePublisher publisher,
    PlanArtifacts artifacts,
    IDecisionSessionResumeStore resumeStore,
    ILoopConsole console) : IAsyncDisposable
{
    public async Task<PlanOutcome> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            console.Phase("Epic Rollover");
            if (await rollover.TryArchiveAsync(cancellationToken))
            {
                // The epic boundary invalidates the loop CLI's persisted decision-session resume state — the next
                // epic must start from a fresh decision process. Cleared here AS WELL AS in the loop's epic-complete
                // gate: a rollover can happen without the loop ever re-running against the completed epic. Idempotent.
                await resumeStore.ClearAsync(cancellationToken);

                // The archive's parent gitlink is recorded IMMEDIATELY (not deferred to the end-of-run
                // reconcile): new-epic leaves specs/ in place, so the run normally continues into planning
                // the next epic from the surviving specs/epic.md — but it may still stop at the very next
                // gate (preflight blocks when no epic exists), and the archive must be fully published
                // either way.
                if (await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ArchivePreviousEpicMessage, cancellationToken))
                {
                    await publisher.RecordParentGitlinkAsync(cancellationToken);
                }
            }

            console.Phase("Preflight");
            IReadOnlyList<string> violations = await preflight.CheckAsync();
            if (violations.Count > 0)
            {
                foreach (string violation in violations)
                {
                    console.Error(violation);
                }

                return PlanOutcome.PreflightBlocked;
            }

            console.Phase("Write Plan");
            await planSession.WritePlanAsync(cancellationToken);
            bool agentsCommitted =
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.WritePlanMessage, cancellationToken);

            console.Phase("Adversarial Review");
            // No publish after this step: the review runs read-only and writes nothing, so there is nothing to publish.
            string output1 = await review.RunAsync(cancellationToken);

            console.Phase("Revise Plan");
            await planSession.ReviseAsync(output1, cancellationToken);
            // Eager close: the codex planning process must not outlive its last turn. The one-shot steps below
            // do not need the planning session, so it is torn down here rather than held until DisposeAsync.
            await planSession.CloseAsync();

            // Seed operational_context.md from the revised plan — the execution loop's Decision Runtime starts
            // from a context that IS the plan. Defensive null-guard only: ReviseAsync's gate already proved
            // plan.md exists and is non-whitespace.
            string plan = await artifacts.ReadAsync(OrchestrationArtifactPaths.Plan)
                ?? throw new PlanStepException($"{OrchestrationArtifactPaths.Plan} disappeared after Revise Plan.");
            await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalContext, plan);
            console.Info(
                $"Seeded {OrchestrationArtifactPaths.OperationalContext} from {OrchestrationArtifactPaths.Plan}.");
            // One commit covers both the revised plan.md and the operational_context.md seeded from it.
            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.RevisePlanMessage, cancellationToken);

            console.Phase("Collect Details");
            SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);
            await oneShot.RunAsync(collectDetails, cancellationToken);
            if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.Details))
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.Details} was not present after Collect Details.");
            }

            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.CollectDetailsMessage, cancellationToken);

            console.Phase("Extract Milestones");
            SandboxedStepPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);
            await oneShot.RunAsync(extractMilestones, cancellationToken);
            IReadOnlyList<string> milestones = await artifacts.ListMilestonesRelativeAsync();
            if (milestones.Count == 0)
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.MilestonesDirectory} was empty after Extract Milestones.");
            }

            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ExtractMilestonesMessage, cancellationToken);

            console.Phase("Extract Details");
            SandboxedStepPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(artifacts);
            await oneShot.RunAsync(extractDetails, cancellationToken);
            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ExtractDetailsMessage, cancellationToken);

            // Any step commit moved the submodule HEAD, so the parent gitlink pointer necessarily lags — the
            // control flow already proves it; no status probe. The rollover branch's publishes deliberately do
            // NOT feed agentsCommitted: its pointer was already recorded immediately, up in step 1.
            if (agentsCommitted)
            {
                await publisher.RecordParentGitlinkAsync(cancellationToken);
            }

            console.Info(
                $"Planning pipeline complete. Produced {OrchestrationArtifactPaths.Plan}, "
                + $"{OrchestrationArtifactPaths.Details}, and {milestones.Count} milestone file(s) under "
                + $"{OrchestrationArtifactPaths.MilestonesDirectory}.");

            return PlanOutcome.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancelled ONLY on the caller's token: an OperationCanceledException without caller cancellation
            // (e.g. an internal timeout) is a failure, not a cancellation — it falls through to the catch below.
            return PlanOutcome.Cancelled;
        }
        catch (PlanStepException ex)
        {
            console.Error(ex.Message);
            return PlanOutcome.Failed;
        }
        catch (Exception ex)
        {
            // Anything else (Win32Exception on a bad CODEX_EXECUTABLE, IOException on app-server handshake
            // death, ...) must still honor the documented exit-code contract: surface the message, exit Failed —
            // never crash with a bare stack trace.
            console.Error(ex.Message);
            return PlanOutcome.Failed;
        }
    }

    public async ValueTask DisposeAsync() => await planSession.DisposeAsync();
}
