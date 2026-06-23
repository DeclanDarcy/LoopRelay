# Reasoning Ownership Boundaries

Reasoning Trajectory explains how project thinking evolved. It does not own the authoritative state of decisions, operational context, governance, or execution.

## Ownership Matrix

| Subject | Owner | Reasoning Role |
| --- | --- | --- |
| Proposal revisions | Decision Lifecycle | May reference and explain why a revision mattered. |
| Decision outcomes | Decision Lifecycle | May explain why an outcome replaced, reframed, or superseded earlier thinking. |
| Settled understanding | Operational Context | May preserve the reasoning history that led to settled understanding. |
| Execution directives | Execution Projection | May explain why a directive emerged from decisions or constraints. |
| Contradiction detection | Governance | May preserve contradiction history after detection, investigation, recurrence, acceptance, or resolution. |
| Contradiction history | Reasoning Trajectory events | Owns explanatory event traces only. |
| Hypothesis history | Reasoning Trajectory events | Owns explanatory event traces only. |
| Alternative history beyond proposal scope | Reasoning Trajectory events | Owns explanatory event traces only. |
| Direction evolution | Derived from Reasoning Trajectory events | Remains derived until materialization is approved. |

## Domain Boundaries

Decision Lifecycle owns candidates, proposals, proposal revisions, reviews, refinements, resolution, supersession, archival, certification, and decision governance outputs. Reasoning may reference those artifacts but must not mutate or reinterpret their authoritative state.

Operational Context owns current settled understanding in `.agents/operational_context.md` and its proposal, review, promotion, compression, and diagnostics workflow. Reasoning may explain how an understanding emerged, but it does not promote or rewrite operational context.

Governance owns detection of current decision issues and contradictions. Reasoning may preserve the history and impact of those findings, but it does not enforce them.

Execution owns execution context, provider sessions, handoff validation, execution projections, commit preparation, commit, and push workflow. Reasoning may reference execution outputs and explain their influence, but it does not create execution directives or provider continuity.

Repository files remain authoritative. Runtime memory, graph indexes, query results, and reconstructions are caches or reports derived from repository artifacts.

## Boundary Rules

- Reasoning records must use typed references to source artifacts where possible.
- Reasoning records must preserve provenance.
- Reasoning records must not duplicate complete source artifacts.
- Reasoning records must not become alternate current-state records.
- Reasoning corrections must be new events, not edits to existing event history.
- Reasoning graph and reconstruction results are evidence views, not source-of-truth state.
