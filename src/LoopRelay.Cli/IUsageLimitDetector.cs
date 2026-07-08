using System.Globalization;
using System.Text.RegularExpressions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

/// <summary>Detects codex "You've hit your usage limit" turn failures and waits out the advertised retry
/// time so the caller can retry the turn instead of failing the run.</summary>
internal interface IUsageLimitDetector
{
    /// <summary>Non-null when the failed result is a codex usage-limit error. Silent — safe to call on
    /// every result, including ones the caller decides not to retry.</summary>
    UsageLimitHit? Detect(AgentTurnResult result);

    /// <summary>Announces the hit on the console and sleeps out its wait.</summary>
    Task WaitOutAsync(UsageLimitHit hit, CancellationToken cancellationToken);

    /// <summary>Announces that the limit outlasted every waited retry, so the failure is about to
    /// surface — without this the operator cannot tell "capped on quota" from an unrelated crash.</summary>
    void WarnRetriesExhausted(int retries);
}
