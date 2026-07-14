namespace LoopRelay.Permissions.Models.Policy;

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
