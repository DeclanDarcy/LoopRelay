using System.Collections.Frozen;

namespace LoopRelay.Permissions.Models;

public sealed class RecursiveForceDeleteOptions
{
    internal RecursiveForceDeleteOptions(
        string command,
        IReadOnlyList<IReadOnlySet<string>> flagSets)
    {
        Command = command;
        FlagSets = flagSets;
    }

    public string Command { get; }

    public IReadOnlyList<IReadOnlySet<string>> FlagSets { get; }
}
