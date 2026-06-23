using System.Text;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Projections;

public sealed class ReasoningArtifactProjectionService : IReasoningArtifactProjectionService
{
    public string RenderEvent(ReasoningEvent reasoningEvent)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Reasoning Event {reasoningEvent.Id}");
        builder.AppendLine();
        builder.AppendLine($"- Event ID: {reasoningEvent.Id}");
        builder.AppendLine($"- Event Family: {reasoningEvent.Family}");
        builder.AppendLine($"- Event Type: {reasoningEvent.Type}");
        builder.AppendLine($"- Timestamp: {reasoningEvent.CreatedAt:O}");
        builder.AppendLine($"- Title: {reasoningEvent.Title}");
        builder.AppendLine();
        builder.AppendLine("## Narrative");
        builder.AppendLine();
        builder.AppendLine(reasoningEvent.Narrative.Summary);
        if (!string.IsNullOrWhiteSpace(reasoningEvent.Narrative.Details))
        {
            builder.AppendLine();
            builder.AppendLine(reasoningEvent.Narrative.Details);
        }

        builder.AppendLine();
        builder.AppendLine("## Threads");
        AppendValues(builder, reasoningEvent.ThreadIds);
        builder.AppendLine();
        builder.AppendLine("## References");
        AppendReferences(builder, reasoningEvent.References);
        builder.AppendLine();
        builder.AppendLine("## Provenance");
        AppendProvenance(builder, reasoningEvent.Provenance);
        builder.AppendLine();
        builder.AppendLine("## Tags");
        AppendValues(builder, reasoningEvent.Tags);
        builder.AppendLine();
        builder.AppendLine("## Diagnostics");
        builder.AppendLine();
        builder.AppendLine("- Markdown projection is generated from event.json.");
        return builder.ToString();
    }

    public string RenderThread(ReasoningThread thread, IReadOnlyList<ReasoningRelationship> relationships)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Reasoning Thread {thread.Id}");
        builder.AppendLine();
        builder.AppendLine($"- Thread ID: {thread.Id}");
        builder.AppendLine($"- Theme: {thread.Theme}");
        builder.AppendLine($"- Title: {thread.Title}");
        builder.AppendLine($"- Created At: {thread.CreatedAt:O}");
        builder.AppendLine($"- Updated At: {thread.UpdatedAt:O}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(thread.Summary);
        builder.AppendLine();
        builder.AppendLine("## Events");
        AppendValues(builder, thread.EventIds);
        builder.AppendLine();
        builder.AppendLine("## Relationships");
        AppendValues(builder, relationships.Select(relationship => relationship.Id).ToArray());
        builder.AppendLine();
        builder.AppendLine("## Derived Status");
        builder.AppendLine();
        builder.AppendLine("- Derived from event sequence; not authoritative lifecycle state.");
        builder.AppendLine();
        builder.AppendLine("## Diagnostics");
        builder.AppendLine();
        builder.AppendLine("- Markdown projection is generated from thread.json.");
        return builder.ToString();
    }

    public string RenderRelationship(ReasoningRelationship relationship)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Reasoning Relationship {relationship.Id}");
        builder.AppendLine();
        builder.AppendLine($"- Relationship ID: {relationship.Id}");
        builder.AppendLine($"- Type: {relationship.Type}");
        builder.AppendLine($"- Created At: {relationship.CreatedAt:O}");
        builder.AppendLine();
        builder.AppendLine("## Source");
        AppendReference(builder, relationship.Source);
        builder.AppendLine();
        builder.AppendLine("## Target");
        AppendReference(builder, relationship.Target);
        builder.AppendLine();
        builder.AppendLine("## Narrative");
        builder.AppendLine();
        builder.AppendLine(relationship.Narrative.Summary);
        if (!string.IsNullOrWhiteSpace(relationship.Narrative.Details))
        {
            builder.AppendLine();
            builder.AppendLine(relationship.Narrative.Details);
        }

        builder.AppendLine();
        builder.AppendLine("## Provenance");
        AppendProvenance(builder, relationship.Provenance);
        builder.AppendLine();
        builder.AppendLine("## Diagnostics");
        builder.AppendLine();
        builder.AppendLine("- Markdown projection is generated from relationship.json.");
        return builder.ToString();
    }

    public string RenderCertificationReport(ReasoningCertificationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Reasoning Certification {report.Id}");
        builder.AppendLine();
        builder.AppendLine($"- Report ID: {report.Id}");
        builder.AppendLine($"- Generated At: {report.GeneratedAt:O}");
        builder.AppendLine($"- Result: {report.Result.Kind}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(report.Result.Summary);
        builder.AppendLine();
        builder.AppendLine("## Evidence");
        builder.AppendLine();
        if (report.Evidence.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (ReasoningCertificationEvidence evidence in report.Evidence.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                builder.AppendLine($"### {evidence.Id}: {evidence.Scenario}");
                builder.AppendLine();
                builder.AppendLine($"- Passed: {evidence.Passed}");
                builder.AppendLine($"- Summary: {evidence.Summary}");
                builder.AppendLine("- Details:");
                AppendValues(builder, evidence.Details);
                builder.AppendLine("- References:");
                AppendReferences(builder, evidence.References);
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Diagnostics");
        AppendValues(builder, report.Diagnostics);
        builder.AppendLine();
        builder.AppendLine("- Markdown projection is generated from certification report JSON.");
        return builder.ToString();
    }

    private static void AppendValues(StringBuilder builder, IReadOnlyList<string> values)
    {
        builder.AppendLine();
        if (values.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        foreach (string value in values.Order(StringComparer.Ordinal))
        {
            builder.AppendLine($"- {value}");
        }
    }

    private static void AppendReferences(StringBuilder builder, IReadOnlyList<ReasoningReference> references)
    {
        builder.AppendLine();
        if (references.Count == 0)
        {
            builder.AppendLine("- None");
            return;
        }

        foreach (ReasoningReference reference in references.OrderBy(reference => reference.Kind).ThenBy(reference => reference.Id, StringComparer.Ordinal))
        {
            AppendReference(builder, reference);
        }
    }

    private static void AppendReference(StringBuilder builder, ReasoningReference reference)
    {
        builder.AppendLine($"- Kind: {reference.Kind}");
        builder.AppendLine($"  Id: {reference.Id}");
        AppendOptional(builder, "Relative Path", reference.RelativePath);
        AppendOptional(builder, "Section", reference.Section);
        AppendOptional(builder, "Excerpt", reference.Excerpt);
        AppendOptional(builder, "Fingerprint", reference.Fingerprint);
    }

    private static void AppendProvenance(StringBuilder builder, ReasoningProvenance provenance)
    {
        builder.AppendLine();
        builder.AppendLine($"- Source Kind: {provenance.SourceKind}");
        builder.AppendLine($"- Captured By: {provenance.CapturedBy}");
        AppendOptional(builder, "Relative Path", provenance.RelativePath);
        AppendOptional(builder, "Section", provenance.Section);
        AppendOptional(builder, "Excerpt", provenance.Excerpt);
        AppendOptional(builder, "Fingerprint", provenance.Fingerprint);
    }

    private static void AppendOptional(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"- {label}: {value}");
        }
    }
}
