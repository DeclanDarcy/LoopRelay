# Decisions

## Newly Authorized

- Treat Milestone 6A Certification Shell as complete.
- Proceed next with Milestone 6B Determinism and Recovery Certification.
- Certification remains a proof layer, not a hidden repair or control plane.
- Certification must observe and report without mutating lifecycle state.
- Preserve the dependency boundary where `CommandCenter.DecisionSessions` does not reference `CommandCenter.Workflow`.
- Preserve the read-only workflow integration proof strategy:
  - `CommandCenter.DecisionSessions` certifies lifecycle core.
  - Workflow consumes lifecycle through observability.
  - Backend and workflow tests prove integration boundaries.
- Add actual recomputation checks:
  - Same evidence produces the same metrics.
  - Same metrics produces the same economics.
  - Same evidence produces the same coherence.
  - Same analysis produces the same lifecycle policy.
- Add recovery certification proving:
  - Missing snapshots are rebuilt.
  - Corrupt snapshots are diagnosed or rebuilt.
  - Authoritative corruption is diagnosed only.
- Certification output is limited to pass/fail findings, evidence, and reports.
- Certification must not repair, retry transfer, select an active session, or override policy.
