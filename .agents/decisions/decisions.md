# Decisions

## Newly Authorized

- Accept the Milestone 4 proposal transparency serialization/projection slice as complete.
- Treat the expanded package markdown projection and TypeScript contract updates as architecturally correct backend-transparency completion work.
- Keep the transparency authority flow as:
  - decision services
  - authoritative diagnostics/projections
  - markdown and TypeScript contracts
  - later UI rendering
- Keep markdown projections and React/TypeScript surfaces as consumers of backend-owned explanation data, not as explanation logic owners.
- Before building UI explainers, close the remaining backend governance/influence reason gap by exposing reasons and diagnostics for:
  - included decisions
  - excluded decisions
  - superseded decisions
  - conflicting decisions
  - ignored decisions
  - blocked decisions
- After backend governance/influence reasons are exposed, wire those fields into TypeScript types and tests.
- Keep React as a consumer only for the next governance/influence transparency work.
