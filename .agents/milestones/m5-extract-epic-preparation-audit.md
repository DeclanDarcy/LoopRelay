# Milestone 5: Extract Epic Preparation Audit

## Handler Method

```csharp
Task<EpicPreparationResult> ExecuteAsync(
    SelectionDecision selectionDecision,
    ProjectContext projectContext,
    CancellationToken cancellationToken)
```

## Boundary

- [ ] Handler returns `EpicPreparationResult`.
- [ ] Selected-existing-epic routing after the result remains outside.

## Prompt And Projection

- [ ] Phase `Audit selected epic`.
- [ ] Runtime prompt `EpicPreparationAudit`.
- [ ] Projection prompt `ProjectionForEpicPreparationAudit`.
- [ ] State `ExistingEpicSelected -> EpicPreparationAudit`.
- [ ] Projection path `.agents/projections/epic-preparation-audit.md`.
- [ ] Output during prompt envelope `.agents/evidence/audits`.
- [ ] Numbered evidence stem `epic-preparation-audit`.
- [ ] Secondary input is active selection content.
- [ ] Envelope is the normal prompt transition.

## Required Precondition

- [ ] Read and validate fresh active selection through `ActiveSelectionReader`
  before audit prompt execution.

## Audit Context Section Order

1. `Projection Content`
2. `Selected Epic`
3. `Repository Inspection Instructions`

## Success Order Before Branching

1. Ensure projection.
2. Build audit context.
3. Run normal prompt envelope.
4. Write `.agents/evidence/audits/epic-preparation-audit.NNNN.md`.
5. Capture HITL requests from audit evidence.
6. Parse `EpicPreparationAuditDecision`.
7. Append audit decision-ledger entry.
8. Route parsed disposition.

## Parser Requirements

- [ ] `## Audit Disposition` field table.
- [ ] `## Selected Epic` field table.
- [ ] Selected epic fields: `Epic ID`, `Epic Name`.
- [ ] Disposition fields: `Disposition`, `Confidence`, `Primary Reason`,
  `Evidence Strength`, `Recommended Next Step`.
- [ ] Disposition must be one of `Realign`, `Reimagine`, `Retire`,
  `Insufficient Evidence`.
- [ ] Recommended next step must be one of `Realign Epic`, `Reimagine Epic`,
  `Retire Epic`, `Gather More Evidence`.

## Audit Decision Ledger Entry

- [ ] State, transition, prompt: `EpicPreparationAudit`.
- [ ] Projection `.agents/projections/epic-preparation-audit.md`.
- [ ] Output: numbered audit evidence path.
- [ ] Decision: parsed disposition.
- [ ] Confidence: parsed confidence.
- [ ] Rationale: parsed recommended next step.

## Retire Branch

- [ ] Load persisted state.
- [ ] Build `RetiredEpic` from selection and audit.
- [ ] Identity is first known audit `Epic ID`, then selection existing epic id.
- [ ] Name is first known audit `Epic Name`, then selection existing epic name,
  then selection recommended initiative.
- [ ] Reason is first known audit primary reason, then selection primary reason.
- [ ] Throw if no stable identity can be built.
- [ ] Upsert retired epics by stable identity.
- [ ] Append second decision-ledger entry at state `RetireEpic`, decision
  `Retired Epic`, output audit evidence path.
- [ ] Save state `RetireEpic` / `Completed`, from `EpicPreparationAudit` to
  `RetireEpic`, prompt `EpicPreparationAudit`, output audit evidence path,
  decision `Retired Epic`, with replacement retired epics.
- [ ] Supersede active trusted selection provenance with
  `RetiredEpicStateDrift`.
- [ ] Mark `.agents/selection.md` lifecycle `Superseded`.
- [ ] Use lifecycle note
  `Retired epic state changed after EpicPreparationAudit.`.
- [ ] Return `EpicPreparationResult.Retired`.

## Insufficient-Evidence Branch

- [ ] Throw `RoadmapStepException("Epic preparation audit requires more evidence.")`.
- [ ] Audit transition, evidence, HITL capture, and audit decision are already
  durable.
- [ ] Do not write a durable blocker inside the branch.
- [ ] Outer `RunAsync` reports an ephemeral blocker and returns failed.

## Realign And Reimagine Branches

- [ ] Delegate to `ActiveEpicRewriteTransition`.
- [ ] Return `EpicPreparationResult.ActiveEpicReady` when promoted.
- [ ] Return `EpicPreparationResult.Blocked` when not promoted.

## Input Snapshot

- [ ] Required projection.
- [ ] Required selection.
- [ ] Secondary hash of selection content.

## Integration

- [ ] Keep `ContinueAfterSelectionAsync` mapping `Retired` and `Blocked` to
  `RoadmapOutcome.Paused`.
- [ ] Keep `ActiveEpicReady` falling through to milestone generation.
