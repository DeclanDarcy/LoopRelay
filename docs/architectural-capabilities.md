# Architectural Capabilities

This matrix records architectural capabilities as they become observable, protected, certified, and documented during the post-MVP architecture program.

| Capability | Introduced | Protected | Certified | Reference Documentation | Status |
| --- | --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, future architectural regression framework | 0.1 local command-line baseline | `docs/architectural-mechanisms.md` | Certified locally with quarantines |
| Canonical contract Oracle | 0.2 | Repository dashboard golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, repository workspace golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, cross-pilot repeatability evidence, workflow projection field inventory, workflow fixture field classification, workflow instance golden fixture comparison, and procedural change workflow | Repository dashboard pilot certified locally as of Slice 0018; request-boundary extension verified in Slice 0019; repository workspace fixture verified in Slice 0020; repository workspace consumer verification verified in Slice 0021; repository workspace artifact freshness verified in Slice 0022; repository workspace request-boundary verification verified in Slice 0023; repository workspace pilot certified locally as of Slice 0024; cross-pilot repeatability checkpoint recorded in Slice 0025; workflow projection inventory recorded in Slice 0026; workflow fixture field classification recorded in Slice 0027; workflow instance fixture comparison verified in Slice 0028 | `docs/contracts.md` | Repository dashboard and repository workspace pilots are locally certified; Oracle lifecycle repeatability is evidenced at two-family pilot scope; workflow projection now has initial fixture comparison but not consumer/freshness/request-boundary verification or local certification; Milestone 0.2 remains active |

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
- workflow projection gated field inventory for `WorkflowInstance`,
- workflow fixture field classification for `WorkflowInstance`,
- workflow instance golden fixture and recursive backend serialization comparison,
- initial parallel truth inventory,
- fixture gating rule.

The Oracle is now locally certified for two pilot contracts: repository dashboard and repository workspace. Dashboard certification evidence is recorded in `.agents/milestones/m0.2-repository-dashboard-oracle-certification-slice-0018.md`; workspace certification evidence is recorded in `.agents/milestones/m0.2-repository-workspace-oracle-certification-slice-0024.md`. The workspace certification covers the complete current Oracle mechanism set for that pilot: fixture comparison, consumer verification, artifact freshness, primary GET request-boundary verification, the combined Oracle mechanism filter, and the full backend test project. Cross-pilot repeatability evidence is recorded in `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0025.md`; it shows that the dashboard and workspace pilots reused the same Oracle lifecycle without framework redesign. This does not certify Milestone 0.2 globally.

Consumer verification covers the Rust, TypeScript, and dev mock repository dashboard consumers and the Rust, TypeScript, and dev mock repository workspace response consumers. Freshness verification covers the repository dashboard and repository workspace TypeScript contract artifact as a Phase 0 verified manual artifact, and request-boundary verification covers the repository dashboard no-argument command/API path plus the repository workspace required repository-id GET path. Workflow projection now has field inventory, fixture field classification, and initial `WorkflowInstance` fixture comparison only; it has no workflow consumer verifier, freshness manifest, request-boundary verifier, populated `decisionSession` fixture variant, or local certification. The Oracle change workflow is procedural rather than automated. Milestone-level certification still requires broader golden serialized fixtures, expanded dependency graph coverage, deterministic generated artifacts, fixture update tooling, richer non-empty command/query/body verification, semantic reinterpretation checks, versioning rules, and workflow automation where needed.
