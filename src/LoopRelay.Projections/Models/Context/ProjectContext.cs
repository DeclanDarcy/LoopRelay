namespace LoopRelay.Projections.Models;

public sealed record ProjectContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
