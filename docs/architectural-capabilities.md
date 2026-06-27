# Architectural Capabilities

This matrix records architectural capabilities as they become observable, protected, certified, and documented during the post-MVP architecture program.

| Capability | Introduced | Protected | Certified | Reference Documentation | Status |
| --- | --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, future architectural regression framework | 0.1 local command-line baseline | `docs/architectural-mechanisms.md` | Certified locally with quarantines |
| Canonical contract Oracle | 0.2 | Repository dashboard golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, repository workspace golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, cross-family repeatability evidence, workflow projection field inventory, workflow fixture field classification, workflow instance golden fixture comparison, workflow TypeScript consumer verification, workflow request-boundary verification, workflow artifact freshness verification, workflow local certification, milestone certification review, milestone acceptance baseline, and procedural change workflow | Repository dashboard pilot certified locally as of Slice 0018; request-boundary extension verified in Slice 0019; repository workspace fixture verified in Slice 0020; repository workspace consumer verification verified in Slice 0021; repository workspace artifact freshness verified in Slice 0022; repository workspace request-boundary verification verified in Slice 0023; repository workspace pilot certified locally as of Slice 0024; two-family repeatability checkpoint recorded in Slice 0025; workflow projection inventory recorded in Slice 0026; workflow fixture field classification recorded in Slice 0027; workflow instance fixture comparison verified in Slice 0028; workflow TypeScript consumer verification verified in Slice 0029; workflow request-boundary verification verified in Slice 0030; workflow artifact freshness verified in Slice 0031; primary workflow projection pilot certified locally as of Slice 0032; three-family repeatability checkpoint recorded in Slice 0033; scoped milestone certification review recorded in Slice 0034; scoped milestone acceptance baseline recorded in Slice 0035 | `docs/contracts.md` | Accepted and baselined as the Phase 0 Contract Oracle foundation with explicit deferrals; repository dashboard, repository workspace, and primary workflow projection pilots are locally certified; full contract-surface coverage and generated contract lifecycle remain later work |
| Architectural regression framework | 0.3 | Backend architecture-test namespace, mechanism catalog, fixture-wiring meta-regression, architectural invariant catalog, catalog metadata regression, regression taxonomy, taxonomy metadata regression, ownership matrix, severity model, and ownership/severity metadata regression | Initial M0.3 skeleton verified locally in Slice 0036; invariant catalog guard verified locally in Slice 0037; regression taxonomy guard verified locally in Slice 0038; ownership/severity guard verified locally in Slice 0039 | `docs/architectural-mechanisms.md` | In progress; invariant catalog, regression taxonomy, ownership matrix, and severity model are installed and guarded; confidence model, frontend regression area, shell regression classification, and certification remain pending |

## Structural Verification

Structural verification is the ability to run known verifier entry points and understand what each one protects before architectural migration begins.

The current certified scope is local command-line verification only. CI verification, IDE integration, packaged Tauri release verification, and broad Rust shell behavioral coverage are not certified.

The certified local baseline is recorded in `.agents/milestones/m0.1-structural-verification-certification.md`.

Accepted quarantines:

- missing CI baseline,
- serialized .NET verifier execution,
- partial Rust shell behavioral coverage,
- unknown IDE verification path,
- unknown Tauri packaged release path.

The shell passive relay regressions now prove successful opaque backend JSON and boundary-violation error envelopes are preserved without shell-owned domain interpretation through the generic GET value helper. The next protections are POST relay coverage, non-boundary error semantics, and command-family classification.

## Canonical Contract Oracle

The Contract Oracle is introduced as a durable definition and initial inventory in `docs/contracts.md`.

Current scope:

