# Decisions

## Newly Authorized

- Treat the first Milestone 2 supersession-capture slice as architecturally correct because reasoning capture occurs only after the authoritative decision transition succeeds.
- Do not treat the intermittent Windows `execution-sessions.json` lock as a blocker for the supersession-capture slice when focused decision tests pass and the lock migrates between unrelated tests.
- Track the Windows execution-session file-lock issue separately because it may reduce confidence in full-suite certification later.
- Proceed next with proposal-resolution capture, but preserve the same boundary: proposal resolution must complete authoritatively first, and reasoning capture may only observe the completed transition.
- Proposal-resolution reasoning capture must avoid reasoning-side authority concepts such as resolving, approving, or selecting proposals.
- Proposal-resolution capture may emit explanatory reasoning such as `DecisionEvolution` or `EvidenceAdded` only after the source domain has resolved the proposal.
- The next proposal-resolution slice must test that capture happens only after successful proposal resolution.
- The next proposal-resolution slice must test that failed or stale proposal resolution emits no reasoning event.
- The next proposal-resolution slice must test that re-running the same transition does not duplicate events.
- The next proposal-resolution slice must test that reasoning does not mutate proposal or decision artifacts.
- The next proposal-resolution slice must ensure the fingerprint includes source transition identity, not just narrative text.
