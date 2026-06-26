# Architectural Capabilities

This matrix records architectural capabilities as they become observable, protected, certified, and documented during the post-MVP architecture program.

| Capability | Introduced | Protected | Certified | Reference Documentation | Status |
| --- | --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, future architectural regression framework | 0.1 local command-line baseline | `docs/architectural-mechanisms.md` | Certified locally with quarantines |
| Canonical contract Oracle | 0.2 | Repository dashboard golden fixture comparison pilot | Pending | `docs/contracts.md` | Partially executable; uncertified |

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
- first explicit dashboard compatibility drift finding against a Rust shell mirror,
- initial parallel truth inventory,
- fixture gating rule.

The Oracle is now executable for one pilot contract only. Certification still requires broader golden serialized fixtures, expanded dependency graph coverage, generated or verified consumers, fixture update tooling, and an Oracle change workflow.
