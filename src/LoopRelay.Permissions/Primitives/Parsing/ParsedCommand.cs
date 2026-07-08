namespace LoopRelay.Permissions.Primitives.Parsing;

public readonly record struct ParsedCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);
