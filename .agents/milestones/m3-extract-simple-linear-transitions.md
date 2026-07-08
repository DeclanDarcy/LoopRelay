# Milestone 3: Extract Simple Linear Transitions

## Work Items

- [x] Move the smallest single-purpose flows first.

## Bootstrap Roadmap Completion Context

Handler method:

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

### Boundary

- [x] Keep entry inside `RunFromCoreReadyAsync` after the caller verifies
  `.agents/core/roadmap-completion-context.md` is not `Present`.
- [x] Handler starts after the caller observes missing or empty completion
  context.
- [x] Handler ends after context artifact write, HITL capture, and lifecycle
  `Ready`.

### Prompt And Projection

- [x] Phase `Bootstrap roadmap completion context`.
- [x] Runtime prompt `CreateRoadmapCompletionContext`.
- [x] Projection prompt `ProjectionForCreateRoadmapCompletionContext`.
- [x] State `CoreReady -> RoadmapCompletionContextReady`.
- [x] Projection path `.agents/projections/roadmap-completion.md`.
- [x] Output `.agents/core/roadmap-completion-context.md`.
- [x] Context starts with `# Roadmap Completion Bootstrap`, then projection
  content.
- [x] Secondary input is rendered completed epic archive evidence.
- [x] Optional inputs are `.agents/archive/epics/*.md`.
- [x] Envelope is the normal prompt transition.

### Completed-Epic Archive Rendering

- [x] List `.agents/archive/epics/*.md` in ordinal path order.
- [x] Skip files that disappear before read.
- [x] Extract first `# ` title.
- [x] Extract `Epic ID` from field table.
- [x] Extract known evidence sections.
- [x] Fall back to normalized full content.
- [x] Assign evidence quality.
- [x] Apply per-epic and total truncation budgets.
- [x] Render the fixed no-archive message when no completed epic markdown files
  exist.

### Success Order

1. Ensure projection.
2. Render bootstrap context and completed-epic evidence.
3. Run normal prompt envelope.
4. Write prompt output verbatim to
   `.agents/core/roadmap-completion-context.md`.
5. Capture HITL requests from the context artifact.
6. Mark context lifecycle `Ready`.
7. Return to caller, which proceeds to selection.

### Preserve

- [x] `TransitionStarted`, `TransitionCompleted`, and `TransitionFailed`.
- [x] Started decision `Pending`.
- [x] Completed decision `Completed`.
- [x] Runtime failure persists `EvidenceBlocked` / `Failed` with output
  `.agents/core/roadmap-completion-context.md` and intent
  `ResolveTransitionFailure`.
- [x] Projection failures occur before transition-start state.

### Input Snapshot

- [x] Required projection.
- [x] Optional completed epic archives.
- [x] Prompt context hash.
- [x] Secondary hash of rendered archive evidence.

## Select Next Epic

Handler method:

```csharp
Task<SelectionDecision> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

### Boundary

- [ ] Handler returns `SelectionDecision`.
- [ ] Downstream selection routing remains outside the handler.

### Prompt And Projection

- [ ] Phase `Select next strategic initiative`.
- [ ] Runtime prompt `SelectNextEpic`.
- [ ] Projection prompt `ProjectionForSelectNextEpic`.
- [ ] State `RoadmapCompletionContextReady -> SelectNextStrategicInitiative`.
- [ ] Projection path `.agents/projections/select-next-epic.md`.
- [ ] Output `.agents/selection.md`.
- [ ] Secondary input is empty string.
- [ ] Envelope is the normal prompt transition.

### Selection Context Section Order

1. `Projection Content`
2. `Current Roadmap Completion Context`
3. `Roadmap Source References`
4. `Retired Epics`

### Required Inputs

- [ ] Projection path.
- [ ] `.agents/core/roadmap-completion-context.md`.
- [ ] Every non-empty `.agents/roadmap/*.md`.

### Success Order

1. Ensure projection.
2. Load existing state for retired epics.
3. Build selection context.
4. Run normal prompt envelope.
5. Write `.agents/selection.md`.
6. Capture HITL requests.
7. Write `.agents/evidence/selection/selection.NNNN.md`.
8. Record active selection provenance.
9. Mark `.agents/selection.md` lifecycle `Ready` with evidence path as notes.
10. Parse selection.
11. Append decision-ledger entry.
12. Return `SelectionDecision`.

### Decision Ledger Entry

- [ ] State `SelectNextStrategicInitiative`.
- [ ] Transition `SelectNextEpic`.
- [ ] Prompt `SelectNextEpic`.
- [ ] Projection `.agents/projections/select-next-epic.md`.
- [ ] Input artifacts: empty list.
- [ ] Output artifacts: `.agents/selection.md`.
- [ ] Decision: parsed recommended outcome.
- [ ] Confidence: parsed confidence.
- [ ] Rationale: parsed primary reason.

### Preserve

- [ ] Started state before `TransitionStarted`.
- [ ] `TransitionCompleted` before completed state.
- [ ] Completed state before `.agents/selection.md` write.
- [ ] `.agents/selection.md` write before HITL capture.
- [ ] HITL capture before numbered selection evidence.
- [ ] Selection evidence before provenance recording.
- [ ] Provenance before selection lifecycle `Ready`.
- [ ] Parsing after lifecycle.
- [ ] Decision-ledger append after parsing.
- [ ] Parse failures are not converted into `TransitionFailed`.
- [ ] Parse failure happens after selection artifact, numbered evidence,
  provenance, and lifecycle are written, and before decision-ledger append.

### Input Snapshot

- [ ] Required projection.
- [ ] Roadmap completion context.
- [ ] Roadmap source files.
- [ ] Secondary hash of empty string.

## Integration

- [ ] Keep `RunSelectionAndFollowingAsync` calling this handler and then calling
  `ContinueAfterSelectionAsync`.
