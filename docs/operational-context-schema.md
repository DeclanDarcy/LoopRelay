# Operational Context Schema

This document is the implementation contract for Command Center's operational-context model. It defines how `.agents/operational_context.md` maps to `OperationalContextDocument` and how continuity services may interpret that document.

## Purpose

Operational context represents current project understanding. It is a compact, durable synthesis of what a future execution slice needs to know to make coherent decisions.

Operational context may contain:

- Current mental model.
- Architecture.
- Authority boundaries.
- Constraints.
- Stable decisions.
- Decision rationale.
- Open questions.
- Active risks.
- Recent understanding changes.

Operational context must not contain:

- Raw history.
- Execution streams.
- Conversation logs.
- Complete handoff archives.
- Git commit history.
- Milestone status tracking.
- Provider transcripts.
- Session lifecycle metadata.

## Canonical Markdown

Generated operational context should use this structure:

```markdown
# Operational Context

## Current Mental Model

## Architecture

## Authority Boundaries

## Constraints

## Stable Decisions

## Decision Rationale

## Open Questions

## Active Risks

## Recent Understanding Changes
```

Sections may be empty. The parser and UI must tolerate absent canonical sections in hand-written Markdown.

## Document Model

`OperationalContextDocument` is the canonical internal representation for generation, review, semantic diff, compression, decision assimilation, projection, diagnostics, and reporting.

Initial shape:

```csharp
public sealed class OperationalContextDocument
{
    public string Title { get; init; } = "Operational Context";
    public IReadOnlyList<OperationalContextItem> CurrentMentalModel { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> Architecture { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> AuthorityBoundaries { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> Constraints { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> StableDecisions { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> DecisionRationale { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> OpenQuestions { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> ActiveRisks { get; init; } = [];
    public IReadOnlyList<OperationalContextItem> RecentUnderstandingChanges { get; init; } = [];
    public IReadOnlyList<OperationalContextSection> AdditionalSections { get; init; } = [];
}

public sealed class OperationalContextItem
{
    public string Id { get; init; } = "";
    public OperationalContextItemKind Kind { get; init; }
    public string Text { get; init; } = "";
    public string? Rationale { get; init; }
    public string? SourceRelativePath { get; init; }
}
```

## Item Kinds

Allowed initial `OperationalContextItemKind` values:

- `MentalModel`
- `Architecture`
- `AuthorityBoundary`
- `Constraint`
- `StableDecision`
- `DecisionRationale`
- `OpenQuestion`
- `ActiveRisk`
- `RecentChange`
- `Unknown`

Unknown content must be preserved. It must not be silently discarded or reclassified as stable understanding without an explicit parser or reviewer signal.

## Parser Expectations

The Markdown parser must:

- Recognize canonical headings case-insensitively.
- Map list items under canonical headings to the corresponding document list and item kind.
- Preserve non-list content in a section-level representation when item extraction is unreliable.
- Preserve unknown headings and their content in `AdditionalSections`.
- Normalize whitespace only for comparison and generated item identity.
- Avoid rewriting user-authored Markdown as part of parsing.
- Treat missing canonical sections as empty sections.
- Degrade to section-level changes when item-level parsing is not reliable.

Coarse item identity may start as normalized section name plus a normalized text hash. Durable manually assigned IDs are optional later.

## Renderer Expectations

The renderer must:

- Emit the canonical title and section order.
- Render known item lists in their canonical sections.
- Preserve `AdditionalSections` after canonical sections.
- Produce stable Markdown for generated documents.
- Avoid dropping unknown content.

Promotion is allowed to preserve reviewer-edited Markdown exactly. Rendering is required when services create generated Markdown from `OperationalContextDocument`; it is not required to normalize reviewer edits during promotion.

## Projection Mapping

Workspace projections must read operational-context content through `OperationalContextDocument`.

Initial projection fields should expose:

- Current mental model.
- Architecture.
- Authority boundaries.
- Constraints.
- Stable decisions.
- Decision rationale.
- Open questions.
- Active risks.
- Recent understanding changes.
- Unknown or additional sections.
- Source artifact path.
- Presence, size, and parse warnings.

The UI must treat backend projections as authoritative and must not infer workflow authority from client-only state.

## Coarse Diff Categories

Initial semantic diff categories are:

- `SectionAdded`
- `SectionRemoved`
- `SectionChanged`
- `ItemAdded`
- `ItemRemoved`
- `ItemChanged`
- `ConstraintAdded`
- `ConstraintRemoved`
- `QuestionAdded`
- `QuestionRemoved`
- `RiskAdded`
- `RiskRemoved`
- `DecisionAdded`
- `DecisionRemoved`
- `RationaleChanged`
- `PreservationWarning`

The diff service must remain deterministic. It must not perform natural-language entailment, correctness judgment, confidence scoring, automatic drift correction, or fine-grained paragraph rewrite analysis.

## Compression Tiers

Compression classifies content by retention need:

- Preserve: architecture, authority boundaries, constraints, stable decisions, active risks, unresolved open questions, and rationale that still affects future work.
- Summarize: recent understanding changes, resolved questions with lingering rationale, repeated handoff conclusions, and tactical context that explains current state.
- Retire: stale progress notes, superseded tactical detail, raw history, resolved transient issues, and provider-output residue.

Compression must operate over `OperationalContextDocument` sections and item kinds. It must not become a raw Markdown scanner or a milestone-status updater.

## Decision Assimilation

Decision analysis may propose operational-context items from current and historical decisions when they materially affect future work.

Assimilation expectations:

- Architectural and strategic decisions may become stable decisions.
- Rationale that affects future implementation choices may become decision rationale.
- Open or contradictory decisions may become open questions or active risks.
- Tactical decisions should remain in decision artifacts unless they materially affect future work.
- Contradictions should produce warnings rather than automatic replacement.

Decision artifacts remain the decision record. Operational context carries only the still-relevant understanding distilled from those decisions.

## Compatibility

Hand-written Markdown is supported. Unknown sections, non-canonical headings, and content that cannot be classified must survive parse, projection, and generated follow-up proposals. The first implementation should prefer preservation and coarse warnings over aggressive interpretation.
