# Milestone 4: Extract Active Epic Promotion Transitions

## Work Items

- [x] Introduce `ActiveEpicPromotionCoordinator` before moving these handlers.
- [x] Use the coordinator for the shared final promotion behavior required by
  `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, and `SplitEpic`.

## ActiveEpicPromotionCoordinator Rules Used By This Milestone

### Input Rules

- [x] Target is always `.agents/epic.md`.
- [x] Evidence directory is `.agents/evidence/blockers`.
- [x] Evidence stem is `active-epic-promotion`.
- [x] Artifact name is `active epic`.
- [x] Classifier is `EpicAuthoringOutputClassifier`.
- [x] Validator is `EpicArtifactValidator`.
- [x] Promoted lifecycle state is `Ready`.

### Classification Rules

- [x] No top-level markdown heading: `Ambiguous`.
- [x] First top-level heading contains `Blocked`: `Blocked`.
- [x] First top-level heading matches `# Epic: ...`: `Promotable`.
- [x] Heading resembles `# Epic` without the required colon form, or content
  contains `## Epic Metadata` while the first heading is wrong: `Malformed`.
- [x] Otherwise: `Ambiguous`.

### Validation Rules For Promotable Output

- [x] Reject blank content.
- [x] Reject anything that reclassifies as non-promotable.
- [x] Require headings `## Epic Metadata`, `## Desired Capability`,
  `## Acceptance Criteria`, and `## Milestone Roadmap`.
- [x] Require either `## Strategic Purpose` or `## Strategic Continuity`.
- [x] Require non-empty `Epic ID` and `Status` in the metadata field table.
- [x] Require at least one milestone row with columns `Milestone ID`,
  `Milestone Name`, `Purpose`, `Outcome`, `Depends On`, and
  `Completion Signal`.
- [x] Require non-empty values for `Milestone ID`, `Milestone Name`, `Purpose`,
  `Outcome`, and `Completion Signal`.

### Promotion Success

- [x] Write `.agents/epic.md`.
- [x] Mark `.agents/epic.md` lifecycle `Ready` with the caller-supplied note.
- [x] Capture HITL requests from `.agents/epic.md`.
- [x] Append `ArtifactPromoted` with prompt contract key
  `ArtifactPromotionService`, result `Promoted`, parser decision
  `Active epic promoted`, and the original prompt input snapshot.
- [x] Save state `ActiveEpicReady` / `Completed`, decision
  `Artifact Promoted`, output `.agents/epic.md`.

### Promotion Rejection

- [x] Write exact rejected output to
  `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
- [x] Mark evidence lifecycle `Blocked` with the rejection reason.
- [x] Append `ArtifactPromotionBlocked`.
- [x] Save `EvidenceBlocked` / `Paused`.
- [x] Use transition intent `ResolveArtifactPromotionBlocker`.
- [x] Use next transition `Resolve blocker and rerun`.
- [x] Map promotion status to decision text:
  - blocked: `Artifact Promotion Blocked`
  - ambiguous: `Artifact Promotion Ambiguous`
  - structurally invalid: `Artifact Promotion Invalid`
  - other rejected status: `Artifact Promotion Rejected`

## Create New Epic

Handler method:

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

### Boundary

- [x] Handler returns `ArtifactPromotionResult`.
- [x] Caller controls milestone continuation.
- [x] Caller continues to milestone generation only when `Promoted == true`.

### Prompt And Projection

- [x] Phase `Create new epic`.
- [x] Runtime prompt `CreateNewEpic`.
- [x] Projection prompt `ProjectionForCreateNewEpic`.
- [x] Source/target `NewEpicProposed -> ActiveEpicReady`.
- [x] Projection path `.agents/projections/create-new-epic.md`.
- [x] Output `.agents/epic.md`.
- [x] Secondary input is active selection content.
- [x] Envelope is the promotion-candidate prompt transition.

### Required Precondition

- [x] Read and validate fresh active selection through `ActiveSelectionReader`.

### Context

- [x] Runtime context is built through `BuildCreateOrSplitContext` with
  projection content, selection proposal, and repository inspection
  instructions.
- [x] Reject raw Project Context markers.

### Preserve

- [x] Prompt start state `NewEpicProposed` / `Started`, decision
  `Prompt Started`.
- [x] Prompt-completed state `NewEpicProposed` / `PromptCompleted`, decision
  `Prompt Completed`.
- [x] After `PromptCompleted`, promote through
  `ActiveEpicPromotionCoordinator`.
- [x] Success state `ActiveEpicReady` / `Completed`, decision
  `Artifact Promoted`, lifecycle note `Promoted by CreateNewEpic.`.
- [x] Rejection writes `active-epic-promotion.NNNN.md`, saves
  `EvidenceBlocked` / `Paused`, and returns not promoted.
- [x] Prompt runtime failure saves `EvidenceBlocked` / `Failed`, decision
  `Runtime Failure`, intent `ResolveTransitionFailure`.
- [x] No decision-ledger entry from this transition.

### Input Snapshot

- [x] Required projection.
- [x] Required selection.
- [x] Secondary hash of selection content.

## Realign and Reimagine Active Epic

Handler method:

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(
    string prompt,
    RoadmapState sourceState,
    ProjectContext projectContext,
    string auditPath,
    CancellationToken cancellationToken)
```

