using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Completion;

public sealed record CompletedEpicArchiveResult(
    int Index,
    string ArchiveDirectory,
    string SynthesisPath,
    string SynthesisContent);
