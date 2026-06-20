# Command Center Decision Continuity Implementation Plan

## Objective

Implement project-understanding continuity in Command Center without introducing new session abstractions, session reuse, parallel state machines, or duplicate workflow systems.

At completion, Command Center must be able to:

- Include current project understanding in execution context.
- Generate a reviewable proposal for updated project understanding.
- Let a human compare, edit, accept, reject, and promote that proposal.
- Preserve prior understanding as versioned repository artifacts.
- Compress long-running history into high-signal current understanding.
- Preserve important decisions, rationale, open questions, and active risks.
- Surface understanding through the existing repository workspace.
- Certify that understanding survives repeated execution and context updates.
- Report continuity diagnostics without turning metrics into workflow authority.

The completed continuity loop is:

```text
Current Understanding
Input Artifacts
Generate Proposal
Persist Proposal
Review Proposal
Accept Or Reject
Promote Accepted Understanding
Archive Prior Understanding
Execute With Updated Understanding
Measure Continuity Quality
```

Execution sessions remain disposable. Repository artifacts remain authoritative. Human review remains mandatory before mutating current understanding.

## Current Codebase Baseline

Command Center currently has:

- A .NET backend sidecar in `src/CommandCenter.Backend`.
- A React/TypeScript UI in `src/CommandCenter.UI`.
- A Rust/Tauri desktop shell in `src/CommandCenter.Shell`.
- Backend tests in `tests/CommandCenter.Backend.Tests`.
- Repository registration and projection services.
- Artifact discovery, load, save, and rotation services.
- Planning readiness based on `.agents/plan.md` and `.agents/milestones/*.md`.
- Execution context preview, execution launch, monitoring, handoff review, commit, and push workflow.
- A single repository execution state model represented by `RepositoryExecutionState`.
- Disposable execution sessions represented by `ExecutionSession`.
- Repository-owned artifact discovery through `ArtifactService`.
- Workspace projection through `RepositoryProjectionService`.
- Current operational-context presence detection through `ArtifactInventory.OperationalContext` and `RepositoryWorkspaceProjection.HasOperationalContext`.

Existing canonical repository-owned artifact layout:

```text
<repository>/
  .agents/
    plan.md
    operational_context.md
    milestones/
      *.md
    handoffs/
      handoff.md
      handoff.0001.md
      handoff.0002.md
    decisions/
      decisions.md
      decisions.0001.md
      decisions.0002.md
```

Current implementation gaps:

- `ExecutionContextService` loads plan, selected milestone, optional current handoff, optional current decisions, and Git snapshot, but not `.agents/operational_context.md`.
- `ExecutionPromptBuilder` orders `Plan`, `Milestone`, `CurrentHandoff`, and `CurrentDecisions`, but not operational context.
- `ArtifactRotationService` supports handoff and decisions rotation, but not operational context rotation.
- `ArtifactService` discovers only the current operational-context artifact, not historical operational-context revisions.
- There is no canonical internal understanding model between operational-context Markdown and workspace projections.
- There is no operational-context proposal model, proposal persistence, review state, promotion operation, compression logic, semantic diff, or continuity instrumentation.
- The UI exposes operational context as an editable artifact only; it does not expose current understanding, proposals, review, lifecycle, decisions, or continuity diagnostics as first-class workspace projections.

## Architectural Principles

### Continuity Is Artifact-Mediated

Project understanding is carried by repository artifacts, not by live sessions, hidden app memory, provider process state, or conversation reuse.

Repository-owned artifacts remain the source of truth:

```text
Plan
Milestones
Operational Context
Handoffs
Decisions
Code
```

Command Center may persist proposal metadata and continuity reports, but those artifacts must live under the repository `.agents` tree unless the data is purely local application metadata.

### Operational Context Means Current Understanding

Operational context is the current project understanding needed to make good future decisions.

It contains:

- Architecture.
- Authority boundaries.
- Current constraints.
- Stable decisions.
- Decision rationale that materially affects future work.
- Current mental model.
- Open questions.
- Active risks.

It must not become:

- Raw execution history.
- Provider output.
- Handoff replay.
- Commit log mirror.
- Milestone status tracking.
- Session transcript storage.

### Understanding Has A Canonical Internal Model

Markdown is the repository serialization format. It is not the internal understanding model.

Backend continuity services must parse operational-context Markdown into `OperationalContextDocument` before generation, diffing, compression, decision assimilation, projection, diagnostics, or reporting. This prevents later milestones from becoming ad hoc string analysis layered directly on Markdown.

The canonical flow is:

```text
Markdown
OperationalContextDocument
Generation / Diff / Compression / Decision Analysis
OperationalContextProjection
Markdown
```

The renderer may write Markdown back to repository artifacts, but the services reason over the document model.

### Existing Artifact Responsibilities Remain Distinct

Artifact responsibilities:

```text
Plan                 = Intent and scope
Milestones           = Planned execution slices
Handoff              = Latest execution result
Decisions            = Decision record and rationale history
Operational Context  = Current project understanding
```

Operational context supplements these artifacts. It does not replace any of them.

### Review Before Mutation

Operational-context mutation must follow:

```text
Generate
Persist
Review
Validate
Mutate
```

Generation creates a proposal, not authoritative context. Promotion is available only after an accepted review passes stale-state checks.

### Single Workflow Authority

Do not add:

- Decision sessions.
- Continuity sessions.
- Session routers.
- Session reuse.
- Separate repository state machines.
- Client-owned workflow state.

Continuity status is projected from backend-owned repository artifacts and proposal metadata. The UI remains projection-only.

### Provider Boundary Remains Stable

Execution providers receive an `ExecutionPrompt`. They do not need to know where operational context, decisions, or handoffs came from.

The execution boundary remains:

```text
Repository
ExecutionContextService
ExecutionPromptBuilder
ExecutionPrompt
IExecutionProvider
```

