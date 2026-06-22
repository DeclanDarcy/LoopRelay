# Command Center Reasoning Trajectory Preservation Implementation Plan

## Objective

Implement Reasoning Trajectory Preservation as a small, event-led capability that records how project thinking evolved without creating a second source of truth.

The durable core is intentionally narrow:

```text
Reasoning Event
Reasoning Thread
Reasoning Relationship
Reasoning Reference
Reasoning Reconstruction
```

Hypotheses, alternatives, contradictions, and strategic direction are first represented as event classifications, thread themes, relationship patterns, and derived reconstructions. They are not separate repository-backed state machines at the start.

The implementation is complete when Command Center can answer questions such as:

- Why did this decision replace an earlier decision?
- Why was this alternative rejected?
- What assumption failed?
- Which contradiction changed the direction of work?
- How did the current strategy emerge?

without moving authority away from the existing Decisions, Operational Context, Governance, or Execution domains.

## Authority Model

Reasoning Trajectory is explanatory. It is not authoritative.

Existing authority remains unchanged:

- Decision Lifecycle owns decisions, candidates, proposals, outcomes, resolution, supersession, archival, and decision governance.
- Operational Context owns current settled understanding.
- Governance owns detection of current decision issues and contradictions.
- Execution owns execution directives, execution context, provider sessions, handoff validation, commit, and push.
- Repository files remain authoritative. Runtime memory remains a cache.

Reasoning Trajectory may:

- explain why thinking changed
- preserve why an alternative, assumption, contradiction, or direction mattered
- connect events across decisions, milestones, handoffs, governance findings, operational-context revisions, and execution outputs
- reconstruct a narrative from evidence

Reasoning Trajectory may not:

- approve or reject decisions
- supersede decisions
- mutate `.agents/operational_context.md`
- promote operational-context proposals
- create execution directives
- enforce governance findings
- become a private knowledge database
- resurrect sessions or provider processes as continuity

## Current Codebase Baseline

Command Center currently has:

- .NET backend sidecar in `src/CommandCenter.Backend`.
- React/TypeScript UI in `src/CommandCenter.UI`.
- Rust/Tauri shell in `src/CommandCenter.Shell`.
- Backend tests in `tests/CommandCenter.Backend.Tests`.
- Repository registration, artifact discovery, planning readiness, workspace projections, execution sessions, Git workflow, operational-context continuity, decision lifecycle, and artifact rotation.
- Shared repository, artifact, configuration, planning, and projection infrastructure in `src/CommandCenter.Core`.
- Operational-context parsing, generation, review, promotion, diagnostics, and reporting in `src/CommandCenter.Continuity` and `src/CommandCenter.Middle`.
- Structured decision lifecycle in `src/CommandCenter.Decisions`, including candidates, proposals, reviews, refinements, resolution, governance, execution projection, certification, repository-backed JSON persistence, and markdown projections.
- Execution context, provider launch, event monitoring, handoff workflow, commit, and push services in `src/CommandCenter.Execution`.
- UI primary tabs for Workspace, Execution, Operational Context, Decisions, and Continuity.

Primary gaps:

- No durable record of reasoning evolution across project time.
- No event-level history for failed assumptions, rejected alternatives, recurring contradictions, or direction changes outside decision proposal/revision scope.
- No cross-decision explanation for why thinking changed.
- No derived graph or reconstruction capability for explaining why the current direction exists.
- No outcome certification that reasoning can be reconstructed later.

## Design Principles

1. Preserve events first.
   The first durable model is a reasoning event with provenance. Larger concepts are derived until repeated use proves they need materialized records.

2. Avoid premature ontology lock-in.
   Hypothesis, alternative, contradiction, and direction begin as event families and thread themes. They become top-level persisted entities only after a materialization review proves that event-derived reconstruction is insufficient.

3. Event families do not imply entity existence.
   Event families and event types are classification vocabulary. They are not hidden lifecycle state machines, and they do not authorize creating matching persisted entities. A sequence such as `HypothesisRaised`, `HypothesisSupported`, and `HypothesisInvalidated` is evidence for reconstruction, not proof that a `Hypothesis` aggregate should exist.

4. Derived if reconstructable.
   Before adding any new artifact type, ask:

   ```text
   Can this be reconstructed from events, threads, relationships, and existing domain artifacts?
   ```

   If yes, do not persist it as a separate domain record.

5. Threads are useful but reviewable.
   Threads are introduced early as a pragmatic grouping mechanism for long-lived event chains. They are not exempt from review. If graph traversal and reconstruction later prove thread records are unnecessary or too authoritative, thread storage should be demoted to a derived read model or cache.

6. Graph is an index.
   The graph is rebuilt from events, relationships, threads, and existing domain references. It is not authority and should not be required for recovery.

7. Reconstruction is the product.
   The goal is not to store many artifact families. The goal is to explain how project thinking evolved, with evidence.

8. Certification is outcome-oriented.
   Low-level invariants matter, but certification primarily proves that meaningful questions can be answered later.

9. No hidden knowledge system.
   `.agents/reasoning` stores the minimum event substrate and optional user-requested reports. It must not grow into a parallel Decisions/Operational Context replacement.

## Target Solution Structure

Add a dedicated backend project:

```text
src/
  CommandCenter.Reasoning/
    Abstractions/
    Extensions/
    Models/
    Persistence/
    Primitives/
    Projections/
    Services/
```

Project references:

- `CommandCenter.Reasoning` references `CommandCenter.Core`.
- `CommandCenter.Backend` references `CommandCenter.Reasoning` and maps reasoning endpoints.
- `CommandCenter.Middle` may reference `CommandCenter.Reasoning` for read-only dashboard and workspace projection summaries.
- `CommandCenter.Decisions` should not directly depend on `CommandCenter.Reasoning` at first. Use backend-level or composition-root adapters to append reasoning events after successful decision operations.
- `CommandCenter.Continuity` and `CommandCenter.Execution` should not depend on `CommandCenter.Reasoning` unless a later materialization review approves a small append-only sink.

