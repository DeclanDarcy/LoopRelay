# Phase 6 - Decision Submit and Continuation Loop

Goal: complete the repeated Submit -> ContinueExecution -> next handoff -> next decisions loop.

## Implementation

- [ ] Add `POST /api/repositories/{id}/decision/submit` with `{ decisions }`.
- [ ] Persist edited decisions to `.agents/decisions/decisions.0001.md`, then `.0002.md`, using the run-scoped iteration counter.
- [ ] Run `ContinueExecution.Render(MemoryCache.Get("{repositoryId}:Plan"), handoff, decisions)` as an Operational, Medium, one-shot Codex turn.
- [ ] Stream continuation output through the execution stream.
- [ ] On completion, verify `.agents/handoff.md`.
- [ ] Read the new handoff into orchestrator state.
- [ ] Move the handoff to `.agents/handoffs/handoff.0002.md`, then `.0003.md`, and so on.
- [ ] Record prompt provenance for every `ContinueExecution` turn.
- [ ] Return the UI to decision streaming after continuation and router evaluation.
- [ ] Keep the only required human gate at decision review/submit.
- [ ] Add a conversation projection that is specific to this flow: planning, operational output, decision output, editable decision, submit, continuation, and next decision. Do not broaden this into a repository knowledge platform.

## Certification

- [ ] Submitted decisions are persisted before continuation starts.
- [ ] Continuation uses the cached plan, latest handoff, and submitted decisions.
- [ ] Each operational continuation produces and rotates the next handoff.
- [ ] The UI can complete at least two decision/continuation iterations without leaving the Plan Authoring screen.
- [ ] Recovery can identify the latest persisted decision and handoff sequence.
