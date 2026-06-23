# Decisions

## Newly Authorized

- Confirm the Milestone 7 backend directive-effect slice as correct and strategically aligned.
- Treat directive-driven regeneration as a behavioral mechanism, not just a traceability mechanism.
- Preserve the refinement flow where human guidance changes generation context, which then changes tradeoffs, recommendation, and regenerated package output.
- Keep constraint guidance flowing through generation context into tradeoffs and recommendations rather than patching recommendation output directly.
- Keep priority guidance scoped to option evaluation, not decision authority or automatic decision selection.
- Continue preserving separate provenance paths for direct proposal revisions and package regeneration/refinement artifacts.
- Treat the backend portion of Milestone 7 as approaching completion after directive analysis, refinement plans, scoped regeneration, behavioral directive effects, package versioning, comparison, refinement artifacts, and burden classification.
- Shift the next Milestone 7 work to user-facing directive-driven refinement surfaces.
- Prioritize UI workflow for:
  - guidance analysis
  - directive review
  - regeneration controls
  - package comparison
  - old/new recommendation diff
  - visible human-authoring burden classification
- Make the UI answer what changed, not merely whether a new package was created.
- Surface recommendation changes, evidence changes, risk changes, and constraint changes without requiring users to read raw artifacts.

## Not Authorized

- Do not collapse constraint guidance into hidden recommendation patching.
- Do not let priority guidance select or resolve decisions.
- Do not merge proposal revision provenance with package regeneration provenance.
- Do not continue backend hardening ahead of the directive-driven UI workflow unless UI work exposes a blocking backend gap.
