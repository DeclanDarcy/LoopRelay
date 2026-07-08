namespace LoopRelay.Permissions.Primitives;

public readonly record struct ParsedCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);
