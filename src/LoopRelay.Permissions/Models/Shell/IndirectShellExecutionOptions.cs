using System.Collections.Frozen;

namespace LoopRelay.Permissions.Models;

public sealed class IndirectShellExecutionOptions
{
    internal IndirectShellExecutionOptions(
        IReadOnlySet<string> commands,
        string flag)
    {
        Commands = commands;
        Flag = flag;
    }

    public IReadOnlySet<string> Commands { get; }

    public string Flag { get; }
}
