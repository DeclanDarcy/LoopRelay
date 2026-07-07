# Milestone 7 - HITL Epic Completion Review

## Objective

require explicit human decisions for confirmed non-implementation files at epic completion.

## Work
- [ ] Implement `NonImplementationCompletionReviewService`.
  - [ ] Begin with a fresh repository review refresh, not ledger state alone.
  - [ ] The refresh must detect current changed files not covered by the latest post-execution review and update the ledger before readiness is evaluated.
  - [ ] If no unresolved confirmed or semantically uncertain entries exist after refresh, return `Ready`.
  - [ ] If decisions are missing, write `.agents/review/non-implementation-review.md` plus a decision template at `.agents/review/non-implementation-decisions.md` and return `Blocked`.
  - [ ] Parse human decisions on rerun.
  - [ ] Validate every decision target by ledger entry ID, path, reviewed content hash, and reviewed status.
  - [ ] Apply `Delete` decisions only when the current file path, current content hash, current status, and ledger entry ID match the reviewed target.
  - [ ] If any delete target is stale, replaced, moved, missing unexpectedly, or hash-mismatched, block, rescan, and require a fresh decision.
  - [ ] Validate delete paths stay inside the repository and are not under `.agents`.
  - [ ] Record `Keep`, `Delete`, `KeepSynthesis`, `DiscardSynthesis`, and semantically uncertain entry resolutions durably.
  - [ ] Preserve a `HitlRequested` or `HitlKept` HITL reason where the decision states that the human requested the file or chose to retain it.
- [ ] Decision template grammar:
  - [ ] one table row per unresolved ledger entry
  - [ ] required columns: `Entry ID`, `Path`, `Reviewed SHA-256`, `Reviewed Status`, `Decision`, `HITL Reason`
  - [ ] allowed file decisions: `Keep`, `Delete`, `ResolveFalsePositive`, `Defer`
  - [ ] allowed synthesis decisions in a separate single-row table: `KeepSynthesis`, `DiscardSynthesis`, `DeferSynthesis`
  - [ ] parser rejects duplicate entry IDs, unknown decisions, missing required rows, hash/status/path mismatch, and non-empty decisions for entries no longer unresolved
- [ ] Main CLI integration:
  - [ ] At the top of `LoopRunner.RunAsync`, after `gate.IsEpicCompleteAsync()` returns true and before `completionCertification.CertifyPlanCompletionAsync`, run completion review.
  - [ ] If review is blocked, publish `.agents` state, do not clear the decision-session resume state, and return `LoopOutcome.CompletionBlocked`.
  - [ ] If review applies parent-repo deletions, commit and push those deletions before certification using a commit path that does not increment the stall counter. Add a narrow `CommitGate.CommitPushIfChangedAsync` helper if needed.
  - [ ] Pass review evidence paths to completion certification context so final certification can see the review state.
- [ ] Completion integration:
  - [ ] Extend `CompletionCertificationRequest` with non-implementation review evidence paths or a review summary path.
  - [ ] Include the review summary in `CompletionPromptContextBuilder.BuildEvaluationContextAsync`.
  - [ ] Archive `.agents/review` contents with completed epic artifacts.
- [ ] Roadmap CLI integration:
  - [ ] In `RunCompletionCertificationAsync`, run the same review service before completion evaluation.
  - [ ] If blocked, persist `EvidenceBlocked` with the review request path and a next step to fill the decisions template and rerun.
- [ ] Add tests:
  - [ ] completion review performs a fresh scan before returning ready
  - [ ] ledger has no unresolved entries but current prose/report files exist, so review does not falsely return ready
  - [ ] epic completion blocks when unresolved confirmed entries exist and no decisions file exists
  - [ ] blocked review does not clear decision-session resume state
  - [ ] keep decision records HITL keep/request evidence and allows certification to continue
  - [ ] delete decision removes only repository files outside `.agents` when entry ID/path/hash/status match
  - [ ] stale delete decision blocks when hash changed after review
  - [ ] delete decision rejects path traversal and `.agents` paths
  - [ ] synthesis keep/discard is recorded separately from file keep/delete
  - [ ] semantically uncertain entry can be resolved as false positive, keep, delete, or deferred

## Acceptance
- [ ] Epic completion review happens before final certification closes the epic.
- [ ] Readiness is based on a fresh review refresh plus ledger state, not stale ledger state alone.
- [ ] The human can keep/delete files, keep/discard synthesis, and resolve semantically uncertain entries.
- [ ] Delete decisions cannot remove content that was not reviewed.
- [ ] Decisions are durable and auditable.
- [ ] The workflow does not become autonomous repository acceptance.
