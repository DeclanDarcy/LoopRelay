using System.Text.Json;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Persistence;

namespace CommandCenter.Reasoning.Services;

public sealed class FileSystemReasoningRepository(
    IArtifactStore artifactStore,
    IReasoningArtifactProjectionService projectionService)
    : IReasoningRepository
{
    public async Task<IReadOnlyList<ReasoningEvent>> ListEventsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await artifactStore.ListDirectoriesAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventsRootPath()));
        var events = new List<ReasoningEvent>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            ReasoningEvent? reasoningEvent = await GetEventAsync(repository, id);
            if (reasoningEvent is not null)
            {
                events.Add(reasoningEvent);
            }
        }

        return events.OrderBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<ReasoningEvent?> GetEventAsync(Repository repository, string eventId)
    {
        ReasoningArtifactPaths.ValidateEventId(eventId);
        string path = ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventJson(eventId));
        ReasoningEvent? reasoningEvent = await ReadPayloadAsync<ReasoningEvent>(repository, path);
        return reasoningEvent is null ? null : EnrichCaptureProvenance(reasoningEvent);
    }

    public async Task<ReasoningEvent> CreateEventAsync(Repository repository, CreateReasoningEventCommand command)
    {
        ValidateProvenance(command.Provenance);
        ValidateNarrative(command.Narrative);
        foreach (ReasoningReference reference in command.References ?? Array.Empty<ReasoningReference>())
        {
            ValidateReference(reference);
        }

        foreach (string threadId in command.ThreadIds ?? Array.Empty<string>())
        {
            ReasoningArtifactPaths.ValidateThreadId(threadId);
        }

        string id = await AllocateIdAsync(repository, ReasoningArtifactPaths.EventsRootPath(), "EVT");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var reasoningEvent = new ReasoningEvent(
            id,
            repository.Id,
            now,
            command.Family,
            command.Type,
            RequireText(command.Title, "Event title is required."),
            command.Narrative,
            Normalize(command.References),
            command.Provenance,
            Normalize(command.ThreadIds),
            Normalize(command.Tags));
        reasoningEvent = EnrichCaptureProvenance(reasoningEvent);

        await WriteDocumentAsync(repository, ReasoningArtifactPaths.EventJson(id), reasoningEvent, now, null);
        await artifactStore.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventMarkdown(id)),
            projectionService.RenderEvent(reasoningEvent));
        return reasoningEvent;
    }

    public async Task<IReadOnlyList<ReasoningThread>> ListThreadsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await artifactStore.ListDirectoriesAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ThreadsRootPath()));
        var threads = new List<ReasoningThread>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            ReasoningThread? thread = await GetThreadAsync(repository, id);
            if (thread is not null)
            {
                threads.Add(thread);
            }
        }

        return threads.OrderBy(thread => thread.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<ReasoningThread?> GetThreadAsync(Repository repository, string threadId)
    {
        ReasoningArtifactPaths.ValidateThreadId(threadId);
        string path = ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ThreadJson(threadId));
        return await ReadPayloadAsync<ReasoningThread>(repository, path);
    }

    public async Task<ReasoningThread> CreateThreadAsync(Repository repository, CreateReasoningThreadCommand command)
    {
        foreach (string eventId in command.EventIds ?? Array.Empty<string>())
        {
            await RequireEventAsync(repository, eventId);
        }

        string id = await AllocateIdAsync(repository, ReasoningArtifactPaths.ThreadsRootPath(), "THR");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var thread = new ReasoningThread(
            id,
            repository.Id,
            RequireText(command.Title, "Thread title is required."),
            command.Theme,
            now,
            now,
            RequireText(command.Summary, "Thread summary is required."),
            Normalize(command.EventIds),
            Normalize(command.Tags));

        await SaveThreadAsync(repository, thread);
        return thread;
    }

    public async Task<ReasoningThread> AppendThreadEventAsync(Repository repository, string threadId, string eventId)
    {
        ReasoningArtifactPaths.ValidateThreadId(threadId);
        await RequireEventAsync(repository, eventId);
        ReasoningThread thread = await GetThreadAsync(repository, threadId)
            ?? throw new ReasoningValidationException($"Reasoning thread {threadId} was not found.");

        if (thread.EventIds.Contains(eventId, StringComparer.Ordinal))
        {
            return thread;
        }

        var updated = thread with
        {
            UpdatedAt = DateTimeOffset.UtcNow,
            EventIds = thread.EventIds.Concat([eventId]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
        };
        await SaveThreadAsync(repository, updated);
        return updated;
    }

    public async Task<IReadOnlyList<ReasoningRelationship>> ListRelationshipsAsync(Repository repository)
    {
        IReadOnlyList<string> directories = await artifactStore.ListDirectoriesAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.RelationshipsRootPath()));
        var relationships = new List<ReasoningRelationship>();
        foreach (string directory in directories)
        {
            string id = Path.GetFileName(directory);
            ReasoningArtifactPaths.ValidateRelationshipId(id);
            string path = ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.RelationshipJson(id));
            ReasoningRelationship? relationship = await ReadPayloadAsync<ReasoningRelationship>(repository, path);
            if (relationship is not null)
            {
                relationships.Add(relationship);
            }
        }

        return relationships.OrderBy(relationship => relationship.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<ReasoningRelationship> CreateRelationshipAsync(Repository repository, CreateReasoningRelationshipCommand command)
    {
        ValidateProvenance(command.Provenance);
        ValidateNarrative(command.Narrative);
        await ValidateRelationshipEndpointAsync(repository, command.Source);
        await ValidateRelationshipEndpointAsync(repository, command.Target);

        IReadOnlyList<ReasoningRelationship> existing = await ListRelationshipsAsync(repository);
        if (existing.Any(relationship =>
            relationship.Type == command.Type &&
            relationship.Source.Kind == command.Source.Kind &&
            string.Equals(relationship.Source.Id, command.Source.Id, StringComparison.Ordinal) &&
            relationship.Target.Kind == command.Target.Kind &&
            string.Equals(relationship.Target.Id, command.Target.Id, StringComparison.Ordinal)))
        {
            throw new ReasoningConflictException(
                "Reasoning relationship already exists.",
                DuplicateRelationshipBoundary(command));
        }

        string id = await AllocateIdAsync(repository, ReasoningArtifactPaths.RelationshipsRootPath(), "REL");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var relationship = new ReasoningRelationship(
            id,
            repository.Id,
            now,
            command.Type,
            command.Source,
            command.Target,
            command.Narrative,
            command.Provenance);

        await WriteDocumentAsync(repository, ReasoningArtifactPaths.RelationshipJson(id), relationship, now, null);
        await artifactStore.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.RelationshipMarkdown(id)),
            projectionService.RenderRelationship(relationship));
        return relationship;
    }

    public async Task<IReadOnlyList<ReasoningReconstructionReport>> ListReconstructionReportsAsync(Repository repository)
    {
        string root = ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ReportsRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "*.json");
        var reports = new List<ReasoningReconstructionReport>();
        foreach (string file in files)
        {
            string reportId = Path.GetFileNameWithoutExtension(file);
            try
            {
                ReasoningArtifactPaths.ValidateReconstructionReportId(reportId);
                ReasoningReconstructionReport? report = await ReadPayloadAsync<ReasoningReconstructionReport>(repository, file);
                if (report is not null)
                {
                    reports.Add(report);
                }
            }
            catch (ReasoningValidationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        return reports.OrderByDescending(report => report.GeneratedAt).ThenBy(report => report.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<ReasoningReconstructionReport> SaveReconstructionReportAsync(
        Repository repository,
        ReasoningReconstructionReport report)
    {
        ReasoningArtifactPaths.ValidateReconstructionReportId(report.Id);
        if (report.RepositoryId != repository.Id)
        {
            throw new ReasoningValidationException("Reasoning reconstruction report belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            ReasoningArtifactPaths.ReconstructionReportJson(report.Id),
            report,
            report.GeneratedAt,
            null);
        await artifactStore.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ReconstructionReportMarkdown(report.Id)),
            projectionService.RenderReconstructionReport(report));
        return report;
    }

    public async Task<IReadOnlyList<ReasoningCertificationReport>> ListCertificationReportsAsync(Repository repository)
    {
        string root = ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ReportsRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "*.json");
        var reports = new List<ReasoningCertificationReport>();
        foreach (string file in files)
        {
            string reportId = Path.GetFileNameWithoutExtension(file);
            try
            {
                ReasoningArtifactPaths.ValidateCertificationReportId(reportId);
                ReasoningCertificationReport? report = await ReadPayloadAsync<ReasoningCertificationReport>(repository, file);
                if (report is not null)
                {
                    reports.Add(report);
                }
            }
            catch (ReasoningValidationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        return reports.OrderByDescending(report => report.GeneratedAt).ThenBy(report => report.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<ReasoningCertificationReport> SaveCertificationReportAsync(
        Repository repository,
        ReasoningCertificationReport report)
    {
        ReasoningArtifactPaths.ValidateCertificationReportId(report.Id);
        if (report.RepositoryId != repository.Id)
        {
            throw new ReasoningValidationException("Reasoning certification report belongs to a different repository.");
        }

        await WriteDocumentAsync(
            repository,
            ReasoningArtifactPaths.CertificationReportJson(report.Id),
            report,
            report.GeneratedAt,
            null);
        await artifactStore.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.CertificationReportMarkdown(report.Id)),
            projectionService.RenderCertificationReport(report));
        return report;
    }

    private async Task SaveThreadAsync(Repository repository, ReasoningThread thread)
    {
        await WriteDocumentAsync(repository, ReasoningArtifactPaths.ThreadJson(thread.Id), thread, thread.CreatedAt, thread.UpdatedAt);
        IReadOnlyList<ReasoningRelationship> relationships = await ListRelationshipsAsync(repository);
        await artifactStore.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ThreadMarkdown(thread.Id)),
            projectionService.RenderThread(thread, relationships.Where(relationship =>
                IsReferenceToThread(relationship.Source, thread.Id) || IsReferenceToThread(relationship.Target, thread.Id)).ToArray()));
    }

    private async Task<T?> ReadPayloadAsync<T>(Repository repository, string path)
    {
        ReasoningArtifactDocument<T>? document;
        try
        {
            // ReadAs caches the deserialized document graph keyed by the file signature, so a single request that
            // lists events/threads/relationships (each a per-id Get re-read) deserializes each unchanged artifact
            // once. ReasoningArtifactDocument<T> and its record payloads are immutable, so aliasing is safe. The
            // delegate mirrors the prior inline logic exactly: a malformed-JSON JsonException propagates out of
            // ReadAs into the catch below, and a JSON literal `null` for an existing file throws the same
            // "document is empty" validation error (which, throwing, is never cached). A genuinely absent file
            // makes ReadAs return null with no delegate call, which we map to default(T) just as before.
            document = await artifactStore.ReadAs(
                path,
                json => JsonSerializer.Deserialize<ReasoningArtifactDocument<T>>(json, ReasoningJson.Options)
                    ?? throw new ReasoningValidationException("Reasoning artifact document is empty."));
        }
        catch (JsonException exception)
        {
            throw new ReasoningValidationException($"Reasoning artifact could not be read: {exception.Message}");
        }

        if (document is null)
        {
            return default;
        }

        if (!string.Equals(document.SchemaVersion, ReasoningArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new ReasoningValidationException($"Unsupported reasoning schema version {document.SchemaVersion}.");
        }

        if (document.RepositoryId != repository.Id)
        {
            throw new ReasoningValidationException("Reasoning artifact belongs to a different repository.");
        }

        return document.Payload;
    }

    private async Task WriteDocumentAsync<T>(Repository repository, string relativePath, T payload, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        var document = new ReasoningArtifactDocument<T>(
            ReasoningArtifactPaths.SchemaVersion,
            repository.Id,
            createdAt,
            updatedAt,
            payload);
        string json = JsonSerializer.Serialize(document, ReasoningJson.Options);
        await artifactStore.WriteAsync(ReasoningArtifactPaths.Resolve(repository, relativePath), json);
    }

    private async Task<string> AllocateIdAsync(Repository repository, string rootPath, string prefix)
    {
        IReadOnlyList<string> directories = await artifactStore.ListDirectoriesAsync(ReasoningArtifactPaths.Resolve(repository, rootPath));
        int next = directories
            .Select(Path.GetFileName)
            .Where(id => id is not null && id.StartsWith($"{prefix}-", StringComparison.Ordinal))
            .Select(id => int.TryParse(id![4..], out int number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}-{next:0000}";
    }

    private async Task RequireEventAsync(Repository repository, string eventId)
    {
        ReasoningArtifactPaths.ValidateEventId(eventId);
        if (await GetEventAsync(repository, eventId) is null)
        {
            throw new ReasoningValidationException(
                $"Reasoning event {eventId} was not found.",
                MissingReasoningReferenceBoundary(ReasoningReferenceKind.ReasoningEvent, eventId));
        }
    }

    private async Task ValidateRelationshipEndpointAsync(Repository repository, ReasoningReference reference)
    {
        ValidateReference(reference);
        if (reference.Kind == ReasoningReferenceKind.ReasoningEvent)
        {
            await RequireEventAsync(repository, reference.Id);
        }
        else if (reference.Kind == ReasoningReferenceKind.ReasoningThread)
        {
            ReasoningArtifactPaths.ValidateThreadId(reference.Id);
            if (await GetThreadAsync(repository, reference.Id) is null)
            {
                throw new ReasoningValidationException(
                    $"Reasoning thread {reference.Id} was not found.",
                    MissingReasoningReferenceBoundary(ReasoningReferenceKind.ReasoningThread, reference.Id));
            }
        }
    }

    private static ReasoningBoundaryViolation MissingReasoningReferenceBoundary(
        ReasoningReferenceKind referenceKind,
        string referenceId)
    {
        string owningDomain = referenceKind == ReasoningReferenceKind.ReasoningThread
            ? "ReasoningThread"
            : "ReasoningEvent";
        return new ReasoningBoundaryViolation(
            "Reasoning relationships may only target existing reasoning-owned artifacts.",
            owningDomain,
            $"{referenceKind}:{referenceId}",
            referenceKind == ReasoningReferenceKind.ReasoningThread
                ? "Create or recover the reasoning thread before linking it, or use a non-reasoning reference kind for external domain evidence."
                : "Create or recover the reasoning event before linking it, or use a non-reasoning reference kind for external domain evidence.",
            $"{owningDomain} authority could not resolve {referenceId}; accepting the relationship would create an unreconstructable reasoning edge.",
            "Blocking");
    }

    private static ReasoningBoundaryViolation DuplicateRelationshipBoundary(CreateReasoningRelationshipCommand command)
    {
        return new ReasoningBoundaryViolation(
            "Reasoning relationship identity is source, target, and relationship type.",
            "ReasoningRelationship",
            $"{command.Type}:{command.Source.Kind}:{command.Source.Id}->{command.Target.Kind}:{command.Target.Id}",
            "Use the existing relationship as evidence, or create a distinct relationship type/source/target when the assertion is semantically different.",
            "The requested relationship duplicates an existing reasoning edge and would not add new authoritative evidence.",
            "Warning");
    }

    private static bool IsReferenceToThread(ReasoningReference reference, string threadId)
    {
        return reference.Kind == ReasoningReferenceKind.ReasoningThread &&
            string.Equals(reference.Id, threadId, StringComparison.Ordinal);
    }

    private static void ValidateReference(ReasoningReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Id))
        {
            throw new ReasoningValidationException("Reasoning reference id is required.");
        }

        if (reference.Kind == ReasoningReferenceKind.ReasoningEvent)
        {
            ReasoningArtifactPaths.ValidateEventId(reference.Id);
        }
        else if (reference.Kind == ReasoningReferenceKind.ReasoningThread)
        {
            ReasoningArtifactPaths.ValidateThreadId(reference.Id);
        }
    }

    private static void ValidateProvenance(ReasoningProvenance provenance)
    {
        RequireText(provenance.SourceKind, "Reasoning provenance source kind is required.");
        RequireText(provenance.CapturedBy, "Reasoning provenance captured-by value is required.");
    }

    private static void ValidateNarrative(ReasoningNarrative narrative)
    {
        RequireText(narrative.Summary, "Reasoning narrative summary is required.");
    }

    private static ReasoningEvent EnrichCaptureProvenance(ReasoningEvent reasoningEvent)
    {
        if (reasoningEvent.CaptureProvenance is { DiagnosticGroups: { } existingGroups } &&
            existingGroups.Count > 0)
        {
            return reasoningEvent;
        }

        ReasoningCaptureMode mode = ResolveCaptureMode(reasoningEvent.Provenance.SourceKind, reasoningEvent.Tags);
        string? sourceTransition = mode == ReasoningCaptureMode.Inferred
            ? ResolveSourceTransition(reasoningEvent.Provenance.SourceKind, reasoningEvent.Provenance.Section)
            : null;
        string captureReason = ResolveCaptureReason(mode, reasoningEvent.Provenance);
        string? duplicateSignal = string.IsNullOrWhiteSpace(reasoningEvent.Provenance.Fingerprint)
            ? null
            : $"Fingerprint {reasoningEvent.Provenance.Fingerprint}";
        ReasoningCaptureProvenance provenance = reasoningEvent.CaptureProvenance ?? new ReasoningCaptureProvenance(
            mode,
            reasoningEvent.Provenance.SourceKind,
            reasoningEvent.Provenance.CapturedBy,
            captureReason,
            sourceTransition,
            reasoningEvent.Provenance.RelativePath,
            mode == ReasoningCaptureMode.Inferred ? reasoningEvent.CreatedAt : null,
            null,
            duplicateSignal,
            null);

        return reasoningEvent with
        {
            CaptureProvenance = provenance with
            {
                DiagnosticGroups = CreateCaptureDiagnosticGroups(provenance)
            }
        };
    }

    private static IReadOnlyList<ReasoningDiagnosticGroup> CreateCaptureDiagnosticGroups(
        ReasoningCaptureProvenance provenance)
    {
        var diagnostics = new List<string>
        {
            $"Capture mode: {provenance.Mode}.",
            $"Source kind: {provenance.SourceKind}.",
            $"Captured by: {provenance.CapturedBy}.",
            $"Capture reason: {provenance.CaptureReason}."
        };

        AddIfPresent(diagnostics, "Source transition", provenance.SourceTransition);
        AddIfPresent(diagnostics, "Source artifact", provenance.SourceArtifact);
        AddIfPresent(diagnostics, "Source timestamp", provenance.SourceTimestamp?.ToString("O"));
        AddIfPresent(diagnostics, "Duplicate signal", provenance.DuplicateSignal);
        AddIfPresent(diagnostics, "Skip reason", provenance.SkipReason);
        if (provenance.ExistingEventReference is not null)
        {
            diagnostics.Add($"Existing event reference: {provenance.ExistingEventReference.Id}.");
        }

        string title = provenance.Mode switch
        {
            ReasoningCaptureMode.Manual => "Manual capture",
            ReasoningCaptureMode.Assisted => "Assisted capture",
            ReasoningCaptureMode.Inferred => "Inferred capture",
            _ => "Capture"
        };

        return
        [
            new ReasoningDiagnosticGroup("capture", title, diagnostics)
        ];
    }

    private static ReasoningCaptureMode ResolveCaptureMode(string sourceKind, IReadOnlyList<string> tags)
    {
        if (sourceKind.StartsWith("Inferred", StringComparison.Ordinal) ||
            tags.Contains("inferred-capture", StringComparer.Ordinal))
        {
            return ReasoningCaptureMode.Inferred;
        }

        if (sourceKind.StartsWith("Assisted", StringComparison.Ordinal) ||
            tags.Contains("assisted-capture", StringComparer.Ordinal))
        {
            return ReasoningCaptureMode.Assisted;
        }

        return ReasoningCaptureMode.Manual;
    }

    private static string ResolveSourceTransition(string sourceKind, string? section)
    {
        return sourceKind switch
        {
            "InferredProposalResolution" => "ProposalResolved",
            "InferredDecisionSupersession" => "DecisionSuperseded",
            "InferredDecisionArchive" => "DecisionArchived",
            "InferredGovernanceContradiction" => "GovernanceContradictionObserved",
            "InferredOperationalContextPromotion" => "OperationalContextPromotionReasoningObserved",
            "InferredExecutionHandoffAcceptance" => "ExecutionHandoffAcceptedReasoningObserved",
            "InferredExecutionHandoffRejection" => "ExecutionHandoffRejectedReasoningObserved",
            _ when !string.IsNullOrWhiteSpace(section) => section,
            _ => sourceKind
        };
    }

    private static string ResolveCaptureReason(ReasoningCaptureMode mode, ReasoningProvenance provenance)
    {
        if (!string.IsNullOrWhiteSpace(provenance.Excerpt))
        {
            return provenance.Excerpt;
        }

        return mode switch
        {
            ReasoningCaptureMode.Manual => "Captured from explicit user-supplied reasoning.",
            ReasoningCaptureMode.Assisted => "Captured from an assisted reasoning workflow.",
            ReasoningCaptureMode.Inferred => "Captured from an authoritative lifecycle transition.",
            _ => "Captured from reasoning provenance."
        };
    }

    private static string RequireText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReasoningValidationException(message);
        }

        return value.Trim();
    }

    private static IReadOnlyList<T> Normalize<T>(IReadOnlyList<T>? values)
    {
        return values is null ? Array.Empty<T>() : values.ToArray();
    }

    private static void AddIfPresent(List<string> diagnostics, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add($"{label}: {value}.");
        }
    }
}
