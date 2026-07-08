namespace LoopRelay.Roadmap.Cli.Models.TransitionInputs;

internal sealed record TransitionInputContext(
    string? AuditEvidencePath = null,
    string? CompletionEvaluationPath = null,
    string? ExecutionEvidencePath = null)
{
    public static TransitionInputContext Empty { get; } = new();

    public static TransitionInputContext AuditEvidence(string path) =>
        new(AuditEvidencePath: path);

    public static TransitionInputContext CompletionEvaluation(string path) =>
        new(CompletionEvaluationPath: path);

    public static TransitionInputContext ExecutionEvidence(string path) =>
        new(ExecutionEvidencePath: path);
}