Add `src/CommandCenter.Reasoning/CommandCenter.Reasoning.csproj` to `CommandCenter.slnx`.

Register services through:

```text
src/CommandCenter.Reasoning/Extensions/ServiceCollectionExtensions.cs
```

and call `builder.Services.AddReasoning()` from `src/CommandCenter.Backend/Program.cs`.

## Repository Layout

Persist the minimum durable substrate:

```text
.agents/
  reasoning/
    events/
      EVT-0001/
        event.json
        event.md
      EVT-0002/
        event.json
        event.md
    threads/
      THR-0001/
        thread.json
        thread.md
    relationships/
      REL-0001/
        relationship.json
        relationship.md
    reports/
      reconstruction.<timestamp>.json
      reconstruction.<timestamp>.md
      certification.<timestamp>.json
      certification.<timestamp>.md
```

Do not initially persist:

```text
.agents/reasoning/hypotheses/
.agents/reasoning/alternatives/
.agents/reasoning/contradictions/
.agents/reasoning/directions/
.agents/reasoning/graph/
.agents/reasoning/queries/
```

Those concepts remain derived projections unless a later materialization review approves them.

Rules:

- `event.json`, `thread.json`, and `relationship.json` are structured records.
- `event.md`, `thread.md`, and `relationship.md` are deterministic human-readable projections.
- Reports are optional outputs created only when a user explicitly asks to persist a reconstruction or certification result.
- A current graph may be cached in memory. If a file cache is later added, it must be explicitly documented as rebuildable derived data, not repository authority.
- IDs are repository-scoped, human-inspectable, sequence allocated by scanning existing artifact directories, and stable across restart.

ID prefixes:

```text
EVT-0001  Reasoning event
THR-0001  Reasoning thread
REL-0001  Reasoning relationship
```

Report IDs:

```text
reconstruction.YYYYMMDDHHMMSSFFFFFFF
certification.YYYYMMDDHHMMSSFFFFFFF
```

## Durable Core Domain Model

Implement these primitives and models first:

```text
ReasoningEventId
ReasoningThreadId
ReasoningRelationshipId
ReasoningEvent
ReasoningEventFamily
ReasoningEventType
ReasoningNarrative
ReasoningReference
ReasoningReferenceKind
ReasoningProvenance
ReasoningThread
ReasoningThreadTheme
ReasoningThreadState
ReasoningRelationship
ReasoningRelationshipType
ReasoningGraph
ReasoningGraphNode
ReasoningGraphRelationship
ReasoningTrace
ReasoningQuery
ReasoningQueryCategory
ReasoningQueryResult
ReasoningReconstruction
ReasoningReconstructionEvidence
ReasoningCertificationReport
ReasoningCertificationEvidence
ReasoningCertificationResult
```

Event families:

```text
Hypothesis
Alternative
Contradiction
Direction
DecisionEvolution
AssumptionEvolution
ConstraintEvolution
Evidence
Thread
```

Event types:

```text
HypothesisRaised
HypothesisSupported
HypothesisChallenged
HypothesisInvalidated
HypothesisRetired
AlternativeIntroduced
AlternativeCompared
AlternativeRejected
AlternativeRevisited
AlternativeSelected
ContradictionIdentified
ContradictionInvestigated
ContradictionResolved
ContradictionAccepted
ContradictionRecurred
DirectionObserved
DirectionReinforced
DirectionShifted
DirectionAbandoned
DecisionSuperseded
DecisionReframed
DecisionReconsidered
AssumptionIntroduced
AssumptionChallenged
AssumptionInvalidated
AssumptionReplaced
ConstraintIntroduced
ConstraintModified
ConstraintRetired
EvidenceAdded
ThreadStarted
ThreadExtended
ThreadForked
ThreadMerged
```

Event taxonomy rules:

- Event families are not aggregates.
- Event types are not lifecycle transition tables.
- Derived statuses may be displayed by graph and reconstruction services, but they must not become mutation authority.
- Adding a new event family requires only event-schema compatibility, not a matching repository directory.
- Adding a new persisted entity requires the materialization review.
- An event sequence can suggest that a concept is important, but importance alone is not enough to materialize it.

Supported reference kinds:

```text
Decision
Proposal
ProposalRevision
Candidate
OperationalContextRevision
GovernanceFinding
ExecutionProjection
ExecutionOutput
Handoff
Artifact
ReasoningEvent
ReasoningThread
```

Supported relationship types:

```text
CausedBy
InfluencedBy
Supports
Challenges
Contradicts
Supersedes
Extends
DerivesFrom
LeadsTo
Replaces
Invalidates
Resolves
Reopens
BelongsTo
ComparesWith
SelectedOver
```

## Event Record Requirements

Every `ReasoningEvent` must include:

```text
Id
RepositoryId
CreatedAt
Family
Type
Title
Narrative
References
Provenance
ThreadIds
Tags
```

Rules:

- Events are immutable.
- Events are append-only.
- Events require provenance.
- Events may reference other domain artifacts, but do not own them.
- Events may be classified as hypothesis, alternative, contradiction, or direction without creating a separate hypothesis, alternative, contradiction, or direction record.
- Corrections are represented by new events that supersede, clarify, or invalidate prior events.

## Thread Record Requirements

Every `ReasoningThread` must include:

```text
Id
RepositoryId
Title
Theme
CreatedAt
UpdatedAt
Summary
EventIds
Tags
```

Rules:

- Threads group events across decisions, milestones, epics, and years.
- Threads are not sessions.
- Threads are not decisions.
- Threads are not authoritative.
- Threads are not proof that a corresponding domain entity exists.
- A thread may be expanded or summarized from its events.
- A thread may be split or merged through append-only events and relationships.
- Threads remain subject to the materialization review. A later review may keep them as persisted grouping records, demote them to derived graph clusters, or restrict their use to reports if persisted thread identity proves too strong.

## Relationship Record Requirements

