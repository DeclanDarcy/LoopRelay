# Milestone 4: Extract Active Epic Promotion Transitions

## Work Items

- [ ] Introduce `ActiveEpicPromotionCoordinator` before moving these handlers.
- [ ] Use the coordinator for the shared final promotion behavior required by `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, and `SplitEpic`.

## Create New Epic

Handler method:

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

### Preserve

- [ ] Phase `Create new epic`.
- [ ] Fresh active selection validation.
- [ ] Prompt `CreateNewEpic`.
- [ ] State `NewEpicProposed -> ActiveEpicReady`.
- [ ] Projection `.agents/projections/create-new-epic.md`.
- [ ] Secondary input equal to selection content.
- [ ] Prompt start state `NewEpicProposed` / `Started`, decision `Prompt Started`.
- [ ] Prompt-completed state `NewEpicProposed` / `PromptCompleted`, decision `Prompt Completed`.
- [ ] Success state `ActiveEpicReady` / `Completed`, decision `Artifact Promoted`.
- [ ] Failure state `EvidenceBlocked` / `Failed`, decision `Runtime Failure`.
- [ ] Promotion-blocked state `EvidenceBlocked` / `Paused`.
- [ ] No decision-ledger entry from this transition.
- [ ] Caller continues to milestone generation only when `Promoted == true`.

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

### Allowed Prompt And State Pairs

- [ ] `RealignEpic`, `RoadmapState.RealignEpic`.
- [ ] `ReimagineEpic`, `RoadmapState.ReimagineEpic`.

### Preserve

- [ ] Phase text exactly equal to the prompt name.
- [ ] `.agents/epic.md` read when present.
- [ ] Current selection fallback when `.agents/epic.md` is absent.
- [ ] Selection fallback freshness validation.
- [ ] Required audit evidence at `auditPath`.
- [ ] Audit evidence passed in context and as secondary input.
- [ ] Transition input roles for projection, active epic or selection, and audit evidence.
- [ ] Prompt start and prompt-completed states.
- [ ] Active epic write only after classifier and validator pass.
- [ ] Blocked output preserved under `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
- [ ] Active epic lifecycle `Ready` note `Promoted by {prompt}.`.
- [ ] Blocked evidence lifecycle `Blocked`.
- [ ] No decision-ledger entry from the rewrite transition itself.
