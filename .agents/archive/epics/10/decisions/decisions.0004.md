# Decisions

## Newly Authorized

- The workflow panel slice is accepted as a valid Milestone 1 continuation, assuming panels continue consuming existing workflow hooks/projections rather than introducing new workflow aggregation authority.
- Workflow panels should follow the direct authority path: workflow service to endpoint to hook to panel.
- Any intermediate workflow UI model is acceptable only if it is purely presentational and does not become a new workflow authority.
- Workflow surfaces belong in the repository workspace experience because workflow is the operational backbone, not a disconnected subsystem.
- Health decomposition remains a boundary to defend: health must render dimensions, findings, evidence, and diagnostics, with any summary label derived from those facts.
- Certification remains observational. It may report, summarize, validate, and explain, but it must not repair, mutate, recover, or advance lifecycle state.
- The next Milestone 1 slice should focus on repository/dashboard workflow summary integration using existing workflow projections, without creating a dashboard-specific workflow lifecycle.
- The next Milestone 1 slice should add concise workflow consumption-pattern documentation before moving into Milestone 2.
- Consumption-pattern documentation should establish that workflow owns operational stage, gates, progression, continuation, recovery, health, and certification.
- Governance should own decision-session lifecycle, transfer, recovery, and continuity artifacts, while consuming workflow for operational status, blocking gates, and required human action.
- Execution should own execution-session lifecycle, while consuming workflow for operational placement and progression context.
- Continuity should own operational-context lifecycle, while consuming workflow where workflow exposes review and promotion requirements.
- If repository/dashboard workflow summary integration and consumption-pattern documentation complete cleanly, Milestone 1 can be treated as effectively ready to transition into Milestone 2.
