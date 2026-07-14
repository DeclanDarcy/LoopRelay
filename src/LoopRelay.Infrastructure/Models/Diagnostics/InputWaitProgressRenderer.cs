namespace LoopRelay.Infrastructure.Models.Diagnostics;

public sealed record InputWaitProgressSnapshot(
    int PromptTokensEstimated,
    TimeSpan Elapsed,
    bool HasFirstOutput);
