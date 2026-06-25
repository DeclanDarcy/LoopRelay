# Decisions

## Newly Authorized

- Accept the `DecisionInfluenceExplorer` UI slice as architecturally correct.
- Treat `DecisionInfluenceExplorer` as the correct decision-local renderer for execution influence reason categories during Milestone 4.
- Preserve the execution influence authority chain as:
  - decision services
  - `ExecutionDecisionProjection`
  - `DecisionInfluenceTrace`
  - `DecisionInfluenceExplorer`
- Keep influence categories backend-owned. React must render included, excluded, superseded, conflicting, ignored, and blocked categories from backend projections or persisted traces, not infer them from statement presence.
- Continue rendering persisted `DecisionInfluenceTrace` for historical execution explanations so historical execution influence remains consistent with current projection inspection.
- Keep characterization tests focused on backend category rendering, backend reason rendering, and backend diagnostics rendering rather than CSS or implementation details.
- Proceed next with proposal transparency renderers for recommendation explanation, option evaluation, rejected options, deduplicated options, recommendation evidence, supporting factors, concerns, assumptions, and alternative explanations.
- Keep proposal transparency render-only: display persisted or generated fields and do not recompute recommendation logic in React.
- Keep option evaluations attached to their options instead of flattening them into one overall recommendation explanation.
- Render rejected options with rejected option content, rejection reason, and diagnostics rather than only listing them as discarded items.
- Keep proposal transparency components decision-local until Milestone 8.
