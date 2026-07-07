namespace LoopRelay.Permissions.Models;

public enum RuleDecision
{
    NoMatch,
    Allow,
    Deny,
}

public readonly record struct PermissionRequest(
    string RequestId,
    string ToolName,
    string? RawCommand,
    string RepoIdentity,
    string WorkingDirectory,
    bool RequestIdIsString = false);

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
