# Handoff

## New State From This Slice

- Continued M4: Decision Review Workspace UI Phase 1.
- Added decision lifecycle TypeScript contracts in `src/CommandCenter.UI/src/types/decisions.ts`, matching the current backend camelCase records for context snapshots, candidates, proposal browser items, review workspace projections, option comparison, evidence inspection, and source attribution.
- Added decision API wrappers in `src/CommandCenter.UI/src/api/decisions.ts` and exported them through the shared API barrel.
- Added initial decision hooks:
  - `useDecisionContext`
  - `useDecisionDiscovery`
  - `useDecisionProposals`
- Added `DecisionLifecycleTab`, a minimal observational Decisions tab showing backend-sourced context, candidate, and proposal browser summaries.
- Wired `decisions` into primary shell tab state, workspace tabs, command-palette navigation targets, active-tab visibility, and `App.tsx`.
- Added Tauri bridge commands for decision context, candidates, proposals, proposal review workspace, option comparison, evidence inspection, and source attribution read endpoints.
- Extended the dev Tauri mock with decision context, candidate, and proposal browser data so mounted UI hooks work in mock mode.
- Updated `.agents/milestones/m4-review-workspace.md`; the Decisions tab and decision lifecycle route composition UI item is now complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0014.md`.

## Verification

- `npm run lint --prefix src/CommandCenter.UI` succeeds.
- `npm run build --prefix src/CommandCenter.UI` succeeds.
- `npm run test --prefix src/CommandCenter.UI` passes with 36 files and 135 tests.
- `cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml` succeeds.
- `dotnet build CommandCenter.slnx` succeeds with 0 warnings and 0 errors.
- `cargo fmt --manifest-path src/CommandCenter.Shell/Cargo.toml` could not run because `rustfmt` is not installed for the stable Windows toolchain.

## Next Slice

- Continue M4 UI with the proposal browser itself:
  1. Add proposal state filters for generated, viewed, needs-refinement, refined, ready-for-resolution, resolved, expired, and discarded.
  2. Keep the browser backed by `DecisionProposalBrowserItem`; do not infer lifecycle state from proposal details.
  3. Add selection state for a proposal, but keep mutation controls out until the review workspace is visible.
