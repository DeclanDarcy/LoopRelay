namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ProjectContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