Every `ReasoningRelationship` must include:

```text
Id
RepositoryId
CreatedAt
Type
Source
Target
Narrative
Provenance
```

Rules:

- Relationships explain reasoning evolution.
- Relationships are not decision authority.
- Relationships may point to reasoning events, reasoning threads, or existing domain references.
- Missing external references produce diagnostics; missing reasoning references fail integrity checks.

## Derived Concepts

Hypotheses, alternatives, contradictions, and direction are initially reconstructed as follows:

Hypothesis:

```text
Event family: Hypothesis
Thread theme: belief under investigation
Common events: HypothesisRaised, HypothesisSupported, HypothesisChallenged, HypothesisInvalidated, HypothesisRetired
Derived status: latest meaningful event in the thread or trace
```

Alternative:

```text
Event family: Alternative
Thread theme: path considered
Common events: AlternativeIntroduced, AlternativeCompared, AlternativeRejected, AlternativeRevisited, AlternativeSelected
Derived status: selected, rejected, revisited, or open based on trace
```

Contradiction:

```text
Event family: Contradiction
Thread theme: conflict between beliefs, decisions, assumptions, or evidence
Common events: ContradictionIdentified, ContradictionInvestigated, ContradictionResolved, ContradictionAccepted, ContradictionRecurred
Derived status: active, resolved, accepted, or recurring based on trace
```

Direction:

```text
Event family: Direction
Thread theme: strategic movement
Common events: DirectionObserved, DirectionReinforced, DirectionShifted, DirectionAbandoned
Derived status: emergent from repeated events and cross-domain traces
```

Direction must remain derived until reconstruction repeatedly shows that a stable first-class `StrategicDirection` object is needed.

## Backend Service Contracts

Add service contracts under `CommandCenter.Reasoning.Abstractions`:

```text
IReasoningRepository
IReasoningArtifactProjectionService
IReasoningEventService
IReasoningEventSink
IReasoningThreadService
IReasoningRelationshipService
IReasoningGraphService
IReasoningQueryService
IReasoningReconstructionService
IReasoningCertificationService
IReasoningReferenceResolver
IReasoningMaterializationReviewService
```

Persistence services:

- `FileSystemReasoningRepository` loads and saves event, thread, relationship, and persisted report artifacts.
- It uses `IArtifactStore` for IO and `ArtifactPath.ResolveRepositoryPath` for path safety.
- It rejects absolute paths, path traversal, and repository escapes.
- It uses deterministic JSON serialization with string enums.
- It stores a `schemaVersion` envelope and rejects unsupported schema versions.
- It validates repository ownership on every artifact.
- It allocates IDs by scanning existing artifact directories.
- It skips corrupt report artifacts in listing operations, matching existing continuity report behavior.

Projection services:

- `ReasoningArtifactProjectionService` renders deterministic markdown projections from structured events, threads, relationships, and persisted reports.
- It does not derive authority from markdown.

Graph and reconstruction services:

- `ReasoningGraphService` builds a derived graph from events, relationships, threads, and resolvable external references.
- `ReasoningQueryService` selects traversal strategies for supported questions.
- `ReasoningReconstructionService` converts traces into narrative explanations with evidence.
- Derived graph and query results should not be persisted by default.

Integration services:

- `ReasoningEventSink` records append-only reasoning events from existing domain workflows.
- A no-op sink should exist for tests or hosts that do not register reasoning.
- Integrations should start with explicit backend endpoints and user-triggered actions while the event schema is still hardening.
- The mature capture model should prefer inferred capture for domain transitions that already occurred, such as decision supersession, proposal resolution, governance report generation, operational-context promotion, handoff acceptance, and execution projection changes.
- Manual capture should remain available for rationale that cannot be inferred from existing artifacts.
- Inferred capture must still preserve provenance and must not mutate the source domain.
- Automatic event capture from existing services should be enabled only after the event schema and idempotency rules are stable.

Capture maturity model:

```text
Manual Capture
  -> Assisted Capture
  -> Inferred Capture
```

Manual capture records reasoning supplied directly by a user.

Assisted capture pre-populates references and provenance from the current workflow, then asks a user for the missing narrative.

Inferred capture records objective reasoning events from already-authoritative domain transitions. It should become the dominant capture path for transitions the system can observe directly.

## Backend API Surface

Add `ReasoningEndpoints.cs` under `src/CommandCenter.Backend/Endpoints` and map it from `Program.cs`.

Repository-scoped endpoints:

```text
GET  /api/repositories/{repositoryId}/reasoning/events
GET  /api/repositories/{repositoryId}/reasoning/events/{eventId}
POST /api/repositories/{repositoryId}/reasoning/events
GET  /api/repositories/{repositoryId}/reasoning/threads
GET  /api/repositories/{repositoryId}/reasoning/threads/{threadId}
POST /api/repositories/{repositoryId}/reasoning/threads
POST /api/repositories/{repositoryId}/reasoning/threads/{threadId}/events
GET  /api/repositories/{repositoryId}/reasoning/relationships
POST /api/repositories/{repositoryId}/reasoning/relationships
GET  /api/repositories/{repositoryId}/reasoning/graph
GET  /api/repositories/{repositoryId}/reasoning/trace/backward
GET  /api/repositories/{repositoryId}/reasoning/trace/forward
POST /api/repositories/{repositoryId}/reasoning/queries
POST /api/repositories/{repositoryId}/reasoning/reconstructions
GET  /api/repositories/{repositoryId}/reasoning/reconstructions
GET  /api/repositories/{repositoryId}/reasoning/certification
POST /api/repositories/{repositoryId}/reasoning/certification
GET  /api/repositories/{repositoryId}/reasoning/certification/reports
GET  /api/repositories/{repositoryId}/reasoning/materialization-review
POST /api/repositories/{repositoryId}/reasoning/materialization-review
```

Do not initially add CRUD endpoints for:

```text
/reasoning/hypotheses
/reasoning/alternatives
/reasoning/contradictions
/reasoning/directions
```

