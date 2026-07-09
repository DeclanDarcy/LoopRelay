namespace LoopRelay.Permissions.Models.Policy;

public sealed record NonImplementationArtifactPolicyOptions(
    bool AllowHitlRequestedNonImplementationFiles,
    bool AllowAuxiliaryNonImplementationFiles)
{
    public static NonImplementationArtifactPolicyOptions Default { get; } =
        new(
            AllowHitlRequestedNonImplementationFiles: false,
            AllowAuxiliaryNonImplementationFiles: false);
}
