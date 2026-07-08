using LoopRelay.Permissions.Abstractions.Parsing;
using LoopRelay.Permissions.Primitives.Parsing;

namespace LoopRelay.Permissions.Services.Parsing;

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
