# Architectural Capabilities

This matrix records architectural capabilities as they become observable, protected, certified, and documented during the post-MVP architecture program.

| Capability | Introduced | Protected | Certified | Reference Documentation | Status |
| --- | --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, future architectural regression framework | 0.1 local command-line baseline | `docs/architectural-mechanisms.md` | Certified locally with quarantines |
| Canonical contract Oracle | 0.2 | Repository dashboard golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, repository workspace golden fixture comparison, repository workspace consumer verification, and procedural change workflow | Repository dashboard pilot certified locally as of Slice 0018; request-boundary extension verified in Slice 0019; repository workspace fixture verified in Slice 0020; repository workspace consumer verification verified in Slice 0021 | `docs/contracts.md` | Second fixture family has fixture and consumer coverage; Milestone 0.2 remains active |

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
- distinct freshness failure modes for stale artifacts, unexpected manual artifact modification, and missing expected artifacts,
- procedural Oracle change workflow for drift classification, fixture update, consumer/artifact refresh, evidence, and rollback,
- initial parallel truth inventory,
- fixture gating rule.

The Oracle is now locally certified for one pilot contract only: repository dashboard. Certification evidence is recorded in `.agents/milestones/m0.2-repository-dashboard-oracle-certification-slice-0018.md` and covers targeted Oracle tests plus the full backend test project. Slice 0019 extends the pilot with no-argument request-boundary verification but does not recertify the full pilot or Milestone 0.2 globally. Slices 0020 and 0021 start the second contract family by adding a repository workspace field catalog, golden fixture comparison, and consumer verification; they do not yet certify repository workspace freshness, request boundaries, local pilot completeness, or Milestone 0.2 globally.

Consumer verification covers the Rust, TypeScript, and dev mock repository dashboard consumers and the Rust, TypeScript, and dev mock repository workspace response consumers. Freshness verification covers only the repository dashboard TypeScript contract artifact as a Phase 0 verified manual artifact, and request-boundary verification covers only the repository dashboard no-argument command/API path. The Oracle change workflow is procedural rather than automated. Milestone-level certification still requires broader golden serialized fixtures, expanded dependency graph coverage, deterministic generated artifacts, fixture update tooling, non-empty command argument/body verification, semantic reinterpretation checks, versioning rules, and workflow automation where needed.
