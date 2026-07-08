# Milestone 3: Extract Simple Linear Transitions

## Work Items

- [ ] Move the smallest single-purpose flows first.

## Bootstrap Roadmap Completion Context

Handler method:

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

- [ ] Keep entry inside `RunFromCoreReadyAsync` after the caller verifies `.agents/core/roadmap-completion-context.md` is not `Present`.

### Preserve

- [ ] Phase `Bootstrap roadmap completion context`.
- [ ] Prompt `CreateRoadmapCompletionContext`.
- [ ] State `CoreReady -> RoadmapCompletionContextReady`.
- [ ] Projection `.agents/projections/roadmap-completion.md`.
- [ ] Output `.agents/core/roadmap-completion-context.md`.
- [ ] Completed-epic archive glob `.agents/archive/epics/*.md`.
- [ ] Optional archive input behavior.
- [ ] `TransitionStarted`, `TransitionCompleted`, and `TransitionFailed`.
- [ ] Started decision `Pending`.
- [ ] Completed decision `Completed`.
- [ ] Runtime failure intent `ResolveTransitionFailure`.
- [ ] Verbatim write of prompt output to `.agents/core/roadmap-completion-context.md`.
- [ ] Optional HITL capture.
- [ ] Lifecycle `Ready` for `.agents/core/roadmap-completion-context.md`.

## Select Next Epic

Handler method:

```csharp
Task<SelectionDecision> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

### Preserve

- [ ] Phase `Select next strategic initiative`.
- [ ] Prompt `SelectNextEpic`.
- [ ] State `RoadmapCompletionContextReady -> SelectNextStrategicInitiative`.
- [ ] Projection `.agents/projections/select-next-epic.md`.
- [ ] Output `.agents/selection.md`.
- [ ] Empty secondary input.
- [ ] Context section order from `BuildSelectionContextAsync`.
- [ ] Started state before `TransitionStarted`.
- [ ] `TransitionCompleted` before completed state.
- [ ] Completed state before `.agents/selection.md` write.
- [ ] `.agents/selection.md` write before HITL capture.
- [ ] HITL capture before numbered selection evidence.
- [ ] Selection evidence before provenance recording.
- [ ] Provenance before selection lifecycle `Ready`.
- [ ] Parsing after lifecycle.
- [ ] Decision-ledger append after parsing.
- [ ] Parse failures not being converted into `TransitionFailed`.

## Integration

- [ ] Keep `RunSelectionAndFollowingAsync` calling this handler and then calling `ContinueAfterSelectionAsync`.