Operational-context generation is a backend continuity service, not an execution session.

## Target Repository Continuity Layout

Use the existing current artifact path and add versioned revisions plus proposal metadata:

```text
<repository>/
  .agents/
    operational_context.md
    operational_context.0001.md
    operational_context.0002.md
    operational_context/
      proposals/
        <proposal-id>/
          metadata.json
          proposed.md
          edited.md
          review.json
      reports/
        continuity.<timestamp>.json
```

Rules:

- `.agents/operational_context.md` is the current authoritative understanding.
- `.agents/operational_context.NNNN.md` files are historical understanding revisions.
- Historical revision numbers are allocated from the highest existing number plus one, never by counting files.
- Proposal directories are repository-owned review artifacts.
- `proposed.md` stores generated content.
- `edited.md` stores reviewer-edited content when present.
- `metadata.json` stores proposal identity, input fingerprints, generation metadata, semantic changes, compression summary, and status.
- `review.json` stores review state, review timestamp, reviewer decision note, accepted content hash, and stale-protection metadata.
- Continuity reports are read-only diagnostic snapshots.

## Target Backend Structure

Add a continuity feature area:

```text
src/CommandCenter.Backend/
  Continuity/
    OperationalContextConstants.cs
    OperationalContextDocument.cs
    OperationalContextDocumentSchema.cs
    OperationalContextSection.cs
    OperationalContextItem.cs
    OperationalContextItemKind.cs
    OperationalContextProjection.cs
    OperationalContextProposal.cs
    OperationalContextProposalStatus.cs
    OperationalContextReview.cs
    OperationalContextReviewState.cs
    OperationalContextInputSet.cs
    OperationalContextInputFingerprint.cs
    OperationalContextSemanticChange.cs
    OperationalContextCompressionSummary.cs
    OperationalContextLifecycleSummary.cs
    ContinuityDiagnostics.cs
    ContinuityReport.cs
    UnderstandingEvolutionLedger.cs
    IOperationalContextService.cs
    OperationalContextService.cs
    IOperationalContextProposalStore.cs
    FileSystemOperationalContextProposalStore.cs
    IOperationalContextGenerationService.cs
    OperationalContextGenerationService.cs
    IOperationalContextReviewService.cs
    OperationalContextReviewService.cs
    IOperationalContextLifecycleService.cs
    OperationalContextLifecycleService.cs
    IOperationalContextParser.cs
    MarkdownOperationalContextParser.cs
    IUnderstandingDiffService.cs
    UnderstandingDiffService.cs
    IUnderstandingCompressionService.cs
    UnderstandingCompressionService.cs
    IDecisionAnalysisService.cs
    DecisionAnalysisService.cs
    IContinuityDiagnosticsService.cs
    ContinuityDiagnosticsService.cs
    IContinuityReportService.cs
    ContinuityReportService.cs
```

Keep filesystem path validation centralized through `ArtifactPath`. Keep low-level file IO behind `IArtifactStore` and high-level artifact semantics in services.

## Operational Context Schema And Understanding Model

Add a short implementation specification at:

```text
docs/operational-context-schema.md
```

This specification is not a user workflow artifact. It is a codebase contract for how Command Center represents current understanding internally.

The specification must define:

- Canonical section names.
- Allowed content types per section.
- Parser expectations.
- Renderer expectations.
- Projection expectations.
- Coarse diff expectations.
- Compression tier expectations.
- Decision assimilation expectations.
- Backward-compatibility behavior for hand-written Markdown.

Initial canonical internal model:

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

Initial item kinds:

```text
MentalModel
Architecture
AuthorityBoundary
Constraint
StableDecision
DecisionRationale
OpenQuestion
ActiveRisk
RecentChange
Unknown
```

Rules:

- `OperationalContextDocument` is the canonical shape for generation, review, semantic diff, compression, decision assimilation, projection, certification, and instrumentation.
- Markdown parsing produces this model.
- Markdown rendering consumes this model.
- Hand-written Markdown that cannot be fully classified must be preserved in `AdditionalSections`.
- Unknown content must not be silently discarded.
- Coarse item identity can start with normalized section name plus normalized text hash; durable manually assigned ids are optional later.
- The first implementation should not attempt deep semantic interpretation beyond section and list-item classification.

## Operational Context Document Format

Generated and promoted operational context should use a stable Markdown structure so backend services can parse it into `OperationalContextDocument`, compute coarse changes, and expose projections.

Markdown remains the persisted repository artifact. `OperationalContextDocument` remains the service-level representation.

Default structure:

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

Rules:

- The generator may omit empty sections only if the parser and UI tolerate absence.
- The promotion path must preserve reviewer edits exactly in the promoted Markdown.
- The parser must degrade gracefully for hand-written Markdown that does not follow the generated structure.
- Semantic diff must start coarse and deterministic:
  - Section added.
  - Section removed.
  - Section changed.
  - Item added.
  - Item removed.
  - Item changed.
  - Constraint added or removed.
  - Question added or removed.
  - Risk added or removed.
- Deeper semantic interpretation is deferred until the coarse model is certified.

## Backend API Surface

Add endpoints under repository scope:

```text
GET  /api/repositories/{repositoryId}/operational-context
POST /api/repositories/{repositoryId}/operational-context/generate
GET  /api/repositories/{repositoryId}/operational-context/proposals
GET  /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}
PUT  /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/content
POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/accept
POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/reject
POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/promote
GET  /api/repositories/{repositoryId}/continuity/diagnostics
POST /api/repositories/{repositoryId}/continuity/reports
GET  /api/repositories/{repositoryId}/continuity/reports
```

Endpoint behavior:

- Generation is manual and user-triggered.
- Regeneration creates a new proposal and makes older pending reviews stale.
- Accept and reject mutate proposal review metadata only.
- Promotion mutates `.agents/operational_context.md` only after validation.
- Diagnostics and reports are read-only observations unless explicitly generating a report artifact.

