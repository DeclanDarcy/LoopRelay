using System.Text.RegularExpressions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;

namespace LoopRelay.Core.Services.Persistence;

public sealed partial class FileBackedExecutionEvidenceStore(IArtifactStore _store) : IExecutionEvidenceStore
{
    public const string ExecutionEvidenceDirectory = ".agents/evidence/execution";

    public async Task<ExecutionEvidenceRecord> WriteAsync(string stem, string content)
    {
        (int sequence, string path) = await NextAsync(stem);
        await _store.WriteAsync(path, content);
        return new ExecutionEvidenceRecord(stem, sequence, path, content);
    }

    public async Task<string> NextPathAsync(string stem)
    {
        (_, string path) = await NextAsync(stem);
        return path;
    }

    public async Task<ExecutionEvidenceRecord?> ReadAsync(string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        Match match = ExecutionEvidencePathRegex().Match(normalizedPath);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out int sequence) || sequence <= 0)
        {
            return null;
        }

        string? content = await _store.ReadAsync(normalizedPath);
        return content is null
            ? null
            : new ExecutionEvidenceRecord(
                match.Groups["stem"].Value,
                sequence,
                normalizedPath,
                content);
    }

    public async Task<IReadOnlyList<ExecutionEvidenceRecord>> ListAsync(string searchPattern = "*.md")
    {
        IReadOnlyList<string> files = await _store.ListAsync(ExecutionEvidenceDirectory, searchPattern);
        var records = new List<ExecutionEvidenceRecord>();
        foreach (string file in files.Order(StringComparer.Ordinal))
        {
            if (await ReadAsync(file) is { } record)
            {
                records.Add(record);
            }
        }

        return records
            .OrderBy(record => record.Stem, StringComparer.Ordinal)
            .ThenBy(record => record.Sequence)
            .ToArray();
    }

    private async Task<(int Sequence, string Path)> NextAsync(string stem)
    {
        IReadOnlyList<string> files = await _store.ListAsync(
            ExecutionEvidenceDirectory,
            $"{stem}.*.md");
        int max = 0;
        foreach (string file in files)
        {
            Match match = NumberedEvidenceRegex().Match(Path.GetFileName(file));
            if (match.Success && int.TryParse(match.Groups["number"].Value, out int number))
            {
                max = Math.Max(max, number);
            }
        }

        int next = max + 1;
        return (next, $"{ExecutionEvidenceDirectory}/{stem}.{next:0000}.md");
    }

    [GeneratedRegex(@"\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedEvidenceRegex();

    [GeneratedRegex(@"^\.agents/evidence/execution/(?<stem>.+)\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionEvidencePathRegex();
}
