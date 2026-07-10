namespace LoopRelay.Cli.Services.Decisions;

/// <summary>
/// The decision-session resume kill switch. Enabled by default; set
/// <c>LoopRelay_DECISION_RESUME=0</c> (or <c>false</c>) to skip the resume-on-open attempt — persist
/// and clear behavior is unchanged either way. Mirrors LoopRelay_SESSION_LOG. Insurance against
/// thread/resume behavioral surprises: the app-server protocol is still marked experimental upstream.
/// </summary>
internal static class DecisionResumeComposition
{
    public const string RecoveryPolicyVariable = "LoopRelay_DECISION_RECOVERY_POLICY";

    public static bool IsEnabled()
    {
        string? flag = Environment.GetEnvironmentVariable("LoopRelay_DECISION_RESUME");
        return !(string.Equals(flag, "0", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase));
    }

    public static DecisionRecoveryPolicy RecoveryPolicy()
    {
        string? value = Environment.GetEnvironmentVariable(RecoveryPolicyVariable);
        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "resume-only" => DecisionRecoveryPolicy.ResumeOnly,
            "reconstructed" => DecisionRecoveryPolicy.Reconstructed,
            "certified" => DecisionRecoveryPolicy.Certified,
            _ => throw new InvalidOperationException(
                $"{RecoveryPolicyVariable} must be resume-only, reconstructed, or certified."),
        };
    }
}

internal enum DecisionRecoveryPolicy
{
    ResumeOnly,
    Reconstructed,
    Certified,
}
