namespace LoopRelay.Permissions.Models;

public readonly record struct ParseResult(
    ParsedCommand[] Commands,
    bool HasUnknownSyntax,
    string? UnknownSyntaxReason);
