using System.Text;
using System.Text.RegularExpressions;

namespace LoopRelay.Roadmap.Cli;

internal sealed record ProjectContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