Update existing endpoints and projections:

- Extend `GET /api/repositories` dashboard projection with operational-context revision and continuity summary fields.
- Extend `GET /api/repositories/{repositoryId}/workspace` with an `OperationalContextProjection`.
- Extend `GET /api/repositories/{repositoryId}/execution/context` so operational context participates in preview diagnostics.

## Tauri Shell Updates

Extend `src/CommandCenter.Shell/src/main.rs` as HTTP bridge only.

Add typed commands:

- `get_operational_context`.
- `generate_operational_context_proposal`.
- `list_operational_context_proposals`.
- `get_operational_context_proposal`.
- `edit_operational_context_proposal`.
- `accept_operational_context_proposal`.
- `reject_operational_context_proposal`.
- `promote_operational_context_proposal`.
- `get_continuity_diagnostics`.
- `generate_continuity_report`.
- `list_continuity_reports`.

Keep Rust command logic limited to request/response bridging and error translation. Do not implement continuity decisions in Rust.

## UI Plan

The current `App.tsx` is large and already carries repository, artifact, execution, handoff, and Git workflow UI. Continuity work should refactor as features are added rather than further concentrating behavior in one component.

Target UI structure:

```text
src/CommandCenter.UI/src/
  api.ts
  types.ts
  App.tsx
  components/
    RepositoryDashboard.tsx
    RepositoryWorkspace.tsx
    ArtifactWorkspace.tsx
    ExecutionWorkspace.tsx
    ExecutionContextPanel.tsx
    HandoffReview.tsx
    GitWorkflow.tsx
    OperationalContextSurface.tsx
    OperationalContextProposalPanel.tsx
    OperationalContextReviewPanel.tsx
    ContinuityDiagnosticsPanel.tsx
```

UI principles:

- Keep a dense operational workspace.
- Do not create a separate top-level continuity workspace.
- Show understanding in the repository workspace alongside execution and artifacts.
- Use backend projections as authority.
- Let users inspect current and proposed understanding side by side.
- Expose semantic understanding changes, not only raw text diffs.
- Keep accept, reject, edit, and promote controls explicit.
- Do not auto-generate or auto-promote from UI lifecycle effects.

## Milestone M0 - Operational Context Architecture Ratification

### Objective

Ratify operational context as the current project understanding artifact and document its authority boundaries before implementation work mutates behavior.

### Deliverables

- Update `docs/architecture.md` with an operational-context section.
- Add `docs/operational-context-schema.md` as the implementation contract for `OperationalContextDocument`.
- Define operational-context ontology:
  - Current mental model.
  - Architecture.
  - Authority boundaries.
  - Constraints.
  - Stable decisions.
  - Decision rationale.
  - Open questions.
  - Active risks.
- Define explicit exclusions:
  - Raw history.
  - Execution streams.
  - Conversation logs.
  - Complete handoff archives.
  - Git commit history.
  - Milestone status tracking.
- Document artifact responsibility boundaries for plan, milestones, handoff, decisions, and operational context.
- Document the future execution-context consumption contract:

```text
Plan
Selected Milestone
Operational Context
Current Handoff
Current Decisions
Git Snapshot
```
- Document schema expectations:
  - Canonical sections.
  - Allowed item kinds.
  - Parser fallback behavior.
  - Renderer behavior.
  - Projection mapping.
  - Coarse diff categories.
  - Compression tiers.
  - Decision-assimilation hooks.

### Implementation Notes

- No runtime workflow changes.
- Implementation may add inert schema/model types and parser tests only if needed to certify the schema contract.
- No UI workflow changes.
- No proposal generation.
- No lifecycle mutation.

### Certification

Verify the architecture document clearly answers:

- What operational context is.
- What belongs in it.
- What does not belong in it.
- How it differs from plan, milestones, handoff, and decisions.
- How it will participate in execution without replacing existing inputs.
- How Markdown maps to `OperationalContextDocument`.
- What coarse semantic changes are supported initially.

## Milestone M1 - Operational Context Consumption

### Objective

Make `.agents/operational_context.md` a first-class optional execution input.

### Backend Changes

- Add `IArtifactService.GetCurrentOperationalContextAsync`.
- Extend `ArtifactService` to load current operational context through the same safe relative-path logic used for handoff and decisions.
- Add `.agents/operational_context.md` as an optional artifact in `ExecutionContextService`.
- Use artifact role `OperationalContext`.
- Update `ExecutionPromptBuilder.ArtifactRoleOrder` to:

```text
Plan
Milestone
OperationalContext
CurrentHandoff
CurrentDecisions
```

- Update prompt text to include an operational-context section when present.
- Update `ExecutionPromptMetadata.IncludedArtifactPaths` ordering expectations.
- Extend diagnostics so operational context contributes byte count, character count, warning threshold status, and hard-limit status.
- Missing operational context must be reported as an optional missing artifact and must not block preview or launch.
- Empty operational context is allowed.
- Oversized operational context uses existing context size diagnostics; hard-limit excess blocks launch through the existing `LaunchBlocked` path.

### UI Changes

- Execution context preview must show operational context presence, size, and content.
- The context artifact list must display `OperationalContext` between milestone and handoff.
- Context diagnostics must show operational-context contribution.
- No generation, review, lifecycle, or promotion controls are added in this milestone.

### Tests

Add or update backend tests:

- `ExecutionContextServiceTests` verifies operational context is included when present.
- Missing operational context is optional and reported.
- Operational-context size contributes to aggregate and per-artifact diagnostics.
- Hard-limit excess blocks launch.
- `ExecutionPromptBuilderTests` verifies ordering and prompt inclusion.
- Endpoint test verifies preview returns operational context artifact data.

### Certification

Operational context is certified as a passive execution input when:

- It is discovered and loaded when present.
- It is optional when missing.
- It appears in prompt output and metadata.
- It appears in preview and diagnostics.
- Providers remain unaware of artifact source details.

