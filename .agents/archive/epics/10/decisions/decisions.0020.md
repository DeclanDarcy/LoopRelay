# Decisions

## Newly Authorized

- Accept the Milestone 4 entry slice and the added transparency inventory as the correct starting point for Decision Transparency.
- Continue Milestone 4 backend-first: authority-owned transparency projections must exist before React composes explanation panels.
- Keep React responsible for presentation only; it must not derive score contribution, quality rating thresholds, burden weighting, projection inclusion, or projection exclusion rules.
- Treat proposal transparency as lower backend risk because recommendation rationale, validation diagnostics, recommendation data, option evaluations, and related proposal facts are already persisted and typed.
- Elevate rejected and deduplicated option payload preservation as a backend-first Milestone 4 concern; counts alone are insufficient for later user-facing explanation.
- Sequence the next Milestone 4 implementation work as:
  - quality explanation projection fields
  - burden explanation projection fields
  - execution influence inclusion and exclusion reasoning through the decision API
  - UI composition after projection fields are in place
- Resume normal implementation verification in the next code slice with backend service tests, endpoint tests when routes change, UI characterization tests when presentation changes, and build verification.
- Stage, commit, and push the accepted Milestone 4 inventory work before beginning implementation.
