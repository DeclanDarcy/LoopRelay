namespace LoopRelay.Permissions.Models;

public readonly record struct CanonicalCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);