### Boundary

- [x] Handler returns `ArtifactPromotionResult`.
- [x] Handler does not parse audit decisions.
- [x] Handler does not append audit decision records.

### Allowed Prompt And State Pairs

- [x] `RealignEpic`, `RoadmapState.RealignEpic`, projection
  `.agents/projections/realign-epic.md`, projection prompt
  `ProjectionForRealignEpic`, lifecycle note `Promoted by RealignEpic.`.
- [x] `ReimagineEpic`, `RoadmapState.ReimagineEpic`, projection
  `.agents/projections/reimagine-epic.md`, projection prompt
  `ProjectionForReimagineEpic`, lifecycle note `Promoted by ReimagineEpic.`.

### Common Behavior

- [x] Phase text exactly equals the prompt name.
- [x] Prefer `.agents/epic.md` as current epic input.
- [x] If `.agents/epic.md` is absent, fall back to fresh active selection
  through `ActiveSelectionReader`.
- [x] Require audit evidence at the supplied `auditPath`.
- [x] Context section order is `Projection Content`, `Current Epic`,
  `Audit Output`, `Repository Inspection Instructions`.
- [x] Audit evidence is both in the context and passed as secondary input.
- [x] Transition input roles are projection, active epic or selection, and audit
  evidence.
- [x] Prompt envelope is promotion-candidate.
- [x] Promotion target is `ActiveEpicReady`.

### Runtime Failure

- [x] Save `EvidenceBlocked` / `Failed`.
- [x] Output path `.agents/epic.md`.
- [x] Decision `Runtime Failure`.
- [x] Intent `ResolveTransitionFailure`.
- [x] Throw already-persisted failure.

### Promotion Success

- [x] Overwrite `.agents/epic.md` only after classification and validation
  pass.
- [x] Lifecycle `Ready` with note `Promoted by {prompt}.`.
- [x] Append `ArtifactPromoted`.
- [x] Save `ActiveEpicReady` / `Completed`, decision `Artifact Promoted`.
- [x] No decision-ledger entry from the rewrite transition itself.

### Promotion Rejection

- [x] Preserve existing `.agents/epic.md`.
- [x] Write `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
- [x] Append `ArtifactPromotionBlocked`.
- [x] Save `EvidenceBlocked` / `Paused`.
- [x] Intent `ResolveArtifactPromotionBlocker`.
- [x] Return not promoted.

### Input Snapshot

- [x] Required projection.
- [x] Required active epic if `.agents/epic.md` exists.
- [x] Required selection if `.agents/epic.md` is absent.
- [x] Required audit evidence path.
- [x] Secondary hash of audit evidence content.
