# Milestone 9 Reasoning Summary Consolidation

## Scope

- Kept `ReasoningTrajectoryTab` as the primary detailed reasoning transparency surface.
- Added a contextual reasoning summary to the selected repository overview.
- Limited the overview to counts, latest activity, certification status, and navigation.
- Avoided duplicating reconstruction confidence rationale, missing evidence, reconstruction scope, graph diagnostics, materialization review, and provenance detail outside Reasoning.

## Implementation

- Added `onOpenReasoning` navigation to `SelectedRepositorySummary`.
- Wired repository overview navigation to the Reasoning tab and `reasoning-trajectory` section from `App`.
- Added characterization coverage proving the repository overview renders compact reasoning status and does not render detailed reasoning transparency blocks.

## Verification

- `npm test -- selectedRepositorySummary.test.tsx`
- `npm test -- selectedRepositorySummary.test.tsx reasoningTrajectory.test.tsx navigation.test.ts`
- `npm run build`

## Residual Risk

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- Other Milestone 9 duplicate-surface targets remain: continuity evolution summaries, health widgets, certification summaries, interaction normalization, and obsolete UI cleanup.
