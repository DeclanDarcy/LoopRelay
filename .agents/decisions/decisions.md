# Decisions

## Newly Authorized

- Treat the completed primary surface reachability slice as a valid Milestone 9 validation-focused slice.
- Treat primary workspace reachability as a user-visible contract requiring the correct tab activation, active navigation state, and expected primary landmark.
- Keep `Workflow` terminology reserved for the authoritative operational lifecycle.
- Keep `Git Evidence` terminology as execution-owned evidence, not as a competing workflow concept.
- Treat backend endpoint disposition verification as the next Milestone 9 slice.
- Treat endpoint disposition as the backend analogue of the UI reachability audit.
- Classify retained endpoint routes as `Keep`, `Redirect`, `Internal`, or `Remove`.
- Verify retained endpoint routes still satisfy the current UI.
- Verify compatibility routes remain intentional.
- Verify obsolete routes are removed or explicitly documented.
- Verify no duplicate endpoint exposes the same semantic capability through parallel APIs.
- After backend endpoint disposition verification, rerun focused backend endpoint tests, the UI navigation and reachability suite, and update Milestone 9 evidence.
- Treat Milestone 9 as having moved from implementation into verification.
- Treat the remaining Milestone 9 work as backend endpoint disposition, final terminology verification, and final cohesion audit before preparing for Milestone 10.
