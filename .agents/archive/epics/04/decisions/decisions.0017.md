# Decisions

## Newly Authorized

- Treat the choice not to extract `getDecisionContinuityWarnings` as an important M0 authority-boundary validation.
- Keep `getDecisionContinuityWarnings` out of `src/lib` unless a later backend-owned projection makes decision-specific warning relevance explicit.
- Classify warning filtering that decides which generic warnings are decision-relevant as frontend interpretation, not presentation mapping.
- Continue allowing `getOperationalContextSectionItems` in `src/lib` because it parses proposal content into section display items without deriving semantic authority.
- Do not begin the next `mergeExecutionEvents` slice by asking only whether it can be extracted; begin by determining who should own the merge behavior.
- Audit `mergeExecutionEvents` against three ownership candidates: `App.tsx`, `src/lib`, and `useExecutionEvents`.
- Prefer `useExecutionEvents` as the likely owner if merge behavior guarantees stream correctness, ordering, duplicate sequence replacement, session isolation, or other subscription lifecycle behavior.
- Consider `src/lib` only if `mergeExecutionEvents` is pure event-list-to-event-list transformation with no lifecycle assumptions.
- Treat `App.tsx` as the weakest owner for `mergeExecutionEvents` now that `useExecutionEvents` owns subscription, cleanup, ordering, duplicate replacement, and session isolation concerns.
- Characterize ordering, duplicate replacement, sequence gaps, session boundaries, empty state, and append behavior before moving `mergeExecutionEvents`.
- Decide the destination for `mergeExecutionEvents` only after classification determines whether it is stream lifecycle support or display transformation.

## Next Authorized Slice

Classify `mergeExecutionEvents` ownership before extraction. If it guarantees stream correctness, move ownership toward `useExecutionEvents`; if it transforms already-correct events for display, consider `src/lib`.
