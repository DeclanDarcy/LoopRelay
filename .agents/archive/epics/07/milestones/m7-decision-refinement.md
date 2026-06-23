# Milestone 7: Interactive Decision Refinement

## Goal

let humans guide regeneration without becoming package authors.

## Work

- [x] Add structured `RefinementDirective` and `RefinementDirectiveType`:
  - [x] `AddConstraint`
  - [x] `RemoveConstraint`
  - [x] `IncreasePriority`
  - [x] `DecreasePriority`
  - [x] `ExploreAlternative`
  - [x] `ReevaluateRisk`
  - [x] `ReevaluateCost`
  - [x] `ReevaluateRecommendation`
  - [x] `ClarifyGoal`
- [x] Add `IRefinementAnalysisService`.
- [x] Add `RefinementPlan`:
  - [x] regenerate options
  - [x] reevaluate tradeoffs
  - [x] reevaluate recommendation
  - [x] full regeneration
  - [x] applied constraints
  - [x] diagnostics
- [x] Keep current direct `DecisionRefinementRequest` support for compatibility, but prefer directive-driven refinement in UI.
- [x] Add endpoints to analyze refinement before mutation and regenerate scoped package versions.
  - [x] Analyze refinement before mutation.
  - [x] Regenerate scoped package versions.
- [x] Preserve every refinement as:
  - [x] request
  - [x] directives
  - [x] plan
  - [x] old package version
  - [x] new package version
  - [x] comparison
  - [x] diagnostics
- [x] Classify the refinement's human authoring burden:
  - [x] small directive-only adjustment is `MinorEdit`
  - [x] scoped regeneration is `MajorRefinement`
  - [x] replacement of generated content with human-authored content is `FullRewrite`
- [x] Ensure refinement never mutates prior package versions.
- [x] Add UI controls for structured directives and show old/new recommendation diff.

## Tests

- [x] Constraint directive affects recommendation.
- [x] Priority directive changes option evaluation.
- [x] Risk directive updates tradeoff analysis.
- [x] Alternative exploration adds or changes options.
- [x] Goal clarification can trigger full regeneration.
- [x] Stale package fingerprint rejects refinement.
- [x] Version history and comparison persist after restart.

## Exit Criteria

- [x] Humans can correct assumptions, constraints, priorities, risks, and goals, then receive a regenerated package without manually rewriting the decision.
