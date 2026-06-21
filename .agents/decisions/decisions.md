# Decisions

## Newly Authorized

- Consider Workstream 0.4 architecturally successful because navigation now has explicit authority in `shellState`.
- Treat the current authority map as:
  - Projection authority: `hooks/`
  - Navigation authority: `shellState`
  - Workflow authority: `App.tsx`
  - Draft authority: `App.tsx`
- Aggressively protect `shellState` as navigation-only state.
- Keep `shellState` limited to ids, paths, active tab, and command-palette state.
- Do not place commit message, review note, proposal draft, git status, or execution status in `shellState`.
- Add a characterization test proving artifact draft edits do not trigger projection reloads.
- Defer optional section anchors and expanded sections to Milestone 7 unless the current UI, `App.tsx` complexity, or Milestone 2 shell migration requires them earlier.
- Treat remaining M0 work as small certification gaps, boundary hardening, and closure review rather than major architectural construction.
- Prefer an M0 closure audit over additional automatic extraction.

## Validation Expected For Next Slice

- Add draft-does-not-reload-projection characterization.
- Re-run projection authority review.
- Re-run navigation authority review.
- Perform an M0 closure audit.
- Decide whether M0 is complete based on whether authority boundaries are explicit, stable, and certified.
