using LoopRelay.Cli.Services;

namespace LoopRelay.Cli.Models;

/// <summary>
/// A snapshot of Codex quota. Percentages are the capacity REMAINING (0 = exhausted); the durations are
/// how long until each window resets. Populated live from the codex app-server
/// <c>account/rateLimits/read</c> response (see <see cref="CodexRateLimitsParser"/>).
/// </summary>
internal sealed record CodexUsageStatus(
    int FiveHourRemainingPercent,
    TimeSpan FiveHourTimeUntilReset,
    int WeeklyRemainingPercent,
    TimeSpan WeeklyTimeUntilReset);
