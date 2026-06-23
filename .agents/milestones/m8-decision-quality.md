# Milestone 8: Decision Quality Evaluation

## Goal

measure whether generated decisions reduce human decision-production burden.

## Work

- [x] Add quality models:
  - [x] `DecisionQualityAssessment`
  - [x] `DecisionQualityRating`
  - [x] `DecisionQualitySignal`
  - [x] `QualitySignalDirection`
  - [x] `QualitySignalSeverity`
  - [x] `DecisionQualityReport`
  - [x] `DecisionQualityTrend`
  - [x] `HumanAuthoringBurdenSignal`
  - [x] `HumanAuthoringBurdenReport`
- [x] Add `IDecisionQualitySignalService`.
- [x] Add `IDecisionQualityAssessmentService`.
- [x] Add `IDecisionQualityReportService`.
- [x] Add `IHumanAuthoringBurdenService` if burden classification is not implemented inside the quality services.
- [ ] Extract signals from:
  - [x] resolution outcome
  - [x] selected option
  - [x] recommendation divergence
  - [x] refinement count
  - [x] refinement scope
  - [ ] recommendation stability
  - [x] alternative utilization
  - [x] rejection/archive/supersession
  - [x] human rewrite indicators
  - [x] generation bypass indicators
- [ ] Evaluate categories:
  - [x] recommendation quality
  - [x] option quality
  - [ ] tradeoff quality
  - [ ] context quality
  - [ ] constraint quality
  - [x] human effort
  - [x] human authoring burden
- [ ] Generate repository reports:
  - [x] generated package count
  - [x] accepted count/rate
  - [x] modified count/rate
  - [x] rejected count/rate
  - [x] superseded count/rate
  - [x] recommendation divergence rate
  - [x] alternative utilization rate
  - [x] review-only count/rate
  - [x] minor-edit count/rate
  - [x] major-refinement count/rate
  - [x] full-rewrite count/rate
  - [x] generation-bypassed count/rate
- [ ] Generate trend reports over persisted assessments.
- [ ] Add UI quality dashboard and trend view.
- [x] Keep quality advisory and non-mutating.

## Tests

- [x] Accepted recommended option produces positive quality signals.
- [x] Rejected decision produces negative quality signals.
- [x] Alternative selection lowers recommendation quality but preserves option usefulness.
- [x] Major refinement or rewrite increases human-effort penalty.
- [x] Full rewrite and generation bypass are recorded separately from ordinary refinement.
- [ ] Repeated recommendation reversal reduces stability.
- [ ] Reports and trends are deterministic and persisted.
- [x] Quality assessment does not mutate decisions, proposals, packages, or execution projection.

## Exit Criteria

- [ ] Command Center can answer whether generated decisions are useful, how much human effort remains, whether recommendations are improving, and whether alternatives/tradeoffs are valuable.
