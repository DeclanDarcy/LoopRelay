# Milestone 4: Tradeoff Analysis

## Goal

analyze benefits, costs, risks, dependencies, consequences, and cross-option comparison before recommendation.

## Work

- [ ] Add structured analysis models:
  - [ ] `AnalyzedDecisionOption`
  - [ ] `DecisionBenefit`
  - [ ] `DecisionCost`
  - [ ] `DecisionRisk`
  - [ ] `DecisionDependency`
  - [ ] `DecisionConsequence`
  - [ ] `TradeoffImpact`
  - [ ] `TradeoffSeverity`
- [ ] Preserve existing `DecisionTradeoff` fields during migration, but make the generated package use structured analysis.
- [ ] Add `ITradeoffAnalysisService`.
- [ ] Add `IOptionComparisonService`.
- [ ] Analyze each option against:
  - [ ] candidate type
  - [ ] typed context goals
  - [ ] constraints
  - [ ] risks
  - [ ] prior decisions
  - [ ] repository state
  - [ ] dependencies
- [ ] Require every option to have at least:
  - [ ] one benefit
  - [ ] one cost
  - [ ] one risk
- [ ] Represent unknown risk explicitly instead of omitting risk.
- [ ] Add cross-option comparison:
  - [ ] relative strengths
  - [ ] relative weaknesses
  - [ ] unique advantages
  - [ ] unique risks
  - [ ] disqualifying constraints
- [ ] Persist analysis diagnostics:
  - [ ] input option
  - [ ] context fingerprint
  - [ ] generated analysis
  - [ ] unknowns
  - [ ] validation warnings

## Tests

- [ ] Benefits, costs, risks, dependencies, and consequences are generated for every option.
- [ ] Unknowns are explicit.
- [ ] Analysis is candidate-specific, not generic filler.
- [ ] Cross-option comparison identifies differences.
- [ ] Constraint-violating options are surfaced as risks or disqualifiers, not silently recommended.
- [ ] Diagnostics explain generated analysis.

## Exit Criteria

- [ ] Humans can compare consequences without producing the analysis manually.
- [ ] Recommendation generation has structured evidence to consume.
