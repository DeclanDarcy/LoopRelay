# Milestone 5: Extract Epic Preparation Audit

## Handler Method

```csharp
Task<EpicPreparationResult> ExecuteAsync(
    SelectionDecision selectionDecision,
    ProjectContext projectContext,
    CancellationToken cancellationToken)
```

## Boundary

- [x] Handler returns `EpicPreparationResult`.
- [x] Selected-existing-epic routing after the result remains outside.

## Prompt And Projection

- [x] Phase `Audit selected epic`.
- [x] Runtime prompt `EpicPreparationAudit`.
- [x] Projection prompt `ProjectionForEpicPreparationAudit`.
- [x] State `ExistingEpicSelected -> EpicPreparationAudit`.
- [x] Projection path `.agents/projections/epic-preparation-audit.md`.
- [x] Output during prompt envelope `.agents/evidence/audits`.
- [x] Numbered evidence stem `epic-preparation-audit`.
- [x] Secondary input is active selection content.
- [x] Envelope is the normal prompt transition.

## Required Precondition

- [x] Read and validate fresh active selection through `ActiveSelectionReader`
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

- [x] `## Audit Disposition` field table.
- [x] `## Selected Epic` field table.
- [x] Selected epic fields: `Epic ID`, `Epic Name`.
- [x] Disposition fields: `Disposition`, `Confidence`, `Primary Reason`,
  `Evidence Strength`, `Recommended Next Step`.
- [x] Disposition must be one of `Realign`, `Reimagine`, `Retire`,
  `Insufficient Evidence`.
- [x] Recommended next step must be one of `Realign Epic`, `Reimagine Epic`,
  `Retire Epic`, `Gather More Evidence`.

## Audit Decision Ledger Entry

- [x] State, transition, prompt: `EpicPreparationAudit`.
- [x] Projection `.agents/projections/epic-preparation-audit.md`.
- [x] Output: numbered audit evidence path.
- [x] Decision: parsed disposition.
- [x] Confidence: parsed confidence.
- [x] Rationale: parsed recommended next step.

## Retire Branch

- [x] Load persisted state.
- [x] Build `RetiredEpic` from selection and audit.
- [x] Identity is first known audit `Epic ID`, then selection existing epic id.
- [x] Name is first known audit `Epic Name`, then selection existing epic name,
  then selection recommended initiative.
- [x] Reason is first known audit primary reason, then selection primary reason.
- [x] Throw if no stable identity can be built.
- [x] Upsert retired epics by stable identity.
- [x] Append second decision-ledger entry at state `RetireEpic`, decision
  `Retired Epic`, output audit evidence path.
- [x] Save state `RetireEpic` / `Completed`, from `EpicPreparationAudit` to
  `RetireEpic`, prompt `EpicPreparationAudit`, output audit evidence path,
  decision `Retired Epic`, with replacement retired epics.
- [x] Supersede active trusted selection provenance with
  `RetiredEpicStateDrift`.
- [x] Mark `.agents/selection.md` lifecycle `Superseded`.
- [x] Use lifecycle note
  `Retired epic state changed after EpicPreparationAudit.`.
- [x] Return `EpicPreparationResult.Retired`.

## Insufficient-Evidence Branch

- [x] Throw `RoadmapStepException("Epic preparation audit requires more evidence.")`.
- [x] Audit transition, evidence, HITL capture, and audit decision are already
  durable.
- [x] Do not write a durable blocker inside the branch.
- [x] Outer `RunAsync` reports an ephemeral blocker and returns failed.

## Realign And Reimagine Branches

- [x] Delegate to `ActiveEpicRewriteTransition`.
- [x] Return `EpicPreparationResult.ActiveEpicReady` when promoted.
- [x] Return `EpicPreparationResult.Blocked` when not promoted.

## Input Snapshot

- [x] Required projection.
- [x] Required selection.
- [x] Secondary hash of selection content.

## Integration

- [x] Keep `ContinueAfterSelectionAsync` mapping `Retired` and `Blocked` to
  `RoadmapOutcome.Paused`.
- [x] Keep `ActiveEpicReady` falling through to milestone generation.
