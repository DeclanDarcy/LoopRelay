namespace LoopRelay.Projections.Models.Context;

public sealed record ProjectContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
