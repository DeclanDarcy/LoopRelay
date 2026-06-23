# Decisions

## Newly Authorized

- Proceed directly into Milestone 1 after the completed Milestone 0 foundation; do not change roadmap sequencing.
- Keep Workflow as a read-only coordination/projection layer. Timeline projection must remain derived from domain evidence and must not become workflow authority.
- Implement Milestone 1 with a canonical graph-first transition representation so validation, next-stage discovery, and later continuation can consume the same model.

## Carry Into Next Slice

- M1 should expose projection inputs, candidate stages, selected stage, and selection reason clearly enough to support deterministic precedence review.
- M1/M2 should preserve an explicit path for unknown or unprovable workflow state.
