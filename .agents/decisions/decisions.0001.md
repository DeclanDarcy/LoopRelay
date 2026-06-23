# Decisions

## Newly Authorized

- Milestone 1 thread records must remain limited to grouping, navigation, and summarization; do not add workflow semantics such as thread status, owner, resolution, or outcome.
- Milestone 1 event taxonomy must not grow lifecycle-style event types that imply approval, closure, active state, finalization, or other unapproved state-machine semantics.
- Reasoning relationships such as `Supersedes`, `Invalidates`, and `SelectedOver` remain explanatory records only; authoritative mutation remains in the source domain.
- Reasoning persistence treats structured JSON as authority and Markdown as deterministic projection; generated Markdown must not become the parsed source of domain behavior.
- Milestone 1 success requires evidence for restart-safe persistence, stable IDs across restart, path traversal rejection, unsupported schema rejection, event immutability, mandatory provenance, thread and relationship round trips, deterministic Markdown projections, and no specialized artifact directories for hypotheses, alternatives, contradictions, or directions.
