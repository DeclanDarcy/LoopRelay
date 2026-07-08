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
    private readonly PreflightGate _preflight = preflight;
    private readonly PlanSession _planSession = planSession;
    private readonly ReviewStep _review = review;
    private readonly IProjectContextProjectionService _projectionService = projectionService;
    private readonly PermissionedArtifactOperationStep _artifactOperation = artifactOperation;
    private readonly AgentsSubmodulePublisher _publisher = publisher;
    private readonly PlanArtifacts _artifacts = artifacts;
    private readonly ILoopConsole _console = console;
    public async Task<PlanOutcome> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _console.Phase("Preflight");
            IReadOnlyList<string> violations = await _preflight.CheckAsync();
            if (violations.Count > 0)
            {
                foreach (string violation in violations)
                {
                    _console.Error(violation);
                }

                return PlanOutcome.PreflightBlocked;
            }

            _console.Phase("Write Plan");
            await _planSession.WritePlanAsync(cancellationToken);
            bool agentsCommitted =
                await _publisher.PublishAgentsAsync(AgentsSubmodulePublisher.WritePlanMessage, cancellationToken);

            _console.Phase("Generate Adversarial Review Projection");
            ProjectContextProjectionResult adversarialProjection = await _projectionService.EnsureFreshAsync(
                ProjectionRuntimePromptNames.AdversarialPlanReview,
                cancellationToken);
            agentsCommitted |=
                await _publisher.PublishAgentsAsync(
                    AgentsSubmodulePublisher.GenerateAdversarialReviewProjectionMessage,
                    cancellationToken);

            _console.Phase("Adversarial Review");
            // No publish after this step: the review runs read-only and writes nothing, so there is nothing to publish.
            string output1 = await _review.RunAsync(adversarialProjection.Content, cancellationToken);

            _console.Phase("Revise Plan");
            await _planSession.ReviseAsync(output1, cancellationToken);
            // Eager close: the codex planning process must not outlive its last turn. The artifact operations below
            // do not need the planning session, so it is torn down here rather than held until DisposeAsync.
            await _planSession.CloseAsync();

            // Seed operational_context.md from the revised plan — the execution loop's Decision Runtime starts
            // from a context that IS the plan. Defensive null-guard only: ReviseAsync's gate already proved
            // plan.md exists and is non-whitespace.
            string plan = await _artifacts.ReadAsync(OrchestrationArtifactPaths.Plan)
                ?? throw new PlanStepException($"{OrchestrationArtifactPaths.Plan} disappeared after Revise Plan.");
            await _artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalContext, plan);
            _console.Info(
                $"Seeded {OrchestrationArtifactPaths.OperationalContext} from {OrchestrationArtifactPaths.Plan}.");
            // One commit covers both the revised plan.md and the operational_context.md seeded from it.
            agentsCommitted |=
                await _publisher.PublishAgentsAsync(AgentsSubmodulePublisher.RevisePlanMessage, cancellationToken);

            _console.Phase("Collect Details");
            ArtifactOperationPlan collectDetails = await OneShotSteps.CollectDetailsAsync(_artifacts);
            await _artifactOperation.RunAsync(collectDetails, cancellationToken);
            if (!await _artifacts.ExistsAsync(OrchestrationArtifactPaths.Details))
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.Details} was not present after Collect Details.");
            }

            agentsCommitted |=
                await _publisher.PublishAgentsAsync(AgentsSubmodulePublisher.CollectDetailsMessage, cancellationToken);

            _console.Phase("Extract Milestones");
            ArtifactOperationPlan extractMilestones = await OneShotSteps.ExtractMilestonesAsync(_artifacts);
            await _artifactOperation.RunAsync(extractMilestones, cancellationToken);
            IReadOnlyList<string> milestones = await _artifacts.ListMilestonesRelativeAsync();
            if (milestones.Count == 0)
            {
                throw new PlanStepException(
                    $"{OrchestrationArtifactPaths.MilestonesDirectory} was empty after Extract Milestones.");
            }

            agentsCommitted |=
                await _publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ExtractMilestonesMessage, cancellationToken);

            _console.Phase("Extract Details");
            ArtifactOperationPlan extractDetails = await OneShotSteps.ExtractDetailsAsync(_artifacts);
            await _artifactOperation.RunAsync(extractDetails, cancellationToken);
            agentsCommitted |=
                await _publisher.PublishAgentsAsync(AgentsSubmodulePublisher.ExtractDetailsMessage, cancellationToken);

            // Any step commit moved the submodule HEAD, so the parent gitlink pointer necessarily lags — the
            // control flow already proves it; no status probe.
            if (agentsCommitted)
            {
                await _publisher.RecordParentGitlinkAsync(cancellationToken);
            }

            _console.Info(
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
            _console.Error(ex.Message);
            return PlanOutcome.Failed;
        }
        catch (Exception ex)
        {
            // Anything else (Win32Exception on a bad CODEX_EXECUTABLE, IOException on app-server handshake
            // death, ...) must still honor the documented exit-code contract: surface the message, exit Failed —
            // never crash with a bare stack trace.
            _console.Error(ex.Message);
            return PlanOutcome.Failed;
        }
    }

    public async ValueTask DisposeAsync() => await _planSession.DisposeAsync();
}
