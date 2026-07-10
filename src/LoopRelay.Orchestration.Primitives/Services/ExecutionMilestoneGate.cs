using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Services;

public sealed record ExecutionMilestoneGateResult(
    int TotalCheckboxes,
    int CompletedCheckboxes,
    IReadOnlyList<string> UntickedItems,
    IReadOnlyList<string> Evidence)
{
    public bool ReadinessSatisfied => TotalCheckboxes > 0;

    public bool CompletionSatisfied => TotalCheckboxes > 0 && CompletedCheckboxes == TotalCheckboxes;

    public ProductValidationState MilestoneSetValidationState =>
        !ReadinessSatisfied
            ? ProductValidationState.Invalid
            : CompletionSatisfied
                ? ProductValidationState.Valid
                : ProductValidationState.Unknown;
}

public static class ExecutionMilestoneGate
{
    public static ExecutionMilestoneGateResult Evaluate(
        string repositoryRoot,
        IReadOnlyList<string> relativePaths)
    {
        string root = Path.GetFullPath(repositoryRoot);
        var evidence = new List<string>();
        var unticked = new List<string>();
        int total = 0;
        int completed = 0;

        foreach (string relativePath in relativePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            string absolutePath = Path.Combine(root, Normalize(relativePath));
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            string content = File.ReadAllText(absolutePath);
            (int fileTotal, int fileCompleted, IReadOnlyList<string> fileUnticked) = CountCheckboxes(content);
            total += fileTotal;
            completed += fileCompleted;
            foreach (string item in fileUnticked)
            {
                unticked.Add(item);
            }

            evidence.Add(relativePath);
        }

        return new ExecutionMilestoneGateResult(total, completed, unticked, evidence);
    }

    public static (int Total, int Completed, IReadOnlyList<string> Unticked) CountCheckboxes(string content)
    {
        int total = 0;
        int completed = 0;
        var unticked = new List<string>();
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
                unticked.Add(line.TrimEnd().ToString());
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (total, completed, unticked);
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}
