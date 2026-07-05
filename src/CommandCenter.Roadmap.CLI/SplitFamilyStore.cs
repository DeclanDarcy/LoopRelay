namespace CommandCenter.Roadmap.Cli;

internal sealed class SplitFamilyStore(RoadmapArtifacts artifacts)
{
    public async Task<string> WriteAsync(SplitFamily family)
    {
        string path = RoadmapArtifactPaths.SplitFamily(family.FamilyId);
        var lines = new List<string>
        {
            "# Split Family",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Family ID | {family.FamilyId} |",
            $"| Created At | {family.CreatedAt:O} |",
            $"| Selected Child | {family.SelectedChildPath} |",
            $"| Selected Child Rationale | {family.SelectedChildRationale.Replace('\n', ' ')} |",
            string.Empty,
            "## Proposal",
            string.Empty,
            family.Proposal,
            string.Empty,
            "## Child Epics",
            string.Empty,
        };

        foreach (string child in family.ChildEpicPaths)
        {
            lines.Add($"- {child}");
        }

        lines.AddRange(
        [
            string.Empty,
            "## Dependency Order",
            string.Empty,
        ]);

        foreach (string child in family.DependencyOrder)
        {
            lines.Add($"- {child}");
        }

        await artifacts.WriteAsync(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        return path;
    }

    public async Task<bool> ExistsForChildAsync(string childEpicPath)
    {
        IReadOnlyList<string> families = await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md");
        foreach (string family in families)
        {
            string? content = await artifacts.ReadAsync(family);
            if (content?.Contains(childEpicPath, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }
}
