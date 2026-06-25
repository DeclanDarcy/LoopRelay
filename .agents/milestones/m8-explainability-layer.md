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

- [ ] what happened
- [ ] why
- [ ] evidence
- [ ] alternatives
- [ ] constraints
- [ ] uncertainty
- [ ] next action

### Integration

- [ ] Add adapter functions under `src/CommandCenter.UI/src/lib/explainability/` for each domain. Adapters map authoritative domain fields into presentation concepts without computing domain outcomes.
   - [x] Workflow health and certification adapter slice.
- [ ] Adapters may reorganize authoritative information, but they must not omit semantically relevant evidence, constraints, uncertainty, diagnostics, findings, or eligible actions.
- [ ] Replace domain-specific explanation widgets in:
   - [ ] decisions
   - [ ] workflow
      - [x] Workflow health dimensions and certification findings now render through shared explainability components.
   - [ ] decision sessions
   - [ ] execution
   - [ ] reasoning
   - [ ] operational context
   - [ ] health
   - [ ] diagnostics
   - [ ] certification
- [ ] Keep domain-specific detail panels where needed, but render evidence, constraints, alternatives, uncertainty, health, diagnostics, and certification findings through shared components.
- [ ] Keep visual language consistent across status badges, diagnostics, warnings, findings, evidence references, and action eligibility.

### Tests

- [x] Component tests for all shared explainability components.
- [ ] Adapter tests proving adapters do not compute domain scores, decisions, lifecycle state, or eligibility.
   - [x] Workflow adapter preservation coverage for health and certification.
- [ ] Adapter tests proving semantically relevant evidence and diagnostics are preserved when mapping into presentation concepts.
   - [x] Workflow health and certification evidence/diagnostic preservation coverage.
- [ ] UI characterization tests proving major domains use shared explainability components.

### Exit Criteria

- [ ] Evidence, constraints, alternatives, uncertainty, health, diagnostics, action eligibility, and certification findings render consistently across the app.
- [ ] Explanations are composed from authoritative projections.
- [ ] No second domain authority is introduced.
- [ ] Users encounter the same explanation model across every major workspace.
