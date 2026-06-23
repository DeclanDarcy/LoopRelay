# Milestone 7: Interactive Decision Refinement

## Goal

let humans guide regeneration without becoming package authors.

## Work

- [ ] Add structured `RefinementDirective` and `RefinementDirectiveType`:
  - [ ] `AddConstraint`
  - [ ] `RemoveConstraint`
  - [ ] `IncreasePriority`
  - [ ] `DecreasePriority`
  - [ ] `ExploreAlternative`
  - [ ] `ReevaluateRisk`
  - [ ] `ReevaluateCost`
  - [ ] `ReevaluateRecommendation`
  - [ ] `ClarifyGoal`
- [ ] Add `IRefinementAnalysisService`.
- [ ] Add `RefinementPlan`:
  - [ ] regenerate options
  - [ ] reevaluate tradeoffs
  - [ ] reevaluate recommendation
  - [ ] full regeneration
  - [ ] applied constraints
  - [ ] diagnostics
- [ ] Keep current direct `DecisionRefinementRequest` support for compatibility, but prefer directive-driven refinement in UI.
- [ ] Add endpoints to analyze refinement before mutation and regenerate scoped package versions.
- [ ] Preserve every refinement as:
  - [ ] request
  - [ ] directives
  - [ ] plan
  - [ ] old package version
  - [ ] new package version
  - [ ] comparison
  - [ ] diagnostics
- [ ] Classify the refinement's human authoring burden:
  - [ ] small directive-only adjustment is `MinorEdit`
  - [ ] scoped regeneration is `MajorRefinement`
  - [ ] replacement of generated content with human-authored content is `FullRewrite`
- [ ] Ensure refinement never mutates prior package versions.
- [ ] Add UI controls for structured directives and show old/new recommendation diff.

## Tests

- [ ] Constraint directive affects recommendation.
- [ ] Priority directive changes option evaluation.
- [ ] Risk directive updates tradeoff analysis.
- [ ] Alternative exploration adds or changes options.
- [ ] Goal clarification can trigger full regeneration.
- [ ] Stale package fingerprint rejects refinement.
- [ ] Version history and comparison persist after restart.

## Exit Criteria

- [ ] Humans can correct assumptions, constraints, priorities, risks, and goals, then receive a regenerated package without manually rewriting the decision.
