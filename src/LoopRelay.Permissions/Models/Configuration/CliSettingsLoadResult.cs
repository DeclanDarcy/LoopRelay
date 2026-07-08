namespace LoopRelay.Permissions.Models.Configuration;

public sealed record CliSettingsLoadResult(
    PermissionPolicyOptions Permissions,
    NonImplementationArtifactPolicyOptions ArtifactPolicy,
    string Path,
    bool IsDefaultTemplate);
