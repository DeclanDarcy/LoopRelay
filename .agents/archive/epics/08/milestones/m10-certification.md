# Milestone 10: Workflow Certification

Objective: prove the workflow is correct, recoverable, explainable, and authority-preserving.

Deliver:

- [x] `IWorkflowCertificationService`.
- [x] `WorkflowCertificationResult`.
- [x] `WorkflowCertificationFinding`.
- [x] `RepositoryWorkflowReport`.
- [x] `WorkflowProgressionReport`.
- [x] `HumanGovernanceReport`.
- [x] `WorkflowReadinessReport`.
- [x] authority certification.
- [x] recovery certification.
- [x] continuation certification.
- [x] preparation certification.
- [x] end-to-end workflow fixture.
- [x] workflow history certification.
- [x] workflow diagnostics certification.
- [x] workflow health certification.

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

- [x] happy path from execution through completed workflow.
- [x] unresolved decision halts at decision resolution gate.
- [x] context not reviewed halts at operational context review gate.
- [x] context accepted but not promoted halts at operational context promotion gate.
- [x] commit approval required halts at commit approval gate.
- [x] push approval required halts at push approval gate.
- [x] application restart recovers workflow without duplicate progression.
- [x] preparation creates reviewable artifacts idempotently through existing domain commands.
- [x] execution failure is diagnosable and recoverable.
- [x] missing work selection halts at work selection gate.

Certification failure conditions:

- [x] workflow selected work.
- [x] workflow resolved a decision.
- [x] workflow accepted, edited, rejected, or promoted context.
- [x] workflow approved or executed commit.
- [x] workflow approved or executed push.
- [x] workflow crossed an open gate.
- [x] workflow created or used a parallel domain command.
- [x] workflow preparation satisfied a gate.
- [x] workflow preparation moved the workflow stage.
- [x] workflow preparation created duplicate review artifacts for the same fingerprint.
- [x] workflow state cannot be reconstructed from domain evidence.
- [x] continuation duplicated progression after restart.
- [x] preparation duplicated artifacts or preparation events after restart.
- [x] blocked, recovered, or progressed states lack diagnostics.
- [x] preparation decisions lack diagnostics.
- [x] authority history cannot be reconstructed.

Tests:

- [x] failures generate findings.
- [x] passing scenarios generate readiness evidence.
- [x] authority certification detects forbidden mutation.
- [x] recovery certification detects lost state, corruption, and duplicate progression.
  - [x] missing timeline evidence is treated as derived/rebuildable evidence.
  - [x] stale persisted timeline evidence is detected and domain projection wins.
  - [x] corrupted timeline evidence is detected without losing domain state.
  - [x] corrupted continuation/preparation history does not duplicate events or artifacts.
  - [x] restart duplicate progression is certified.
  - [x] derived continuation/preparation history corruption is diagnosed as recoverable derived evidence.
- [x] continuation certification detects missed gate halting.
- [x] preparation certification detects duplicate artifacts, parallel commands, and gate bypass attempts.
- [x] end-to-end fixture validates progression, gates, recovery, diagnostics, history, and certification.

Exit criteria:

- [x] certification service exists.
- [x] repository, progression, human-governance, and readiness reports exist.
- [x] authority certification passes.
- [x] recovery certification passes.
  - [x] initial domain-truth recovery certification passes.
  - [x] full corruption and idempotency recovery matrix passes.
- [x] continuation certification passes.
- [x] preparation certification passes.
- [x] end-to-end fixture passes.
- [x] diagnostics and health certification pass.
