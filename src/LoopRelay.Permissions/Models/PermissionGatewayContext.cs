namespace LoopRelay.Permissions.Models;

public sealed record PermissionGatewayContext(
    string RepoIdentity,
    string WorkingDirectory,
    OperationPermissionProfile? OperationScope = null);
