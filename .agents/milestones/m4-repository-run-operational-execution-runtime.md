# Phase 4 - Execute Plan and Operational Turns

Goal: implement Execute Plan exactly as the bridge from planning into operational execution.

## Implementation

- [ ] Close and dispose the held-open planning process.
- [ ] Read `.agents/plan.md`.
- [ ] Copy plan text to `.agents/operational_context.md`.
- [ ] Store plan text in memory cache under `{repositoryId}:Plan` for the active run.
- [ ] Run `ExtractMilestones.Text` as an Operational, ExtraHigh, one-shot Codex turn.
- [ ] Stream milestone extraction output through the execution stream.
- [ ] Verify milestone files exist under `.agents/milestones/m*.md` as produced by Codex.
- [ ] Commit and push the planning/milestone artifacts using the existing Git services.
- [ ] Set repository lifecycle to `ExecutingPlan`.
- [ ] Run `StartExecution.Render(plan)` as an Operational, Medium, one-shot Codex turn.
- [ ] Stream start-execution output through the execution stream.
- [ ] On completion, verify `.agents/handoff.md` exists.
- [ ] Read `.agents/handoff.md` into orchestrator state.
- [ ] Move `.agents/handoff.md` to `.agents/handoffs/handoff.0001.md`.
- [ ] Use a run-scoped monotonic four-digit counter for handoff rotation.
- [ ] Do not transition to the existing `AwaitingAcceptance` gate for this flow; the human gate is the Decision Submit step.
- [ ] Record prompt provenance for `ExtractMilestones` and `StartExecution`.

## Certification

- [ ] Execute Plan performs planning-process closure, operational context copy, memory cache write, milestone extraction, commit/push, start execution, and first handoff rotation in order.
- [ ] Start execution uses `StartExecution.Render(plan)`, not a literal or legacy prompt builder.
- [ ] Handoff rotation produces `handoff.0001.md` and removes or replaces the live `.agents/handoff.md` according to the run-scoped protocol.
- [ ] Failure at each multi-write boundary is recoverable without corrupting plan, operational context, handoff history, or Git state.
- [ ] Tests cover missing plan, missing handoff, failed milestone extraction, failed commit/push, and stream terminal states.
