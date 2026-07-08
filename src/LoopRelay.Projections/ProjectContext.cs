using System.Text;
using System.Text.RegularExpressions;

namespace LoopRelay.Projections;

public sealed record ProjectContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
