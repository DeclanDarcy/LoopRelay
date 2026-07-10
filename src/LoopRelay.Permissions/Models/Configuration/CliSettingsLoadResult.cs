using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Permissions.Models.Configuration;

public sealed record CliSettingsLoadResult(
    BrainConfiguration Brain,
    PermissionPolicyOptions Permissions,
    NonImplementationArtifactPolicyOptions ArtifactPolicy,
    string Path,
    bool IsDefaultTemplate);
