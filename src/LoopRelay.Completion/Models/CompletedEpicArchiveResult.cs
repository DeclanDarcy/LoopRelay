namespace LoopRelay.Completion.Models;

public sealed record CompletedEpicArchiveResult(
    int Index,
    string ArchiveDirectory,
    string SynthesisPath,
    string SynthesisContent);
