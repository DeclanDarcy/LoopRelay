# Decisions

## Newly Authorized

- Treat the refinement and resolution interaction normalization as accepted Milestone 9 work because it proves the shared interaction language can support richer lifecycle phases without over-generalizing the base component.
- Keep `InteractionPatternView` as the common presentation shell for subject, result, eligibility, evidence, and diagnostics.
- Continue using thin phase-specific wrappers for rich lifecycle surfaces such as refinement and resolution instead of expanding `InteractionPatternView`.
- Preserve the boundary where backend projections own lifecycle legality and eligibility, while React coordinates and renders the selected authoritative projection.
- Keep characterization coverage focused on normalized presentation, not backend lifecycle rule validation.
- Treat the Decisions domain interaction normalization as coherently spanning proposal review, candidate actions, resolved decision actions, refinement, and resolution.
- Continue Milestone 9 interaction normalization with execution commit/push actions next.
- Normalize execution interaction actions in this order: commit preparation, commit execution, push, push retry, then recovery actions.
- Execution interaction wrappers should present subject, expected result, backend-owned eligibility, evidence, and diagnostics while adding retry history or failure context only where execution-specific transparency requires it.
