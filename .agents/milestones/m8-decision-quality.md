# Milestone 8: Decision Quality Evaluation

## Goal

measure whether generated decisions reduce human decision-production burden.

## Work

- [ ] Add quality models:
  - [ ] `DecisionQualityAssessment`
  - [ ] `DecisionQualityRating`
  - [ ] `DecisionQualitySignal`
  - [ ] `QualitySignalDirection`
  - [ ] `QualitySignalSeverity`
  - [ ] `DecisionQualityReport`
  - [ ] `DecisionQualityTrend`
  - [ ] `HumanAuthoringBurdenSignal`
  - [ ] `HumanAuthoringBurdenReport`
- [ ] Add `IDecisionQualitySignalService`.
- [ ] Add `IDecisionQualityAssessmentService`.
- [ ] Add `IDecisionQualityReportService`.
- [ ] Add `IHumanAuthoringBurdenService` if burden classification is not implemented inside the quality services.
- [ ] Extract signals from:
  - [ ] resolution outcome
  - [ ] selected option
  - [ ] recommendation divergence
  - [ ] refinement count
  - [ ] refinement scope
  - [ ] recommendation stability
  - [ ] alternative utilization
  - [ ] rejection/archive/supersession
  - [ ] human rewrite indicators
  - [ ] generation bypass indicators
- [ ] Evaluate categories:
  - [ ] recommendation quality
  - [ ] option quality
  - [ ] tradeoff quality
  - [ ] context quality
  - [ ] constraint quality
  - [ ] human effort
  - [ ] human authoring burden
- [ ] Generate repository reports:
  - [ ] generated package count
  - [ ] accepted count/rate
  - [ ] modified count/rate
  - [ ] rejected count/rate
  - [ ] superseded count/rate
  - [ ] recommendation divergence rate
  - [ ] alternative utilization rate
  - [ ] review-only count/rate
  - [ ] minor-edit count/rate
  - [ ] major-refinement count/rate
  - [ ] full-rewrite count/rate
  - [ ] generation-bypassed count/rate
- [ ] Generate trend reports over persisted assessments.
- [ ] Add UI quality dashboard and trend view.
- [ ] Keep quality advisory and non-mutating.

## Tests

- [ ] Accepted recommended option produces positive quality signals.
- [ ] Rejected decision produces negative quality signals.
- [ ] Alternative selection lowers recommendation quality but preserves option usefulness.
- [ ] Major refinement or rewrite increases human-effort penalty.
- [ ] Full rewrite and generation bypass are recorded separately from ordinary refinement.
- [ ] Repeated recommendation reversal reduces stability.
- [ ] Reports and trends are deterministic and persisted.
- [ ] Quality assessment does not mutate decisions, proposals, packages, or execution projection.

## Exit Criteria

- [ ] Command Center can answer whether generated decisions are useful, how much human effort remains, whether recommendations are improving, and whether alternatives/tradeoffs are valuable.
