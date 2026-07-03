using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// Pipeline step 0, run BEFORE preflight: when a COMPLETE previous planning workspace is present
/// (<c>.agents/plan.md</c>, <c>.agents/details.md</c> and <c>.agents/operational_context.md</c> all exist AND
/// <c>.agents/milestones/</c> is non-empty), invokes the external <c>new-epic</c> tool against the target
/// repository. The tool moves everything under <c>.agents/</c> except <c>archive</c>, <c>.git</c> and
/// <c>specs</c> into <c>.agents/archive/epics/&lt;n&gt;/</c> and re-scaffolds empty working directories.
/// Deliberately conservative: only a complete previous workspace is auto-archived; partial states still hit
/// the preflight violations. NOTE the intended UX consequence: new-epic leaves <c>.agents/specs/</c> in
/// place, so a rollover run continues straight into planning the next epic from the surviving
/// <c>specs/roadmap.md</c>; it still exits PreflightBlocked (exit 4) if no roadmap existed in the first place.
/// </summary>
internal sealed class EpicRolloverStep(
    IProcessRunner processRunner, PlanArtifacts artifacts, ILoopConsole console, Repository repository)
{
    private const string ExecutableOverrideVariable = "NEW_EPIC_EXECUTABLE";

    /// <summary>
    /// Archives the previous epic's workspace via <c>new-epic</c> when it is complete. Returns true when the
    /// tool ran (and its effect was verified), false when there was nothing to archive. Any tool failure —
    /// nonzero exit, or a zero exit whose promised filesystem effect did not happen — throws
    /// <see cref="PlanStepException"/> (loud-failure convention; nothing is retried).
    /// </summary>
    public async Task<bool> TryArchiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool complete =
            await artifacts.ExistsAsync(OrchestrationArtifactPaths.Plan)
            && await artifacts.ExistsAsync(OrchestrationArtifactPaths.Details)
            && await artifacts.ExistsAsync(OrchestrationArtifactPaths.OperationalContext)
            && (await artifacts.ListMilestonesRelativeAsync()).Count > 0;
        if (!complete)
        {
            console.Info(
                "No complete previous epic workspace (plan.md + details.md + operational_context.md + "
                + "non-empty milestones/) — nothing to archive.");
            return false;
        }

        // `new-epic` is not directly launchable from .NET Process (UseShellExecute=false): only the batch
        // alias new-epic.bat is on PATH (wrapping new-epic.exe, whose own directory is NOT on PATH), and
        // CreateProcess does not perform PATHEXT resolution for .bat files. cmd.exe /c resolves it the way a
        // shell would. NEW_EPIC_EXECUTABLE overrides with a directly-launchable binary, invoked with no
        // arguments — new-epic rejects any argument and operates on its working directory.
        string? overridePath = Environment.GetEnvironmentVariable(ExecutableOverrideVariable);
        string fileName;
        string[] arguments;
        if (string.IsNullOrWhiteSpace(overridePath))
        {
            fileName = "cmd.exe";
            arguments = ["/c", "new-epic"];
        }
        else
        {
            fileName = overridePath;
            arguments = [];
        }

        // The provided directory is the working directory; new-epic resolves the repository root upward from it.
        ProcessRunResult result = await processRunner.RunAsync(fileName, arguments, repository.Path);
        if (result.ExitCode != 0)
        {
            // new-epic prints its certification findings on stderr; fall back to stdout when stderr is blank.
            throw new PlanStepException(
                $"new-epic failed with exit code {result.ExitCode}: "
                + $"{NonEmpty(result.StandardError, result.StandardOutput)}");
        }

        // Deterministic post-gate (loud-failure convention): a successful new-epic moved plan.md into the
        // archive, so it must no longer exist at its working-tree path.
        if (await artifacts.ExistsAsync(OrchestrationArtifactPaths.Plan))
        {
            throw new PlanStepException(
                $"new-epic reported success but {OrchestrationArtifactPaths.Plan} was not archived.");
        }

        string summary = result.StandardOutput.Trim();
        if (summary.Length > 0)
        {
            console.Info(summary);
        }

        return true;
    }

    private static string NonEmpty(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
