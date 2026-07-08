namespace LoopRelay.Completion.Models.Archive;

public sealed record CompletedEpicArchiveResult(
    int Index,
    string ArchiveDirectory,
    string SynthesisPath,
    string SynthesisContent);
