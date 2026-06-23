# Decisions

## Newly Authorized

- Accept the context-aware Milestone 4 slice as the correct architectural direction.
- Keep Milestone 4 open.
- Continue treating `DecisionContextService` as the sole extractor and interpretation owner for repository decision context.
- Preserve the projection boundary:
  - repository evidence flows into `DecisionContextService`
  - `DecisionContextService` emits `DecisionGenerationContext`
  - downstream generation services consume that projection
- Treat `IDecisionContextProjectionService` as a central extension point for later M5, M6, M8, M9, and M10 work.
- Keep `DecisionGenerationContext` additive and scoped to what M4 needs right now.
- Continue resisting package-version, quality, certification, dashboard, and recommendation work until M4 closes.
- Continue M4 next by improving context-derived option comparison quality.
- Comparison output should explain why options differ, not only how they differ.
- Comparison output must remain descriptive and non-recommendational.
- Add disqualifying constraint detection as M4 tradeoff analysis output.
- Constraint-derived disqualifiers belong in analysis/comparison, not recommendation authority.
- Verify unknown handling remains explicit after context integration.
- Preserve explicit unknown risk, unknown dependency, and unknown consequence modeling when evidence is insufficient.
- Require context-aware comparisons, disqualifying constraints, and unknown validation before starting M5.

## Not Authorized

- Do not start M5 recommendation generation yet.
- Do not add package/version, quality, dashboard, certification, or recommendation machinery before M4 analysis closes.
- Do not let recommendation logic compensate for weak tradeoff analysis.
