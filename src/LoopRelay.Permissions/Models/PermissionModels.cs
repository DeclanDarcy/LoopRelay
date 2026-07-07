namespace LoopRelay.Permissions.Models;

public enum RuleDecision
{
    NoMatch,
    Allow,
    Deny,
}

public enum PermissionRequestKind
{
    Unknown,
    CommandExecution,
    FileChange,
    ToolCall,
    UserInput,
    McpElicitation,
    Permissions,
}

public enum PermissionPathAccess
{
    Unknown,
    Read,
    Write,
    Delete,
}

public sealed record OperationPathGlob(string Directory, string Pattern);

public sealed record OperationPermissionProfile(
    string Label,
    string RepositoryRoot,
    IReadOnlyList<string> AllowedReads,
    IReadOnlyList<OperationPathGlob> AllowedReadGlobs,
    IReadOnlyList<string> AllowedWrites,
    IReadOnlyList<OperationPathGlob> AllowedWriteGlobs);

public sealed record PermissionGatewayContext(
    string RepoIdentity,
    string WorkingDirectory,
    OperationPermissionProfile? OperationScope = null);

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

public readonly record struct PermissionRequest(
    string RequestId,
    string ToolName,
    string? RawCommand,
    string RepoIdentity,
    string WorkingDirectory,
    bool RequestIdIsString = false,
    PermissionRequestDetails? Details = null);

public readonly record struct PermissionResult(RuleDecision Decision, string Reason);

public readonly record struct ParseResult(
    ParsedCommand[] Commands,
    bool HasUnknownSyntax,
    string? UnknownSyntaxReason);

public readonly record struct ParsedCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);

public readonly record struct CanonicalCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);

public readonly record struct EvalResult(RuleDecision Decision, string Reason);

public readonly record struct CacheEntry(RuleDecision Decision, string Reason);
