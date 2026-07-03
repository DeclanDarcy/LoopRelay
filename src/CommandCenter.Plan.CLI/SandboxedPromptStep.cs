using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// Declarative description of one seeded-sandbox one-shot turn. All paths are REPOSITORY-relative (e.g.
/// <c>.agents/plan.md</c>) — <see cref="SandboxedPromptStep"/> alone applies the <c>.agents/</c>-prefix-stripped
/// sandbox mapping, in both directions (seeding and verification/copy-back). See the plan's Key Signatures
/// and Artifact Protocol sections for the authoritative contract this record encodes.
/// </summary>
internal sealed record SandboxedStepPlan(
    string Label,
    string Prompt,
    IReadOnlyList<string> Seeds,
    IReadOnlyList<string> RequiredOutputs,
    (string Directory, string Pattern)? RequiredOutputGlob,
    string? ChangedGuard,
    IReadOnlyList<string> CopyBackFiles,
    (string Directory, string Pattern)? CopyBackGlob,
    bool RequireChecklistInGlob = false);

/// <summary>
/// Pipeline steps 6-10's generic seeded-sandbox one-shot runner (CollectDetails/ExtractMilestones/ExtractDetails
/// all ride this one implementation via a <see cref="SandboxedStepPlan"/>). Mirrors the reference CLI's
/// <c>DecisionSession.EvolveOperationalContextAsync</c>/<c>OptimizeOperationalDocumentsAsync</c> sandbox
/// create/seed/run/verify/copy-back pattern: one temp sandbox per turn (disposed via <c>await using</c> even on
/// failure), seeded with the <c>.agents/</c> prefix STRIPPED (codex hides dot-directories from workspace
/// listings — seeding under a sandbox <c>.agents/</c> tree would hide the seeds from the agent), every
/// deterministic gate run BEFORE any copy-back so a failed step leaves the repository's inputs untouched.
/// </summary>
internal sealed class SandboxedPromptStep(
    IAgentRuntime runtime,
    ISandboxWorkspaceFactory sandboxFactory,
    PlanArtifacts artifacts,
    ILoopConsole console,
    Repository repository)
{
    public async Task RunAsync(SandboxedStepPlan step, CancellationToken cancellationToken)
    {
        await using ISandboxWorkspace sandbox = await sandboxFactory.CreateAsync(step.Label, cancellationToken);

        string? changedGuardSnapshot = null;

        // Seeding is a precondition, not an option: a missing seed throws BEFORE any codex call so a
        // misconfigured/incomplete repo state never burns a codex turn.
        foreach (string seed in step.Seeds)
        {
            string? content = await artifacts.ReadAsync(seed);
            if (content is null)
            {
                throw new PlanStepException(
                    $"{step.Label}: required seed {seed} was not found in the repository.");
            }

            await artifacts.WriteAbsoluteAsync(sandbox.Resolve(StripAgentsPrefix(seed)), content);

            if (step.ChangedGuard is not null && string.Equals(seed, step.ChangedGuard, StringComparison.Ordinal))
            {
                changedGuardSnapshot = content;
            }
        }

        var renderer = new ConsoleTurnRenderer(console);
        AgentTurnResult result = await runtime.RunOneShotAsync(
            AgentSpecs.SandboxedOneShot(repository, sandbox.RootPath),
            step.Prompt,
            renderer.Stream,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new PlanStepException(WithDiagnostics(
                $"{step.Label} turn ended in state {result.State}.", result.Diagnostics));
        }

        renderer.EchoIfSilent(result.Output);

        // Gate order (all BEFORE any copy-back, so repo inputs survive a failed step): required outputs exist,
        // required-output glob is non-empty (+ the checklist false-closure guard for ExtractMilestones), then
        // the changed-content guard.
        foreach (string requiredOutput in step.RequiredOutputs)
        {
            if (!await artifacts.ExistsAbsoluteAsync(sandbox.Resolve(StripAgentsPrefix(requiredOutput))))
            {
                throw new PlanStepException($"{step.Label} did not produce {requiredOutput}.");
            }
        }

        if (step.RequiredOutputGlob is { } requiredGlob)
        {
            IReadOnlyList<string> matches = await artifacts.ListAbsoluteAsync(
                sandbox.Resolve(StripAgentsPrefix(requiredGlob.Directory)), requiredGlob.Pattern);
            if (matches.Count == 0)
            {
                throw new PlanStepException(
                    $"{step.Label} produced no files matching {requiredGlob.Directory}/{requiredGlob.Pattern}.");
            }

            if (step.RequireChecklistInGlob)
            {
                int total = 0;
                foreach (string match in matches)
                {
                    string content = await artifacts.ReadAbsoluteAsync(match) ?? string.Empty;
                    (int matchTotal, _) = MilestoneChecklist.CountCheckboxes(content);
                    total += matchTotal;
                }

                if (total == 0)
                {
                    throw new PlanStepException("extracted milestones contain no trackable checkboxes");
                }
            }
        }

        if (step.ChangedGuard is { } changedGuard)
        {
            string guardSandboxPath = sandbox.Resolve(StripAgentsPrefix(changedGuard));
            if (!await artifacts.ExistsAbsoluteAsync(guardSandboxPath))
            {
                throw new PlanStepException(
                    $"{step.Label} left {changedGuard} missing in the sandbox — it must remain present.");
            }

            string changedContent = await artifacts.ReadAbsoluteAsync(guardSandboxPath) ?? string.Empty;
            if (string.Equals(changedContent, changedGuardSnapshot ?? string.Empty, StringComparison.Ordinal))
            {
                throw new PlanStepException(
                    $"{step.Label} left {changedGuard} unchanged — the expected rewrite did not happen.");
            }
        }

        // Copy-back: existence-guarded (a file the agent legitimately left absent/untouched is not an error)
        // EXCEPT the ChangedGuard file, whose copy-back is strict — but its existence was already proven above.
        foreach (string copyBackFile in step.CopyBackFiles)
        {
            string sandboxPath = sandbox.Resolve(StripAgentsPrefix(copyBackFile));
            bool isChangedGuard = step.ChangedGuard is not null
                && string.Equals(copyBackFile, step.ChangedGuard, StringComparison.Ordinal);

            if (!await artifacts.ExistsAbsoluteAsync(sandboxPath))
            {
                if (isChangedGuard)
                {
                    throw new PlanStepException(
                        $"{step.Label} left {copyBackFile} missing in the sandbox at copy-back.");
                }

                continue;
            }

            string content = await artifacts.ReadAbsoluteAsync(sandboxPath) ?? string.Empty;
            await artifacts.WriteAsync(copyBackFile, content);
        }

        if (step.CopyBackGlob is { } copyBackGlob)
        {
            string sandboxDirectory = sandbox.Resolve(StripAgentsPrefix(copyBackGlob.Directory));
            IReadOnlyList<string> matches = await artifacts.ListAbsoluteAsync(sandboxDirectory, copyBackGlob.Pattern);
            foreach (string match in matches)
            {
                string repoRelative = ArtifactPath.CombineRelative(copyBackGlob.Directory, Path.GetFileName(match));
                string content = await artifacts.ReadAbsoluteAsync(match) ?? string.Empty;
                await artifacts.WriteAsync(repoRelative, content);
            }
        }
    }

    // Strips a leading ".agents/" from a repository-relative path so the seed is visible to codex inside the
    // sandbox (codex hides dot-directories from workspace listings — the 0baf0e7b flat-seeding convention).
    // Repository-relative paths outside .agents/ (none exist in this pipeline today) pass through unchanged.
    private static string StripAgentsPrefix(string repositoryRelativePath)
    {
        const string prefix = OrchestrationArtifactPaths.AgentsDirectory + "/";
        return repositoryRelativePath.StartsWith(prefix, StringComparison.Ordinal)
            ? repositoryRelativePath[prefix.Length..]
            : repositoryRelativePath;
    }

    // A failed turn's Diagnostics (the codex process's retained stderr tail) rides along in the thrown message
    // so the actual refusal/error text reaches the operator instead of a bare turn state. Copied verbatim from
    // DecisionSession's WithDiagnostics idiom.
    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}
