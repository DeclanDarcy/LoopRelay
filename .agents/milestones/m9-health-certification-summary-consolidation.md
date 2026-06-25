# Milestone 9 Health and Certification Summary Consolidation

## Scope

- Continued Milestone 9 duplicate-surface consolidation for repository-level health and certification summaries.
- Kept detailed health dimensions, findings, diagnostics, evidence, and certification report detail in primary Workflow, Governance, Decisions, and Reasoning workspaces.
- Reduced the selected repository summary to compact health and certification facts:
  - governance health dimension count,
  - governance health finding count,
  - governance health assessment timestamp,
  - reasoning certification result,
  - reasoning certification latest run timestamp,
  - navigation to Governance and Reasoning primary workspaces.

## Implementation

- Updated `src/CommandCenter.UI/src/features/repositories/SelectedRepositorySummary.tsx`.
- Updated `src/CommandCenter.UI/src/test/characterization/selectedRepositorySummary.test.tsx`.

## Consolidation Rules Preserved

- Secondary repository summaries do not render individual governance health findings.
- Secondary repository summaries do not render governance diagnostics.
- Secondary repository summaries do not render reasoning reconstruction rationale, missing evidence, graph authority, materialization authority, certification findings, or certification diagnostics.
- Detailed health and certification detail remains available through the primary workspace surfaces.

## Verification

- `npm test -- selectedRepositorySummary.test.tsx workspaceInspectorRail.test.tsx`
- `npm run build`

## Residual Risk

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- Milestone 9 still needs interaction normalization, unified dashboard cohesion, obsolete compatibility cleanup, and terminology alignment.
