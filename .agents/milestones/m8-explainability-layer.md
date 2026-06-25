## Milestone 8: Unified Explainability Layer

### Objective

Create one shared rendering language for explanations across workflow, decision sessions, decisions, execution, reasoning, continuity, health, diagnostics, and certification. This milestone consumes authoritative explanation projections produced by earlier milestones and replaces domain-specific rendering duplication. It does not create new domain explanations.

### Model

Add `src/CommandCenter.UI/src/types/explainability.ts` with presentation-only concepts:

- [x] `Explanation`
- [x] `ExplanationEvidence`
- [x] `ExplanationConstraint`
- [x] `ExplanationAlternative`
- [x] `ExplanationAssumption`
- [x] `ExplanationDiagnostic`
- [x] `ExplanationUncertainty`
- [x] `ExplanationRecommendation`
- [x] `ExplanationAction`
- [x] `ExplanationHealthDimension`

These types organize already-authoritative domain projection data. They do not normalize backend domains into one generic authority.

Milestone 4 and related transparency milestones own domain explanation projections. Milestone 8 owns shared presentation components and adapters only.

### Components

Add shared components under `src/CommandCenter.UI/src/components/explainability/`:

- [x] `EvidenceList`
- [x] `DecisionBasis`
- [x] `ConstraintViewer`
- [x] `AlternativeExplorer`
- [x] `UncertaintyView`
- [x] `HealthView`
- [x] `DiagnosticList`
- [x] `ActionEligibilityView`
- [x] `CertificationFindingsView`

Each component must render:

- [x] what happened
- [x] why
- [x] evidence
- [x] alternatives
- [x] constraints
- [x] uncertainty
- [x] next action

### Integration

- [x] Add adapter functions under `src/CommandCenter.UI/src/lib/explainability/` for each domain. Adapters map authoritative domain fields into presentation concepts without computing domain outcomes.
   - [x] Workflow health, certification, recovery, gates, continuation, and reports adapter slice.
   - [x] Governance certification, recovery, health, and transfer eligibility adapter slice.
   - [x] Decision certification, generation certification, governance findings, evidence sources, diagnostics, and lifecycle eligible actions adapter slice.
   - [x] Decision recommendation, quality, burden, refinement, rejected-option, resolution assimilation, and influence/adherence adapter slice.
   - [x] Execution prompt manifest, repository snapshot, governed conflict, artifact diagnostics, event consequences, session history/failure evidence, generated handoff review, git eligibility, recovery, monitoring, and handoff-processing adapter slice.
   - [x] Reasoning reconstruction confidence, missing evidence, scope, provenance, reachability, diagnostics, materialization review, and certification adapter slice.
   - [x] Continuity compression, semantic diff, operational evolution, grouped diagnostics, repeated signals, warning diagnostics, trend evidence, and report evidence adapter slice.
   - [x] Operational-context proposal lifecycle, review/promotion diagnostics, current/proposal summary, decision assimilation, taxonomy basis, limits, consequences, and contradictions adapter slice.
- [x] Adapters may reorganize authoritative information, but they must not omit semantically relevant evidence, constraints, uncertainty, diagnostics, findings, or eligible actions.
- [x] Replace domain-specific explanation widgets in:
   - [x] decisions
      - [x] Decision certification evidence/findings, generation certification findings/failures/diagnostics, governance findings/diagnostics, evidence source attributions, and lifecycle eligible actions now render through shared explainability components.
      - [x] Recommendation evidence/concerns/assumptions/alternatives, quality score basis/contributions, burden selection, refinement constraints/diagnostics, rejected-option rationale, resolution assimilation evidence/diagnostics, and decision influence/adherence diagnostics now render through shared explainability components.
   - [x] workflow
      - [x] Workflow health dimensions and certification findings now render through shared explainability components.
      - [x] Workflow recovery diagnostics/artifacts, gate action eligibility/diagnostics, continuation action eligibility/diagnostics, and workflow reports now render through shared explainability components.
   - [x] decision sessions
      - [x] Governance certification findings/diagnostics, recovery findings/diagnostics, health dimensions, transfer eligibility findings, and eligible transfer action now render through shared explainability components.
   - [x] execution
      - [x] Prompt manifest evidence/diagnostics, execution transparency diagnostics, repository snapshot evidence, governed conflict diagnostics, and git commit/push eligibility diagnostics now render through shared explainability components.
      - [x] Artifact size diagnostics, execution event consequences, session history evidence/failure diagnostics, and generated handoff review actions/evidence now render through shared explainability components.
   - [x] reasoning
      - [x] Reconstruction confidence rationale, missing evidence, reconstruction scope, reachability, grouped diagnostics, materialization review evidence/thresholds/risks, taxonomy findings, and certification findings now render through shared explainability components.
   - [x] operational context
      - [x] Compression item outcomes, revision evidence, compressed-understanding diagnostics, semantic diff evidence, operational evolution timeline evidence, continuity compression observations, grouped diagnostics, repeated signals, warnings, and report evidence now render through shared explainability components.
      - [x] Proposal lifecycle status evidence, review/promotion diagnostics, current/proposal summary evidence, decision assimilation status/evidence/constraints/open questions, taxonomy basis, assimilation limits, consequences, and contradictions now render through shared explainability components.
   - [x] health
   - [x] diagnostics
   - [x] certification
