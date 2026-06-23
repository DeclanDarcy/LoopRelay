# Decisions

## Newly Authorized

- Confirm the Milestone 7 human-authoring burden slice as correct and strategically important.
- Preserve `HumanAuthoringBurden` as the metric foundation for M8 quality assessment, M10 certification, workflow-replacement validation, and recommendation effectiveness measurement.
- Keep human-authoring burden observational only; record `FullRewrite` and other burden outcomes without blocking or controlling workflow transitions.
- Preserve the distinction between human guidance followed by system regeneration and human-authored package rewriting.
- Preserve scoped package regeneration as `MajorRefinement`, because authorship remains primarily with the system after human guidance.
- Preserve generated context, options, tradeoffs, recommendation, or assumptions replacement as `FullRewrite`, because that indicates human-authored final decision content.
- Proceed next to durable refinement artifacts for the analyzed refinement request, directive set, and refinement plan.
- Use durable refinement artifacts to trace why a regenerated package was created from the prior package version.
- After durable refinement artifacts, expose directive-driven regeneration and old/new recommendation diff in the UI.
- Treat recommendation diff visibility as the highest-value review surface for regenerated packages.

## Not Authorized

- Do not make burden classification govern, block, punish, or distort human decision workflows.
- Do not collapse scoped regeneration into `FullRewrite`.
- Do not proceed to dashboards or certification before preserving the refinement trace needed to explain regenerated packages.
