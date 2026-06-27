# Architecture Decision Governance

Architectural decision governance defines how Command Center changes architectural authority, ownership, contracts, transport, runtime behavior, mechanisms, and durable baselines.

Governance is required when a change affects an architectural invariant, capability lifecycle, semantic authority, projection ownership, contract identity, transport responsibility, state ownership, controller or workspace boundary, runtime failure scope, compatibility obligation, regression strength, generated artifact exception, or reference architecture definition.

## Decision Roles

| Role | Responsibility | Required evidence |
| --- | --- | --- |
| Proposer | Identifies the architectural change, affected invariant, alternatives, compatibility impact, rollback path, and required documentation updates. | Decision record with evidence package references and affected consumer list. |
| Authority owner | Confirms the proposed owner is the correct semantic, projection, contract, transport, state, runtime, mechanism, or documentation authority. | Owner map, source evidence, and consumer impact analysis. |
| Mechanism owner | Confirms regressions, fixtures, generated artifacts, or review checks protect the new or changed invariant. | Mechanism evidence with command, protected scope, lifecycle state, and known limits. |
| Compatibility owner | Confirms affected consumers, transitional fields, routes, or mirrors have a migration and retirement path. | Compatibility evidence with consumer list, replacement path, retirement condition, and derivation proof where applicable. |
| Certifier | Confirms exit criteria, evidence, rollback readiness, and durable documentation alignment before acceptance. | Certification evidence package with command results, limits, and baseline updates. |

## Decision Classes

| Decision class | Applies when | Minimum evidence | Required regression or guard | Durable docs update |
| --- | --- | --- | --- | --- |
| New authority | A new semantic owner is introduced or an existing authority boundary changes. | Authority owner map, alternatives, downstream consumer list, and duplicate authority scan. | Authority source or duplicate-computation guard, at least advisory until the owning milestone strengthens it. | `docs/authority.md` or relevant reference document. |
| New projection | A derived read model, projection field, or projection owner is introduced. | Authority source, projection owner, invalidation rule, consumer list, and purity rationale. | Projection purity or fixture drift guard. | `docs/projections.md` and contract docs when externally observable. |
| Contract change | Externally observable request or response shape changes. | Oracle fixture diff, request-boundary analysis, consumer verification, compatibility analysis, and artifact freshness impact. | Contract Oracle fixture, consumer, request-boundary, or freshness guard. | `docs/contracts.md` and capability matrix if capability scope changes. |
| Compatibility exception | A temporary field, route, command, mirror, or behavior is retained for migration. | Owner, consumers, replacement path, derivation source, retirement condition, and rollback path. | Compatibility inventory or derivation guard. | Owning reference doc and retirement evidence. |
| Regression weakening | A guard loses scope, severity, freshness, lifecycle strength, command coverage, or failure quality. | Before/after protection, risk, affected invariant, compensating control, and restoration or replacement path. | Replacement guard or explicit quarantine metadata. | `docs/architectural-mechanisms.md` and capability matrix. |
| Generated artifact exception | Generated output is bypassed, hand-edited, stale, or temporarily replaced by manual output. | Reason, owner, affected artifact, consumer list, freshness impact, removal condition, and regeneration path. | Freshness or generated-header guard, or quarantined exception. | `docs/contracts.md` or relevant generation documentation. |
| Transport exception | Shell or transport layer interprets, mirrors, filters, rewrites, or classifies backend-shaped data. | Command family, shell-owned justification, status/error/null preservation analysis, and mirror retirement path. | Passive transport or shell classification guard. | `docs/transport.md` or shell transport classification. |
| State ownership change | Mutable state owner, synchronization rule, cache owner, or mutation owner changes. | State ownership matrix entry, synchronization graph, stale-response behavior, and affected controllers/workspaces. | State ownership or resource/action guard. | `docs/frontend-architecture.md`. |
| Controller/workspace boundary change | Feature orchestration, resource ownership, workspace composition, or root responsibility changes. | Boundary map, imports, owner responsibilities, view-model construction location, and failure scope. | Controller or workspace boundary guard. | `docs/frontend-architecture.md`. |
| Runtime failure scope change | Error envelope, absence semantics, partial data, streaming behavior, or recovery boundary changes. | Failure reproduction, scoped behavior, partial-data impact, telemetry, and recovery path. | Runtime isolation or error-envelope guard. | `docs/frontend-architecture.md` or `docs/architectural-mechanisms.md`. |
| Reference architecture change | Durable architecture definitions, capability lifecycle, or acceptance baseline changes. | Reason, affected reference docs, affected decisions, capability matrix impact, and rollback path. | Governance metadata guard and documentation alignment review. | Relevant `docs/` reference document. |

## Acceptance Levels

