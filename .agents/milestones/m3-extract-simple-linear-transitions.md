# Milestone 3: Extract Simple Linear Transitions

## Work Items

- [ ] Move the smallest single-purpose flows first.

## Bootstrap Roadmap Completion Context

Handler method:

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

### Boundary

- [ ] Keep entry inside `RunFromCoreReadyAsync` after the caller verifies
  `.agents/core/roadmap-completion-context.md` is not `Present`.
- [ ] Handler starts after the caller observes missing or empty completion
  context.
- [ ] Handler ends after context artifact write, HITL capture, and lifecycle
  `Ready`.

### Prompt And Projection

- [ ] Phase `Bootstrap roadmap completion context`.
- [ ] Runtime prompt `CreateRoadmapCompletionContext`.
- [ ] Projection prompt `ProjectionForCreateRoadmapCompletionContext`.
- [ ] State `CoreReady -> RoadmapCompletionContextReady`.
- [ ] Projection path `.agents/projections/roadmap-completion.md`.
- [ ] Output `.agents/core/roadmap-completion-context.md`.
- [ ] Context starts with `# Roadmap Completion Bootstrap`, then projection
  content.
- [ ] Secondary input is rendered completed epic archive evidence.
- [ ] Optional inputs are `.agents/archive/epics/*.md`.
- [ ] Envelope is the normal prompt transition.

### Completed-Epic Archive Rendering

- [ ] List `.agents/archive/epics/*.md` in ordinal path order.
- [ ] Skip files that disappear before read.
- [ ] Extract first `# ` title.
- [ ] Extract `Epic ID` from field table.
- [ ] Extract known evidence sections.
- [ ] Fall back to normalized full content.
- [ ] Assign evidence quality.
- [ ] Apply per-epic and total truncation budgets.
- [ ] Render the fixed no-archive message when no completed epic markdown files
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

- [ ] `TransitionStarted`, `TransitionCompleted`, and `TransitionFailed`.
- [ ] Started decision `Pending`.
- [ ] Completed decision `Completed`.
- [ ] Runtime failure persists `EvidenceBlocked` / `Failed` with output
  `.agents/core/roadmap-completion-context.md` and intent
  `ResolveTransitionFailure`.
- [ ] Projection failures occur before transition-start state.

### Input Snapshot

- [ ] Required projection.
- [ ] Optional completed epic archives.
- [ ] Prompt context hash.
- [ ] Secondary hash of rendered archive evidence.

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
