namespace LoopRelay.Permissions.Models.Configuration;

public sealed record LegacyArtifactPolicyInputs(
    bool? AllowHitlRequestedNonImplementationFiles,
    bool? AllowAuxiliaryNonImplementationFiles);

/// <summary>
/// The workspace-config layer of the operational policy, exactly as configured: every field is
/// nullable so the policy resolver can distinguish "configured in the settings file" from
/// "falls through to the built-in default" when recording provenance. Defaults are owned by
/// the resolver, not by this document.
/// </summary>
public sealed record CliPolicyDocument(
    int? MaxUnboundedContinuationSteps,
    int? MaxNoChangesCommits,
    int? OperationalContextGrowthWarningStreak,
    bool? DecisionSessionResume,
    string? DecisionRecoveryStrategy = null,
    LegacyArtifactPolicyInputs? LegacyArtifactPolicy = null)
{
    public static CliPolicyDocument Empty { get; } = new(null, null, null, null, null, null);
}
