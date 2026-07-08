namespace LoopRelay.Permissions.Models;

public readonly record struct ParsedCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);
