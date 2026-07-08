# Milestone 7 - HITL Epic Completion Review

## Objective

require explicit human decisions for confirmed non-implementation files at epic completion before the completion flow proceeds. This is a pending-review decision point, not repository acceptance or certification authority.

## Work
- [x] Implement `NonImplementationCompletionReviewService`.
  - [x] Begin with a fresh repository review refresh, not ledger state alone.
  - [x] The refresh must detect current changed files not covered by the latest post-execution review and update the ledger before readiness is evaluated.
  - [x] If no unresolved confirmed or semantically uncertain entries exist after refresh, return `Ready`.
  - [x] If decisions are missing, write `.agents/review/non-implementation-review.md` plus a decision template at `.agents/review/non-implementation-decisions.md` and return `Blocked`.
  - [x] Parse human decisions on rerun.
  - [x] Validate every decision target by ledger entry ID, path, reviewed content hash, and reviewed status.
  - [x] Apply `Delete` decisions only when the current file path, current content hash, current status, and ledger entry ID match the reviewed target.
  - [x] If any delete target is stale, replaced, moved, missing unexpectedly, or hash-mismatched, block, rescan, and require a fresh decision.
  - [x] Validate delete paths stay inside the repository and are not under `.agents`.
  - [x] Record `Keep`, `Delete`, `ResolveFalsePositive`, `Defer`, `KeepSynthesis`, `DiscardSynthesis`, and `DeferSynthesis` decisions durably.
  - [x] Preserve a `HitlRequested` or `HitlKept` HITL reason where the decision states that the human requested the file or chose to retain it.
- [x] Decision template grammar:
  - [x] one table row per unresolved ledger entry
  - [x] required columns: `Entry ID`, `Path`, `Reviewed SHA-256`, `Reviewed Status`, `Decision`, `HITL Reason`
  - [x] allowed file decisions: `Keep`, `Delete`, `ResolveFalsePositive`, `Defer`
  - [x] allowed synthesis decisions in a separate single-row table: `KeepSynthesis`, `DiscardSynthesis`, `DeferSynthesis`
  - [x] parser rejects duplicate entry IDs, unknown decisions, missing required rows, hash/status/path mismatch, and non-empty decisions for entries no longer unresolved
- [x] Main CLI integration:
  - [x] At the top of `LoopRunner.RunAsync`, after `gate.IsEpicCompleteAsync()` returns true and before `completionCertification.CertifyPlanCompletionAsync`, run completion review.
  - [x] If review is blocked, publish `.agents` state, do not clear the decision-session resume state, and return `LoopOutcome.CompletionBlocked`.
  - [x] If review applies parent-repo deletions, persist those approved deletions before completion evaluation using a narrow commit/push helper that does not increment the stall counter if the existing flow requires parent-repo changes to be published.
  - [x] Pass review evidence paths to completion context so final evaluation can see the pending-review decision state.
- [x] Completion integration:
  - [x] Extend `CompletionCertificationRequest` with non-implementation review evidence paths or a review summary path.
  - [x] Include the review summary in `CompletionPromptContextBuilder.BuildEvaluationContextAsync`.
  - [x] Archive `.agents/review` contents with completed epic artifacts.
- [x] Roadmap CLI integration:
  - [x] In `RunCompletionCertificationAsync`, run the same review service before completion evaluation.
  - [x] If blocked, persist `EvidenceBlocked` with the review request path and a next step to fill the decisions template and rerun.
- [x] Add tests:
  - [x] completion review performs a fresh scan before returning ready
  - [x] ledger has no unresolved entries but current prose/report files exist, so review does not falsely return ready
  - [x] epic completion blocks when unresolved confirmed entries exist and no decisions file exists
  - [x] blocked review does not clear decision-session resume state
  - [x] keep decision records HITL keep/request evidence and allows completion evaluation to continue
  - [x] delete decision removes only repository files outside `.agents` when entry ID/path/hash/status match
  - [x] stale delete decision blocks when hash changed after review
  - [x] delete decision rejects path traversal and `.agents` paths
  - [x] synthesis keep/discard/defer is recorded separately from file keep/delete
  - [x] semantically uncertain entry can be resolved as false positive, keep, delete, or deferred

## Detail Notes

Completion review must begin with a fresh repository review refresh, not ledger state alone. This prevents false readiness when the ledger has no unresolved entries but current changed prose/report files exist.

`NonImplementationCompletionReviewService` should return `Ready` only when the fresh refresh plus ledger state has no unresolved confirmed non-implementation entries and no unresolved semantic uncertainties. False positives should remain auditable but should not block by themselves.

If decisions are missing, write:

- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`

and return `Blocked`.

Decision template requirements:

```markdown
| Entry ID | Path | Reviewed SHA-256 | Reviewed Status | Decision | HITL Reason |
| --- | --- | --- | --- | --- | --- |
```

Allowed file decisions:

- `Keep`
- `Delete`
- `ResolveFalsePositive`
- `Defer`

Allowed synthesis decisions in a separate single-row table:

- `KeepSynthesis`
- `DiscardSynthesis`
- `DeferSynthesis`

The parser must reject duplicate entry IDs, unknown decisions, missing required rows, path mismatch, hash mismatch, status mismatch, and non-empty decisions for entries that are no longer unresolved.

Delete decisions require stale-decision validation:

- ledger entry ID matches
- path matches
- reviewed status matches
- current content hash matches the reviewed hash
- current file is still at the reviewed path
- delete path resolves inside the repository
- delete path is not under `.agents`

If a delete target is stale, replaced, moved, missing unexpectedly, hash-mismatched, outside the repository, or under `.agents`, block, rescan, and require a fresh decision.

`Defer` is a valid explicit human decision. Once recorded as `HitlDeferred`, it should not be treated as a missing decision for that review cycle, but it must remain visible in completion context and audit output.

Keep decisions should record `HitlKept`. If the human states the file was originally requested, preserve or attach `HitlRequested` evidence where possible.

In the main CLI, run completion review after `gate.IsEpicCompleteAsync()` returns true and before `completionCertification.CertifyPlanCompletionAsync`. The sequencing ensures required HITL review decisions exist before final completion evaluation; it must not be used as autonomous repository acceptance.

If completion review is blocked:

- publish `.agents` state
- keep decision-session resume state intact
- return `LoopOutcome.CompletionBlocked`

If completion review applies parent-repository deletions, persist those approved deletions before completion evaluation without incrementing the stall counter when the existing flow requires parent-repository changes to be published. A narrow `CommitGate.CommitPushIfChangedAsync` helper is acceptable if needed.

Pass review evidence paths or a summary path into `CompletionCertificationRequest` and include the review summary in `CompletionPromptContextBuilder.BuildEvaluationContextAsync`. This context is evidence of HITL review state, not a repository certification signal. Completed epic archiving should include `.agents/review` contents.

For roadmap completion, run the same review service before completion evaluation. If blocked, persist blocked evidence with the review request path and a next step to fill the decisions template and rerun.

## Acceptance
- [x] Epic completion review happens before final completion evaluation closes the epic.
- [x] Readiness is based on a fresh review refresh plus ledger state, not stale ledger state alone.
- [x] The human can keep/delete files, keep/discard synthesis, and resolve semantically uncertain entries.
- [x] Delete decisions cannot remove content that was not reviewed.
- [x] Decisions are durable and auditable.
- [x] The workflow does not become autonomous repository acceptance.
