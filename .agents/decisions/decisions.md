# Decisions

## Newly Authorized

- `DecisionProjection` is the authoritative execution-facing representation of decision authority.
- Execution must consume `DecisionProjection` rather than repository decision artifacts as its authoritative decision input.
- `DecisionProjection` is the execution-facing authority boundary for governed decision constraints, directives, conflicts, diagnostics, and provenance.
- Raw repository decision artifacts remain decision-lifecycle authority artifacts and compatibility context only; they are not execution-facing authority surfaces.
