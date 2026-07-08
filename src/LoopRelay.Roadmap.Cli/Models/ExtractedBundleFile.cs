using System.Text.RegularExpressions;

namespace LoopRelay.Roadmap.Cli;

internal sealed record ExtractedBundleFile(string Path, string Content, string Hash);
