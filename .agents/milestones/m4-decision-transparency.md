## Milestone 4: Decision Transparency

### Objective

Expose why decisions, recommendations, options, quality ratings, governance findings, and execution influence outcomes exist without changing the decision algorithms. This milestone produces and surfaces authoritative decision explanation projections. Shared cross-domain rendering belongs to Milestone 8.

### Backend

- [x] Inventory existing recommendation, option, diagnostic, governance, influence, quality, and burden semantic facts before adding projection fields. Evidence: `.agents/milestones/m4-transparency-inventory.md`.
- [x] Ensure `DecisionProposal` serialization includes all generated transparency data already produced by services:
   - [x] generation diagnostics
   - [x] option validation results
   - [x] rejected options
   - [x] deduplicated options
   - [x] analyzed options
   - [x] tradeoff comparisons
   - [x] tradeoff analysis diagnostics
   - [x] recommendation mode
   - [x] recommendation evidence
   - [x] option evaluations
   - [x] supporting factors
   - [x] concerns
   - [x] assumptions
   - [x] alternative explanations
- [x] If any of these are computed but not persisted or projected, add them to the owning model and repository serialization.
- [x] Add read-only projection fields where quality and burden currently expose labels without basis:
   - [x] quality score contribution
   - [x] threshold crossed
   - [x] signal contribution
   - [x] override reason
   - [x] burden selection rule
   - [x] burden winning signal
   - [x] unknown vs inferred status
- [ ] Extend governance and influence projections where needed to expose included, excluded, superseded, conflicting, ignored, and blocked decisions with reasons.
   - [x] Decision execution projection and persisted influence traces expose included, excluded, superseded, conflicting, ignored, and blocked decision reason categories.
   - [ ] Render those decision-owned reason categories in decision/execution UI panels.
- [ ] Keep these outputs as decision-owned projections. They are the semantic inputs that later shared explainability components will render.

### UI

- [ ] Add decision-specific projection renderers under `src/CommandCenter.UI/src/features/decisions/`:
   - [ ] `DecisionRecommendationExplanation`
   - [ ] `DecisionOptionEvaluationTable`
   - [ ] `DecisionRejectedOptionList`
   - [ ] `DecisionQualityExplanation`
   - [ ] `DecisionBurdenExplanation`
   - [ ] `DecisionGovernanceExplanation`
   - [ ] `DecisionInfluenceExplorer`
- [ ] Update `DecisionProposalViewer` to display recommendation mode, rationale, confidence when available, supporting factors, concerns, assumptions, alternative explanations, recommendation evidence, and option evaluations.
- [ ] Update option views to display score, rank, score explanation, benefits, costs, risks, dependencies, constraints, disqualification, and required human action.
- [ ] Display rejected, disqualified, deduplicated, invalid, insufficient-evidence, and duplicate options in a visible section rather than hiding them behind diagnostics.
- [ ] Update `DecisionQualityPanel` to show score, rating, signal contribution, thresholds, overrides, warnings, unknowns, and burden reasoning.
- [ ] Update governance panels to show resolution authority, stale authority, recommendation divergence, lifecycle state, allowed transitions, blocked transitions, transition reasons, governance findings, and authority violations.
- [ ] Update execution influence panels to show why decisions were included, excluded, superseded, conflicted, ignored, or converted into constraints/directives/priorities/rules.
- [ ] Keep all calculations in backend projections. UI components render fields and group them for comprehension only.
- [ ] Avoid building generic explanation abstractions in this milestone. If a component would be useful across domains, keep it local and migrate it during Milestone 8.

### Tests

- [ ] Backend serialization and projection tests for transparency fields.
- [ ] UI characterization tests for recommendation explanation, option scoring, rejected options, quality contribution, burden reasoning, governance state, and influence exclusion/conflict reasons.
- [ ] Regression tests proving no UI-side scoring, ranking, quality, burden, or governance calculation helpers exist.

### Exit Criteria

- [ ] Every recommendation explains why it exists and which assumptions, concerns, evidence, and alternatives matter.
- [ ] Every option explains score, rank, constraints, disqualification, and evidence.
- [ ] Rejected and excluded alternatives remain visible.
- [ ] Quality, burden, governance, and influence are explainable from authoritative data.
- [ ] Duplicate decision reasoning and duplicate presentation summaries are removed or replaced.
