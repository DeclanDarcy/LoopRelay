# Architectural Evidence

Architectural evidence is the recorded proof used to authorize decisions, install mechanisms, certify milestones, accept baselines, and roll back unsafe migrations.

Evidence must be concrete enough for a future slice to understand the architectural claim without relying on memory or transient command output.

## Evidence Package Schema

Every evidence package must include the following fields.

| Field | Required content | Used by |
| --- | --- | --- |
| Evidence id | Stable name, usually matching the milestone slice or certification artifact. | Traceability and supersession. |
| Capability | Capability affected, such as Contract Oracle, passive transport, state ownership, or decision governance. | Capability matrix and certification review. |
| Invariant | Architectural invariant being observed, protected, weakened, certified, accepted, or rolled back. | Regression framework and decision records. |
| Slice or milestone | Implementation slice, milestone, or certification boundary. | Milestone evidence trail. |
| Decision records | Linked decision records that authorize the work or exception. | Governance audit. |
| Files and modules observed | Source files, docs, fixtures, generated artifacts, commands, routes, or consumers inspected. | Reproduction and compatibility review. |
| Commands run | Exact verifier commands, working directories, and relevant filters. | Certification and rollback. |
| Results | Pass/fail counts, fixture diffs, generated artifact diffs, runtime observations, or review outcome. | Acceptance and mechanism lifecycle. |
| Consumers affected | Runtime, compile-time, development/test, shell, generated, or documentation consumers affected. | Compatibility governance. |
| Known limits | Gaps, deferrals, quarantines, unsupported paths, and confidence limits. | Scoped claims. |
| Rollback path | How to restore the prior verified behavior or compatibility path. | Migration safety. |
| Retention location | Durable file path for the evidence package and referenced artifacts. | Long-term audit. |
| Reviewer or certifier | Person or agent role that reviewed the evidence. | Accountability. |

## Evidence Types

| Evidence type | Required proof | Typical location |
| --- | --- | --- |
| Inventory evidence | Owners, consumers, duplicates, gaps, uncertainty, and next strengthening path. | `.agents/milestones/`, `docs/` inventory tables. |
| Contract evidence | Serialized fixtures, request boundary analysis, fixture diffs, consumer verification, artifact freshness, and compatibility analysis. | `tests/`, `docs/contracts.md`, `.agents/milestones/`. |
| Authority evidence | Semantic owner map, duplicate computation scan, projection field ownership, and downstream leakage review. | `docs/authority.md`, milestone evidence. |
| Projection evidence | Authority source, projection owner, invalidation rule, purity constraints, and consumer set. | `docs/projections.md`, tests, fixtures. |
| Transport evidence | Command classification, status/error/null/empty preservation, unknown-field preservation, and mirror inventory. | `docs/shell-transport-classification.md`, shell tests. |
| State evidence | State ownership matrix, synchronization graph, mutation owner, stale-response behavior, and cache owner. | `docs/frontend-architecture.md`, frontend tests. |
| Runtime evidence | Failure reproduction, error scope, partial-data behavior, telemetry, streaming behavior, and recovery path. | Backend/frontend tests, E2E evidence, mechanism docs. |
| Mechanism evidence | Guard command/source, protected invariant, owner, severity, lifecycle state, failure UX, limits, and retirement criteria. | `docs/architectural-mechanisms.md`, tests, milestone evidence. |
| Compatibility evidence | Consumers, replacement path, retirement condition, derivation proof, migration status, and rollback path. | Decision records, contract docs, milestone evidence. |
| Certification evidence | Exit criteria mapping, commands, results, decisions, compatibility obligations, known limits, and durable docs updates. | `.agents/milestones/` certification artifacts. |
| Acceptance evidence | Downstream validation, rollback readiness, accepted deferrals, baseline docs, and revalidation triggers. | `.agents/milestones/`, capability matrix. |
| Rollback evidence | Trigger, restored behavior, affected files/consumers, compatibility layer, verification, and next attempt. | Decision records and rollback slice evidence. |

## Evidence Requirements by Decision Class

| Decision class | Required evidence |
| --- | --- |
| New authority | Authority inventory, owner justification, duplicate authority scan, consumer list, regression plan, rollback path. |
| New projection | Projection owner, authority source, invalidation rule, consumer list, projection purity evidence, contract impact. |
| Contract change | Golden fixture diff, request-boundary review, consumer verification, artifact freshness impact, compatibility review, rollback path. |
| Compatibility exception | Owner, consumers, derivation source, replacement path, retirement condition, compensating guard, rollback path. |
| Regression weakening | Before/after mechanism scope, affected invariant, risk, compensating control, restoration or replacement path. |
| Generated artifact exception | Affected artifact, reason, owner, consumers, freshness impact, removal condition, regeneration path. |
| Transport exception | Command classification, shell-owned justification or retirement path, unknown-field/null/error/status preservation evidence. |
| State ownership change | State ownership matrix entry, synchronization graph, mutation owner, stale-response behavior, affected controller/workspace. |
| Controller/workspace boundary change | Boundary map, import/owner review, action/resource ownership, failure scope, root composition impact. |
| Runtime failure scope change | Failure reproduction, scoped behavior, partial-data impact, telemetry or diagnostics, recovery and rollback path. |
| Reference architecture change | Affected reference docs, capability matrix impact, superseded decisions, mechanism impact, rollback path. |

## Retention Rules

- Active milestone evidence belongs under `.agents/milestones/`.
- Active decision records and templates belong under `.agents/decisions/`.
- Durable evidence standards belong in this document.
- Durable architecture definitions belong in the relevant `docs/` reference document, not in milestone evidence.
- Generated outputs and transient command output may be referenced, but the evidence package must summarize the result.
- Superseded evidence remains traceable through rotated files or explicit supersession notes.

## Traceability Rules

Evidence must trace in both directions:

- a decision record links to evidence packages,
- an evidence package links to the decision records it supports,
- a certification artifact maps exit criteria to evidence,
- the capability matrix records the accepted capability status,
- durable docs contain the architecture definition after acceptance.

If one side of the trace is missing, the architecture change is incomplete even if tests pass.

## Certification Standards

Certification evidence must state the exact architectural claim and its limits. It must not imply broader enforcement than the commands, fixtures, source scans, runtime observations, or consumer validation prove.

A certification package is valid only when it includes:

- scoped claim,
- exit criteria mapping,
- decision records,
- mechanisms used,
- commands and results,
- compatibility obligations,
- rollback readiness,
- known limits,
- durable docs and capability matrix updates.

## Acceptance Standards

Acceptance evidence must prove that downstream consumers still work or that every affected consumer has an authorized compatibility path.

Acceptance is blocked when a consumer silently loses data, a compatibility field lacks a retirement condition, rollback cannot restore prior verified behavior, durable docs disagree with the implemented baseline, or evidence cannot be reproduced.
