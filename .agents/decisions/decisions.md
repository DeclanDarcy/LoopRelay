# Decisions

## Newly Authorized

- Accept the completed Milestone 5 Git semantics slice as architecturally correct.
- Preserve the execution transparency authority chain: Execution authority -> execution-owned projection -> transport -> typed client -> render-only UI.
- Treat `origin` and `originBasis` as backend-authored answers that the UI must render without recomputing.
- Keep Git path classification rooted in the launch-time dirty repository snapshot rather than timestamps, filenames, directory patterns, or other UI/client heuristics.
- Keep Git execution events limited to lifecycle transitions.
- Keep repository Git state in Git projections, not in the execution event stream.
- Proceed with a dedicated Milestone 5 Exit Audit before declaring Milestone 5 complete.
- Evaluate the preview-versus-launched prompt backend test against whether backend preview and launched prompt representations are both authoritative product artifacts whose relationship belongs to Milestone 5.
- If preview-versus-launched prompt behavior is authoritative and in-scope, close the missing regression test before Milestone 5 graduation.
- If preview integration is incomplete or scheduled for a later milestone, document the test as a dependency/deferred item rather than forcing coverage for incomplete behavior.
- The Milestone 5 Exit Audit must verify authority, projection coverage, transport completeness, regression coverage, and outstanding item classification.
- Classify any remaining Milestone 5 items as required before graduation, intentionally deferred with rationale, or no longer applicable.
- If the audit closes or legitimately defers the preview-versus-launched prompt test gap and finds no other blockers, Milestone 5 is ready for closure and Milestone 6 should begin from that baseline.
