namespace LoopRelay.Roadmap.Cli.Models.Projections;

internal sealed record ProjectContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
