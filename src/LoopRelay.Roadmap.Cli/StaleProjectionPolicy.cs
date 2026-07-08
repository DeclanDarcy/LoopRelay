using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli;

internal enum StaleProjectionPolicy
{
    Block,
    WarnOnly,
    Allow,
}