| Level | Meaning | Required proof |
| --- | --- | --- |
| Completion | Planned code, documentation, generated artifacts, tests, and evidence exist for the slice. | Evidence package names changed files, commands, results, and known limits. |
| Certification | The milestone or slice exit criteria are proven for the scoped architectural claim. | Certification evidence links decisions, mechanisms, verification output, compatibility impact, and rollback. |
| Acceptance | Downstream consumers still work, compatibility obligations are satisfied, rollback is ready, and blockers are resolved or quarantined. | Acceptance evidence names consumers, migration obligations, deferrals, and revalidation triggers. |
| Baseline update | Durable docs, capability matrix, decision records, and verifier commands reflect the accepted state. | Reference docs and capability matrix contain the accepted scope and limits. |

Implementation alone cannot accept an architectural change. Acceptance requires evidence and baseline alignment.

## Approval Rules

- A new authority is approved only when no existing authority owns the semantic concept and the proposed authority can expose projections, tests, recovery guidance, and regressions.
- A new projection is approved only when it has a named authority source, owner, invalidation rule, consumer set, and purity regression or scheduled guard.
- A contract change is approved only after Oracle drift, request boundary, consumer compatibility, artifact freshness, and rollback evidence are reviewed.
- A compatibility exception is approved only with owner, consumers, replacement path, retirement condition, and a guard proving it remains transitional.
- A regression may be weakened, quarantined, replaced, or retired only through an explicit decision record and mechanism evidence.
- A generated artifact exception is approved only as a quarantined exception with a removal condition or blocking condition.
- A transport exception is approved only when the shell-owned responsibility is explicit or a retirement path exists for the mirror.
- A UI-local semantic computation is approved only for non-authoritative preview behavior that is labeled disposable and non-persistent.

## Mechanism Lifecycle Governance

| Lifecycle change | Approval requirement | Evidence requirement |
| --- | --- | --- |
| Add mechanism | Decision required when the mechanism protects a new invariant or changes certification scope. | Protected invariant, owner, severity, command/source, lifecycle state, and known limits. |
| Strengthen mechanism | Decision required when certification or baseline claims expand. | Before/after scope, new command results, failure behavior, and affected consumers. |
| Weaken mechanism | Decision always required for guarded or stronger mechanisms. | Risk, affected invariant, compensating control, rollback, and restoration or replacement path. |
| Quarantine mechanism | Decision required when certified, accepted, compatibility-related, or release-blocking protection is affected. | Owner, reason, affected invariant, consumers, risk, retirement condition, and revalidation command. |
| Replace mechanism | Decision required unless the replacement is mechanically equivalent in scope, severity, owner, command, and failure behavior. | Old/new scope map, confidence comparison, command results, compatibility impact, and rollback. |
| Retire mechanism | Decision required for guarded or stronger mechanisms. | Reason, duplicate or obsolete protection proof, docs updates, final verification, and rollback. |

## Emergency Exceptions

Emergency exceptions are allowed only for release-blocking issues where the normal governance sequence would prolong an outage, data loss, or blocked release.

| Requirement | Rule |
| --- | --- |
| Maximum duration | The exception must name an expiration date or blocking condition. |
| Owner | One owner must be accountable for removing or certifying the exception. |
| Compensating regression | A temporary guard, source scan, manual review check, or runtime monitor must be named. |
| Follow-up certification | The next slice must either retire the exception or produce certification evidence for the permanent path. |
| Scope | The exception cannot redefine the architecture; it only permits a temporary implementation variance. |

## Rollback Policy

Rollback is required when verification becomes nondeterministic, generated artifacts cannot be reproduced, a consumer silently loses data, runtime errors cross the intended boundary, semantic authority moves downstream, compatibility consumers lack a migration path, or implementation requires redefining an invariant outside governance.

Rollback evidence must include the trigger, prior verified behavior, files and consumers affected, compatibility layer status, command results, decision record, and the narrower next-attempt plan.

Preferred rollback order:

1. Revert the slice before acceptance.
2. Re-enable a documented compatibility layer.
3. Disable the new generated consumer and restore the verified manual consumer.
4. Quarantine the failing mechanism with owner, reason, affected invariant, and retirement criteria.
5. Split the work into smaller inventory, mechanism, migration, removal, and certification slices.

## Baseline Update Policy

An accepted decision must update every durable artifact that would otherwise tell a future reader a different story:

- relevant `docs/` reference documents,
- `docs/architectural-capabilities.md`,
- `docs/architectural-mechanisms.md` when mechanism lifecycle changes,
- contract fixtures or generated artifact manifests when observable shape changes,
- milestone evidence under `.agents/milestones/`,
- active decision records under `.agents/decisions/`.

Superseded decisions remain traceable. They are rotated or marked superseded; they are not silently deleted.
