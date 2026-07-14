using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Permissions.Models.Evaluation;

public sealed record PermissionGatewayContext(
    string RepoIdentity,
    string WorkingDirectory,
    OperationPermissionProfile? OperationScope = null);
