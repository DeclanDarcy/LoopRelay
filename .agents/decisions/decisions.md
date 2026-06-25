# Decisions

## Newly Authorized

- Accept and close Milestone 3: Decision Pipeline Completion.
- Treat the added endpoint, transport, UI, and lifecycle characterization coverage as sufficient milestone closure evidence.
- Keep lifecycle legality backend-owned through `DecisionLifecycleRules` and `DecisionLifecycleEligibilityService`.
- Keep React responsible only for rendering backend eligibility and invoking backend commands.
- Retain the Milestone 3 proposal feature dispositions:
  - proposal review note authoring remains Deferred
  - proposal revision list remains Diagnostic and read-only
  - revision comparison remains Diagnostic and read-only
  - standalone context snapshot browser remains Internal for Milestone 3 and deferred to Continuity
- Keep Proposal Actions separate from Proposal Viewer for now because the split separates lifecycle mutations from semantic facts.
- Revisit Proposal Actions consolidation only during Milestone 8 or Milestone 9 product cohesion work.
- Begin Milestone 4 with a transparency inventory before adding projection fields or UI composition.
- Sequence Milestone 4 as:
  - inventory existing recommendation, option, diagnostic, governance, influence, quality, and burden semantic facts
  - complete missing authority-owned projections
  - compose UI over completed projections
- Stage, commit, and push the completed Milestone 3 work before beginning Milestone 4.
