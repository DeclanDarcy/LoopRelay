# Decisions

## Newly Authorized

- Treat candidate lifecycle interaction normalization as accepted Milestone 9 work because it completes a coherent action family without expanding `InteractionPatternView` beyond subject, result, eligibility, evidence, and diagnostics presentation.
- Continue preserving the architectural boundary:
  - backend owns legality, eligibility, transition rules, evidence, and diagnostics,
  - `InteractionPatternView` owns presentation only.
- Keep characterization tests focused on normalized interaction presentation rather than duplicating backend lifecycle validation.
- Evaluate refinement and resolution separately before adopting the same base interaction pattern directly.
- If refinement or resolution need revision history, before/after comparison, consequence preview, resolution artifacts, or assimilation effects, introduce a thin phase-specific wrapper that composes `InteractionPatternView`.
- Keep the base `InteractionPatternView` stable while allowing richer lifecycle phases to add only the presentation they genuinely require.
- Continue incremental action-family normalization rather than a broad lifecycle refactor because each action family remains independently testable and easier to reason about.
