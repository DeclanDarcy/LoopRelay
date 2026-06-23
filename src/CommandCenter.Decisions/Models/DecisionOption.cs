using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionOption(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<DecisionEvidence> Evidence)
{
    public DecisionOptionType Type { get; init; } = DecisionOptionType.Adopt;

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