## Milestone M2 - Operational Context Generation

### Objective

Generate and persist reviewable proposed project understanding without modifying `.agents/operational_context.md`.

### Backend Changes

- Implement `OperationalContextDocument`, `OperationalContextItem`, `OperationalContextSection`, and `OperationalContextItemKind` according to `docs/operational-context-schema.md`.
- Implement `MarkdownOperationalContextParser` and renderer before proposal generation.
- Add `IOperationalContextGenerationService`.
- Add `IOperationalContextProposalStore`.
- Add repository-owned proposal persistence under:

```text
.agents/operational_context/proposals/<proposal-id>/
```

- Introduce `OperationalContextProposal` with:
  - `ProposalId`.
  - `RepositoryId`.
  - `GeneratedAt`.
  - `Status`.
  - `InputFingerprints`.
  - `BaselineCurrentContextHash`.
  - `GeneratedContentHash`.
  - `GeneratedContentRelativePath`.
  - `EditedContentRelativePath`.
  - `SemanticChanges`.
  - `CompressionSummary`.
- Introduce `OperationalContextInputSet` containing:
  - Current operational context when present.
  - Current handoff when present.
  - Current decisions when present.
  - Bounded execution session summaries from `IExecutionSessionStore`.
  - Planning state and milestone inventory.
  - Repository identity and availability.
- Do not consume Git commit history, raw execution streams, raw provider output, or full conversation logs.
- Implement deterministic generation as a backend service that:
  - Parses existing context into `OperationalContextDocument`.
  - Generates a new `OperationalContextDocument`.
  - Renders the proposed document to Markdown for persistence.
  - Preserves existing stable understanding.
  - Incorporates latest handoff and decision signal.
  - Uses execution history only as bounded metadata.
  - Produces the stable operational-context Markdown structure.
  - Compresses completed work into current conclusions.
  - Excludes chronological session replay.
- Generate a coarse semantic change summary from current document to proposed document.
- Persist generated proposal content and metadata before returning.
- Regeneration creates a new proposal and marks previous pending proposal as stale or superseded.

### API Changes

- `POST /api/repositories/{repositoryId}/operational-context/generate` creates a proposal.
- `GET /api/repositories/{repositoryId}/operational-context/proposals` lists proposals.
- `GET /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}` loads proposal detail.

### Projection Changes

- Extend `RepositoryWorkspaceProjection` with a proposal summary:
  - Pending proposal exists.
  - Latest proposal id.
  - Generated timestamp.
  - Proposal status.
  - Source input count.
  - Content byte and character count.

### UI Changes

- Add a manual `Generate Proposal` action.
- Show proposal existence, generated timestamp, status, and semantic changes.
- Do not add accept, reject, edit, or promote actions yet.

### Tests

Add backend tests:

- Generation succeeds without existing operational context.
- Generation uses existing operational context when present.
- Generation succeeds when handoff, decisions, or execution history are missing.
- Proposal persists across service recreation.
- Proposal content contains understanding sections rather than chronological replay.
- Parser maps canonical Markdown sections into `OperationalContextDocument`.
- Parser preserves unknown hand-written sections instead of discarding them.
- Renderer round-trips the document model into stable Markdown.
- Coarse semantic changes report section and item changes without deep interpretation.
- Regeneration creates a new proposal and supersedes stale pending review state.
- Workspace projection surfaces latest proposal summary.

### Certification

Generation is certified when Command Center can read current understanding and available repository artifacts, create a proposed understanding artifact, persist it, surface it in the workspace, and leave `.agents/operational_context.md` unchanged.

## Milestone M3 - Operational Context Review

### Objective

Introduce human review for proposed project understanding without promoting it.

### Backend Changes

- Add `IOperationalContextReviewService`.
- Add `OperationalContextReview` with:
  - `ProposalId`.
  - `ReviewState`.
  - `BaselineCurrentContextHash`.
  - `ReviewedContentHash`.
  - `ReviewedAt`.
  - `ReviewNote`.
  - `StaleReason`.
- Supported review states:
  - `PendingReview`.
  - `Edited`.
  - `Accepted`.
  - `Rejected`.
  - `Stale`.
- Add edit operation:
  - Stores reviewer content in `edited.md`.
  - Parses reviewer content into `OperationalContextDocument`.
  - Recomputes content hash.
  - Recomputes coarse semantic changes against current context.
  - Keeps proposal reviewable.
- Add accept operation:
  - Requires proposal exists.
  - Requires proposal is latest or otherwise not superseded.
  - Requires current operational-context hash matches proposal baseline.
  - Stores accepted content hash.
  - Does not write `.agents/operational_context.md`.
- Add reject operation:
  - Stores rejection state and optional review note.
  - Leaves proposal content for audit.
- Add stale protection:
  - Blocks accept when current context changed after proposal generation.
  - Blocks accept when proposal was superseded by regeneration.

### API Changes

- `PUT /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/content`
- `POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/accept`
- `POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/reject`

### UI Changes

- Display current understanding and proposed understanding side by side.
- Display semantic understanding changes:
  - Sections added, removed, or changed.
  - Items added, removed, or changed.
  - Constraints added or removed.
  - Questions added or removed.
  - Risks added or removed.
  - Decision items added or removed when available.
- Provide edit, accept, and reject controls.
- Preserve reviewer edits in a Markdown editor.
- Make stale proposals visibly blocked.
- Promotion controls remain absent.

### Tests

Add backend tests:

- Pending proposal is reviewable.
- Current and proposed content load together.
- Current and proposed content parse into `OperationalContextDocument`.
- Editing persists and changes accepted content candidate.
- Accept records review state without changing current context.
- Reject records review state and blocks promotion.
- Accept fails for missing, superseded, or stale proposals.
- Review state survives service recreation.

### Certification

