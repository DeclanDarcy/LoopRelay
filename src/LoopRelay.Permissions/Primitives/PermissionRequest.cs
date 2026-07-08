using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Primitives;

public readonly record struct PermissionRequest(
    string RequestId,
    string ToolName,
    string? RawCommand,
    string RepoIdentity,
    string WorkingDirectory,
    bool RequestIdIsString = false,
    PermissionRequestDetails? Details = null);
