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

(See ./milestones/m0-architecture-ratification.md)

## Milestone M1 - Operational Context Consumption

(See ./milestones/m1-context-consumption.md)

## Milestone M2 - Operational Context Generation

(See ./milestones/m2-context-generation.md)

## Milestone M3 - Operational Context Review

(See ./milestones/m3-context-review.md)

## Milestone M4 - Operational Context Lifecycle

(See ./milestones/m4-context-lifecycle.md)

## Milestone M5 - Understanding Compression

(See ./milestones/m5-understanding-compression.md)

## Milestone M6 - Decision Continuity

(See ./milestones/m6-decision-continuity.md)

## Milestone M7 - Understanding Workspace

(See ./milestones/m7-understanding-workspace.md)

## Milestone M8 - Long-Horizon Certification

(See ./milestones/m8-long-horizon-certification.md)

## Milestone M9 - Continuity Instrumentation

(See ./milestones/m9-continuity-instrumentation.md)

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
