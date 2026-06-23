namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationDiagnostics(
    int GeneratedOptionCount,
    int AcceptedOptionCount,
    int RejectedOptionCount,
    int DeduplicatedOptionCount,
    int FallbackOptionCount,
    IReadOnlyList<DecisionOptionValidationResult> OptionValidationResults,
    IReadOnlyList<string> Diagnostics);
