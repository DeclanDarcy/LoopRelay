using CommandCenter.Orchestration;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// Pipeline step 1: verifies the repository is in a clean state to start a fresh planning run — none of
/// plan.md / operational_context.md / details.md exist, milestones/ is empty — and that the mandatory
/// input (specs/epic.md) is present. Returns one human-actionable violation string per failed check;
/// an empty result means the pipeline is clear to run.
/// </summary>
internal sealed class PreflightGate(PlanArtifacts artifacts)
{
    public async Task<IReadOnlyList<string>> CheckAsync()
    {
        var violations = new List<string>();

        if (await artifacts.ExistsAsync(OrchestrationArtifactPaths.Plan))
        {
            violations.Add($"{OrchestrationArtifactPaths.Plan} already exists — archive or delete it before planning");
        }

        if (await artifacts.ExistsAsync(OrchestrationArtifactPaths.OperationalContext))
        {
            violations.Add(
                $"{OrchestrationArtifactPaths.OperationalContext} already exists — archive or delete it before planning");
        }

        if (await artifacts.ExistsAsync(OrchestrationArtifactPaths.Details))
        {
            violations.Add($"{OrchestrationArtifactPaths.Details} already exists — archive or delete it before planning");
        }

        IReadOnlyList<string> milestones = await artifacts.ListMilestonesRelativeAsync();
        if (milestones.Count > 0)
        {
            violations.Add(
                $"{OrchestrationArtifactPaths.MilestonesDirectory} is not empty — archive or delete its contents before planning");
        }

        if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.SpecsEpic))
        {
            violations.Add(
                $"{OrchestrationArtifactPaths.SpecsEpic} not found — author the epic before running the planning pipeline");
        }

        return violations;
    }
}
