# Handoff

## Slice Summary

Closed Milestone 0 Workstream 0.6 by inventorying workflow-mutating frontend backend commands and adding the remaining explicit-action characterization.

## New State

- Added app-level smoke characterization in `src/CommandCenter.UI/src/test/characterization/app.smoke.test.tsx` for artifact mutation authority.
- Artifact draft edits are certified not to invoke `save_artifact_content`, `rotate_current_handoff`, or `rotate_current_decisions`.
- `Save` is certified as the explicit UI path for `save_artifact_content`.
- Confirmed `Rotate` is certified as the explicit UI path for `rotate_current_handoff` and `rotate_current_decisions`, depending on selected current artifact family.
- Added app-level smoke characterization for execution launch and generated handoff decision authority.
- Rendering, repository navigation, and execution-context build are certified not to invoke `start_execution`.
- `Start Execution` is certified as the explicit UI path for `start_execution`.
- Generated handoff review display is certified not to invoke `accept_execution_handoff` or `reject_execution_handoff`.
- `Accept Handoff` and confirmed `Reject Handoff` are certified as the explicit UI paths for their backend decision commands.
- Updated `.agents/audits/m0-closure-authority-matrix.md` with a complete workflow-mutating command inventory for M0.6.
- Updated `.agents/audits/m0-app-responsibility-inventory.md` with artifact, launch, and handoff-decision authority boundaries.
- Updated `.agents/milestones/m0-frontend-foundations.md` to mark Workstream 0.6 complete and redirect remaining M0 work back to M0.5 decomposition.
- Rotated the previous handoff to `.agents/handoffs/handoff.0034.md`.

## Verification

- `npm run test -- app.smoke`
- `npm run test`
- `npm run lint`
- `npm run build`
- `npm run test:e2e`
- `dotnet test CommandCenter.slnx`

## Next Slice

Return to Milestone 0 Workstream 0.5. Start with another small presentation-only extraction from `App.tsx`, preferably a low-risk region adjacent to already extracted execution/workspace components, and keep current layout/classes unchanged.
