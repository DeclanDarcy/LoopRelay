# Milestone 5 - Post-Execution CLI Integration

## Objective

make the identification loop actually run after every execution slice.

## Work
- [ ] Implement `NonImplementationPostExecutionReviewService`.
  - [ ] Accept the pre-slice baseline and post-slice snapshot.
  - [ ] Detect execution-produced changes.
  - [ ] Classify changed files.
  - [ ] Create or update ledger entries.
  - [ ] Run semantic confirmation for candidates not covered by valid ledger identity.
  - [ ] Render review evidence under `.agents/evidence/non-implementation/`.
  - [ ] Return evidence paths and summary counts.
- [ ] Main CLI integration:
  - [ ] Capture the pre-slice baseline immediately before `execution.RunAsync`.
  - [ ] Run the post-execution review service immediately after `execution.RunAsync` succeeds and before the `.agents` post-execution publish.
  - [ ] Keep the service before `CommitGate.CommitPushAndEvaluateAsync` so parent repository changes are reviewed before commit/push.
  - [ ] Publish `.agents` after the review service so ledger and evidence are not stranded.
  - [ ] If review infrastructure fails, return `LoopOutcome.Failed` with evidence rather than silently skipping the loop.
- [ ] Roadmap execution integration:
  - [ ] If legacy roadmap execution is re-enabled, apply the same pre/post baseline and post-execution review service around `RoadmapExecutionBridge`.
  - [ ] If roadmap execution remains paused, document that the main CLI is the active execution integration and keep roadmap completion review refresh as a backstop.
- [ ] Add tests:
  - [ ] main CLI captures pre-slice baseline before execution
  - [ ] main CLI runs detector/classifier/confirmer/ledger after execution and before `.agents` post-execution publish
  - [ ] component tests alone are insufficient: add an end-to-end loop test that a generated root Markdown file reaches the ledger after one execution slice
  - [ ] pre-existing dirty Markdown that execution does not touch is not ledgered as current slice output
  - [ ] post-execution review failure fails the loop and does not report epic completion

## Acceptance
- [ ] The review loop runs after every successful execution slice.
- [ ] `.agents` publication includes review ledger and evidence.
- [ ] False closure is impossible where components pass but the operational loop never invokes them.
