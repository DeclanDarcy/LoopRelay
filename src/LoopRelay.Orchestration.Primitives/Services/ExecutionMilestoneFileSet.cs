namespace LoopRelay.Orchestration.Services;

public sealed record ExecutionMilestoneFileSetResult(
    bool IsValid,
    IReadOnlyList<string> InvalidFiles,
    IReadOnlyList<string> DuplicateIdentities)
{
    public string Explanation => InvalidFiles.Count > 0
        ? $"Execution milestone filenames must use `m<number>` or `m<number>-<label>` identities; invalid: {string.Join(", ", InvalidFiles)}."
        : DuplicateIdentities.Count > 0
            ? $"Execution milestone identities must be unique; duplicate identities: {string.Join(", ", DuplicateIdentities)}."
            : "Execution milestone filenames have valid, unique identities.";
}

public static class ExecutionMilestoneFileSet
{
    public static ExecutionMilestoneFileSetResult Evaluate(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var invalid = new List<string>();
        var identities = new List<string>();
        foreach (string path in paths)
        {
            string file = Path.GetFileName(path);
            string stem = Path.GetFileNameWithoutExtension(file);
            if (!TryIdentity(stem, out string identity))
            {
                invalid.Add(file);
                continue;
            }

            identities.Add(identity);
        }

        string[] duplicates = identities
            .GroupBy(identity => identity, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"M{group.Key}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] invalidFiles = invalid.Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ExecutionMilestoneFileSetResult(
            invalidFiles.Length == 0 && duplicates.Length == 0,
            invalidFiles,
            duplicates);
    }

    private static bool TryIdentity(string stem, out string identity)
    {
        identity = string.Empty;
        if (stem.Length < 2 || char.ToLowerInvariant(stem[0]) != 'm')
        {
            return false;
        }

        int end = 1;
        while (end < stem.Length && char.IsAsciiDigit(stem[end]))
        {
            end++;
        }

        if (end == 1 || (end < stem.Length && stem[end] is not ('-' or '_' or '.')) ||
            (end < stem.Length && end == stem.Length - 1))
        {
            return false;
        }

        string digits = stem[1..end].TrimStart('0');
        identity = digits.Length == 0 ? "0" : digits;
        return true;
    }
}