Review is certified when a user can inspect current and proposed understanding, understand semantic changes, edit proposed content, accept or reject the proposal, and still leave `.agents/operational_context.md` unchanged.

## Milestone M4 - Operational Context Lifecycle

### Objective

Promote accepted proposed understanding into authoritative current understanding and preserve prior understanding revisions.

### Backend Changes

- Extend `ArtifactService` to discover historical operational-context files:

```text
.agents/operational_context.0001.md
.agents/operational_context.0002.md
```

- Extend `ArtifactInventory` with `HistoricalOperationalContexts`.
- Extend `ArtifactRotationService`:
  - Add `RotateCurrentOperationalContextAsync`.
  - Support `ArtifactFamily.OperationalContext`.
  - Current path: `.agents/operational_context.md`.
  - Historical directory: `.agents`.
  - Historical base name: `operational_context`.
- Add `IOperationalContextLifecycleService`.
- Add `PromoteOperationalContextAsync`.
- Promotion preconditions:
  - Proposal exists.
  - Proposal is accepted.
  - Proposal is latest or not superseded.
  - Proposal accepted content hash matches stored accepted content.
  - Current operational-context hash matches review baseline.
  - Rejected proposals cannot promote.
- Promotion sequence:
  - If current context exists, archive it first to the next `.agents/operational_context.NNNN.md`.
  - If archive fails, block promotion and leave current context unchanged.
  - Write accepted content to `.agents/operational_context.md`.
  - Record promotion timestamp, proposal id, revision number, and archived path in proposal metadata.
  - Refresh repository projections.
- Bootstrap sequence:
  - If no current context exists, write accepted content as first `.agents/operational_context.md`.
  - No historical revision is created for bootstrap.

### Failure Handling

- Archive failure blocks write.
- Write failure leaves current context unchanged; any copied archive remains valid as a historical duplicate of the still-current context and must be reported in promotion metadata.
- Stale proposal blocks promotion.
- Missing accepted review blocks promotion.

### UI Changes

- Add `Promote` action only for accepted, non-stale proposals.
- Show current revision count.
- Show last promotion timestamp.
- Show archived prior version path when present.
- Show understanding history available as a count, not a full history browser.

### Tests

Add backend tests:

- Bootstrap promotion creates `.agents/operational_context.md`.
- Revision promotion archives prior current context before replacement.
- Historical numbering uses highest existing number plus one.
- Promotion rejects rejected, pending, superseded, or stale proposals.
- Archive failure blocks promotion.
- Write failure does not erase current context.
- Artifact inventory includes current and historical operational-context revisions.
- Workspace projection updates after promotion.
- Promotion state survives service recreation.

### Certification

Lifecycle is certified when accepted understanding can become authoritative, prior understanding is preserved, numbering is correct, stale promotion is blocked, and repository projections reflect the lifecycle state.

## Milestone M5 - Understanding Compression

### Objective

Keep operational context high-signal across many revisions by preserving understanding while reducing historical detail.

M5 implements section- and tier-level compression over `OperationalContextDocument`. Decision-specific compression remains limited until decision analysis is added in M6.

### Backend Changes

- Add `IUnderstandingCompressionService`.
- Introduce information tiers:
  - `PermanentUnderstanding`: architecture, authority boundaries, fundamental constraints, stable decisions, system mental model.
  - `ActiveUnderstanding`: active risks, open questions, current tradeoffs, pending research.
  - `HistoricalUnderstanding`: resolved risks, completed investigations, retired tradeoffs.
  - `HistoricalNoise`: execution narratives, repeated status updates, superseded detail.
- Extend generation to classify proposal content by tier.
- Classify content using `OperationalContextDocument` sections and item kinds, not raw Markdown scanning.
- Extend proposal metadata with `OperationalContextCompressionSummary`:
  - Preserved item count.
  - Added item count.
  - Modified item count.
  - Removed item count.
  - Compressed item count.
  - Noise removed indicators.
  - Stable understanding retention warnings.
- Add compression rules:
  - Always preserve architecture, constraints, intent, authority boundaries, and current mental model.
  - Preserve risks, questions, tradeoffs, and research areas while active.
  - Compress resolved investigations into outcomes and current relevance.
  - Remove transient execution details and repeated information.
- Add quality warnings when:
  - Architecture disappears.
  - Constraints disappear.
  - Open questions disappear without resolution.
  - Active risks disappear without retirement.
  - Proposal growth indicates historical replay.
- Treat decision items conservatively in M5:
  - Preserve stable decision and rationale items already present in the document.
  - Do not attempt to decide which new decision artifacts deserve assimilation.
  - Defer decision taxonomy, rationale analysis, and decision-aware compression to M6.

### UI Changes

- Review panel shows:
  - Added understanding.
  - Removed understanding.
  - Compressed understanding.
  - Stable understanding retention warnings.
  - Revision summary.
- Do not expose low-level compression internals as controls.

### Tests

Add backend tests:

- Architecture survives multiple generated revisions.
- Constraints survive compression.
- Resolved questions are removed or moved to conclusions only when resolution evidence exists.
- Unresolved questions remain visible.
- Retired risks compress appropriately.
- Historical noise does not accumulate.
- Compression summary flags accidental loss of stable understanding.
- Decision-related content already present in operational context is preserved rather than aggressively compressed.

### Certification

Compression is certified when operational context can evolve through repeated proposal and promotion cycles while preserving architecture, constraints, stable decisions, open questions, and active risks without becoming a chronological archive.

## Milestone M6 - Decision Continuity

### Objective

Assimilate important decisions and rationale into current understanding while keeping decision artifacts as the decision history authority.

M6 completes the decision-aware portion of compression. After M6, compression can distinguish durable decision rationale from tactical or historical decision noise.

### Backend Changes

- Add `IDecisionAnalysisService`.
- Parse current decisions and bounded relevant historical decision artifacts through `ArtifactService`.
- Introduce decision taxonomy:
  - `ArchitecturalDecision`.
  - `StrategicDecision`.
  - `TacticalDecision`.
  - `HistoricalDecision`.
