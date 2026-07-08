namespace LoopRelay.Permissions.Primitives;

public readonly record struct ParseResult(
    ParsedCommand[] Commands,
    bool HasUnknownSyntax,
    string? UnknownSyntaxReason);
