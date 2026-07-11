namespace LoopRelay.Permissions.Models.Configuration;

/// <summary>
/// The workspace-config layer of the operational policy, exactly as configured: every field is
/// nullable so the policy resolver can distinguish "configured in the settings file" from
/// "falls through to the built-in default" when recording provenance. Defaults are owned by
/// the resolver, not by this document.
/// </summary>
public sealed record CliPolicyDocument(
    bool? AllowHitlRequestedNonImplementationFiles,
    bool? AllowAuxiliaryNonImplementationFiles,
    int? MaxUnboundedContinuationSteps,
    int? MaxNoChangesCommits,
    int? OperationalContextGrowthWarningStreak,
    bool? DecisionSessionResume)
{
    public static CliPolicyDocument Empty { get; } = new(null, null, null, null, null, null);
}
