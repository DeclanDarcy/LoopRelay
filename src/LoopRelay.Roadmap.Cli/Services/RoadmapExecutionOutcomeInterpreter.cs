using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class RoadmapExecutionOutcomeInterpreter
{
    private readonly ExecutionDispositionParser dispositionParser = new();
    private readonly ExecutionDispositionPolicy dispositionPolicy = new();

    public RoadmapExecutionOutcome Interpret(RoadmapExecutionTransportResult transport)
    {
        if (transport.Status != ExecutionTransportStatus.Completed)
        {
            string message = string.IsNullOrWhiteSpace(transport.Diagnostics)
                ? $"Execution transport ended in state {transport.AgentState}."
                : transport.Diagnostics.Trim();
            return RoadmapExecutionOutcome.RuntimeFailure(message);
        }

        try
        {
            ExecutionDisposition disposition = dispositionParser.Parse(transport.Output);
            ExecutionDispositionValidationResult validation = dispositionPolicy.Validate(disposition);
            return validation.IsValid
                ? RoadmapExecutionOutcome.Validated(validation)
                : RoadmapExecutionOutcome.MalformedOutput(validation.ViolationReason!, disposition, validation);
        }
        catch (MarkdownParseException exception)
        {
            return RoadmapExecutionOutcome.MalformedOutput(exception.Message);
        }
    }
}
