# Milestone 6: Decision Workflow Integration

Objective: make workflow decision-aware by consuming the existing Decisions domain.

Deliver:

- [ ] `WorkflowDecisionProjection` with decision id, candidate id, proposal id, package id, status, review state, resolution state, human authoring burden, created timestamp, and resolved timestamp.
- [ ] `WorkflowDecisionStatus` with missing, discovered, generated, under review, awaiting resolution, resolved, archived, and superseded.
- [ ] `IWorkflowDecisionService`.
- [ ] decision resolution rules for awaiting resolution, resolved, archived, and superseded.
- [ ] decision governance integration that treats healthy as eligible, advisory findings as eligible with warning, and blocked findings as workflow blocked.
- [ ] decision quality integration that surfaces human authoring burden, recommendation stability, tradeoff quality, context quality, and constraint quality as diagnostics.
- [ ] decision certification integration that surfaces certified, warning, and failed status as observability.
- [ ] `WorkflowDecisionDiagnostics`.
- [ ] timeline events: decision discovered, generated, reviewed, refined, resolved, archived, and superseded.
- [ ] recovery integration for decision status, governance, quality, certification, and resolution.

Rules:

- [ ] Workflow projection never mutates decisions. Later preparation may request existing Decisions discovery or generation commands, but workflow never refines, resolves, archives, supersedes, governs, or certifies decisions.
- [ ] Workflow never treats recommendations as authority.
- [ ] Progression eligibility must be based on resolved decision authority, not recommendation output.
- [ ] Superseded decisions follow replacement authority.

Tests:

- [ ] discovered, generated, awaiting resolution, resolved, archived, and superseded decisions project correctly.
- [ ] awaiting resolution opens decision resolution gate.
- [ ] resolved decisions close the gate and make operational context eligible.
- [ ] superseded decisions follow replacement lineage.
- [ ] governance healthy/advisory/blocked statuses project correctly.
- [ ] quality and certification signals surface as diagnostics.
- [ ] recovery rebuilds decision workflow state.
- [ ] workflow never mutates decisions.

Exit criteria:

- [ ] decision projection exists.
- [ ] decision integration works.
- [ ] resolution gates work.
- [ ] governance, quality, and certification signals surface.
- [ ] timeline integration exists.
- [ ] recovery integration exists.
- [ ] diagnostics exist.