Those views are returned through graph, query, reconstruction, and materialization-review endpoints until a materialization decision is approved.

Endpoint behavior:

- Return `404` for missing repository or reasoning object.
- Return `400` for invalid requests, invalid IDs, unsafe paths, unsupported reference kinds, and missing provenance.
- Return `409` for invalid lifecycle requests, stale commands, duplicate relationships, graph integrity failures, or unresolved required reasoning references.
- Return `200 OK` for successful reads and mutations that return projections.
- Use existing failure body convention: `{ error = "..." }`.

## Tauri Shell Updates

Add Rust bridge commands in `src/CommandCenter.Shell/src/main.rs`:

```text
list_reasoning_events
get_reasoning_event
create_reasoning_event
list_reasoning_threads
get_reasoning_thread
create_reasoning_thread
append_reasoning_thread_event
list_reasoning_relationships
create_reasoning_relationship
get_reasoning_graph
trace_reasoning_backward
trace_reasoning_forward
run_reasoning_query
create_reasoning_reconstruction
list_reasoning_reconstructions
get_reasoning_certification
run_reasoning_certification
list_reasoning_certification_reports
get_reasoning_materialization_review
run_reasoning_materialization_review
```

The commands should mirror existing bridge style: call backend HTTP endpoints, deserialize typed responses where practical, and use `response_error` for non-success responses.

## UI Plan

Add a dedicated Reasoning workspace tab.

Update:

```text
src/CommandCenter.UI/src/state/shellState.ts
src/CommandCenter.UI/src/components/shell/WorkspaceTabs.tsx
src/CommandCenter.UI/src/components/shell/CommandPalette.tsx
src/CommandCenter.UI/src/lib/navigation.ts
src/CommandCenter.UI/src/App.tsx
src/CommandCenter.UI/src/devTauriMock.ts
```

Add:

```text
src/CommandCenter.UI/src/types/reasoning.ts
src/CommandCenter.UI/src/api/reasoning.ts
src/CommandCenter.UI/src/hooks/useReasoningEvents.ts
src/CommandCenter.UI/src/hooks/useReasoningThreads.ts
src/CommandCenter.UI/src/hooks/useReasoningGraph.ts
src/CommandCenter.UI/src/hooks/useReasoningQuery.ts
src/CommandCenter.UI/src/hooks/useReasoningReconstruction.ts
src/CommandCenter.UI/src/hooks/useReasoningCertification.ts
src/CommandCenter.UI/src/hooks/useReasoningMaterializationReview.ts
src/CommandCenter.UI/src/features/reasoning/ReasoningTrajectoryTab.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningEventFeed.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningThreadPanel.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningGraphPanel.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningTracePanel.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningQueryPanel.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningReconstructionPanel.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningMaterializationReviewPanel.tsx
src/CommandCenter.UI/src/features/reasoning/ReasoningCertificationPanel.tsx
src/CommandCenter.UI/src/test/characterization/reasoning*.test.tsx
```

UI rules:

- Show reasoning as explanatory history, not authority.
- Show hypotheses, alternatives, contradictions, and direction as derived views until materialization is approved.
- Keep decision state, operational-context state, governance state, and execution state visually distinct from reasoning references.
- Display provenance beside each event, thread, relationship, query answer, reconstruction narrative, and certification finding.
- Make event immutability visible: editing an event should not exist; correction requires a new event.
- Make graph and reconstruction results auditable by exposing events, nodes, relationships, and evidence used.
- Keep command palette reasoning actions navigation-only until explicit backend workflow authority exists for mutation commands.

## Materialization Gate

Before adding any top-level persisted entity beyond events, threads, relationships, and reports, run a materialization review. The same review also evaluates whether persisted thread records remain justified or should become derived graph clusters.

The review must answer:

- What question cannot be answered from events, threads, relationships, and existing domain artifacts?
- What repeated workflow requires a mutable domain object?
- Why is a derived projection insufficient?
- What authority could this new artifact accidentally imply?
- How will the artifact remain explanatory instead of authoritative?
- How will repository recovery work if the artifact is deleted?
- Can the artifact be rebuilt from events?
- Are event families or event types starting to behave like an unapproved lifecycle state machine?
- Should persisted thread identity remain first-class, become a derived graph cluster, or be limited to persisted reports?

Possible outcomes:

```text
Remain Derived
Add Derived Cache
Add Read Model Report
Promote To First-Class Entity
Reject Concept
```

Promotion to a first-class entity requires a separate implementation slice and must not be bundled into the event substrate work.

Event taxonomy changes require the same discipline. Adding many event types to simulate a hidden lifecycle is not allowed; either keep the concept derived and reconstructable, simplify the taxonomy, or run the materialization review.

## Milestone 0: Boundary and Minimal Ontology Foundation

Goal: establish terminology, boundaries, materialization rules, and repository contracts before storage or workflows.

Workstreams:

- [ ] Add `docs/reasoning-taxonomy.md` with definitions for Reasoning Trajectory, Reasoning Event, Reasoning Thread, Reasoning Relationship, Reasoning Reference, Reasoning Graph, Reasoning Query, and Reasoning Reconstruction.
- [ ] Define hypothesis, alternative, contradiction, and direction as analytical categories first, not first-class persisted entities.
- [ ] Add `docs/reasoning-ownership-boundaries.md` with an ownership matrix:
  - Proposal revisions: Decision Lifecycle.
  - Decision outcomes: Decision Lifecycle.
  - Settled understanding: Operational Context.
  - Execution directives: Execution Projection.
  - Contradiction detection: Governance.
  - Contradiction history: Reasoning Trajectory events.
  - Hypothesis history: Reasoning Trajectory events.
  - Alternative history beyond proposal scope: Reasoning Trajectory events.
  - Direction evolution: derived from Reasoning Trajectory events until materialization is approved.
