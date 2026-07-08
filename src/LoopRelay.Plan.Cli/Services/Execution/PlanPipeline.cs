using LoopRelay.Orchestration.Services;
using LoopRelay.Plan.Cli.Abstractions;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Primitives;
using LoopRelay.Plan.Cli.Services.Agents;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.ProjectionArtifacts;

namespace LoopRelay.Plan.Cli.Services.Execution;

/// <summary>
/// The sequencer for the pipeline steps: preflight, write plan, adversarial review, revise plan (eager session
/// close, then operational_context.md seeded from the revised plan), then the three scoped artifact operations
/// (collect details, extract milestones, extract details) — each followed by a deterministic filesystem gate.
/// Every artifact-mutating step is followed by a commit+push of the .agents submodule (the adversarial review
/// writes nothing, so it publishes nothing); the parent repo's moved gitlink pointer is recorded ONCE at the
/// end of a successful run. Mirrors the reference CLI's <c>LoopRunner</c> in shape: a thin sequencer that owns
/// no agent-facing logic itself, only orders the steps, surfaces console phase markers, and translates step
/// failures into a terminal <see cref="PlanOutcome"/>.
/// </summary>
internal sealed class PlanPipeline(
    PreflightGate preflight,
    PlanSession planSession,
    ReviewStep review,
    IProjectContextProjectionService projectionService,
    PermissionedArtifactOperationStep artifactOperation,
    AgentsSubmodulePublisher publisher,
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
            bool agentsCommitted =
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.WritePlanMessage, cancellationToken);

            console.Phase("Generate Adversarial Review Projection");
            ProjectContextProjectionResult adversarialProjection = await projectionService.EnsureFreshAsync(
                ProjectionRuntimePromptNames.AdversarialPlanReview,
                cancellationToken);
            agentsCommitted |=
                await publisher.PublishAgentsAsync(
                    AgentsSubmodulePublisher.GenerateAdversarialReviewProjectionMessage,
                    cancellationToken);

            console.Phase("Adversarial Review");
            // No publish after this step: the review runs read-only and writes nothing, so there is nothing to publish.
            string output1 = await review.RunAsync(adversarialProjection.Content, cancellationToken);

            console.Phase("Revise Plan");
            await planSession.ReviseAsync(output1, cancellationToken);
            // Eager close: the codex planning process must not outlive its last turn. The artifact operations below
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
            ArtifactOperationPlan collectDetails = await OneShotSteps.CollectDetailsAsync(artifacts);
            await artifactOperation.RunAsync(collectDetails, cancellationToken);
            if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.Details))
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.Details} was not present after Collect Details.");
            }

            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.CollectDetailsMessage, cancellationToken);

            console.Phase("Extract Milestones");
            ArtifactOperationPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(artifacts);
            await artifactOperation.RunAsync(extractMilestones, cancellationToken);
            IReadOnlyList<string> milestones = await artifacts.ListMilestonesRelativeAsync();
            if (milestones.Count == 0)
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.MilestonesDirectory} was empty after Extract Milestones.");
            }

            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ExtractMilestonesMessage, cancellationToken);

            console.Phase("Extract Details");
            ArtifactOperationPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(artifacts);
            await artifactOperation.RunAsync(extractDetails, cancellationToken);
            agentsCommitted |=
                await publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ExtractDetailsMessage, cancellationToken);

            // Any step commit moved the submodule HEAD, so the parent gitlink pointer necessarily lags — the
            // control flow already proves it; no status probe.
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
