using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Plan.Cli.Abstractions;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Cli;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;

namespace LoopRelay.Plan.Cli.Services.Execution;

/// <summary>
/// Pipeline step 4: the zero-permission adversarial review. A single turn on a fresh, read-only, persistent
/// session reviews the plan content inlined via
/// <see cref="AdversarialPlanReview"/>. The session is opened and closed exactly once, on every path, via a
/// try/finally around the single turn (mirroring ExecutionStep's session lifetime, collapsed to one turn). The
/// captured review text becomes `{output1}`, fed into <c>PlanSession.ReviseAsync</c> by the pipeline.
/// </summary>
internal sealed class ReviewStep(
    IAgentRuntime _runtime, PlanArtifacts _artifacts, ILoopConsole _console, Repository _repository)
{
    public async Task<string> RunAsync(string projectContextProjection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectContextProjection))
        {
            throw new PlanStepException("Project Context projection was not generated before the adversarial review.");
        }

        // Defensive: the pipeline gates plan.md's existence/non-whitespace immediately after WritePlanAsync, so
        // this should be unreachable in the real pipeline sequence. Guards ReviewStep against being called out
        // of order (e.g. directly, or after some future refactor drops the pipeline's own gate).
        string? plan = await _artifacts.ReadAsync(OrchestrationArtifactPaths.Plan);
        if (string.IsNullOrWhiteSpace(plan))
        {
            throw new PlanStepException(
                $"{OrchestrationArtifactPaths.Plan} was not found before the adversarial review.");
        }

        IAgentSession session = await _runtime.OpenSessionAsync(AgentSpecs.Review(_repository), cancellationToken);
        try
        {
            var renderer = new ConsoleTurnRenderer(_console);
            AgentTurnResult result = await session.RunTurnAsync(
                AdversarialPlanReview.Render(projectContextProjection, plan), renderer.Stream, cancellationToken);
            if (result.State != AgentTurnState.Completed)
            {
                throw new PlanStepException(WithDiagnostics(
                    $"Adversarial review turn ended in state {result.State}.", result.Diagnostics));
            }

            renderer.EchoIfSilent(result.Output);

            if (string.IsNullOrWhiteSpace(result.Output))
            {
                throw new PlanStepException("adversarial review returned no output");
            }

            if (TryExtractVerdict(result.Output, out string verdict))
            {
                _console.Info($"Review verdict: {verdict}");
            }
            else
            {
                _console.Warn("Review verdict not found in output.");
            }

            return result.Output;
        }
        finally
        {
            await _runtime.CloseSessionAsync(session);
        }
    }

    // Scans for a trimmed line starting with one of the three canonical verdict bullets the
    // AdversarialPlanReview prompt asks for ("Choose exactly one"). Ordinal, case-sensitive prefix match; the
    // three prefixes are mutually exclusive (no bullet is a prefix of another), so line order alone decides
    // which occurrence wins when more than one is present. Returns the bare verdict token, not the whole line.
    internal static bool TryExtractVerdict(string review, out string verdict)
    {
        foreach (string rawLine in review.Split('\n'))
        {
            string trimmed = rawLine.Trim();
            if (trimmed.StartsWith("- FAIL", StringComparison.Ordinal))
            {
                verdict = "FAIL";
                return true;
            }

            if (trimmed.StartsWith("- CONDITIONAL PASS", StringComparison.Ordinal))
            {
                verdict = "CONDITIONAL PASS";
                return true;
            }

            if (trimmed.StartsWith("- PASS", StringComparison.Ordinal))
            {
                verdict = "PASS";
                return true;
            }
        }

        verdict = string.Empty;
        return false;
    }

    // A failed turn's Diagnostics (the codex process's retained stderr tail) rides along in the thrown message
    // so the actual refusal/error text reaches the operator instead of a bare turn state. Copied verbatim from
    // DecisionSession's WithDiagnostics idiom.
    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}