- [ ] Add `docs/reasoning-materialization-policy.md` documenting the materialization gate, the "derived if reconstructable" rule, thread review, and the rule that event families do not imply entity existence.
- [ ] Add `docs/reasoning-capture-policy.md` documenting manual capture, assisted capture, inferred capture, idempotency, and the expectation that inferred capture becomes dominant for source-domain transitions the system can observe directly.
- [ ] Add `docs/reasoning-authority-boundary.md` documenting that reasoning may support, influence, and explain decisions, but may not override decisions, become authority, or replace governance.
- [ ] Add `docs/reasoning-repository-contracts.md` defining `.agents/reasoning` as repository-scoped, event-led, recoverable, schema-versioned, and separate from `.agents/decisions` and `.agents/operational_context.md`.
- [ ] Add boundary certification notes answering:
  - Why this does not belong in Operational Context.
  - Why this does not belong in Decision Lifecycle.
  - Why this is not just another decision artifact.
  - How reasoning remains non-authoritative.
  - How the plan avoids becoming a second knowledge system.

Tests and verification:

- [ ] Build succeeds after documentation and solution scaffolding.
- [ ] Documentation references current code paths and target paths accurately.
- [ ] No specialized hypothesis, alternative, contradiction, or direction persistence is introduced.
- [ ] Event families are documented as classification vocabulary, not lifecycle authority.
- [ ] Capture policy distinguishes user-supplied rationale from inferred source-domain transitions.

Exit criteria:

- [ ] Vocabulary exists.
- [ ] Ownership exists.
- [ ] Materialization policy exists.
- [ ] Authority boundaries exist.
- [ ] Repository contracts exist.
- [ ] Boundary certification passes by documentation alone.

## Milestone 1: Reasoning Event Substrate

Goal: implement durable events, threads, relationships, references, provenance, identity, persistence, markdown projection, endpoints, and basic UI.

Backend work:

- [ ] Add `CommandCenter.Reasoning` project and solution entry.
- [ ] Add primitives and models for events, threads, relationships, references, provenance, and event classification.
- [ ] Implement event identity, thread identity, and relationship identity as repository-scoped sequence IDs.
- [ ] Implement `ReasoningArtifactDocument<T>` with schema version, repository ID, created/updated timestamps, and payload.
- [ ] Implement `ReasoningJson.Options` with deterministic JSON and string enums.
- [ ] Implement `ReasoningArtifactPaths` with safe relative paths and ID validation.
- [ ] Implement `IReasoningRepository` and `FileSystemReasoningRepository`.
- [ ] Implement `IReasoningArtifactProjectionService` and markdown projections for events, threads, and relationships.
- [ ] Implement `IReasoningEventService`, `IReasoningThreadService`, and `IReasoningRelationshipService`.
- [ ] Enforce event immutability.
- [ ] Enforce event provenance.
- [ ] Validate supported reference kinds.
- [ ] Validate relationship source and target references.
- [ ] Add `AddReasoning()` service registration.
- [ ] Map event, thread, and relationship endpoints.

UI work:

- [ ] Add Reasoning tab shell entry.
- [ ] Add reasoning DTOs, API wrappers, and hooks for events, threads, and relationships.
- [ ] Add `ReasoningEventFeed`, `ReasoningThreadPanel`, and `ReasoningTracePanel` components.
- [ ] Add command palette navigation targets for the Reasoning tab, event feed, and thread view.

Tests:

- [ ] ID allocation scans existing reasoning artifacts.
- [ ] Event persistence round trips through repository files.
- [ ] Event immutability is enforced.
- [ ] Events require provenance.
- [ ] Repository ownership is enforced.
- [ ] Unsafe IDs and paths are rejected.
- [ ] Unsupported schema versions are rejected.
- [ ] Thread persistence and event grouping round trip.
- [ ] Relationship persistence and validation round trip.
- [ ] Markdown projections are deterministic.
- [ ] Endpoints return expected status codes.
- [ ] Creating hypothesis, alternative, contradiction, or direction family events does not create corresponding entity directories.
- [ ] Event-family sequences produce derived display status only; they do not authorize lifecycle mutations.
- [ ] UI characterization covers event feed, empty states, provenance display, and thread selection.

Exit criteria:

- [ ] Event substrate is operational.
- [ ] Thread grouping is operational.
- [ ] Relationship persistence is operational.
- [ ] No specialized entity persistence exists.

## Milestone 2: Cross-Decision and Cross-Artifact Capture

Goal: use the event substrate to preserve reasoning evolution across decisions and artifacts without adding new state machines.

Backend work:

- [ ] Add explicit commands for recording decision evolution events:
  - decision superseded
  - decision reframed
  - decision reconsidered
  - assumption invalidated
  - constraint changed
- [ ] Add explicit commands for recording hypothesis, alternative, contradiction, and direction events as event classifications.
- [ ] Add reference helpers for decisions, proposals, candidates, governance findings, operational-context revisions, handoffs, execution outputs, and artifacts.
- [ ] Add event templates for common reasoning captures with required provenance fields.
- [ ] Add assisted-capture adapters that pre-populate references and provenance after successful decision operations, starting with supersession. Keep the decision operation authoritative.
- [ ] Add inferred-capture adapters for objective domain transitions once idempotency is stable:
  - decision superseded
  - proposal resolved
  - decision archived
  - governance report generated with contradiction findings
  - operational-context proposal promoted
  - execution handoff accepted or rejected
- [ ] Ensure inferred capture is idempotent by fingerprinting the source transition and refusing duplicate events for the same source transition.
- [ ] Keep manual capture available for narrative details that cannot be inferred from source artifacts.
- [ ] Add workspace projection summary counts by event family and recent thread activity.

UI work:

- [ ] Add event creation forms scoped to current repository.
- [ ] Add "record reasoning" affordances near decision supersession, proposal review, governance findings, and operational-context revisions where the backend supports a reference.
- [ ] Show event family filters for hypothesis, alternative, contradiction, direction, decision evolution, assumption evolution, and constraint evolution.

Tests:

