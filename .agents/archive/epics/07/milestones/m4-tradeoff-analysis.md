# Milestone 4: Tradeoff Analysis

## Goal

analyze benefits, costs, risks, dependencies, consequences, and cross-option comparison before recommendation.

## Work

- [x] Add structured analysis models:
  - [x] `AnalyzedDecisionOption`
  - [x] `DecisionBenefit`
  - [x] `DecisionCost`
  - [x] `DecisionRisk`
  - [x] `DecisionDependency`
  - [x] `DecisionConsequence`
  - [x] `TradeoffImpact`
  - [x] `TradeoffSeverity`
- [x] Preserve existing `DecisionTradeoff` fields during migration, but make the generated package use structured analysis.
- [x] Add `ITradeoffAnalysisService`.
- [x] Add `IOptionComparisonService`.
- [x] Analyze each option against:
  - [x] candidate type
  - [x] typed context goals
  - [x] constraints
  - [x] risks
  - [x] prior decisions
  - [x] repository state
  - [x] dependencies
- [x] Require every option to have at least:
  - [x] one benefit
  - [x] one cost
  - [x] one risk
- [x] Represent unknown risk explicitly instead of omitting risk.
- [x] Add cross-option comparison:
  - [x] relative strengths
  - [x] relative weaknesses
  - [x] unique advantages
  - [x] unique risks
  - [x] disqualifying constraints
- [x] Persist analysis diagnostics:
  - [x] input option
  - [x] context fingerprint
  - [x] generated analysis
  - [x] unknowns
  - [x] validation warnings

## Tests

- [x] Benefits, costs, risks, dependencies, and consequences are generated for every option.
- [x] Unknowns are explicit.
- [x] Analysis is candidate-specific, not generic filler.
- [x] Cross-option comparison identifies differences.
- [x] Constraint-violating options are surfaced as risks or disqualifiers, not silently recommended.
- [x] Diagnostics explain generated analysis.

## Exit Criteria

- [x] Humans can compare consequences without producing the analysis manually.
- [x] Recommendation generation has structured evidence to consume.
