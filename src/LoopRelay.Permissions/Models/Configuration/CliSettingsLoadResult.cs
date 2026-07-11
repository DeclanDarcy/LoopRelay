using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Permissions.Models.Configuration;

public sealed record CliSettingsLoadResult(
    PermissionPolicyOptions Permissions,
    CliPolicyDocument Policy,
    string Path,
    bool IsDefaultTemplate);
