# Milestone 10: Workflow Certification

Objective: prove the workflow is correct, recoverable, explainable, and authority-preserving.

Deliver:

- [x] `IWorkflowCertificationService`.
- [x] `WorkflowCertificationResult`.
- [x] `WorkflowCertificationFinding`.
- [ ] `RepositoryWorkflowReport`.
- [ ] `WorkflowProgressionReport`.
- [ ] `HumanGovernanceReport`.
- [ ] `WorkflowReadinessReport`.
- [x] authority certification.
- [x] recovery certification.
- [x] continuation certification.
- [x] preparation certification.
- [ ] end-to-end workflow fixture.
- [ ] workflow history certification.
- [ ] workflow diagnostics certification.
- [ ] workflow health certification.

Certification finding categories:

```text
Authority
Recovery
Progression
Preparation
Gate
History
Continuity
Execution
Decision
Git
Workflow
```

Required scenarios:

- [ ] happy path from execution through completed workflow.
- [ ] unresolved decision halts at decision resolution gate.
- [ ] context not reviewed halts at operational context review gate.
- [ ] context accepted but not promoted halts at operational context promotion gate.
- [ ] commit approval required halts at commit approval gate.
- [ ] push approval required halts at push approval gate.
- [ ] application restart recovers workflow without duplicate progression.
- [ ] preparation creates reviewable artifacts idempotently through existing domain commands.
- [ ] execution failure is diagnosable and recoverable.
- [ ] missing work selection halts at work selection gate.

Certification failure conditions:

- [ ] workflow selected work.
- [ ] workflow resolved a decision.
- [ ] workflow accepted, edited, rejected, or promoted context.
- [ ] workflow approved or executed commit.
- [ ] workflow approved or executed push.
- [x] workflow crossed an open gate.
- [x] workflow created or used a parallel domain command.
- [x] workflow preparation satisfied a gate.
- [x] workflow preparation moved the workflow stage.
- [x] workflow preparation created duplicate review artifacts for the same fingerprint.
- [ ] workflow state cannot be reconstructed from domain evidence.
- [ ] continuation duplicated progression after restart.
- [x] preparation duplicated artifacts or preparation events after restart.
- [ ] blocked, recovered, or progressed states lack diagnostics.
- [ ] preparation decisions lack diagnostics.
- [ ] authority history cannot be reconstructed.

Tests:

- [ ] failures generate findings.
- [ ] passing scenarios generate readiness evidence.
- [x] authority certification detects forbidden mutation.
- [ ] recovery certification detects lost state, corruption, and duplicate progression.
  - [x] missing timeline evidence is treated as derived/rebuildable evidence.
  - [x] stale persisted timeline evidence is detected and domain projection wins.
  - [x] corrupted timeline evidence is detected without losing domain state.
  - [x] corrupted continuation/preparation history does not duplicate events or artifacts.
  - [x] restart duplicate progression is certified.
  - [x] derived continuation/preparation history corruption is diagnosed as recoverable derived evidence.
- [x] continuation certification detects missed gate halting.
- [x] preparation certification detects duplicate artifacts, parallel commands, and gate bypass attempts.
- [ ] end-to-end fixture validates progression, gates, recovery, diagnostics, history, and certification.

Exit criteria:

- [x] certification service exists.
- [ ] repository, progression, human-governance, and readiness reports exist.
- [x] authority certification passes.
- [ ] recovery certification passes.
  - [x] initial domain-truth recovery certification passes.
  - [ ] full corruption and idempotency recovery matrix passes.
- [x] continuation certification passes.
- [x] preparation certification passes.
- [ ] end-to-end fixture passes.
- [ ] diagnostics and health certification pass.
