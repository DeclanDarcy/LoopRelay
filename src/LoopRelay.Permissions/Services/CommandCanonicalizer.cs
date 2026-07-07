using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class CommandCanonicalizer : ICommandCanonicalizer
{
    public CanonicalCommand[] Canonicalize(ParsedCommand[] commands) =>
        commands.Select(Canonicalize).ToArray();

    internal static CanonicalCommand Canonicalize(ParsedCommand parsed) =>
        new(
            parsed.Command.ToLowerInvariant(),
            parsed.Subcommand?.ToLowerInvariant(),
            parsed.Flags.Select(flag => flag.ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray(),
            parsed.Args.Select(arg => arg.Trim()).Where(arg => arg.Length > 0).ToArray());
}
