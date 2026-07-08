using System.Collections.Frozen;
using System.Text.Json;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Configuration;

public sealed record CliSettingsLoadResult(
    PermissionPolicyOptions Permissions,
    NonImplementationArtifactPolicyOptions ArtifactPolicy,
    string Path,
    bool IsDefaultTemplate);
