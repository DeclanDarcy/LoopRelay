# Handoff

## Slice Summary

Continued Milestone 0 Workstream 0.5 with a selected repository summary extraction.

## New State

- Extracted selected repository identity/fact rendering from `App.tsx` into `src/CommandCenter.UI/src/features/repositories/SelectedRepositorySummary.tsx`.
- The new component is presentation-only: it receives repository dashboard projection, optional workspace projection, optional execution display summary, caller-computed execution state, and caller-provided label maps.
- Kept workspace refresh, operational-context actions, continuity actions, artifact editor actions, execution workflows, git workflows, repository removal, and all callbacks in `App.tsx`.
- Added characterization in `src/CommandCenter.UI/src/test/characterization/selectedRepositorySummary.test.tsx`.
- The new tests cover repository identity/status labels, dashboard fallbacks when workspace is absent, workspace-over-dashboard facts, execution detail rendering, and existing not-recorded/unknown/none fallbacks.
- Updated `app.smoke.test.tsx` to wait for `Build Execution Context` to become enabled before clicking; this fixed a suite-level race without changing production behavior.
- Updated `.agents/milestones/m0-frontend-foundations.md` and `.agents/audits/m0-app-responsibility-inventory.md` with the selected summary boundary.
- Rotated the previous handoff to `.agents/handoffs/handoff.0038.md`.

## Verification

- `npm run test -- selectedRepositorySummary`
- `npm run lint`
- `npm run test`
- `npm run build`

## Next Slice

Stay in M0.5. The next high-value slice is a focused audit of either the artifact editor header/body or the operational-context proposal review subregions, extracting only display islands that remain meaningful without callbacks. Avoid broad panel extraction where save/rotate/review/promote authority is still intertwined with local draft state.
