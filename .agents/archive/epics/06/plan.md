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

(See ./milestones/m0-boundary-foundation.md)

## Milestone 1: Reasoning Event Substrate

(See ./milestones/m1-event-substrate.md)

## Milestone 2: Cross-Decision and Cross-Artifact Capture

(See ./milestones/m2-cross-artifact-capture.md)

## Milestone 3: Derived Reasoning Graph Navigation

(See ./milestones/m3-graph-navigation.md)

## Milestone 4: Narrative Reconstruction Queries

(See ./milestones/m4-narrative-reconstruction.md)

## Milestone 5: Materialization and Primitive Review

(See ./milestones/m5-materialization-review.md)

## Milestone 6: Optional Specialized Read Models

(See ./milestones/m6-specialized-read-models.md)

## Milestone 7: Long-Horizon Reconstruction Validation

(See ./milestones/m7-long-horizon-validation.md)

## Milestone 8: Outcome-Oriented Certification

(See ./milestones/m8-outcome-certification.md)

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
