using LoopRelay.Infrastructure.Console;

namespace LoopRelay.Infrastructure.Diagnostics;

public sealed record InputWaitProgressSnapshot(
    int PromptTokensEstimated,
    TimeSpan Elapsed,
    bool HasFirstOutput);
