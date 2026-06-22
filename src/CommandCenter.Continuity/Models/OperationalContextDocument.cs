namespace CommandCenter.Continuity.Models;

public sealed class OperationalContextDocument
{
    public string Title { get; init; } = "Operational Context";

    public IReadOnlyList<OperationalContextItem> CurrentMentalModel { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> Architecture { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> AuthorityBoundaries { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> Constraints { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> StableDecisions { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> DecisionRationale { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> OpenQuestions { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> ActiveRisks { get; init; } = [];

    public IReadOnlyList<OperationalContextItem> RecentUnderstandingChanges { get; init; } = [];

    public IReadOnlyList<OperationalContextSection> AdditionalSections { get; init; } = [];
}
