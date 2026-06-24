using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Workflow.Persistence;

public static class WorkflowArtifactPaths
{
    public const string SchemaVersion = "workflow.v1";
    public const string WorkflowRoot = ".agents/workflow";
    public const string TimelinesRoot = ".agents/workflow/timelines";
    public const string ContinuationRoot = ".agents/workflow/continuation";
    public const string PreparationRoot = ".agents/workflow/preparation";
    public const string ReportsRoot = ".agents/workflow/reports";

    public static string TimelineJson(string timelineId) =>
        ArtifactPath.CombineRelative(TimelinesRoot, $"{ValidateTimelineId(timelineId)}.json");

    public static string TimelineMarkdown(string timelineId) =>
        ArtifactPath.CombineRelative(TimelinesRoot, $"{ValidateTimelineId(timelineId)}.md");

    public static string ContinuationJson(string eventId) =>
        ArtifactPath.CombineRelative(ContinuationRoot, $"{ValidateContinuationEventId(eventId)}.json");

    public static string ContinuationMarkdown(string eventId) =>
        ArtifactPath.CombineRelative(ContinuationRoot, $"{ValidateContinuationEventId(eventId)}.md");

    public static string PreparationJson(string eventId) =>
        ArtifactPath.CombineRelative(PreparationRoot, $"{ValidatePreparationEventId(eventId)}.json");

    public static string PreparationMarkdown(string eventId) =>
        ArtifactPath.CombineRelative(PreparationRoot, $"{ValidatePreparationEventId(eventId)}.md");

    public static string ReportJson(string reportId) =>
        ArtifactPath.CombineRelative(ReportsRoot, $"{ValidateReportId(reportId)}.json");

    public static string ReportMarkdown(string reportId) =>
        ArtifactPath.CombineRelative(ReportsRoot, $"{ValidateReportId(reportId)}.md");

    public static string Resolve(Repository repository, string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    public static string TimelineId(DateTimeOffset generatedAt) =>
        $"workflow.{generatedAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}";

    public static string ContinuationEventId(DateTimeOffset occurredAt) =>
        $"continuation.{occurredAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}";

    public static string PreparationEventId(DateTimeOffset occurredAt) =>
        $"preparation.{occurredAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}";

    public static string ValidateTimelineId(string timelineId)
    {
        if (string.IsNullOrWhiteSpace(timelineId) ||
            !timelineId.StartsWith("workflow.", StringComparison.Ordinal) ||
            timelineId.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Invalid workflow timeline id.", nameof(timelineId));
        }

        return timelineId;
    }

    public static string ValidateReportId(string reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId) ||
            reportId.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Invalid workflow report id.", nameof(reportId));
        }

        return reportId;
    }

    public static string ValidateContinuationEventId(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) ||
            !eventId.StartsWith("continuation.", StringComparison.Ordinal) ||
            eventId.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Invalid workflow continuation event id.", nameof(eventId));
        }

        return eventId;
    }

    public static string ValidatePreparationEventId(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) ||
            !eventId.StartsWith("preparation.", StringComparison.Ordinal) ||
            eventId.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Invalid workflow preparation event id.", nameof(eventId));
        }

        return eventId;
    }
}
