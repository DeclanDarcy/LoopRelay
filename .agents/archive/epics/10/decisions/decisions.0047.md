# Decisions

## Newly Authorized

- Continue Milestone 6 with UI authority-boundary notices end to end.
- TypeScript error models and API client behavior should preserve backend `boundaryViolation` payloads unchanged through transport.
- The boundary notice component must be presentation-only and must render boundary rule, owning domain, rejected assertion, allowed alternative, diagnostic detail, and severity verbatim from the backend projection.
- The boundary notice component should accept a generic structured boundary projection rather than reasoning-specific props so it can be reused by other domains in Milestone 8 without introducing shared semantic authority prematurely.
- Add characterization coverage for each structured boundary notice branch.
