namespace LoopRelay.Permissions.Models.Policy;

public sealed record NonImplementationArtifactPolicyOptions(bool AllowHitlRequestedNonImplementationFiles)
{
    public static NonImplementationArtifactPolicyOptions Default { get; } =
        new(AllowHitlRequestedNonImplementationFiles: false);
}
