using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Models;

public sealed record PermissionRequestDetails(
    PermissionRequestKind Kind,
    string Method,
    string? Command = null,
    string? Cwd = null,
    bool RequestsNetwork = false,
    string? FileOperation = null,
    string? FilePath = null,
    string? GrantRoot = null,
    string? ToolName = null,
    IReadOnlyList<string>? PathArguments = null,
    PermissionPathAccess PathAccess = PermissionPathAccess.Unknown);
