namespace LoopRelay.Core.Abstractions.Persistence;

public sealed record ExecutionEvidenceRecord(
    string Stem,
    int Sequence,
    string RelativePath,
    string Content);

public interface IExecutionEvidenceStore
{
    Task<ExecutionEvidenceRecord> WriteAsync(string stem, string content);

    Task<string> NextPathAsync(string stem);

    Task<ExecutionEvidenceRecord?> ReadAsync(string relativePath);

    Task<IReadOnlyList<ExecutionEvidenceRecord>> ListAsync(string searchPattern = "*.md");
}