- Analyze decisions for:
  - Decision statement.
  - Rationale.
  - Constraints introduced.
  - Consequences.
  - Open decision questions.
  - Superseded or retired decisions.
- Generation must assimilate:
  - Architectural decisions.
  - Strategic decisions.
  - Decision rationale that explains durable constraints.
  - Open decisions as open questions.
- Generation must not assimilate:
  - One-time approvals.
  - Temporary workarounds with no future relevance.
  - Execution detail.
  - Closed investigations without current consequence.
- Extend semantic changes with decision-specific change types:
  - Important decision introduced.
  - Decision retired.
  - Rationale changed.
  - Rationale lost warning.
  - Open decision preserved.
  - Open decision resolved.
- Extend compression and review warnings for:
  - Lost decision rationale.
  - Tactical decision accumulation.
  - Historical decision replay.
  - Contradictory decision preservation.

### UI Changes

- Understanding surface shows:
  - Stable decisions.
  - Open decisions.
  - Decision rationale.
  - Decision changes between revisions.
  - Decision rationale changes.
- Review panel asks whether important decisions and rationale were preserved.

### Tests

Add backend tests:

- Architectural decisions survive proposals and promotions.
- Strategic decisions survive while relevant.
- Tactical decisions remain in decisions history without bloating operational context.
- Rationale survives for assimilated decisions.
- Open decisions appear as open questions.
- Duplicate contradictory decisions are flagged.
- Decision rationale loss is surfaced as a warning.

### Certification

Decision continuity is certified when important decisions and their rationale become durable current understanding, unresolved decisions remain visible, and operational context does not become a decision archive.

## Milestone M7 - Understanding Workspace

### Objective

Expose current project understanding as a first-class section inside the existing repository workspace.

### Backend Projection Changes

- Add `OperationalContextProjection` to `RepositoryWorkspaceProjection`.
- Add dashboard continuity summary fields.
- Projection fields:
  - Current context exists.
  - Current relative path.
  - Revision count.
  - Current revision number.
  - Last updated timestamp.
  - Last promotion timestamp.
  - Current understanding summary.
  - Architecture items.
  - Authority boundaries.
  - Constraints.
  - Stable decisions.
  - Decision rationale.
  - Open questions.
  - Active risks.
  - Recent semantic changes.
  - Pending proposal summary.
  - Latest review state.
  - Continuity warnings.
- All values originate from backend parsing and proposal metadata.

### UI Changes

- Add an `OperationalContextSurface` inside repository details.
- Dashboard shows:
  - Operational context present or missing.
  - Revision count.
  - Last updated.
  - Open question count.
  - Active risk count.
- Workspace shows:
  - Current understanding summary.
  - Stable decisions.
  - Open questions.
  - Active risks.
  - Recent understanding changes.
  - Whether operational context is included in execution context preview.
- Keep artifact explorer available for full Markdown editing.
- Avoid building a full historical revision browser.
- Avoid computing understanding state client-side.

### Tests

Add backend tests:

- Projection parses current operational context into expected sections.
- Dashboard exposes revision count and counts for questions/risks.
- Workspace projection includes pending proposal and review state.
- Missing operational context produces explicit missing state, not failure.

Add UI build validation:

- TypeScript build passes.
- Understanding components handle missing, empty, present, pending proposal, accepted proposal, and stale proposal states.

### Certification

Understanding workspace is certified when a user can enter a repository workspace and answer:

- What do we currently believe?
- Why do we believe it?
- What remains unresolved?
- What changed recently?
- What should guide future execution?

without opening historical handoff or decision archives.

## Milestone M8 - Long-Horizon Certification

### Objective

Certify that understanding remains coherent, useful, bounded, and reviewable across repeated execution and operational-context update cycles.

### Certification Harness

Add backend certification tests using temporary repositories and fake services:

```text
Cycle 1:
  execution summary
  handoff update
  decision update
  generate proposal
  review
  promote

Cycle 2:
  execution summary
  handoff update
  decision update
  generate proposal
  review
  promote

Cycle 3:
  execution summary
  handoff update
  decision update
  generate proposal
  review
  promote
```

Verify after each cycle:

- Architecture remains present.
- Constraints remain present.
- Stable decisions remain present.
- Decision rationale remains present.
- Unresolved questions remain visible.
- Resolved questions compress appropriately.
- Active risks remain visible.
- Retired risks do not accumulate indefinitely.
- Context size remains bounded relative to inputs.
- Semantic changes correspond to input changes.
- Restart and service recreation preserve proposals, reviews, current context, and history.

### Context Reconstruction Test

Verify a reviewer can reconstruct project mental model from:

```text
Plan
Current Milestone
Operational Context
```

without reading handoff archives, decision archives, execution events, or session history.

### Drift Detection Test

Verify diagnostics flag understanding changes that have no corresponding input evidence:

- Constraint disappears without decision or context evidence.
- Architecture changes without handoff or decision evidence.
- Open question disappears without resolution evidence.
- Decision rationale disappears while decision remains.

### Workspace Certification

Verify the workspace remains usable after multiple revisions:

- Current understanding remains concise.
- Open questions are visible.
- Stable decisions are visible.
- Active risks are visible.
- Recent changes are visible.
- Dashboard summary remains scannable.

### Certification Exit

Long-horizon continuity is certified when repeated cycles preserve understanding and avoid:

- Knowledge erosion.
- Historical accretion.
- Decision amnesia.
- Open question loss.
- Understanding drift.

## Milestone M9 - Continuity Instrumentation

### Objective

Measure continuity quality without making metrics authoritative or automatic.

### Backend Changes

