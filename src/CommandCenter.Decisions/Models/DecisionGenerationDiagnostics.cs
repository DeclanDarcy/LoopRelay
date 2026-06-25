namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationDiagnostics(
    int GeneratedOptionCount,
    int AcceptedOptionCount,
    int RejectedOptionCount,
    int DeduplicatedOptionCount,
    int FallbackOptionCount,
    IReadOnlyList<DecisionOptionValidationResult> OptionValidationResults,
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<DecisionOption> RejectedOptions { get; init; } = [];

    public IReadOnlyList<DecisionOption> DeduplicatedOptions { get; init; } = [];
}
