using System.Text.RegularExpressions;

namespace LoopRelay.Roadmap.Cli;

internal enum SplitEpicBundleInterpretationStatus
{
    Valid,
    Blocked,
    Invalid,
}
