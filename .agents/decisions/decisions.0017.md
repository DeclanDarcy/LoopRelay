# Decisions

## Newly Authorized

- Confirm the Milestone 7 durable refinement artifact slice as correct.
- Preserve refinement trace as a first-class artifact rather than folding package regeneration provenance into proposal revisions.
- Maintain the workflow boundary:
  - direct proposal edits create proposal revisions
  - package regeneration creates refinement artifacts and new immutable package versions
- Keep proposal revisions scoped to mutation of the proposal workflow object.
- Keep refinement artifacts scoped to generation provenance for immutable package evidence.
- Preserve durable refinement artifacts as the answer to why a regenerated package exists, what human guidance caused it, which directives were extracted, what plan was executed, and what burden classification resulted.
- Treat the explicit trace chain as required Milestone 7 provenance: refinement request, directives, plan, old package, new package, comparison, and burden.
- Use durable refinement artifacts later for quality assessment, burden reporting, and generation certification.
- Complete backend directive-effect coverage before moving to UI regeneration surfaces.
- Prioritize the remaining backend behavior tests:
  - constraint directive affects recommendation
  - priority directive changes option evaluation
  - risk directive updates tradeoff analysis
  - goal clarification triggers or verifies full regeneration
- After backend directive-effect coverage, expose directive-driven package regeneration and old/new recommendation diff in the UI.

## Not Authorized

- Do not blur revision history with regeneration provenance.
- Do not move to UI regeneration and diff surfaces before finishing backend directive-effect coverage.
