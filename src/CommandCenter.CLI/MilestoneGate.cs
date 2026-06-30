using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// LoopStart epic-complete gate. Aggregates GitHub task-list checkboxes (parsed with the canonical
/// RepositoryProjectionService.CountCheckboxes rule) across .agents/plan.md (if present) and every
/// .agents/milestones/m*.md. The epic is complete only when at least one checkbox exists across them
/// and every checkbox is checked. Files with zero checkboxes contribute nothing and never block.
/// </summary>
internal sealed class MilestoneGate(IArtifactStore store, Repository repository)
{
    public async Task<bool> IsEpicCompleteAsync()
    {
        int total = 0;
        int completed = 0;

        // .agents/plan.md (if present) contributes its checkboxes alongside the milestones.
        string? plan = await store.ReadAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan));
        if (plan is not null)
        {
            Accumulate(plan, ref total, ref completed);
        }

        string dir = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.MilestonesDirectory);
        IReadOnlyList<string> files = await store.ListAsync(dir, OrchestrationArtifactPaths.MilestoneSearchPattern);
        foreach (string file in files)
        {
            string content = await store.ReadAsync(file) ?? string.Empty;
            Accumulate(content, ref total, ref completed);
        }

        return total > 0 && completed == total;
    }

    private static void Accumulate(string content, ref int total, ref int completed)
    {
        (int t, int c) = CountCheckboxes(content);
        total += t;
        completed += c;
    }

    // Ported verbatim from RepositoryProjectionService.CountCheckboxes (the canonical, authoritative rule).
    internal static (int total, int completed) CountCheckboxes(string content)
    {
        int total = 0;
        int completed = 0;
        bool insideFence = false;

        foreach (ReadOnlySpan<char> rawLine in content.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> line = rawLine.TrimStart();
            if (line.StartsWith("```"))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence || line.Length < 6)
            {
                continue;
            }

            if (line[0] != '-' || line[1] != ' ' || line[2] != '[' || line[4] != ']' || line[5] != ' ')
            {
                continue;
            }

            char mark = line[3];
            if (mark == ' ')
            {
                total++;
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (total, completed);
    }
}