- Add `IContinuityDiagnosticsService`.
- Add `IContinuityReportService`.
- Add `UnderstandingEvolutionLedger`.
- Compute read-only diagnostics:
  - Revision count.
  - Revision frequency.
  - Current context bytes and characters.
  - Growth rate.
  - Architecture preservation changes.
  - Constraint retention changes.
  - Decision retention changes.
  - Rationale retention warnings.
  - Open question added/resolved/lost counts.
  - Active risk added/resolved/lost counts.
  - Compression summary trends.
  - Repeated investigation indicators.
  - Repeated question indicators.
  - Decision rework indicators.
- Continuity reports can be generated on demand under:

```text
.agents/operational_context/reports/
```

- Reports are diagnostic artifacts, not workflow gates.

### Explicit Non-Metrics

Do not add productivity or session-routing metrics:

- Session reuse.
- Session lifetime as a continuity quality signal.
- Session routing.
- Token consumption unless directly tied to context size diagnostics.
- Commit count.
- Lines changed.
- Execution count as quality.
- Ranking repositories by productivity.
- Scoring users or providers.

### UI Changes

- Add read-only `ContinuityDiagnosticsPanel`.
- Show:
  - Revision count.
  - Context growth.
  - Open question trends.
  - Risk trends.
  - Decision preservation trends.
  - Compression observations.
  - Continuity warnings.
- Avoid single numeric quality scores.
- Avoid auto-correction, auto-rejection, or auto-promotion.

### Tests

Add backend tests:

- Revision tracking reads current and historical operational contexts.
- Constraint loss is detected.
- Decision retention is measured.
- Rationale loss is warned.
- Open question resolution is distinguished from disappearance.
- Compression metrics are calculated from proposal summaries.
- Repeated investigation indicators can be observed.
- Report generation writes a diagnostic artifact without mutating current context.

### Certification

Instrumentation is certified when Command Center can answer, from observable evidence:

- Is understanding improving?
- Is understanding degrading?
- Are decisions surviving?
- Are questions being resolved?
- Is compression working?
- Is continuity succeeding?

while preserving explicit user control and review-before-mutation.

## Cross-Cutting Implementation Details

### Fingerprints And Stale Protection

Use SHA-256 hashes over normalized UTF-8 content for:

- Current operational context at generation time.
- Current operational context at review time.
- Proposed content.
- Edited content.
- Accepted content.
- Current handoff input.
- Current decisions input.

Store absent artifacts as explicit absent markers in fingerprints.

Stale cases:

- Current operational context changed after proposal generation.
- Proposal content changed after acceptance.
- Newer proposal superseded pending proposal.
- Accepted proposal no longer matches accepted content hash.

Stale proposals remain auditable but cannot be accepted or promoted.

### Understanding Model Strategy

All continuity services operate on `OperationalContextDocument`.

Required service boundaries:

- Parser: Markdown to `OperationalContextDocument`.
- Renderer: `OperationalContextDocument` to Markdown.
- Generator: input artifacts to proposed `OperationalContextDocument`.
- Diff: current document plus proposed document to coarse `OperationalContextSemanticChange` values.
- Compression: current document plus proposed document to tier classifications and warnings.
- Decision analysis: decision artifacts to candidate document items and warnings.
- Projection: current document plus lifecycle metadata to workspace read models.

Do not let generation, diffing, compression, or projection bypass the document model and perform unrelated Markdown-specific parsing.

### Parser Strategy

`MarkdownOperationalContextParser` should:

- Parse known headings.
- Preserve unknown sections as additional content.
- Extract list items where possible.
- Normalize whitespace for comparison.
- Avoid rewriting user-authored Markdown unless promotion uses edited/generated content directly.
- Fall back to section-level semantic changes when item-level parsing is not reliable.

### Semantic Change Scope

The first semantic change implementation must stay coarse.

Allowed initial change types:

- `SectionAdded`.
- `SectionRemoved`.
- `SectionChanged`.
- `ItemAdded`.
- `ItemRemoved`.
- `ItemChanged`.
- `ConstraintAdded`.
- `ConstraintRemoved`.
- `QuestionAdded`.
- `QuestionRemoved`.
- `RiskAdded`.
- `RiskRemoved`.
- `DecisionAdded`.
- `DecisionRemoved`.
- `RationaleChanged`.
- `PreservationWarning`.

Avoid implementing:

- Natural-language entailment.
- Confidence scoring.
- Automated correctness judgment.
- Automatic drift correction.
- Fine-grained paragraph rewrite analysis.
- Provider-quality interpretation.

### Generation Strategy

The first generator should be deterministic and testable:

- Use current operational context as the primary baseline.
- Use current handoff and decisions as new signal.
- Use bounded execution summaries as supporting metadata.
- Populate `OperationalContextDocument`.
- Render the document to the stable Markdown format only after generation.
- Prefer preserving existing categories over replacing them.
- Add warnings where inputs indicate possible understanding loss.

Future model-assisted generation can be added behind `IOperationalContextGenerationService` only if it preserves the same proposal, review, and promotion contract.

### Error Handling

Return structured `400`, `404`, or `409` responses with `{ error }` bodies, following existing `Program.cs` conventions.

Use `409 Conflict` for:

- Stale proposal.
- Invalid review transition.
- Invalid promotion transition.
- Archive collision.
- Proposal superseded by regeneration.

Use `404 Not Found` for:

- Missing repository.
- Missing proposal.
- Missing report.

Use `400 Bad Request` for:

- Unsafe paths.
- Invalid request payloads.
- Unsupported operation inputs.

### Projection Cache Refresh

After proposal generation, review, edit, rejection, acceptance, or promotion:

- Refresh repository workspace projection.
- Keep dashboard and workspace summaries consistent.
- Do not rely on client-side state to infer workflow authority.

### Filesystem Safety

All proposal, report, current-context, and historical-context paths must:

- Be repository-relative.
- Stay under repository root.
- Use `ArtifactPath.ResolveRepositoryPath`.
- Avoid user-provided absolute paths.
- Avoid path traversal.

