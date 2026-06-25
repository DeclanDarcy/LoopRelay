# Milestone 9 Operational Dashboard

## Scope

- Built the selected repository summary into a unified operational dashboard without adding new backend authority.
- Kept the dashboard compact and navigational, with detail remaining in the owning workspaces.
- Covered the required dashboard domains: workflow, governance, execution, operational context, reasoning, repository, health, certification, and diagnostics.

## Implementation

- `src/CommandCenter.UI/src/features/repositories/SelectedRepositorySummary.tsx`
  - Adds sectioned dashboard summaries for repository, workflow, execution, governance, operational context, reasoning, health, certification, and diagnostics.
  - Uses existing repository, workspace, workflow, governance, reasoning, and continuity projections.
  - Preserves direct navigation into primary workspaces for execution, governance, operational context, reasoning, continuity diagnostics, and milestones.
- `src/CommandCenter.UI/src/App.css`
  - Adds responsive dashboard section and fact-grid styling.
- `src/CommandCenter.UI/src/test/characterization/selectedRepositorySummary.test.tsx`
  - Characterizes the sectioned dashboard and verifies it stays compact.
  - Verifies detailed governance, reasoning, and continuity evidence is not duplicated into the dashboard.

## Verification

- `npm test -- selectedRepositorySummary.test.tsx`
- `npm run build`

## Notes

- `npm run build` still reports the existing Vite main bundle chunk-size warning.
- No backend model, endpoint, shell command, or authority changes were required.
