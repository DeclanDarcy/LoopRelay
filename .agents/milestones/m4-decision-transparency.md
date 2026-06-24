## Milestone 4: Decision Transparency

### Objective

Expose why decisions, recommendations, options, quality ratings, governance findings, and execution influence outcomes exist without changing the decision algorithms. This milestone produces and surfaces authoritative decision explanation projections. Shared cross-domain rendering belongs to Milestone 8.

### Backend

- [ ] Ensure `DecisionProposal` serialization includes all generated transparency data already produced by services:
   - [ ] generation diagnostics
   - [ ] option validation results
   - [ ] rejected options
   - [ ] deduplicated options
   - [ ] analyzed options
   - [ ] tradeoff comparisons
   - [ ] tradeoff analysis diagnostics
   - [ ] recommendation mode
   - [ ] recommendation evidence
   - [ ] option evaluations
   - [ ] supporting factors
   - [ ] concerns
   - [ ] assumptions
   - [ ] alternative explanations
- [ ] If any of these are computed but not persisted or projected, add them to the owning model and repository serialization.
- [ ] Add read-only projection fields where quality and burden currently expose labels without basis:
   - [ ] quality score contribution
   - [ ] threshold crossed
   - [ ] signal contribution
   - [ ] override reason
   - [ ] burden selection rule
   - [ ] burden winning signal
   - [ ] unknown vs inferred status
- [ ] Extend governance and influence projections where needed to expose included, excluded, superseded, conflicting, ignored, and blocked decisions with reasons.
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
