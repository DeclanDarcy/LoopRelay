namespace LoopRelay.Permissions.Primitives.Parsing;

public readonly record struct ParseResult(
    ParsedCommand[] Commands,
    bool HasUnknownSyntax,
    string? UnknownSyntaxReason);
