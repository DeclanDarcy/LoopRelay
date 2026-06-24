# Milestone 6: Decision Workflow Integration

Objective: make workflow decision-aware by consuming the existing Decisions domain.

Deliver:

- [x] `WorkflowDecisionProjection` with decision id, candidate id, proposal id, package id, status, review state, resolution state, human authoring burden, created timestamp, and resolved timestamp.
- [x] `WorkflowDecisionStatus` with missing, discovered, generated, under review, awaiting resolution, resolved, archived, and superseded.
- [x] `IWorkflowDecisionService`.
- [x] decision resolution rules for awaiting resolution, resolved, archived, and superseded.
- [x] decision governance integration that treats healthy as eligible, advisory findings as eligible with warning, and blocked findings as workflow blocked.
- [x] decision quality integration that surfaces human authoring burden, recommendation stability, tradeoff quality, context quality, and constraint quality as diagnostics.
- [x] decision certification integration that surfaces certified, warning, and failed status as observability.
- [x] `WorkflowDecisionDiagnostics`.
- [x] timeline events: decision discovered, generated, reviewed, refined, resolved, archived, and superseded.
- [x] recovery integration for decision status, governance, quality, certification, and resolution.

Rules:

- [x] Workflow projection never mutates decisions. Later preparation may request existing Decisions discovery or generation commands, but workflow never refines, resolves, archives, supersedes, governs, or certifies decisions.
- [x] Workflow never treats recommendations as authority.
- [x] Progression eligibility must be based on resolved decision authority, not recommendation output.
- [x] Superseded decisions follow replacement authority.

Tests:

- [x] discovered, generated, awaiting resolution, resolved, archived, and superseded decisions project correctly.
- [x] awaiting resolution opens decision resolution gate.
- [x] resolved decisions close the gate and make operational context eligible.
- [x] superseded decisions follow replacement lineage.
- [x] governance healthy/advisory/blocked statuses project correctly.
- [x] quality and certification signals surface as diagnostics.
- [x] recovery rebuilds decision workflow state.
- [x] workflow never mutates decisions.

Exit criteria:

- [x] decision projection exists.
- [x] decision integration works.
- [x] resolution gates work.
- [x] governance, quality, and certification signals surface.
- [x] timeline integration exists.
- [x] recovery integration exists.
- [x] diagnostics exist.