- [ ] Decision A superseded by Decision B can explain why through events and relationships.
- [ ] Decision supersession can create an inferred event from the source transition without a second human action once the adapter is enabled.
- [ ] Inferred capture does not duplicate events when the same source transition is processed twice.
- [ ] Alternative considered, rejected, and revisited is preserved as an event thread.
- [ ] Contradiction discovered and resolved is preserved as an event thread.
- [ ] Direction shift is recorded as an event and remains non-authoritative.
- [ ] Existing decision, governance, operational-context, and execution state is not mutated by reasoning capture.

Exit criteria:

- [ ] Cross-decision evolution is preservable through events.
- [ ] Hypothesis, alternative, contradiction, and direction history can be captured as event families.
- [ ] Observable source-domain transitions have an assisted or inferred capture path.
- [ ] Reasoning capture remains append-only and non-authoritative.

## Milestone 3: Derived Reasoning Graph Navigation

Goal: create a derived graph for navigation only. The graph is not persisted as authority.

Backend work:

- [ ] Add `ReasoningGraph`, `ReasoningGraphNode`, `ReasoningGraphRelationship`, and `ReasoningTrace` models as derived read models.
- [ ] Implement `IReasoningGraphService`.
- [ ] Build graph nodes from events, threads, relationships, and external references.
- [ ] Build graph relationships from persisted relationships, event thread membership, references, and event provenance.
- [ ] Implement backward traceability.
- [ ] Implement forward impact traceability.
- [ ] Implement thread traversal.
- [ ] Add graph read endpoint.
- [ ] Add backward and forward trace endpoints.
- [ ] Keep graph rebuild in memory unless a later cache is justified.

UI work:

- [ ] Add `ReasoningGraphPanel` with node filters, relationship filters, selected node details, backward trace, forward trace, and thread traversal.
- [ ] Use accessible lists/tables first; add visual graph rendering only if it remains readable and tested.

Tests:

- [ ] Graph nodes resolve or report missing external reference diagnostics.
- [ ] No orphan persisted reasoning relationships are produced.
- [ ] Backward trace for a decision can explain causes.
- [ ] Forward trace from a hypothesis event can show resulting alternatives, decisions, contradictions, or direction events.
- [ ] Thread traversal reconstructs event order.
- [ ] Graph output is reproducible from the same repository state.

Exit criteria:

- [ ] Navigation is operational.
- [ ] Causal tracing is operational.
- [ ] Forward impact tracing is operational.
- [ ] Graph remains derived.

## Milestone 4: Narrative Reconstruction Queries

Goal: turn graph traversal into explanations.

Backend work:

- [ ] Add `ReasoningQuery`, `ReasoningQueryCategory`, `ReasoningQueryResult`, `ReasoningReconstruction`, `ReasoningNarrative`, and explainability models.
- [ ] Implement `IReasoningQueryService` and `IReasoningReconstructionService`.
- [ ] Support query categories:
  - Decision: why made, why superseded, what alternatives existed.
  - Hypothesis: what happened, why failed, what evidence mattered.
  - Contradiction: how resolved, did it recur.
  - Direction: why strategy changed, what replaced it.
  - Thread: how a reasoning thread evolved.
- [ ] Convert traces into narratives with cited events, relationships, references, and evidence.
- [ ] Implement "why" reconstruction for decisions, rejected alternatives, direction shifts, accepted contradictions, and invalidated assumptions.
- [ ] Implement historical state reconstruction from event timelines:
  - what hypothesis events were active at a point in time
  - what alternatives existed at a point in time
  - what contradictions were active at a point in time
  - what direction events were visible at a point in time
- [ ] Persist reconstruction reports only when explicitly requested.
- [ ] Add query and reconstruction endpoints.

UI work:

- [ ] Add `ReasoningQueryPanel` with predefined question categories and scoped target selection.
- [ ] Add `ReasoningReconstructionPanel` showing narrative, confidence, evidence, graph path, and diagnostics.
- [ ] Make source evidence visible without forcing users to inspect JSON files.

Tests:

- [ ] "Why was this decision superseded?" reconstructs the chain.
- [ ] "What killed this hypothesis?" reconstructs contradicting evidence.
- [ ] "Why does current strategy exist?" reconstructs direction evolution from events.
- [ ] "What alternatives were rejected?" reconstructs alternative history.
- [ ] M4 does not require persisted hypothesis, alternative, contradiction, or direction entities.
- [ ] Same query over unchanged repository state returns the same reasoning path.
- [ ] UI exposes narrative and supporting evidence.

Exit criteria:

- [ ] Query model is operational.
- [ ] Narrative reconstruction is operational.
- [ ] Historical reconstruction is operational.
- [ ] Explainability is operational.

## Milestone 5: Materialization and Primitive Review

Goal: decide whether any analytical category, including threads, needs first-class persistence based on actual reconstruction limits.

Backend work:

- [ ] Implement `IReasoningMaterializationReviewService`.
- [ ] Analyze event and query usage for repeated patterns that derived reconstruction cannot handle cleanly.
- [ ] Produce a materialization review report with one recommendation per concept:
  - hypothesis
  - alternative
  - contradiction
  - direction
  - thread
- [ ] For each concept, recommend one outcome:
  - remain derived
  - add derived cache
  - add read-model report
  - promote to first-class entity
  - reject concept
- [ ] Require concrete evidence for promotion, including failed reconstruction scenarios or workflow friction.
- [ ] Keep direction derived unless repeated reconstruction proves it is a stable abstraction.
- [ ] Review whether persisted thread identity is still justified or whether thread-like grouping should become a derived graph cluster.
- [ ] Review whether event family/type growth is creating implicit state machines that should be collapsed, renamed, or kept explicitly derived.

UI work:

- [ ] Add `ReasoningMaterializationReviewPanel`.
- [ ] Show each concept's current status, reconstruction evidence, recommendation, and risk.

Tests:

