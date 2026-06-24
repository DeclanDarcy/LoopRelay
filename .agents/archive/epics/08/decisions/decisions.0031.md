# Decisions

## Newly Authorized

- Proceed next with Workflow Health Certification and Minimal Report Artifacts before the end-to-end fixture.
- Do not authorize any additional workflow behavior in the remaining Milestone 10 work.
- Remaining work should focus entirely on proving correctness, observability, and reconstructability of the existing workflow system.
- Health certification should remain observable, not opinionated.
- Health certification should cover:
  - corrupt evidence causing degraded recovery health.
  - open gates causing blocked gate health without failing certification merely because human authority is waiting.
  - duplicate progression risk surfacing as a continuation health finding.
  - duplicate artifact risk surfacing as a preparation health finding.
  - unreconstructable state surfacing as a projection health finding.
- Required reports should stay minimal and aggregate already-existing evidence:
  - `RepositoryWorkflowReport`.
  - `WorkflowProgressionReport`.
  - `HumanGovernanceReport`.
  - `WorkflowReadinessReport`.
- Reports should summarize existing certification findings and workflow evidence rather than introduce new evaluation logic or conclusions.
- Build the final end-to-end fixture only after health certification and minimal report artifacts exist.
