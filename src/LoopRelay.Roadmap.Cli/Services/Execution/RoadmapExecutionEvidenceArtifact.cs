using System.Text;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

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
