# Milestone 8 Explainability Surface Inventory

## New State This Slice

Milestone 8 now has a shared presentation foundation in the UI. Workflow is integrated across health, certification, recovery, gates, continuation, and reports. Governance is integrated across certification, recovery, health, and transfer eligibility.

## Current Explanation Surfaces

- Workflow currently projects health dimensions, certification findings, gate diagnostics, continuation reasoning, recovery diagnostics, completion evidence, transition reasoning, and governance health through `src/CommandCenter.UI/src/types/workflow.ts`.
- Governance currently renders lifecycle policy reasons and analysis warnings inside `GovernanceWorkspace.tsx`; certification findings/diagnostics, eligibility findings/actions, recovery findings/diagnostics, and health dimensions now use the shared explainability layer.
- Decisions currently have specialized explanation components for burden, recommendation, quality, governance, option comparison, evidence sources, revision history, certification, generation certification, refinement, and resolution.
- Execution currently renders launch readiness, context validation, artifact diagnostics, decision influence, prompt manifests, git evidence, handoff review, session failure metadata, and history through feature-specific panels.
- Reasoning currently renders reconstruction confidence rationale, reachable and unreachable evidence, diagnostic groups, trace evidence, materialization findings, and certification evidence through reasoning-specific panels.
- Operational context currently renders semantic changes, identity-aware modifications, assimilation limits, compression explanation, consequences, contradictions, taxonomy, proposal status, proposal comparison, and evolution timeline through operational-context-specific panels.
- Continuity currently renders diagnostic groups and continuity warnings through continuity-specific panels.
- Health, diagnostics, and certification are repeated as local card/list patterns across several workspaces.

## Implemented Shared Presentation Layer

- Added presentation-only explainability types in `src/CommandCenter.UI/src/types/explainability.ts`.
- Added shared components under `src/CommandCenter.UI/src/components/explainability/`:
  - `EvidenceList`
  - `DecisionBasis`
  - `ConstraintViewer`
  - `AlternativeExplorer`
  - `UncertaintyView`
  - `HealthView`
  - `DiagnosticList`
  - `ActionEligibilityView`
  - `CertificationFindingsView`
- Added workflow adapter functions under `src/CommandCenter.UI/src/lib/explainability/workflow.ts` for health, certification, recovery, gates, continuation, and reports.
- Added governance adapter functions under `src/CommandCenter.UI/src/lib/explainability/governance.ts` for certification, recovery, health, and transfer eligibility.
- Integrated `HealthView`, `DiagnosticList`, `CertificationFindingsView`, `EvidenceList`, and `ActionEligibilityView` into workflow health, certification, recovery, gates, continuation, and report panels.
- Integrated `HealthView`, `DiagnosticList`, `CertificationFindingsView`, and `ActionEligibilityView` into governance certification, recovery, health, and eligibility panels.

## Authority Boundary

- The shared types are presentation concepts only.
- Workflow health status, reasons, evidence, diagnostics, certification pass/fail state, gate state, continuation outcome, recovery state, and report status still come from workflow projections.
- The workflow adapter maps strings and typed workflow objects into presentation props without recomputing status, certification, gate state, lifecycle state, readiness, continuation, recovery, or eligibility.
- Governance certification result, finding severity, recovery success, recovery findings, health status, transfer eligibility status, and lifecycle policy reason still come from decision-session projections.
- The governance adapter maps these fields into presentation props without recomputing certification result, lifecycle policy, recovery outcome, health outcome, recommendation score, or transfer eligibility.

## Verification

- `npm test -- --run src/test/characterization/explainabilityComponents.test.tsx src/test/characterization/explainabilityWorkflowAdapters.test.ts src/test/characterization/workflowPanels.test.tsx`
- `npm test -- --run src/test/characterization/explainabilityComponents.test.tsx src/test/characterization/explainabilityWorkflowAdapters.test.ts src/test/characterization/explainabilityGovernanceAdapters.test.ts src/test/characterization/governanceWorkspace.test.tsx`
- `npm run build`

## Residual Work

- Continue governance for remaining lifecycle policy and analysis diagnostics if they become semantically rich enough for shared components.
- Add adapters and shared rendering for decisions, execution, reasoning, operational context, continuity, diagnostics, and certification.
- Add cross-domain characterization tests proving major workspaces render shared explainability components.
