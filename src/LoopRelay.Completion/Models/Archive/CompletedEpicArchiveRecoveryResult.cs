namespace LoopRelay.Completion.Models.Archive;

public sealed record CompletedEpicArchiveRecoveryResult(
    int ArchiveIndex,
    string ArchiveDirectory,
    IReadOnlyList<CompletedEpicArchiveRecord> Records);

public sealed record CompletedEpicArchiveRecord(
    string Domain,
    string LogicalPath,
    string ExportPath,
    string ContentHash);
