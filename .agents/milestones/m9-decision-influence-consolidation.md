# Milestone 9 Evidence: Decision Influence Consolidation

## Scope

- Continued Milestone 9 by consolidating decision influence semantics.
- Kept the Decisions tab at `#decision-lifecycle` as the primary detailed decision lifecycle and influence surface.
- Converted Execution decision influence from a full explanation surface into a contextual summary for the selected execution session.

## Implemented

- Added `DecisionInfluenceTracePanel` under `features/decisions` to render the full persisted influence trace:
  - projected statement groups,
  - backend-provided decision projection categories,
  - evidence and adherence details,
  - diagnostics.
- Updated `DecisionLifecycleTab` to include the detailed influence trace from the current selected execution session.
- Updated `ExecutionDecisionInfluencePanel` to show only:
  - session and projection metadata,
  - projected statement count,
  - influencing decision IDs,
  - category and diagnostic counts,
  - navigation to Decisions.
- Added `App` navigation from Execution decision influence to `#decision-lifecycle`.
- Updated characterization coverage to prove Execution summarizes and links while Decisions owns detailed influence semantics.

## Verification

- `npm test -- executionDecisionInfluencePanel.test.tsx decisionLifecycleNavigation.test.tsx`
- `npm test -- selectedRepositorySummary.test.tsx navigation.test.ts decisionLifecycleNavigation.test.tsx executionDecisionInfluencePanel.test.tsx executionHistoryPanel.test.tsx executionEventFeed.test.tsx governanceWorkspace.test.tsx`
- `npm run build`

## Residual Risk

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- The Decisions influence panel currently reflects the selected execution session trace because that is the persisted trace already loaded by `App`; a repository-level influence history surface remains outside this slice.
- Other Milestone 9 duplicate surfaces remain: reasoning confidence displays, continuity evolution summaries, health widgets, and certification summaries.