- canonical Oracle definition,
- boundary taxonomy,
- family-level contract relationship matrix,
- endpoint catalog and consumer taxonomy,
- narrow serialization rules required before fixture selection,
- backend HTTP JSON serialization observations,
- repository dashboard field ownership pilot,
- repository dashboard golden fixture and recursive backend serialization comparison test,
- repository dashboard drift policy classification for structural drift versus compatibility-review drift,
- recursive executable dashboard consumer drift verification against the Rust shell mirror,
- recursive executable dashboard consumer verification against the manual TypeScript type,
- recursive executable dashboard consumer verification against the dev Tauri mock,
- consumer category reporting for runtime, compile-time, and development/test consumers,
- shared consumer-verification test-support infrastructure for the recursive comparison engine and Rust, TypeScript, and dev mock shape providers,
- repository dashboard contract artifact freshness manifest and verifier,
- repository dashboard no-argument request-boundary verifier,
- repository workspace field ownership catalog and golden fixture comparison,
- repository workspace recursive consumer verification against Rust, TypeScript, and dev mock payload shapes,
- repository workspace contract artifact freshness manifest and verifier,
- repository workspace single-route-argument request-boundary verifier,
- distinct freshness failure modes for stale artifacts, unexpected manual artifact modification, and missing expected artifacts,
- procedural Oracle change workflow for drift classification, fixture update, consumer/artifact refresh, evidence, and rollback,
- cross-family repeatability evidence across repository dashboard, repository workspace, and primary workflow projection,
- workflow projection gated field inventory for `WorkflowInstance`,
- workflow fixture field classification for `WorkflowInstance`,
- workflow instance golden fixture and recursive backend serialization comparison,
- workflow TypeScript consumer verification against the backend golden fixture,
- workflow request-boundary verification for the primary workflow projection endpoint,
- workflow artifact freshness verification for the manual TypeScript workflow contract artifact,
- initial parallel truth inventory,
- fixture gating rule.

The Oracle is now locally certified for three pilot contracts: repository dashboard, repository workspace, and primary workflow projection. Dashboard certification evidence is recorded in `.agents/milestones/m0.2-repository-dashboard-oracle-certification-slice-0018.md`; workspace certification evidence is recorded in `.agents/milestones/m0.2-repository-workspace-oracle-certification-slice-0024.md`; workflow certification evidence is recorded in `.agents/milestones/m0.2-workflow-oracle-certification-slice-0032.md`. Cross-family repeatability evidence is recorded in `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0033.md`; it shows that the three pilots reused the same Oracle lifecycle without framework redesign. Milestone-level certification review is recorded in `.agents/milestones/m0.2-oracle-certification-review-slice-0034.md`; scoped acceptance and baseline evidence is recorded in `.agents/milestones/m0.2-oracle-acceptance-baseline-slice-0035.md`. Milestone 0.2 is accepted as the Phase 0 Contract Oracle foundation with explicit deferrals rather than full contract-surface coverage.

Consumer verification covers the Rust, TypeScript, and dev mock repository dashboard consumers, the Rust, TypeScript, and dev mock repository workspace response consumers, and the manual TypeScript `WorkflowInstance` shape for the first workflow fixture variant. Freshness verification covers the repository dashboard, repository workspace, and workflow TypeScript contract artifacts as Phase 0 verified manual artifacts, and request-boundary verification covers the repository dashboard no-argument command/API path, the repository workspace required repository-id GET path, and the primary workflow projection required repository-id GET path. Workflow projection now has field inventory, fixture field classification, `WorkflowInstance` fixture comparison, TypeScript consumer verification, request-boundary verification, artifact freshness verification, and local certification. It still has no dev mock workflow handler verification or populated `decisionSession` fixture variant; those are accepted gaps for the initial workflow pilot. The Oracle change workflow is procedural rather than automated. Remaining work for later milestones includes broader golden serialized fixtures, expanded dependency graph coverage, deterministic generated artifacts, fixture update tooling, richer non-empty command/query/body verification, semantic reinterpretation checks, mechanical versioning, and workflow automation where needed.

## Architectural Regression Framework

Milestone 0.3 is introduced with an initial backend architecture-test namespace and meta-regression. The first skeleton protects the existing Contract Oracle mechanisms as architectural regression targets: fixture drift detection, consumer verification, artifact freshness, request-boundary verification, and the framework wiring itself.

Current scope:

- backend architecture-test namespace under `tests/CommandCenter.Backend.Tests/Architecture`,
- mechanism catalog with owner, severity, intent, and remediation fields,
- discoverability regression for required Oracle mechanism test classes,
- output-wiring regression for Oracle golden fixtures,
- architectural invariant catalog in `docs/architectural-mechanisms.md`,
- invariant catalog guard that verifies required columns and populated metadata for core invariants,
- regression taxonomy with preferred mechanism, minimum acceptable mechanism, preferred execution phase, ownership, severity, evidence, drift, and remediation metadata,
- taxonomy guard that verifies required regression categories and populated mechanism-selection metadata,
- regression ownership matrix covering backend, frontend, shell, cross-layer, Oracle, generated artifacts, build, and CI surfaces,
- regression severity model separating architectural impact from local, CI, and release execution behavior,
- ownership/severity guard that verifies evidence, remediation, and escalation metadata,
- severity rules in `docs/architectural-mechanisms.md`.

This is not full M0.3 certification. The confidence model, frontend regression area, shell regression classification, and milestone-level certification remain pending.
