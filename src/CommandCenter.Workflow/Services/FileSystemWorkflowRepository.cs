using System.Text;
using System.Text.Json;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Persistence;

namespace CommandCenter.Workflow.Services;

public sealed class FileSystemWorkflowRepository(IArtifactStore artifactStore) : IWorkflowRepository
{
    public async Task<WorkflowTimeline> SaveTimelineAsync(Repository repository, WorkflowTimeline timeline)
    {
        if (timeline.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Workflow timeline belongs to a different repository.");
        }

        string timelineId = WorkflowArtifactPaths.TimelineId(timeline.GeneratedAt);
        var document = new WorkflowArtifactDocument<WorkflowTimeline>(
            WorkflowArtifactPaths.SchemaVersion,
            repository.Id,
            timeline.GeneratedAt,
            timeline);

        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.TimelineJson(timelineId)),
            JsonSerializer.Serialize(document, WorkflowJson.Options));
        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.TimelineMarkdown(timelineId)),
            RenderTimelineMarkdown(timeline));
        return timeline;
    }

    public async Task<WorkflowTimeline?> LoadTimelineAsync(Repository repository, string timelineId)
    {
        WorkflowArtifactPaths.ValidateTimelineId(timelineId);
        return await ReadTimelineAsync(repository, WorkflowArtifactPaths.TimelineJson(timelineId));
    }

    public async Task<IReadOnlyList<WorkflowTimeline>> ListTimelinesAsync(Repository repository)
    {
        string root = WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.TimelinesRoot);
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "*.json");
        var timelines = new List<WorkflowTimeline>();

        foreach (string file in files
            .Where(file => Path.GetFileName(file).StartsWith("workflow.", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string? timelineId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(timelineId))
            {
                continue;
            }

            WorkflowTimeline? timeline = await LoadTimelineAsync(repository, timelineId);
            if (timeline is not null)
            {
                timelines.Add(timeline);
            }
        }

        return timelines
            .OrderBy(timeline => timeline.GeneratedAt)
            .ToArray();
    }

    public async Task<WorkflowTimeline?> GetLatestTimelineAsync(Repository repository) =>
        (await ListTimelinesAsync(repository))
            .OrderByDescending(timeline => timeline.GeneratedAt)
            .FirstOrDefault();

    public async Task SaveReportAsync(Repository repository, string reportId, string jsonContent, string markdownContent)
    {
        WorkflowArtifactPaths.ValidateReportId(reportId);
        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.ReportJson(reportId)),
            jsonContent);
        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.ReportMarkdown(reportId)),
            markdownContent);
    }

    private async Task<WorkflowTimeline?> ReadTimelineAsync(Repository repository, string relativePath)
    {
        string? json = await artifactStore.ReadAsync(WorkflowArtifactPaths.Resolve(repository, relativePath));
        if (json is null)
        {
            return null;
        }

        WorkflowArtifactDocument<WorkflowTimeline>? document = JsonSerializer.Deserialize<WorkflowArtifactDocument<WorkflowTimeline>>(
            json,
            WorkflowJson.Options);
        if (document is null)
        {
            return null;
        }

        if (!string.Equals(document.SchemaVersion, WorkflowArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported workflow artifact schema version '{document.SchemaVersion}'.");
        }

        if (document.RepositoryId != repository.Id || document.Payload.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Workflow artifact belongs to a different repository.");
        }

        return document.Payload;
    }

    private static string RenderTimelineMarkdown(WorkflowTimeline timeline)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Workflow Timeline");
        builder.AppendLine();
        builder.AppendLine($"Repository: {timeline.RepositoryId}");
        builder.AppendLine($"Generated At: {timeline.GeneratedAt:O}");
        builder.AppendLine($"Workflow Fingerprint: {timeline.Fingerprint}");
        builder.AppendLine($"Current Stage: {timeline.CurrentStage}");
        builder.AppendLine($"Previous Stage: {timeline.PreviousStage}");
        builder.AppendLine($"Progress State: {timeline.ProgressState}");
        builder.AppendLine($"Blocking Gate: {timeline.BlockingGate}");
        builder.AppendLine();
        builder.AppendLine("## Timeline Entries");

        if (timeline.Entries.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("- None");
            return builder.ToString();
        }

        foreach (WorkflowTimelineEntry entry in timeline.Entries)
        {
            builder.AppendLine();
            builder.AppendLine($"- {entry.OccurredAt:O} | {entry.Stage} | {entry.EventType} | {entry.Summary}");
            builder.AppendLine($"  - Source: {entry.SourceDomain}:{entry.SourceArtifact}");
            builder.AppendLine($"  - Fingerprint: {entry.Fingerprint}");
        }

        return builder.ToString();
    }
}
