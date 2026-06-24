# Decisions

## Newly Authorized

- Accept the Milestone 3 resolved-decision supersede/archive slice as architecturally sound.
- Treat resolved-decision lifecycle reachability as functionally integrated for the MVP surface.
- Preserve the resolved-decision authority chain:
  - `DecisionLifecycleRules`
  - lifecycle eligibility projection
  - React rendering of backend facts
  - backend command invocation
  - projection refresh
- Continue using `lifecycleEligibility.decisions` as the action source for the MVP resolved-decision surface instead of introducing a frontend-specific resolved-decision model.
- Keep broad post-mutation refresh for supersede/archive across decision lifecycle, governance, quality, and execution context preview.
- If refresh scope is optimized later, keep that optimization behind hook boundaries rather than distributing refresh policy across components.
- Proceed next with proposal generation UX completion before adding the end-to-end decision lifecycle characterization.
- The next proposal generation slice should surface backend-owned outputs without recomputation:
  - generated proposal identifier
  - generation mode
  - accepted option count
  - rejected option count
  - deduplicated option count
  - validation diagnostics
- Proposal generation should navigate directly to the generated proposal when appropriate.
- Proposal generation should refresh candidates, proposals, and lifecycle eligibility.
- After proposal generation UX is complete, add a single high-value end-to-end characterization path covering discovery through archive.
