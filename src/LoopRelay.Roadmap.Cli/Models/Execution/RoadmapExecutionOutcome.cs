using LoopRelay.Roadmap.Cli.Primitives.Execution;
using LoopRelay.Roadmap.Cli.Services.Execution;

namespace LoopRelay.Roadmap.Cli.Models.Execution;

internal sealed record RoadmapExecutionOutcome(
    RoadmapExecutionOutcomeKind Kind,
    string Message,
    ExecutionDisposition? Disposition,
    ExecutionDispositionValidationResult? ProtocolValidation)
{
    public static RoadmapExecutionOutcome Validated(ExecutionDispositionValidationResult validation)
    {
        if (!validation.IsValid || validation.Route is null)
        {
            throw new ArgumentException("A valid execution protocol validation result is required.", nameof(validation));
        }

        return new(validation.Route.OutcomeKind, validation.Disposition.EvidenceSummary, validation.Disposition, validation);
    }

    public static RoadmapExecutionOutcome RuntimeFailure(string message) =>
        new(RoadmapExecutionOutcomeKind.RuntimeFailure, message, null, null);

    public static RoadmapExecutionOutcome MalformedOutput(
        string message,
        ExecutionDisposition? disposition = null,
        ExecutionDispositionValidationResult? protocolValidation = null) =>
        new(RoadmapExecutionOutcomeKind.MalformedOutput, message, disposition, protocolValidation);

    public ExecutionDispositionRoute RequireValidatedRoute() =>
        ProtocolValidation?.Route
        ?? throw new InvalidOperationException($"Execution outcome `{Kind}` does not have a validated execution protocol route.");

    public string DecisionText => Kind switch
    {
        RoadmapExecutionOutcomeKind.EpicComplete => ExecutionDispositionProtocol.StatusText(ExecutionDispositionStatus.EpicComplete),
        RoadmapExecutionOutcomeKind.ContinueRequired => ExecutionDispositionProtocol.StatusText(ExecutionDispositionStatus.ContinueRequired),
        RoadmapExecutionOutcomeKind.ExecutionBlocked => ExecutionDispositionProtocol.StatusText(ExecutionDispositionStatus.ExecutionBlocked),
        RoadmapExecutionOutcomeKind.RuntimeFailure => "Runtime Failure",
        RoadmapExecutionOutcomeKind.MalformedOutput => "Malformed Execution Output",
        _ => Kind.ToString(),
    };
}
