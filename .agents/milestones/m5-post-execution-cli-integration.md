# Milestone 5 - Post-Execution CLI Integration

## Objective

make the identification loop actually run after every execution slice.

## Work
- [x] Implement `NonImplementationPostExecutionReviewService`.
  - [x] Accept the pre-slice baseline and post-slice snapshot.
  - [x] Detect execution-produced changes.
  - [x] Classify changed files.
  - [x] Create or update ledger entries.
  - [x] Skip semantic confirmation only for valid exact ledger identities.
  - [x] Run semantic confirmation for candidates not covered by valid ledger identity.
  - [x] Render review evidence under `.agents/evidence/non-implementation/`.
  - [x] Return evidence paths and summary counts.
- [x] Main CLI integration:
  - [x] Capture the pre-slice baseline immediately before `execution.RunAsync`.
  - [x] Run the post-execution review service immediately after `execution.RunAsync` succeeds and before the `.agents` post-execution publish.
  - [x] Keep the service before `CommitGate.CommitPushAndEvaluateAsync` so parent repository changes are reviewed before commit/push.
  - [x] Publish `.agents` after the review service so ledger and evidence are not stranded.
  - [x] If review infrastructure fails, return `LoopOutcome.Failed` with evidence rather than silently skipping the loop.
- [x] Roadmap execution integration:
  - [x] If legacy roadmap execution is re-enabled, apply the same pre/post baseline and post-execution review service around `RoadmapExecutionBridge`.
  - [x] If roadmap execution remains paused, document that the main CLI is the active execution integration and keep roadmap completion review refresh as a backstop.
- [x] Add tests:
  - [x] main CLI captures pre-slice baseline before execution
  - [x] main CLI runs detector/classifier/confirmer/ledger after execution and before `.agents` post-execution publish
  - [x] component tests alone are insufficient: add an end-to-end loop test that a generated root Markdown file reaches the ledger after one execution slice
  - [x] pre-existing dirty Markdown that execution does not touch is not ledgered as current slice output
  - [x] post-execution review failure fails the loop and does not report epic completion

## Detail Notes

`NonImplementationPostExecutionReviewService` should run after every successful execution slice and before the post-execution `.agents` publish. It should:

- accept the pre-slice baseline and post-slice snapshot
- detect execution-produced changes
- classify changed files
- create or update ledger entries
- skip confirmation only for valid exact ledger identities
- run semantic confirmation for routed candidates
- render review evidence under `.agents/evidence/non-implementation/`
- return evidence paths and summary counts

If review infrastructure fails, the loop should return `LoopOutcome.Failed` with evidence rather than silently skipping the review. Component tests are insufficient; at least one CLI-level test must prove a root Markdown file created during an execution slice reaches the ledger.

The service should run before `CommitGate.CommitPushAndEvaluateAsync` so parent repository changes are reviewed before commit/push. The `.agents` publication after review should include ledger and evidence so review state is not stranded.

When wiring the pre/post snapshots, prefer the same timing from Milestone 2: pre-slice after LoopRelay's pre-execution `.agents` context publish and immediately before `execution.RunAsync`; post-slice immediately after `execution.RunAsync` succeeds and before later LoopRelay cleanup when possible.

## Acceptance
- [x] The review loop runs after every successful execution slice.
- [x] `.agents` publication includes review ledger and evidence.
- [x] False closure is impossible where components pass but the operational loop never invokes them.
