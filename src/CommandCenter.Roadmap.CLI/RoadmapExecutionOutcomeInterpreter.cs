using System.Text;

namespace CommandCenter.Roadmap.Cli;

internal sealed class RoadmapExecutionOutcomeInterpreter
{
    private readonly ExecutionDispositionParser dispositionParser = new();

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
            return disposition.Status switch
            {
                ExecutionDispositionStatus.EpicComplete => RoadmapExecutionOutcome.EpicComplete(disposition),
                ExecutionDispositionStatus.ContinueRequired => RoadmapExecutionOutcome.ContinueRequired(disposition),
                ExecutionDispositionStatus.ExecutionBlocked => RoadmapExecutionOutcome.ExecutionBlocked(disposition),
                _ => RoadmapExecutionOutcome.MalformedOutput($"Unsupported execution disposition: {disposition.Status}."),
            };
        }
        catch (MarkdownParseException exception)
        {
            return RoadmapExecutionOutcome.MalformedOutput(exception.Message);
        }
    }
}

internal sealed class ExecutionDispositionParser
{
    private static readonly IReadOnlyDictionary<string, ExecutionDispositionStatus> Statuses =
        new Dictionary<string, ExecutionDispositionStatus>(StringComparer.Ordinal)
        {
            ["Epic Complete"] = ExecutionDispositionStatus.EpicComplete,
            ["Continue Required"] = ExecutionDispositionStatus.ContinueRequired,
            ["Execution Blocked"] = ExecutionDispositionStatus.ExecutionBlocked,
        };

    public ExecutionDisposition Parse(string markdown)
    {
        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTable(markdown, "## Execution Disposition");
        return new ExecutionDisposition(
            RequiredStatus(fields),
            RequiredAllowed(fields, "Confidence", CommonAllowedValues.Confidence),
            Required(fields, "Evidence Summary"),
            Required(fields, "Next Step"));
    }

    private static ExecutionDispositionStatus RequiredStatus(IReadOnlyDictionary<string, string> fields)
    {
        string value = Required(fields, "Status");
        return Statuses.TryGetValue(value, out ExecutionDispositionStatus status)
            ? status
            : throw new MarkdownParseException($"Execution disposition status has unsupported value `{value}`.");
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

internal sealed record ExecutionDisposition(
    ExecutionDispositionStatus Status,
    string Confidence,
    string EvidenceSummary,
    string NextStep)
{
    public string StatusText => Status switch
    {
        ExecutionDispositionStatus.EpicComplete => "Epic Complete",
        ExecutionDispositionStatus.ContinueRequired => "Continue Required",
        ExecutionDispositionStatus.ExecutionBlocked => "Execution Blocked",
        _ => Status.ToString(),
    };
}

internal enum ExecutionDispositionStatus
{
    EpicComplete,
    ContinueRequired,
    ExecutionBlocked,
}

internal sealed record RoadmapExecutionOutcome(
    RoadmapExecutionOutcomeKind Kind,
    string Message,
    ExecutionDisposition? Disposition)
{
    public static RoadmapExecutionOutcome EpicComplete(ExecutionDisposition disposition) =>
        new(RoadmapExecutionOutcomeKind.EpicComplete, disposition.EvidenceSummary, disposition);

    public static RoadmapExecutionOutcome ContinueRequired(ExecutionDisposition disposition) =>
        new(RoadmapExecutionOutcomeKind.ContinueRequired, disposition.EvidenceSummary, disposition);

    public static RoadmapExecutionOutcome ExecutionBlocked(ExecutionDisposition disposition) =>
        new(RoadmapExecutionOutcomeKind.ExecutionBlocked, disposition.EvidenceSummary, disposition);

    public static RoadmapExecutionOutcome RuntimeFailure(string message) =>
        new(RoadmapExecutionOutcomeKind.RuntimeFailure, message, null);

    public static RoadmapExecutionOutcome MalformedOutput(string message) =>
        new(RoadmapExecutionOutcomeKind.MalformedOutput, message, null);

    public string DecisionText => Kind switch
    {
        RoadmapExecutionOutcomeKind.EpicComplete => "Epic Complete",
        RoadmapExecutionOutcomeKind.ContinueRequired => "Continue Required",
        RoadmapExecutionOutcomeKind.ExecutionBlocked => "Execution Blocked",
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
            builder.AppendLine($"| Next Step | {Escape(disposition.NextStep)} |");
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
