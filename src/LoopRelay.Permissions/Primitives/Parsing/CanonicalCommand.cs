namespace LoopRelay.Permissions.Primitives.Parsing;

public readonly record struct CanonicalCommand(
    string Command,
    string? Subcommand,
    string[] Flags,
    string[] Args);
