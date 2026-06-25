# Decisions

## Newly Authorized

- Accept the Milestone 4 backend execution influence transparency slice as architecturally correct and complete for its backend objective.
- Treat backend execution influence transparency as structurally complete when execution projections and persisted traces expose included, excluded, superseded, conflicting, ignored, and blocked decisions with backend-owned reasons.
- Preserve the authority flow for this area as:
  - decision services
  - `ExecutionDecisionProjection`
  - `DecisionProjectionDiagnostics`
  - `DecisionInfluenceTrace`
  - API
  - TypeScript contracts
  - UI rendering
- Keep historical execution influence inspection consistent with current projection inspection by preserving the same reason categories in `DecisionInfluenceTrace`.
- Continue treating `DecisionGovernanceReport` findings as the governance explanation source unless UI integration exposes a concrete missing semantic reason.
- Move the next Milestone 4 work from additional backend semantics into UI composition.
- Build `DecisionInfluenceExplorer` next as a decision-local renderer for included, excluded, superseded, conflicting, ignored, and blocked decision reason categories.
- Keep the next UI work render-only: display backend fields and do not synthesize or calculate influence explanations in React.
- Add characterization tests proving each influence reason category renders and guarding against client-side category derivation.
- Delay shared/generic explainability abstractions until Milestone 8.
