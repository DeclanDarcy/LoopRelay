using LoopRelay.Permissions.Models.Evaluation;

namespace LoopRelay.Permissions.Primitives.Requests;

public readonly record struct PermissionRequest(
    string RequestId,
    string ToolName,
    string? RawCommand,
    string RepoIdentity,
    string WorkingDirectory,
    bool RequestIdIsString = false,
    PermissionRequestDetails? Details = null);
