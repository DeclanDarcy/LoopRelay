using System.Collections.Frozen;

namespace LoopRelay.Permissions.Models;

public sealed record NonImplementationArtifactPolicyOptions(bool AllowHitlRequestedNonImplementationFiles)
{
    public static NonImplementationArtifactPolicyOptions Default { get; } =
        new(AllowHitlRequestedNonImplementationFiles: false);
}