- [ ] Review recommends "remain derived" when scenarios are reconstructable from events.
- [ ] Review flags a concept for possible materialization only when supplied fixtures demonstrate repeated reconstruction failure or excessive workflow duplication.
- [ ] Review never promotes direction solely because direction events exist.
- [ ] Review evaluates whether threads should remain persisted, become derived graph clusters, or be restricted to reports.
- [ ] Review flags event-family growth that resembles an unapproved lifecycle state machine.
- [ ] Review report is advisory and does not create new artifact families.

Exit criteria:

- [ ] Materialization decision point exists.
- [ ] Specialized entity persistence remains blocked without evidence.
- [ ] Thread persistence is explicitly reaffirmed or demoted.
- [ ] Event families remain classification vocabulary, not hidden entity lifecycles.
- [ ] Direction materialization is explicitly deferred unless justified.

## Milestone 6: Optional Specialized Read Models

Goal: implement only the specialized read models approved by Milestone 5. Skip this milestone for concepts that remain derived.

Allowed implementation choices:

- Derived cache: rebuildable file or memory cache, clearly marked non-authoritative.
- Read-model report: persisted reconstruction/report artifact created on demand.
- First-class entity: repository-backed structured artifact with explicit authority disclaimers and recovery rules.

If no concept is approved for materialization, close this milestone with a no-op certification report and proceed to long-horizon validation.

Constraints:

- Do not introduce CRUD endpoints for all concepts by default.
- Do not create state machines just because an event family exists.
- Do not persist direction as a first-class object unless the materialization review proves a stable abstraction.
- Every new artifact type must document how it can be rebuilt or why it cannot be rebuilt.

Tests:

- [ ] Approved read models are rebuildable from events or explicitly justified.
- [ ] No unapproved artifact directories are created.
- [ ] New projections remain explanatory.
- [ ] Existing authority boundaries remain intact.

Exit criteria:

- [ ] Only justified specialization exists.
- [ ] Event-led reconstruction remains the primary path.

## Milestone 7: Long-Horizon Reconstruction Validation

Goal: prove event-led reasoning survives large project histories.

Backend work:

- [ ] Add long-horizon fixture builders for many decisions, many reasoning events, repeated alternatives, recurring contradictions, failed assumptions, and strategic shifts.
- [ ] Implement decision evolution reconstruction for chains, branches, convergence, supersession, and replacement.
- [ ] Implement direction reconstruction as an emergent narrative from events and traces.
- [ ] Implement hypothesis reconstruction for raised, supported, challenged, invalidated, and retired hypothesis events.
- [ ] Implement contradiction reconstruction for identified, investigated, resolved, accepted, and recurring contradiction events.
- [ ] Implement project narrative reconstruction across hypotheses, alternatives, contradictions, direction events, and decisions.
- [ ] Add performance diagnostics for large histories without relying on wall-clock elapsed time as a correctness criterion.

UI work:

- [ ] Add project-level narrative reconstruction view.
- [ ] Add horizon selector for decision, milestone, epic, project, and multi-year reconstruction.
- [ ] Add source evidence collapse/expand controls for large reconstructions.

Tests:

- [ ] A large fixture can answer why current strategy exists.
- [ ] A large fixture can answer why an architecture was chosen.
- [ ] A large fixture can list rejected alternatives and their rationale.
- [ ] A large fixture can list failed assumptions and their outcomes.
- [ ] A large fixture can identify contradictions that changed direction.
- [ ] Reconstruction remains traceable to events and source evidence.
- [ ] Reconstruction remains usable enough for UI consumption.

Exit criteria:

- [ ] Decision evolution reconstruction is operational.
- [ ] Direction reconstruction works without first-class direction persistence.
- [ ] Hypothesis reconstruction works without first-class hypothesis persistence unless materialization was approved.
- [ ] Alternative reconstruction works without first-class alternative persistence unless materialization was approved.
- [ ] Contradiction reconstruction works without first-class contradiction persistence unless materialization was approved.
- [ ] Project narrative reconstruction is operational.

## Milestone 8: Outcome-Oriented Certification

Goal: certify that reasoning can be reconstructed, not merely that low-level records exist.

Backend work:

- [ ] Add `ReasoningCertificationReport`, `ReasoningCertificationEvidence`, and `ReasoningCertificationResult` models.
- [ ] Implement `IReasoningCertificationService`.
- [ ] Implement current certification read without persistence.
- [ ] Implement certification run with persisted report.
- [ ] Implement report listing.
- [ ] Certify repository recovery by rebuilding from `.agents/reasoning`.
- [ ] Certify restart recovery by creating a fresh service graph and reloading artifacts.
- [ ] Certify artifact recovery when markdown projections are missing but structured JSON exists.
- [ ] Certify low-level support invariants:
  - event immutability
  - provenance completeness
  - relationship integrity
  - thread navigability
  - query reproducibility
- [ ] Certify outcome scenarios:
  - explain why a decision was superseded
  - explain why an alternative failed
  - explain why a contradiction mattered
  - explain why an assumption failed
  - explain why current strategy exists
  - reconstruct a reasoning thread across multiple milestones
  - reconstruct project reasoning from repository artifacts after restart
- [ ] Add certification endpoints.

UI work:

- [ ] Add `ReasoningCertificationPanel` showing current certification, persisted reports, pass/fail evidence, outcome scenarios, and recovery diagnostics.
- [ ] Link failed outcome evidence to the affected events, threads, relationships, or referenced domain artifacts where possible.

Tests:

- [ ] Certification passes for an empty repository with no reasoning artifacts and reports "no reasoning captured" as a valid baseline.
- [ ] Certification passes for a repository that can answer the required outcome scenarios.
- [ ] Certification fails when an event lacks provenance.
- [ ] Certification fails when a persisted relationship points to a missing reasoning node.
- [ ] Certification reports unresolved external references as diagnostics unless the target is mandatory for a scenario.
- [ ] Certification can rebuild from structured artifacts after deleting generated markdown projections.
- [ ] Certification survives service restart.
- [ ] Certification endpoint returns current report, persisted run, and history.
- [ ] UI characterization covers passed and failed outcome evidence.

