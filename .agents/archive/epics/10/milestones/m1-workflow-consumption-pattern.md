# Milestone 1 Workflow Consumption Pattern

## Authority

`CommandCenter.Workflow` owns operational stage, progress state, gates, required human action, continuation, recovery, workflow health, workflow certification, and the canonical operational timeline.

Consumers may render workflow facts, filter views by workflow facts, and link from workflow facts to owning workspaces. Consumers must not recompute lifecycle progression, gate satisfaction, continuation eligibility, recovery state, health dimensions, certification results, or timeline entries.

## Projection Path

Workflow facts flow through the existing authority path:

- workflow service projection
- workflow backend endpoint
- shell workflow command
- TypeScript workflow client and hook
- presentation component

Dashboard and workspace summaries should accept an already-loaded workflow projection when possible instead of embedding workflow state into repository dashboard projections.

## Workspace Consumption

Repository workspace renders workflow as the primary operational status and may place workflow panels near repository details because workflow is the operational backbone.

Execution workspace owns execution-session details and actions. It consumes workflow for operational placement, current gate, required action, and progression context, but it must not maintain a separate execution lifecycle rail derived from `RepositoryExecutionState`.

Governance workspace owns decision-session lifecycle, transfer, recovery, health, and continuity artifacts. It should consume workflow for operational status, blocking gates, and required human action, especially when governance transfer state affects workflow progress.

Decision pipeline owns candidate, proposal, review, refinement, resolution, supersession, archive, quality, governance, and influence facts. It consumes workflow when decision gates explain why operational progress is blocked.

Operational-context workspace owns context proposals, review, promotion, compression, semantic diff, and continuity diagnostics. It consumes workflow where review and promotion gates expose required action or commit eligibility.

Health and certification consumers must preserve decomposition. They may summarize, but dimensions, findings, evidence, diagnostics, failures, and conflicts remain visible.
