# Decisions

## Newly Authorized

- Proceed next with Workflow History Certification and Workflow Diagnostics Certification before the end-to-end fixture.
- Do not add additional workflow behavior unless certification exposes a concrete proof gap.
- History certification should prove reconstructability without workflow-owned truth for:
  - authority history.
  - gate history.
  - continuation history.
  - preparation history.
- Certification should fail when the current workflow state cannot explain how it got here.
- Certification should fail when authority history is unrecoverable.
- Diagnostics certification should verify explanation exists for:
  - blocked workflow state.
  - recovered workflow state.
  - progressed workflow state.
  - failed workflow state.
- Add a specific certification case for workflow state that cannot be reconstructed from missing domain evidence, missing workflow evidence, and conflicting artifacts.
- That unreconstructable-state case should produce:
  - certification failure.
  - explicit finding.
  - diagnostics present.
- Perform the full end-to-end fixture only after history and diagnostics certification are stable.