### Persistence

Proposal state is repository-owned and should survive:

- Backend restart.
- App restart.
- Repository re-registration.
- Projection cache refresh.

Local app metadata should not be required to recover operational-context lifecycle state.

## Certification Plan

### Domain 0 - Understanding Model

Verify:

- `docs/operational-context-schema.md` defines canonical sections and item kinds.
- Canonical Markdown parses into `OperationalContextDocument`.
- Hand-written unknown sections are preserved.
- Document rendering produces stable Markdown.
- Coarse semantic changes are deterministic.
- Generation, review, compression, decision analysis, projection, and diagnostics consume the document model rather than independent Markdown parsing.

### Domain 1 - Consumption

Verify:

- Current operational context is discovered.
- Missing operational context is optional.
- Execution context includes operational context.
- Prompt includes operational context in the certified ordering.
- Diagnostics show presence, size, and contribution.
- Preview displays operational context content.

### Domain 2 - Generation

Verify:

- Proposal generation succeeds from partial inputs.
- Existing operational context influences new proposals.
- Generated content is understanding, not raw history.
- Proposal persists under repository `.agents`.
- Regeneration supersedes stale pending review.
- Workspace projection surfaces proposal status.

### Domain 3 - Review

Verify:

- Current and proposed understanding are compared.
- Semantic changes are visible.
- Edit persists.
- Accept records accepted state.
- Reject records rejected state.
- Stale proposal accept is blocked.
- Review state survives restart.

### Domain 4 - Lifecycle

Verify:

- Bootstrap promotion creates current context.
- Revision promotion archives prior current context.
- NNNN numbering uses highest existing sequence plus one.
- Archive-before-replace behavior is preserved.
- Archive/write failures are handled safely.
- Stale proposal promotion is blocked.
- Lifecycle state survives restart.

### Domain 5 - Compression

Verify:

- Architecture survives repeated revisions.
- Constraints survive repeated revisions.
- Stable decisions survive repeated revisions.
- Open questions are retained until resolved.
- Active risks are retained until retired.
- Historical noise is removed.
- Context size growth remains bounded.

### Domain 6 - Decision Continuity

Verify:

- Architectural decisions assimilate into operational context.
- Strategic decisions assimilate while relevant.
- Tactical decisions do not bloat operational context.
- Rationale survives.
- Open decisions remain visible.
- Contradictory decisions are flagged.

### Domain 7 - Workspace Visibility

Verify:

- Current understanding is visible.
- Stable decisions are visible.
- Open questions are visible.
- Active risks are visible.
- Recent understanding changes are visible.
- Dashboard summary is scannable.
- All understanding data originates from backend projections.

### Domain 8 - Long-Horizon Operation

Verify:

- Repeated execution/update cycles preserve understanding.
- Restart recovery preserves current context and proposals.
- Context reconstruction succeeds without historical archives.
- Fresh participant orientation is possible from current plan, current milestone, and operational context.
- Understanding drift is detected.

### Domain 9 - Instrumentation

Verify:

- Revision metrics are reported.
- Growth metrics are reported.
- Constraint retention is observable.
- Decision retention is observable.
- Question lifecycle is observable.
- Compression trends are observable.
- Reports generate without mutating current context.
- Metrics remain read-only.

## Implementation Order

1. Ratify operational-context ontology and authority boundaries in `docs/architecture.md`.
2. Add `docs/operational-context-schema.md` and implement the inert `OperationalContextDocument` model, parser, renderer, and coarse diff tests.
3. Add operational-context consumption to `ExecutionContextService`, prompt building, diagnostics, preview UI, and tests.
4. Add current and historical operational-context artifact discovery and rotation support.
5. Add proposal store, generation service, API endpoints, and proposal projection using `OperationalContextDocument`.
6. Add proposal generation UI and shell commands.
7. Add review service, edit/accept/reject endpoints, coarse semantic diff, and review UI.
8. Add lifecycle service, promotion endpoint, archive-before-replace behavior, and lifecycle UI.
9. Add section- and tier-level compression service, compression summaries, preservation warnings, and review integration.
10. Add decision analysis, decision assimilation, rationale preservation, decision-aware compression extensions, and decision surfaces.
11. Add backend `OperationalContextProjection` and dashboard continuity summaries.
12. Refactor UI into `api.ts`, `types.ts`, and focused workspace components while preserving current execution workflow behavior.
13. Add long-horizon certification tests and fixtures.
14. Add continuity diagnostics, evolution ledger, report generation, dashboard diagnostics, and read-only UI reporting.
15. Run backend tests, UI build, backend build, and shell build.

## Verification Commands

Backend tests:

```text
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj
```

Backend build:

```text
dotnet build CommandCenter.slnx
```

UI build:

```text
npm run build --prefix src/CommandCenter.UI
```

Shell build:

```text
cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml
```

## Non-Goals

Do not implement:

- Decision sessions.
- Continuity sessions.
- Session routing.
- Session reuse as project memory.
- Automatic operational-context generation.
- Automatic proposal acceptance.
- Automatic promotion.
- Full historical browser for operational context revisions.
- Git commit history ingestion for understanding generation.
- Raw provider-output ingestion for understanding generation.
- Conversation transcript storage.
- New repository workflow state machine.
- Client-side understanding authority.
- Hidden private database for repository-owned continuity state.
- Background filesystem watchers or polling.
- Productivity scoring.
- Single numeric continuity score.
- Metrics-driven auto-correction.

## Final Exit State

Command Center can execute work, preserve understanding, compress history, retain important decisions and rationale, keep open questions visible, expose current understanding in the unified workspace, and observe continuity quality across long-running development.

The system remains intentionally constrained:

- Every execution is disposable.
- Every understanding update is artifact-mediated.
- Every mutation is human-reviewed.
- Every promotion preserves prior understanding.
- Every continuity metric is observational.
- The repository remains authoritative.
