# Decisions

## Newly Authorized

- Confirm the first Milestone 7 slice as correct and complete for directive analysis.
- Preserve the refinement-analysis boundary as a first-class non-mutating workflow stage.
- Keep refinement analysis advisory only: it may produce directives and a plan, but it must not mutate proposals, revisions, packages, or lifecycle authority.
- Preserve proposal fingerprint validation at the analysis boundary.
- Preserve compatibility with existing direct `DecisionRefinementRequest` workflows while directive-driven refinement becomes the preferred path.
- Proceed next to scoped regeneration from `RefinementPlan`.
- Regeneration must consume `RefinementPlan` as the interpreted input rather than rereading raw refinement text.
- Analysis owns interpretation; regeneration owns execution.
- Scoped regeneration must require matching proposal fingerprint, package fingerprint, and package version.
- Stale package authority must return a conflict rather than silently regenerating.
- Old package versions must remain immutable and preserved.
- Regeneration must create a new package version rather than overwriting an existing package.
- Persist comparison and diagnostics alongside regenerated package versions.

## Not Authorized

- Do not collapse `RefinementPlan` directly into package mutation.
- Do not edit or overwrite existing package versions during regeneration.
- Do not let regeneration bypass directive analysis by independently interpreting raw human guidance.
