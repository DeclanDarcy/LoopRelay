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
    // TODO: replace this with proper user settings.json derived AllowAuxiliaryNonImplementationFiles when merging the CLIs
    private const bool AllowAuxiliaryNonImplementationFiles = false;
    
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
                RenderAdversarialPlanReviewPrompt(projectContextProjection, plan), renderer.Stream, cancellationToken);
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

    private static string RenderAdversarialPlanReviewPrompt(string projectContextProjection, string? plan)
    {
        if (AllowAuxiliaryNonImplementationFiles)
        {
            return AdversarialPlanReview.Render(
                string.Empty,
                string.Empty,
                string.Empty,
                "If no projection rule is violated, explicitly state that the finding is based on generic planning quality rather than project-specific semantics.",
                string.Empty,
                string.Empty,
                string.Empty,
                projectContextProjection,
                plan);
        }
        
        return AdversarialPlanReview.Render(
"""

A plan is not safe because it is coherent, complete-looking, or well documented. It is safe only when it drives implementation-bearing work that advances implemented capability, creates executable progress, and can be validated by operational gates. Treat documentation-centric progress as a failure mode when it creates false implementation confidence or lets non-executable outputs stand in for capability evidence.

""",
"""

During generic review, treat implementation-first fidelity as an explicit attack vector. Attack plans that substitute artifacts about work for work. Look for plan steps that produce reports, inventories, governance language, certification narratives, explanatory documentation, or other non-implementation deliverables without advancing implemented capability.

Treat acceptance criteria that measure prose completion, non-implementation deliverables presented as capability evidence, or completed-looking artifacts that leave code, tests, runtime behavior, project files, required generated artifacts, or sanctioned `.agents` operational artifacts unchanged or insufficiently constrained as material risk.

A HITL-requested non-implementation deliverable is only a narrow, evidence-grounded exception for that deliverable; it does not create general permission to make documentation-centric progress. Treat machine-consumed operational artifacts as valid capability evidence only when they directly constrain execution, validation, or downstream agent behavior.

Optimize for detecting false implementation confidence created by plans that can look complete while the software remains unchanged or insufficiently constrained.

""",
"""

- Implementation-first fidelity: Does the plan advance implemented capability, or does it mistake artifacts about work for work?
- Capability evidence: Are exit criteria tied to code, tests, runtime behavior, project files, generated artifacts, or sanctioned operational artifacts that constrain execution?
- Documentation theater: Do prose deliverables, governance or certification language, and explanatory artifacts create false confidence without executable gates?
- Operational artifact validity: Are `.agents` artifacts or generated artifacts machine-consumed and execution-bearing, or are they auxiliary commentary?
""",
"If no projection rule is violated, explicitly state that the finding is based on generic implementation-first review quality or another generic planning quality axis rather than project-specific projection semantics. Implementation-first failures are material findings even when the projection does not restate them.",
"""

Always include implementation-first false-closure modes when the plan can appear complete through prose deliverables, governance or certification artifacts, inventories, or other non-executable outputs while implemented capability remains incomplete.

""",            
"""

Corrections should move risk back into implementation-bearing plan content, milestone acceptance criteria, validation gates, required generated artifacts, or sanctioned `.agents` operational artifacts. Do not ask for separate policy documents, side reports, or documentation theater as the correction unless they are machine-consumed gates that directly constrain execution.

""",
"""

- no realistic implementation-first false-success path remains
""",
projectContextProjection,
            plan);
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
