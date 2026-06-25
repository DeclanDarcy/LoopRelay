# Milestone 9 Governance Summary Consolidation

## Scope

- Consolidated the selected repository governance area into a contextual decision-session summary.
- Kept the Governance tab, anchored at `#governance-workspace`, as the primary detailed governance lifecycle surface.
- Added dashboard summary navigation from the selected repository summary to the Governance workspace.
- Removed duplicate detailed governance scoring facts from the selected repository summary:
  - coherence score,
  - transfer pressure,
  - cache miss risk.

## Authority Preserved

- The selected repository summary still renders existing authoritative `decisionSessionSummary` fields.
- React does not compute lifecycle, transfer eligibility, health, recovery, certification, or score semantics.
- Detailed governance lifecycle, eligibility, transfer, recovery, health, and certification remain owned by `GovernanceWorkspace`.

## Verification

- `npm test -- selectedRepositorySummary.test.tsx navigation.test.ts governanceWorkspace.test.tsx`

## Residual Risk

- Other governance-adjacent surfaces still need classification in later slices, especially workflow human governance reports and decision advisory governance reports.
- This slice only consolidates the repository summary governance duplication, not all governance terminology or certification/health duplication.
