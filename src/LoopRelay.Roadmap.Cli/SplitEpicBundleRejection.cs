using System.Text.RegularExpressions;

namespace LoopRelay.Roadmap.Cli;

internal sealed record SplitEpicBundleRejection(string Path, string Reason);
