# Architectural Capabilities

This matrix records architectural capabilities as they become observable, protected, certified, and documented during the post-MVP architecture program.

| Capability | Introduced | Protected | Certified | Reference Documentation | Status |
| --- | --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, future architectural regression framework | 0.1 local command-line baseline | `docs/architectural-mechanisms.md` | Certified locally with quarantines |
| Canonical contract Oracle | 0.2 | Repository dashboard golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, repository workspace golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, cross-family repeatability evidence, workflow projection field inventory, workflow fixture field classification, workflow instance golden fixture comparison, workflow TypeScript consumer verification, workflow request-boundary verification, workflow artifact freshness verification, workflow local certification, milestone certification review, milestone acceptance baseline, and procedural change workflow | Repository dashboard pilot certified locally as of Slice 0018; request-boundary extension verified in Slice 0019; repository workspace fixture verified in Slice 0020; repository workspace consumer verification verified in Slice 0021; repository workspace artifact freshness verified in Slice 0022; repository workspace request-boundary verification verified in Slice 0023; repository workspace pilot certified locally as of Slice 0024; two-family repeatability checkpoint recorded in Slice 0025; workflow projection inventory recorded in Slice 0026; workflow fixture field classification recorded in Slice 0027; workflow instance fixture comparison verified in Slice 0028; workflow TypeScript consumer verification verified in Slice 0029; workflow request-boundary verification verified in Slice 0030; workflow artifact freshness verified in Slice 0031; primary workflow projection pilot certified locally as of Slice 0032; three-family repeatability checkpoint recorded in Slice 0033; scoped milestone certification review recorded in Slice 0034; scoped milestone acceptance baseline recorded in Slice 0035 | `docs/contracts.md` | Accepted and baselined as the Phase 0 Contract Oracle foundation with explicit deferrals; repository dashboard, repository workspace, and primary workflow projection pilots are locally certified; full contract-surface coverage and generated contract lifecycle remain later work |
| Architectural regression framework | 0.3 | Backend architecture-test namespace, mechanism catalog, fixture-wiring meta-regression, architectural invariant catalog, catalog metadata regression, regression taxonomy, taxonomy metadata regression, ownership matrix, severity model, ownership/severity metadata regression, architectural drift model, drift metadata regression, regression UX specification, failure-message metadata regression, architectural confidence model, confidence metadata regression, regression lifecycle model, lifecycle metadata regression, regression architecture specification, specification metadata regression, frontend architecture-test area, frontend discoverability metadata regression, shell command-family classification, shell mirror inventory, and shell classification metadata regression | Initial M0.3 skeleton verified locally in Slice 0036; invariant catalog guard verified locally in Slice 0037; regression taxonomy guard verified locally in Slice 0038; ownership/severity guard verified locally in Slice 0039; drift model guard verified locally in Slice 0040; regression UX guard verified locally in Slice 0041; architectural confidence guard verified locally in Slice 0042; regression lifecycle guard verified locally in Slice 0043; regression architecture specification guard verified locally in Slice 0044; frontend regression skeleton verified locally in Slice 0045; shell regression classification verified locally in Slice 0046; milestone certification recorded in Slice 0047 | `docs/architectural-mechanisms.md`, `docs/shell-transport-classification.md` | Certified as a framework-complete Phase 0 architectural regression foundation with explicit enforcement deferrals; broad invariant enforcement remains later milestone work |

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
- architectural drift model for new authority, duplicate authority, transport responsibility growth, projection impurity, contract replication, state duplication, composition growth, dependency cycles, and semantic leakage,
- drift metadata guard that verifies detection, evidence, owner, severity, remediation, and escalation metadata,
- regression UX specification requiring invariant, architectural intent, observed drift, owner, severity, detection confidence, evidence expectation, remediation path, and escalation guidance in architectural failure messages,
- failure-message metadata guard that verifies the durable UX specification remains populated,
- architectural confidence model separating confidence from coverage, severity, detection confidence, implementation quality, and pass percentages,
- confidence metadata guard that verifies named confidence levels remain populated with mechanism quality, evidence quality, coverage breadth, freshness, and certification use,
- regression lifecycle model covering inventory, advisory, guarded, corroborated, certified, accepted, quarantined, weakened, replaced, and retired states,
- lifecycle metadata guard that verifies entry criteria, evidence, allowed transitions, decision requirements, and exit conditions,
- regression architecture specification defining how invariant definition, mechanism selection, ownership/severity, drift classification, failure UX, confidence/lifecycle, and certification mapping compose into one framework,
- specification metadata guard that verifies framework-composition metadata remains populated,
- frontend architecture-test area under `src/CommandCenter.UI/src/test/architecture`,
- frontend discoverability guard that verifies frontend architecture tests are tied to frontend ownership and invariant metadata before broad UI rules are enforced,
- shell command-family classification in `docs/shell-transport-classification.md`,
- shell mirror inventory for current state and target state,
- shell classification guard that verifies passive transport, shell-owned operations, transitional compatibility, and unknown/requires-review categories remain present,
- severity rules in `docs/architectural-mechanisms.md`.

Milestone 0.3 certification is recorded in `.agents/milestones/m0.3-regression-framework-certification-slice-0047.md`. The certification accepts M0.3 as framework-complete, not enforcement-complete: broad authority, transport, state, controller, workspace, runtime, generated-contract, CI, and release-path enforcement remains later milestone work.
