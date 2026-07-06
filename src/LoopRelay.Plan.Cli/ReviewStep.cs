using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Plan.Cli;

/// <summary>
/// Pipeline step 4: the zero-permission adversarial review. A single turn on a fresh, read-only, persistent
/// session (the one-shot codex path never emits `--sandbox`, so read-only enforcement requires the persistent
/// app-server path — see the plan's Process Model notes) reviews the plan content inlined via
/// <see cref="AdversarialPlanReview"/>. The session is opened and closed exactly once, on every path, via a
/// try/finally around the single turn (mirroring ExecutionStep's session lifetime, collapsed to one turn). The
/// captured review text becomes `{output1}`, fed into <c>PlanSession.ReviseAsync</c> by the pipeline.
/// </summary>
internal sealed class ReviewStep(
    IAgentRuntime runtime, PlanArtifacts artifacts, ILoopConsole console, Repository repository)
{
    public async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        // Defensive: the pipeline gates plan.md's existence/non-whitespace immediately after WritePlanAsync, so
        // this should be unreachable in the real pipeline sequence. Guards ReviewStep against being called out
        // of order (e.g. directly, or after some future refactor drops the pipeline's own gate).
        string? plan = await artifacts.ReadAsync(OrchestrationArtifactPaths.Plan);
        if (string.IsNullOrWhiteSpace(plan))
        {
            throw new PlanStepException(
                $"{OrchestrationArtifactPaths.Plan} was not found before the adversarial review.");
        }

        IAgentSession session = await runtime.OpenSessionAsync(AgentSpecs.Review(repository), cancellationToken);
        try
        {
            var renderer = new ConsoleTurnRenderer(console);
            AgentTurnResult result = await session.RunTurnAsync(
                AdversarialPlanReview.Render(plan), renderer.Stream, cancellationToken);
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
                console.Info($"Review verdict: {verdict}");
            }
            else
            {
                console.Warn("Review verdict not found in output.");
            }

            return result.Output;
        }
        finally
        {
            await runtime.CloseSessionAsync(session);
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