- [x] Keep domain-specific detail panels where needed, but render evidence, constraints, alternatives, uncertainty, health, diagnostics, and certification findings through shared components.
- [x] Keep visual language consistent across status badges, diagnostics, warnings, findings, evidence references, and action eligibility.

### Tests

- [x] Component tests for all shared explainability components.
- [x] Adapter tests proving adapters do not compute domain scores, decisions, lifecycle state, or eligibility.
   - [x] Workflow adapter preservation coverage for health, certification, recovery, gates, continuation, and reports.
   - [x] Governance adapter preservation coverage for certification, recovery, health, and transfer eligibility.
   - [x] Decision adapter preservation coverage for certification evidence, generation certification findings, governance findings, evidence source attributions, diagnostics, and lifecycle eligibility.
   - [x] Decision adapter preservation coverage for recommendation explanation, quality score contribution, burden selection, refinement constraints, rejected-option rationale, and influence/adherence diagnostics.
   - [x] Execution adapter preservation coverage for prompt manifest facts, repository snapshot paths, artifact diagnostics, event consequences, session history/failure evidence, generated handoff review actions, governed conflicts, git eligibility, recovery, monitoring, and handoff-processing diagnostics.
   - [x] Reasoning adapter preservation coverage for evidence, provenance, confidence rationale, reconstruction scope, diagnostics, uncertainty, reachability, materialization thresholds/risks, taxonomy findings, and certification findings.
   - [x] Continuity adapter preservation coverage for lifecycle facts, compression outcomes, semantic identity, evidence, grouped diagnostics, warning diagnostics, repeated signals, trend evidence, and continuity reports.
   - [x] Continuity adapter preservation coverage for operational-context proposal lifecycle/review facts, promotion diagnostics, current/proposal summary facts, decision assimilation status, taxonomy basis, limits, consequences, contradictions, and open questions.
- [x] Adapter tests proving semantically relevant evidence and diagnostics are preserved when mapping into presentation concepts.
   - [x] Workflow health, certification, recovery, gates, continuation, and report evidence/diagnostic preservation coverage.
   - [x] Governance certification evidence/diagnostics, recovery evidence/diagnostics, health findings/evidence, and eligibility findings/action coverage.
   - [x] Decision certification evidence/sources, generation certification sources, governance finding evidence, evidence source attributions, and lifecycle rule/input preservation coverage.
   - [x] Decision recommendation evidence, quality contribution metadata, burden signal sources, refinement directive sources, rejected-option evidence, and influence projection/adherence evidence preservation coverage.
   - [x] Execution prompt context evidence, repository snapshot path evidence, artifact threshold evidence, event evidence, session history/failure evidence, generated handoff review constraints, governed conflict source evidence, git action constraints, and recovery/monitoring/handoff diagnostics preservation coverage.
   - [x] Reasoning reconstruction evidence/provenance, confidence uncertainty, scope reachability, diagnostic category evidence, materialization branch evidence, taxonomy evidence, and certification reference preservation coverage.
   - [x] Continuity compression evidence, semantic identity basis, operational evolution evidence, diagnostic category evidence, repeated signal diagnostics, warning diagnostics, and report reference preservation coverage.
   - [x] Operational-context lifecycle hashes/paths/status, review stale reasons, promotion failure reasons, assimilation source evidence, taxonomy matched rules/evidence/diagnostics, limit counts/reason, consequence evidence, contradiction evidence, and resolution guidance preservation coverage.
- [x] UI characterization tests proving major domains use shared explainability components.

### Audit Result

- [x] Coverage audit completed. Remaining domain-specific detail panels are intentional product/detail layouts; explanation facts route through shared components.
- [x] Authority audit completed. Adapters remain projection-only remapping of backend fields; no lifecycle, eligibility, quality, burden, taxonomy, confidence, certification, governance, continuity, or execution outcomes were moved into the UI.
- [x] Preservation audit completed. The audit found small presentation gaps in decision proposal evidence/action rendering, execution validation diagnostics, and shared `DecisionBasis` assumptions/recommendations; these now render through shared components without changing domain authority.
- [x] UI audit completed. Full UI characterization suite and production build pass.

### Exit Criteria

- [x] Evidence, constraints, alternatives, uncertainty, health, diagnostics, action eligibility, and certification findings render consistently across the app.
- [x] Explanations are composed from authoritative projections.
- [x] No second domain authority is introduced.
- [x] Users encounter the same explanation model across every major workspace.
