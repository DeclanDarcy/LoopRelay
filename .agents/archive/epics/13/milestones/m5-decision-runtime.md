# Phase 5 - Decision Runtime

Goal: make Decision Sessions real Codex-backed participants while preserving zero operational authority.

## Implementation

- [ ] Start a Decision, ExtraHigh, held-open process with the strictest available zero-permission sandbox.
- [ ] Submit `StartDecisionSession.Render(File.ReadAllText(.agents/operational_context.md))` to seed the session.
- [ ] Do not expose the seed turn as the primary decision stream unless diagnostics require it.
- [ ] Submit `GetNextDecisions.Render(handoff)` to the same Decision process.
- [ ] Stream the `GetNextDecisions` output to the decision stream and capture it as the proposed decisions text.
- [ ] On turn completion, expose the captured decisions as editable user content.
- [ ] Keep deterministic `CommandCenter.Decisions` generation/scoring services as offline or fallback paths, not the live decision authority for this flow.
- [ ] Decision Runtime must not call Git, mutate code, commit, push, run execution orchestration, or write operational artifacts except through orchestrator-owned persistence of captured decision output.
- [ ] Record prompt provenance for `StartDecisionSession` and `GetNextDecisions`.
- [ ] Add decision stream events for output, completion, failure, review-ready, and diagnostics.

## Certification

- [ ] Decision Sessions are backed by live Agent Runtime processes.
- [ ] Decision sandbox configuration is validated and logged.
- [ ] The Decision process cannot perform operational work.
- [ ] Decision output becomes editable only after the turn completes.
- [ ] Human review is required before operational continuation.
- [ ] Architecture tests prevent Decision Runtime from depending on Execution operational orchestration.
