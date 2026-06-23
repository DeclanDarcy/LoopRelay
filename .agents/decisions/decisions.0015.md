# Decisions

## Newly Authorized

- Confirm the Milestone 7 scoped package-regeneration slice as correct and architecturally aligned.
- Preserve the boundary that `RefinementAnalysisService` interprets human intent and `DecisionPackageService` executes the resulting `RefinementPlan`.
- Preserve package id and package fingerprint as the regeneration authority boundary.
- Preserve immutable package version history during refinement regeneration.
- Preserve deterministic re-evaluation through tradeoff analysis, option comparison, and recommendation services instead of patching recommendation text.
- Proceed next to backend human-authoring burden classification.
- Classify directive-only small adjustments with no major regeneration as `MinorEdit`.
- Classify scoped regeneration that creates a new package version as `MajorRefinement`.
- Classify generated content replacement where human-authored content dominates as `FullRewrite`.
- Keep human-authoring burden classification observational rather than workflow-controlling.
- After backend burden classification, expose regeneration and old/new recommendation diff in the UI.

## Not Authorized

- Do not let regeneration reinterpret raw reviewer text.
- Do not allow burden classification to block or control workflow transitions.
- Do not overwrite prior package versions during refinement regeneration.
- Do not patch recommendation text outside the deterministic recommendation pipeline.
