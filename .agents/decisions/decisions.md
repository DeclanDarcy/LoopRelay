# Decisions

## Newly Authorized

- Preserve the Milestone 8 foundation approach: start with shared presentation primitives, then prove the adapter boundary on a clean domain before migrating more complex surfaces.
- Treat the shared explainability components as renderers of explanation structure only.
- Treat explainability adapters as projection reshaping only; they must not compute lifecycle, eligibility, health, scores, certification, semantic outcomes, or domain authority.
- Continue Milestone 8 workflow expansion before other domains, in this order:
  - Gates
  - Continuation
  - Recovery
  - Reports
- After workflow expansion, migrate governance certification and recovery, then decision certification and governance panels.
- Keep each migration backed by adapter tests proving no domain outcome is computed in the shared layer.
