namespace LoopRelay.Cli.Models;

/// <summary>
/// One row of the per-turn session telemetry log. Capacity fields are a remaining PERCENT (0–100) measured
/// by the post-turn probe, or null when the codex usage probe could not be read. Raw tokens =
/// <see cref="PromptTokens"/> + <see cref="OutputTokens"/>; <see cref="EffectiveTokens"/> is the router's
/// cache-adjusted cost. Many rows share one <see cref="CodexLogPath"/> (one rollout file per codex process
/// serves many turns).
/// </summary>
internal sealed record SessionTelemetryRecord(
    DateTimeOffset Timestamp,
    string RepoName,
    string? CodexLogPath,
    string SessionId,
    string SessionType,
    int TurnIndex,
    int PromptTokens,
    int OutputTokens,
    int CachedTokens,
    double EffectiveTokens,
    int? PostFiveHourPercent,
    int? PostWeeklyPercent,
    string? Transport = null,
    string? Model = null,
    int? PromptChars = null,
    int? PromptBytes = null,
    int? PromptTokensEstimated = null,
    string? TokenEstimateSource = null,
    DateTimeOffset? PromptPreparedAt = null,
    DateTimeOffset? RequestWriteStartedAt = null,
    DateTimeOffset? RequestSubmittedAt = null,
    DateTimeOffset? RequestAcceptedAt = null,
    DateTimeOffset? FirstProtocolEventAt = null,
    DateTimeOffset? FirstOutputAt = null,
    DateTimeOffset? CompletedAt = null,
    int? ReportedPromptTokens = null,
    int? ReportedCachedTokens = null,
    int? ReportedOutputTokens = null,
    string? InputWaitStatus = null,
    string? EstimatorVersion = null,
    string? ProviderThreadId = null,
    string? ProviderTurnId = null,
    string? ContinuityEvent = null,
    string? RecoveryAttemptId = null,
    string? RecoveryPlanDigest = null,
    string? RecoveryMechanism = null,
    string? RecoveryCompleteness = null,
    string? CertificationInvocationId = null,
    string? InvocationRole = null);
