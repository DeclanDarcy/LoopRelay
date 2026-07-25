namespace LoopRelay.Core.Abstractions.Persistence;

public sealed record ExecutionEvidenceRecord(
    string Stem,
    int Sequence,
    string RelativePath,
    string Content);

/// <summary>
/// The identity of one piece of execution evidence without its body (PERF-14). Consumers that only
/// need to enumerate what exists - rather than read what it says - can be served without fetching
/// or hash-validating every document.
/// </summary>
public sealed record ExecutionEvidencePath(
    string Stem,
    int Sequence,
    string RelativePath);

public interface IExecutionEvidenceStore
{
    Task<ExecutionEvidenceRecord> WriteAsync(string stem, string content);

    Task<string> NextPathAsync(string stem);

    Task<ExecutionEvidenceRecord?> ReadAsync(string relativePath);

    Task<IReadOnlyList<ExecutionEvidenceRecord>> ListAsync(string searchPattern = "*.md");
}
