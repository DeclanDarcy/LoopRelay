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

    public async Task<WorkflowContinuationEvent> SaveContinuationEventAsync(
        Repository repository,
        WorkflowContinuationEvent continuationEvent)
    {
        if (continuationEvent.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Workflow continuation event belongs to a different repository.");
        }

        WorkflowArtifactPaths.ValidateContinuationEventId(continuationEvent.EventId);
        var document = new WorkflowArtifactDocument<WorkflowContinuationEvent>(
            WorkflowArtifactPaths.SchemaVersion,
            repository.Id,
            continuationEvent.OccurredAt,
            continuationEvent);

        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.ContinuationJson(continuationEvent.EventId)),
            JsonSerializer.Serialize(document, WorkflowJson.Options));
        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.ContinuationMarkdown(continuationEvent.EventId)),
            RenderContinuationMarkdown(continuationEvent));
        return continuationEvent;
    }

    public async Task<WorkflowContinuationEvent?> LoadContinuationEventAsync(Repository repository, string eventId)
    {
        WorkflowArtifactPaths.ValidateContinuationEventId(eventId);
        string? json = await artifactStore.ReadAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.ContinuationJson(eventId)));
        if (json is null)
        {
            return null;
        }

        WorkflowArtifactDocument<WorkflowContinuationEvent>? document =
            JsonSerializer.Deserialize<WorkflowArtifactDocument<WorkflowContinuationEvent>>(json, WorkflowJson.Options);
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

    public async Task<IReadOnlyList<WorkflowContinuationEvent>> ListContinuationEventsAsync(Repository repository)
    {
        string root = WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.ContinuationRoot);
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "*.json");
        var events = new List<WorkflowContinuationEvent>();

        foreach (string file in files
            .Where(file => Path.GetFileName(file).StartsWith("continuation.", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string? eventId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                continue;
            }

            WorkflowContinuationEvent? continuationEvent = await LoadContinuationEventAsync(repository, eventId);
            if (continuationEvent is not null)
            {
                events.Add(continuationEvent);
            }
        }

        return events
            .OrderBy(continuationEvent => continuationEvent.OccurredAt)
            .ToArray();
    }

    public async Task<WorkflowPreparationEvent> SavePreparationEventAsync(
        Repository repository,
        WorkflowPreparationEvent preparationEvent)
    {
        if (preparationEvent.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Workflow preparation event belongs to a different repository.");
        }

        WorkflowArtifactPaths.ValidatePreparationEventId(preparationEvent.EventId);
        var document = new WorkflowArtifactDocument<WorkflowPreparationEvent>(
            WorkflowArtifactPaths.SchemaVersion,
            repository.Id,
            preparationEvent.OccurredAt,
            preparationEvent);

        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.PreparationJson(preparationEvent.EventId)),
            JsonSerializer.Serialize(document, WorkflowJson.Options));
        await artifactStore.WriteAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.PreparationMarkdown(preparationEvent.EventId)),
            RenderPreparationMarkdown(preparationEvent));
        return preparationEvent;
    }

    public async Task<WorkflowPreparationEvent?> LoadPreparationEventAsync(Repository repository, string eventId)
    {
        WorkflowArtifactPaths.ValidatePreparationEventId(eventId);
        string? json = await artifactStore.ReadAsync(
            WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.PreparationJson(eventId)));
        if (json is null)
        {
            return null;
        }

        WorkflowArtifactDocument<WorkflowPreparationEvent>? document =
            JsonSerializer.Deserialize<WorkflowArtifactDocument<WorkflowPreparationEvent>>(json, WorkflowJson.Options);
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

    public async Task<IReadOnlyList<WorkflowPreparationEvent>> ListPreparationEventsAsync(Repository repository)
    {
        string root = WorkflowArtifactPaths.Resolve(repository, WorkflowArtifactPaths.PreparationRoot);
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "*.json");
        var events = new List<WorkflowPreparationEvent>();

        foreach (string file in files
            .Where(file => Path.GetFileName(file).StartsWith("preparation.", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            string? eventId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                continue;
            }

            WorkflowPreparationEvent? preparationEvent = await LoadPreparationEventAsync(repository, eventId);
            if (preparationEvent is not null)
            {
                events.Add(preparationEvent);
            }
        }

        return events
            .OrderBy(preparationEvent => preparationEvent.OccurredAt)
            .ToArray();
    }

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

    private static string RenderContinuationMarkdown(WorkflowContinuationEvent continuationEvent)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Workflow Continuation Event");
        builder.AppendLine();
        builder.AppendLine($"Repository: {continuationEvent.RepositoryId}");
        builder.AppendLine($"Event Id: {continuationEvent.EventId}");
        builder.AppendLine($"Occurred At: {continuationEvent.OccurredAt:O}");
        builder.AppendLine($"Trigger: {continuationEvent.Trigger}");
        builder.AppendLine($"Input Fingerprint: {continuationEvent.InputFingerprint}");
        builder.AppendLine($"From Stage: {continuationEvent.FromStage}");
        builder.AppendLine($"To Stage: {continuationEvent.ToStage?.ToString() ?? "None"}");
        builder.AppendLine($"Progress State: {continuationEvent.ProgressState}");
        builder.AppendLine($"Blocking Gate: {continuationEvent.BlockingGate}");
        builder.AppendLine($"Decision: {continuationEvent.Decision}");
        builder.AppendLine($"Reason: {continuationEvent.Reason}");
        builder.AppendLine($"Waiting For Human: {continuationEvent.IsWaitingForHuman}");
        builder.AppendLine($"Complete: {continuationEvent.IsComplete}");
        builder.AppendLine($"Required Human Action: {continuationEvent.RequiredHumanAction}");
        builder.AppendLine();
        builder.AppendLine("## Diagnostics");

        if (continuationEvent.Diagnostics.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("- None");
            return builder.ToString();
        }

        foreach (string diagnostic in continuationEvent.Diagnostics)
        {
            builder.AppendLine($"- {diagnostic}");
        }

        return builder.ToString();
    }

    private static string RenderPreparationMarkdown(WorkflowPreparationEvent preparationEvent)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Workflow Preparation Event");
        builder.AppendLine();
        builder.AppendLine($"Repository: {preparationEvent.RepositoryId}");
        builder.AppendLine($"Event Id: {preparationEvent.EventId}");
        builder.AppendLine($"Occurred At: {preparationEvent.OccurredAt:O}");
        builder.AppendLine($"Trigger: {preparationEvent.Trigger}");
        builder.AppendLine($"Input Fingerprint: {preparationEvent.InputFingerprint}");
        builder.AppendLine($"Stage: {preparationEvent.Stage}");
        builder.AppendLine($"Progress State: {preparationEvent.ProgressState}");
        builder.AppendLine($"Blocking Gate: {preparationEvent.BlockingGate}");
        builder.AppendLine($"Command: {preparationEvent.Command}");
        builder.AppendLine($"Command Name: {preparationEvent.CommandName}");
        builder.AppendLine($"Decision: {preparationEvent.Decision}");
        builder.AppendLine($"Reason: {preparationEvent.Reason}");
        builder.AppendLine($"Waiting For Human: {preparationEvent.IsWaitingForHuman}");
        builder.AppendLine($"Duplicate Domain Evidence: {preparationEvent.HasDuplicateDomainEvidence}");
        builder.AppendLine();
        builder.AppendLine("## Duplicate Evidence");
        if (preparationEvent.DuplicateEvidence.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("- None");
        }
        else
        {
            foreach (string duplicate in preparationEvent.DuplicateEvidence)
            {
                builder.AppendLine($"- {duplicate}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Created Artifacts");
        if (preparationEvent.CreatedArtifactIds.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("- None");
        }
        else
        {
            foreach (string artifactId in preparationEvent.CreatedArtifactIds)
            {
                builder.AppendLine($"- {artifactId}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Diagnostics");

        if (preparationEvent.Diagnostics.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("- None");
            return builder.ToString();
        }

        foreach (string diagnostic in preparationEvent.Diagnostics)
        {
            builder.AppendLine($"- {diagnostic}");
        }

        return builder.ToString();
    }
}
