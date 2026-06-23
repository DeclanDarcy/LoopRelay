namespace CommandCenter.Reasoning.Models;

public sealed record ReasoningNarrative(string Summary, string Details = "");

public sealed record ReasoningReference(
    ReasoningReferenceKind Kind,
    string Id,
    string? RelativePath = null,
    string? Section = null,
    string? Excerpt = null,
    string? Fingerprint = null);

public sealed record ReasoningProvenance(
    string SourceKind,
    string CapturedBy,
    string? RelativePath = null,
    string? Section = null,
    string? Excerpt = null,
    string? Fingerprint = null);

public sealed record ReasoningEvent(
    string Id,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    ReasoningEventFamily Family,
    ReasoningEventType Type,
    string Title,
    ReasoningNarrative Narrative,
    IReadOnlyList<ReasoningReference> References,
    ReasoningProvenance Provenance,
    IReadOnlyList<string> ThreadIds,
    IReadOnlyList<string> Tags);

public sealed record ReasoningThread(
    string Id,
    Guid RepositoryId,
    string Title,
    ReasoningThreadTheme Theme,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Summary,
    IReadOnlyList<string> EventIds,
    IReadOnlyList<string> Tags);

public sealed record ReasoningRelationship(
    string Id,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    ReasoningRelationshipType Type,
    ReasoningReference Source,
    ReasoningReference Target,
    ReasoningNarrative Narrative,
    ReasoningProvenance Provenance);

public sealed record CreateReasoningEventCommand(
    ReasoningEventFamily Family,
    ReasoningEventType Type,
    string Title,
    ReasoningNarrative Narrative,
    IReadOnlyList<ReasoningReference>? References,
    ReasoningProvenance Provenance,
    IReadOnlyList<string>? ThreadIds,
    IReadOnlyList<string>? Tags);

public sealed record CreateReasoningThreadCommand(
    string Title,
    ReasoningThreadTheme Theme,
    string Summary,
    IReadOnlyList<string>? EventIds,
    IReadOnlyList<string>? Tags);

public sealed record CreateReasoningRelationshipCommand(
    ReasoningRelationshipType Type,
    ReasoningReference Source,
    ReasoningReference Target,
    ReasoningNarrative Narrative,
    ReasoningProvenance Provenance);

public sealed record ManualReasoningCaptureTemplate(
    ReasoningManualCaptureKind Kind,
    ReasoningEventFamily Family,
    ReasoningEventType Type,
    ReasoningThreadTheme SuggestedThreadTheme,
    string ProvenanceSourceKind,
    IReadOnlyList<ReasoningReferenceKind> SuggestedReferenceKinds);

public sealed record ManualReasoningCaptureCommand(
    ReasoningManualCaptureKind Kind,
    string Title,
    ReasoningNarrative Narrative,
    IReadOnlyList<ReasoningReference>? References,
    ReasoningProvenance Provenance,
    IReadOnlyList<string>? ThreadIds,
    IReadOnlyList<string>? Tags);

public sealed record ReasoningGraph(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ReasoningGraphNode> Nodes,
    IReadOnlyList<ReasoningGraphRelationship> Relationships,
    IReadOnlyList<string> Diagnostics);

public sealed record ReasoningGraphNode(
    string Id,
    ReasoningReferenceKind Kind,
    string ReferenceId,
    string Label,
    bool Resolved,
    ReasoningReference? Reference);

public sealed record ReasoningGraphRelationship(
    string Id,
    ReasoningRelationshipType Type,
    string SourceNodeId,
    string TargetNodeId,
    string Label,
    string Provenance,
    string? RelationshipId);

public sealed record ReasoningTrace(
    Guid RepositoryId,
    ReasoningTraceDirection Direction,
    ReasoningReference Target,
    IReadOnlyList<ReasoningGraphNode> Nodes,
    IReadOnlyList<ReasoningGraphRelationship> Relationships,
    IReadOnlyList<string> Diagnostics);
