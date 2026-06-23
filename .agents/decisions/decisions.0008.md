# Decisions

## Newly Authorized

- Close Milestone 5 as complete.
- Treat the decisive M5 criterion as recommendation generation that is explainable, evidence-backed, and capable of declining to recommend.
- Treat `NoRecommendation` as a first-class generated recommendation state for weak evidence, excessive uncertainty, unresolved contradictions, and no viable option.
- Preserve recommendation output as advisory only.
- Preserve human resolution as the only source of decision authority.
- Begin Milestone 6 next.
- Keep the first Milestone 6 slice narrow:
  - introduce `DecisionPackage`
  - introduce `DecisionPackageMetadata`
  - introduce `DecisionPackageVersion`
  - persist an immutable package snapshot from the generated proposal path
- Treat the initial Milestone 6 package as governance hardening behind the existing proposal workflow, not a replacement workflow.
- Preserve the boundary that a package is evidence and not authority.
- Defer package validation, version history, package comparison, dashboards, quality assessment, throughput reporting, and certification until after the minimal immutable package snapshot exists.

## Not Authorized

- Do not rebuild the workflow during Milestone 6.
- Do not let package acceptance imply decision authority.
- Do not move from package generation to automatic decision approval, acceptance, rejection, or resolution.
