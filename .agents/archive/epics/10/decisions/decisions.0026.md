# Decisions

## Newly Authorized

- Accept the Milestone 4 proposal transparency UI slice as architecturally correct.
- Treat the new proposal transparency components as decision-local renderers, not shared explainability abstractions:
  - `DecisionRecommendationExplanation`
  - `DecisionOptionEvaluationTable`
  - `DecisionRejectedOptionList`
  - `DecisionEvidenceFragments`
- Preserve the proposal transparency authority chain as:
  - decision services
  - `DecisionProposal`
  - TypeScript contract
  - decision-local renderer
  - `DecisionProposalViewer`
- Keep option evaluations attached to their options and render backend evaluation granularity instead of collapsing evaluations into a synthesized UI explanation.
- Keep rejected and deduplicated option semantics backend-owned; React may render rejected options, reasons, diagnostics, deduplicated options, and diagnostics but must not invent rejection categories.
- Do not invent or infer recommendation confidence in React. Confidence may be rendered only after it becomes an explicit backend-owned projection field.
- Do not create UI-only validation categories such as insufficient evidence or duplicate unless the backend exposes those categories explicitly. Continue rendering validation issues and deduplicated diagnostics as backend-provided facts.
- Proceed next with quality transparency, then burden transparency, then governance transparency.
- Keep `DecisionQualityExplanation` and `DecisionBurdenExplanation` narrowly focused on explanation fields rather than broad status-panel summaries.
- For quality transparency, render backend-owned score contribution, thresholds, overrides, unknown/default status, diagnostics, and related reasoning without interpretation.
- For burden transparency, render backend-owned effective burden, winning signal, selection rule, reasoning, and diagnostics without interpretation.
- For governance transparency, reuse existing governance reports where possible and extend backend projections only when a concrete semantic gap appears during integration.
