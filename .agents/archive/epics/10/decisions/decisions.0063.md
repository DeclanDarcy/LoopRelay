# Decisions

## Newly Authorized

- Continue Milestone 7 with `OperationalContextContradictionPanel` as the next implementation slice.
- Render structured contradiction records directly from backend continuity projections, including conflict type, evidence, severity, and resolution guidance.
- Do not synthesize contradiction classifications, priorities, or resolution guidance in React.
- After the contradiction panel, run a Milestone 7 projection-gap audit to verify backend continuity projections have corresponding UI surfaces.
- In the projection-gap audit, confirm compatibility strings are not the only visible representation where typed projections now exist.
- In the projection-gap audit, check semantic-diff coverage for all identity-aware modification paths, operational evolution timeline category coverage, and continuity diagnostic category representation.
- Run a Milestone 7 exit audit against the documented exit criteria before declaring the milestone complete.
- If the projection-gap and exit audits are clean after contradiction rendering, transition next into Milestone 8 shared explainability work without reopening continuity semantics.
