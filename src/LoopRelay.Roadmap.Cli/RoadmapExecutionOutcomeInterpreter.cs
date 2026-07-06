using System.Text;

namespace LoopRelay.Roadmap.Cli;

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

internal sealed class ExecutionDispositionParser
{
    public ExecutionDisposition Parse(string markdown)
    {
        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTable(markdown, "## Execution Disposition");
        return new ExecutionDisposition(
            RequiredStatus(fields),
            RequiredAllowed(fields, "Confidence", CommonAllowedValues.Confidence),
            Required(fields, "Evidence Summary"),
            RequiredCommand(fields));
    }

    private static ExecutionDispositionStatus RequiredStatus(IReadOnlyDictionary<string, string> fields)
    {
        string value = Required(fields, "Status");
        return ExecutionDispositionProtocol.TryParseStatus(value, out ExecutionDispositionStatus status)
            ? status
            : throw new MarkdownParseException($"Execution disposition status has unsupported value `{value}`.");
    }

    private static ExecutionDispositionCommand RequiredCommand(IReadOnlyDictionary<string, string> fields)
    {
        string value = Required(fields, "Next Step");
        return ExecutionDispositionProtocol.TryParseCommand(value, out ExecutionDispositionCommand command)
            ? command
            : throw new MarkdownParseException($"Execution disposition command has unsupported value `{value}`.");
    }

    private static string RequiredAllowed(IReadOnlyDictionary<string, string> fields, string field, IReadOnlyList<string> allowed)
    {
        string value = Required(fields, field);
        string? match = allowed.FirstOrDefault(allowedValue => string.Equals(allowedValue, value, StringComparison.Ordinal));
        return match ?? throw new MarkdownParseException($"Execution disposition field `{field}` has unsupported value `{value}`.");
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string field)
    {
        if (!fields.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new MarkdownParseException($"Required execution disposition field missing: {field}");
        }

        return value.Trim();
    }
}

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

internal enum RoadmapExecutionOutcomeKind
{
    EpicComplete,
    ContinueRequired,
    ExecutionBlocked,
    RuntimeFailure,
    MalformedOutput,
}

internal static class RoadmapExecutionEvidenceArtifact
{
    public static string Render(
        RoadmapExecutionTransportResult transport,
        RoadmapExecutionOutcome outcome,
        DateTimeOffset createdAt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Roadmap Execution Evidence");
        builder.AppendLine();
        builder.AppendLine("## Execution Interpretation");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Transport Status | {Escape(transport.Status.ToString())} |");
        builder.AppendLine($"| Agent State | {Escape(transport.AgentState)} |");
        builder.AppendLine($"| Outcome | {Escape(outcome.DecisionText)} |");
        builder.AppendLine($"| Message | {Escape(outcome.Message)} |");
        builder.AppendLine($"| Created At | {createdAt:O} |");

        if (!string.IsNullOrWhiteSpace(transport.Diagnostics))
        {
            builder.AppendLine($"| Diagnostics | {Escape(transport.Diagnostics)} |");
        }

        if (outcome.Disposition is { } disposition)
        {
            builder.AppendLine();
            builder.AppendLine("## Execution Disposition");
            builder.AppendLine();
            builder.AppendLine("| Field | Value |");
            builder.AppendLine("|---|---|");
            builder.AppendLine($"| Status | {Escape(disposition.StatusText)} |");
            builder.AppendLine($"| Confidence | {Escape(disposition.Confidence)} |");
            builder.AppendLine($"| Evidence Summary | {Escape(disposition.EvidenceSummary)} |");
            builder.AppendLine($"| Next Step | {Escape(disposition.NextStepText)} |");
        }

        if (outcome.ProtocolValidation is { } validation)
        {
            builder.AppendLine();
            builder.AppendLine("## Execution Protocol Validation");
            builder.AppendLine();
            builder.AppendLine("| Field | Value |");
            builder.AppendLine("|---|---|");
            builder.AppendLine($"| Result | {(validation.IsValid ? "Valid" : "Invalid")} |");
            builder.AppendLine($"| Required Recovery Path | {Escape(validation.RequiredRecoveryPath)} |");

            if (validation.Route is { } route)
            {
                builder.AppendLine($"| Validated Command | {Escape(ExecutionDispositionProtocol.CommandText(route.Command))} |");
                builder.AppendLine($"| Workflow Route | {Escape(route.WorkflowTransition)} |");
                builder.AppendLine($"| Target State | {Escape(route.TargetState.ToString())} |");
            }

            if (!string.IsNullOrWhiteSpace(validation.ViolationReason))
            {
                builder.AppendLine($"| Protocol Violation Reason | {Escape(validation.ViolationReason)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Raw Execution Output");
        builder.AppendLine();
        AppendIndented(builder, string.IsNullOrWhiteSpace(transport.Output) ? "(empty)" : transport.Output);
        return builder.ToString();
    }

    private static void AppendIndented(StringBuilder builder, string content)
    {
        foreach (string line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.Append("    ").AppendLine(line);
        }
    }

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
