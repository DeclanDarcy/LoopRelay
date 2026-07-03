using CommandCenter.Orchestration;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// The sequencer for pipeline steps 1-10 (see the plan's Purpose section): preflight, write plan, adversarial
/// review, revise plan (eager session close), then the three sandboxed one-shots (collect details, extract
/// milestones, extract details) — each followed by a deterministic filesystem gate. Mirrors the reference CLI's
/// <c>LoopRunner</c> in shape: a thin sequencer that owns no agent-facing logic itself, only orders the steps,
/// surfaces console phase markers, and translates step failures into a terminal <see cref="PlanOutcome"/>.
/// </summary>
internal sealed class PlanPipeline(
    PreflightGate preflight,
    PlanSession planSession,
    ReviewStep review,
    SandboxedPromptStep oneShot,
    PlanArtifacts artifacts,
    ILoopConsole console) : IAsyncDisposable
{
    public async Task<PlanOutcome> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
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

            console.Phase("Adversarial Review");
            string output1 = await review.RunAsync(cancellationToken);

            console.Phase("Revise Plan");
            await planSession.ReviseAsync(output1, cancellationToken);
            // Eager close: the codex planning process must not outlive its last turn. The one-shot steps below
            // do not need the planning session, so it is torn down here rather than held until DisposeAsync.
            await planSession.CloseAsync();

            console.Phase("Collect Details");
            SandboxedStepPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);
            await oneShot.RunAsync(collectDetails, cancellationToken);
            if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.Details))
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.Details} was not present after Collect Details.");
            }

            console.Phase("Extract Milestones");
            SandboxedStepPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);
            await oneShot.RunAsync(extractMilestones, cancellationToken);
            IReadOnlyList<string> milestones = await artifacts.ListMilestonesRelativeAsync();
            if (milestones.Count == 0)
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.MilestonesDirectory} was empty after Extract Milestones.");
            }

            console.Phase("Extract Details");
            SandboxedStepPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(artifacts);
            await oneShot.RunAsync(extractDetails, cancellationToken);

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