Exit criteria:

- [ ] Decision supersession reasoning is certifiable.
- [ ] Alternative rejection reasoning is certifiable.
- [ ] Contradiction importance is certifiable.
- [ ] Assumption failure is certifiable.
- [ ] Direction emergence is certifiable.
- [ ] Repository recovery is certifiable.
- [ ] Restart recovery is certifiable.
- [ ] Artifact recovery is certifiable.

## Cross-Cutting Implementation Details

### Source Attribution

Every event, relationship, thread extension, query result, reconstruction narrative, materialization recommendation, and certification finding should carry source references where possible.

Minimum source reference fields:

```text
SourceKind
RelativePath
Section
ItemId
DecisionId
ProposalId
CandidateId
ReasoningEventId
ReasoningThreadId
Excerpt
Fingerprint
```

Do not add `HypothesisId`, `AlternativeId`, `ContradictionId`, or `StrategicDirectionId` fields until a materialization review approves those entity types.

### Fingerprints and Stale Protection

Use SHA-256 fingerprints over normalized UTF-8 content for:

- event provenance inputs
- thread event membership commands
- relationship source and target references
- graph inputs
- query inputs
- reconstruction inputs
- certification input state

Reject stale mutation commands when the source event, thread, relationship, or referenced required artifact changed since the command payload was prepared.

### Markdown Projection Rules

Markdown projections must be deterministic and human-readable.

Generated projection order for reasoning events:

```text
Event ID
Event Family
Event Type
Timestamp
Title
Narrative
Threads
References
Provenance
Tags
Diagnostics
```

Generated projection order for reasoning threads:

```text
Thread ID
Theme
Title
Summary
Events
Relationships
Derived Status
Diagnostics
```

Generated projection order for relationships:

```text
Relationship ID
Type
Source
Target
Narrative
Provenance
Diagnostics
```

Generated projection order for reconstructions:

```text
Target
Scope
Question
Narrative
Confidence
Reasoning Path
Evidence Used
Events Used
Relationships Used
Diagnostics
```

Markdown is a projection, not the domain source of truth.

### Error Handling

Use existing backend conventions:

- `400 BadRequest` for invalid payloads, unsafe paths, invalid IDs, and missing provenance.
- `404 NotFound` for missing repository or reasoning object.
- `409 Conflict` for invalid requests, stale commands, duplicate IDs, duplicate relationships, broken required references, and unreconstructable certification scenarios.
- `200 OK` for successful reads and mutations that return projections.

### Workspace and Dashboard Projection Updates

Extend `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection` with a minimal reasoning summary:

```text
ReasoningSummary
  EventCount
  ThreadCount
  RelationshipCount
  HypothesisEventCount
  AlternativeEventCount
  ContradictionEventCount
  DirectionEventCount
  DecisionEvolutionEventCount
  LastEventAt
  LastReconstructionAt
  LastCertificationAt
  CertificationResult
```

Keep this projection read-only and backend-owned.

### Artifact Discovery

Extend artifact discovery only for human-readable reasoning projections and reports that belong in the generic artifact browser:

```text
.agents/reasoning/events/*/event.md
.agents/reasoning/threads/*/thread.md
.agents/reasoning/relationships/*/relationship.md
.agents/reasoning/reports/reconstruction.*.md
.agents/reasoning/reports/certification.*.md
```

Keep structured JSON out of the generic artifact editor unless a typed editor exists.

## Verification Commands

Backend build:

```text
dotnet build CommandCenter.slnx
```

Backend tests:

```text
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj
```

UI lint:

```text
npm run lint --prefix src/CommandCenter.UI
```

UI tests:

```text
npm run test --prefix src/CommandCenter.UI
```

UI build:

```text
npm run build --prefix src/CommandCenter.UI
```

Shell build:

```text
cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml
```

End-to-end UI certification:

```text
npm run test:e2e --prefix src/CommandCenter.UI
```

## Non-Goals

Do not implement:

- decision sessions
- reasoning sessions
- session routers
- session registries
- session reuse
- provider process reuse as continuity
- hidden private reasoning database
- raw conversation transcript storage
- automatic decision approval
- automatic decision resolution
- automatic decision supersession
- automatic operational-context promotion
- reasoning-owned governance enforcement
- reasoning-owned execution directives
- reasoning-owned current understanding
- client-side lifecycle authority
- Tauri-owned reasoning logic
- provider-owned reasoning authority
- productivity scoring
- single numeric reasoning quality score
- metrics-driven mutation
- background filesystem watchers
- background polling for reasoning mutation
- first-class hypothesis persistence before materialization review
- first-class alternative persistence before materialization review
- first-class contradiction persistence before materialization review
- first-class direction persistence before materialization review
- event families treated as implicit entity lifecycles
- persisted threads treated as permanently exempt from review
- manual-only reasoning capture as the mature capture model
- persisted graph authority
- graph visualization that is not backed by accessible structured data

## Final Exit State

Command Center has a dedicated Reasoning Trajectory capability that can:

- preserve immutable reasoning events with provenance
- group events into long-lived reasoning threads
- relate reasoning events, threads, and existing domain artifacts through durable explanatory links
- preserve cross-decision evolution rationale
- preserve hypothesis, alternative, contradiction, and direction history as event-led traces
- infer reasoning events from authoritative source-domain transitions where objective transitions already exist
- build a derived graph for navigation
- trace reasoning backward and forward
- answer reasoning queries with auditable evidence
- reconstruct decision, hypothesis, alternative, contradiction, direction, and project narratives
- validate long-horizon reasoning survivability
- certify that important reasoning questions remain answerable after repository recovery and service restart

The repository remains authoritative, decisions remain authoritative for decisions, operational context remains authoritative for current understanding, governance remains advisory detection, execution remains disposable provider work, and reasoning remains the durable explanation of how project thinking evolved rather than a parallel knowledge system.
