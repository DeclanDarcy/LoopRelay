# Post-MVP Architectural Correctness Implementation Plan

## Goal

Transform Command Center from an application whose architecture is correct by convention into one whose architecture is correct by construction.

The target architecture is:

```text
Domain authority
  -> projection
  -> canonical contract
  -> passive transport
  -> resource
  -> feature controller
  -> workspace
  -> presentation
```

Every milestone must reduce architectural entropy. A change is valuable only when it makes an invariant observable, verifiable, enforceable, recoverable, or simpler to maintain.

The work is complete when:

- contracts cannot silently drift,
- semantic authority cannot silently move downstream,
- state ownership cannot silently duplicate,
- the shell cannot silently become a contract or semantic authority,
- projections have one architectural meaning,
- feature and workspace composition are explicit,
- runtime failures are scoped and typed,
- every architectural invariant has a protecting mechanism,
- and the implementation, runtime, tests, and architecture documentation agree.

## Architecture vs Program Boundary

This document is an implementation program: it describes how Command Center evolves. It must not become the permanent definition of the architecture.

Durable architecture definitions belong in `docs/` and must be extracted as soon as they stabilize. The plan may name target capabilities and order the work, but permanent definitions for concepts such as Oracle, Authority, Projection, Contract, Transport, Resource, Controller, Workspace, Runtime, Mechanism, Certification, and Reference Architecture must live in reference documentation.

Rules:

- If a milestone defines a durable architectural concept, create or update a corresponding `docs/` reference document before certification.
- If the plan and durable documentation disagree, implementation stops until the disagreement is resolved through architectural decision governance.
- Program edits may reorder, split, defer, or retire work. They may not silently redefine architectural invariants.
- Milestone evidence under `.agents/` records what happened. It does not replace durable architecture documentation.

Initial reference document targets:

- `docs/reference-architecture.md`: canonical layer model, ownership chain, and reusable architecture overview.
- `docs/architecture-decision-governance.md`: decision criteria, evidence requirements, exception handling, and rollback rules.
- `docs/architectural-evidence.md`: evidence types, evidence packages, retention, traceability, and certification standards.
- `docs/architectural-capabilities.md`: capability matrix and lifecycle status.
- `docs/contracts.md`: Oracle, contract identity, serialization, generation, compatibility, and versioning.
- `docs/authority.md`: authority taxonomy, semantic ownership, role purity, and decision boundaries.
- `docs/projections.md`: projection taxonomy, purity rules, ownership, lifecycle, and naming.
- `docs/transport.md`: passive transport model, shell responsibility, errors, status, null/empty semantics, and streaming.
- `docs/frontend-architecture.md`: resources, actions, controllers, workspaces, presentation taxonomy, and runtime isolation.
- `docs/architectural-mechanisms.md`: invariant lifecycle, drift detection, regressions, recovery, governance, and confidence.

## Current Codebase Anchors

The implementation must fit the current repository structure:

- `src/CommandCenter.Core`: repository identity, artifact storage, configuration, planning, common projections.
- `src/CommandCenter.Continuity`: operational-context parsing, lifecycle, compression, semantic diff, diagnostics, and reports.
- `src/CommandCenter.DecisionSessions`: governance session registry, lifecycle, transfer, recovery, metrics, economics, coherence, and certification.
- `src/CommandCenter.Decisions`: decision discovery, candidates, proposals, review, refinement, resolution, governance, quality, execution influence, and certification.
- `src/CommandCenter.Execution`: execution context, prompt building, provider execution, monitoring, handoff, recovery, Git status, commit, and push.
- `src/CommandCenter.Workflow`: workflow state machine, projection, gates, execution, handoff, decisions, operational context, Git, continuation, preparation, recovery, health, reports, and certification.
- `src/CommandCenter.Reasoning`: reasoning events, threads, relationships, graph, traces, query, reconstruction, materialization review, and certification.
- `src/CommandCenter.Middle`: repository dashboard/workspace projections and operational-context generation composition.
- `src/CommandCenter.Backend`: minimal API endpoint mapping and dependency composition.
- `src/CommandCenter.Shell`: Tauri sidecar lifecycle and IPC/HTTP bridge.
- `src/CommandCenter.UI`: React app, manual TypeScript contracts, API clients, hooks, feature surfaces, shell state, presentation utilities, and characterization tests.
- `tests/CommandCenter.Backend.Tests`: xUnit backend service, persistence, endpoint, projection, regression, and certification tests.

Important current implementation pressure points:

- `src/CommandCenter.Shell/src/main.rs` contains many Rust structs that mirror backend projection and response shapes. These are contract drift risks and must be retired or quarantined.
- `src/CommandCenter.UI/src/types/*` contains hand-maintained TypeScript representations of backend contracts. These must become generated or mechanically verified.
- `src/CommandCenter.UI/src/devTauriMock.ts` is a large manually maintained mock surface. It must become generated or contract-verified.
- `src/CommandCenter.UI/src/App.tsx` owns many feature hooks, mutation flags, draft states, eligibility decisions, and refresh chains. The root must shrink to repository selection, global shell state, primary navigation, and workspace composition.
- `src/CommandCenter.UI/src/hooks/*` repeats loading, error, stale-response, mutation, and refresh mechanics. These mechanics need shared resource/action primitives without introducing a global semantic authority.
- `src/CommandCenter.UI/src/lib/status.ts`, `src/CommandCenter.UI/src/lib/navigation.ts`, and `src/CommandCenter.UI/src/lib/explainability/*` are presentation-adjacent surfaces that must be audited for semantic inference.
- Backend endpoint files under `src/CommandCenter.Backend/Endpoints/*` are the HTTP boundary. Endpoint tests must pin response shape, status/error behavior, and serialization rules before consumers are regenerated.
- `docs/architecture.md` is the durable architecture documentation entry point. New permanent architecture guidance should land under `docs/`; milestone evidence may land under `.agents/milestones/` or `.agents/certification/`.

## Verification Baseline

Use these commands as the default local verification set:

```powershell
dotnet build CommandCenter.slnx
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj
Set-Location src/CommandCenter.UI; npm run build
Set-Location src/CommandCenter.UI; npm run lint
Set-Location src/CommandCenter.UI; npm run test
Set-Location src/CommandCenter.UI; npm run test:e2e
Set-Location src/CommandCenter.Shell; cargo build
Set-Location src/CommandCenter.Shell; cargo test
```

Run the smallest relevant subset while developing. Run the full set at phase boundaries and before certification. If a command is broken at the start of Phase 0, repair or explicitly quarantine it before architectural refactoring begins.

## Delivery Rules

- Work in milestone order unless a small preparatory change is required to make the current milestone verifiable.
- A milestone may produce code, tests, generated artifacts, durable documentation, and milestone evidence.
- A milestone is not complete until its exit criteria are satisfied and verification failures are either fixed or explicitly quarantined with owner, reason, and retirement criteria.
- Do not move semantic logic while inventorying it. Inventory milestones map reality; restoration milestones change it.
- Do not remove debt until the replacement mechanism is implemented, verified, regression-protected, and adopted by consumers.
- Do not add UI inference when a backend semantic field is missing. Add the semantic field to the owning backend projection.
- Do not make the shell deserialize domain-shaped responses into Rust mirrors unless the command is explicitly shell-owned or a temporary compatibility exception.
- Generated artifacts are replaced wholesale. Manual edits to generated output are forbidden.
- Compatibility fields are transitional. They must derive from structured authority fields and have a documented retirement path.
- Evidence files must not be required to understand this plan; they are implementation records, not plan dependencies.

## Architectural Decision Governance

Architectural decisions are first-class work products. Any change that alters authority, projection ownership, contract identity, transport responsibility, state ownership, controller/workspace boundaries, presentation taxonomy, runtime failure scope, regression severity, compatibility duration, or mechanism strength requires an architectural decision record.

Decision records must answer:

- What invariant or capability is changing?
- What evidence justifies the change?
- Which evidence package contains the supporting proof?
- Which authority owns the decision?
- Which consumers are affected?
- Which compatibility path is required?
- Which regressions protect the new decision?
- Which rollback path exists if evidence proves the decision wrong?
- Which durable architecture document must change?

Decision approval rules:

- A new authority is allowed only when no existing authority owns the semantic concept and the new owner can expose projections, tests, recovery guidance, and regressions.
- A new projection is allowed only when it is a derived read model with a named authority source, owner, invalidation rule, consumer set, and purity regression.
- A compatibility field or route may persist only with owner, consumer list, replacement path, retirement condition, and regression proving it derives from authoritative structure.
- A regression may be weakened only by an explicit decision record that names the invariant, risk, replacement mechanism, and acceptance evidence.
- A generated artifact may be bypassed only as a quarantined exception with a removal date or a blocking condition.
- A UI-local semantic computation is allowed only for explicitly non-authoritative preview behavior and must be labeled as preview, disposable, and non-persistent.

Decision storage:

- Durable decisions belong under `.agents/decisions/` while implementation is active.
- Architecture-defining decisions must also be summarized in the relevant `docs/` reference document before acceptance.
- Superseded decisions remain traceable; they are not silently deleted.

## Architectural Evidence

Evidence is a first-class architectural input. Decisions, mechanisms, certifications, acceptance, rollback, and publication must be grounded in evidence packages rather than assertion.

Evidence flow:

```text
Evidence
  -> decision
  -> mechanism
  -> certification
  -> baseline
```

Evidence packages must identify:

- evidence id,
- capability and invariant,
- milestone or slice,
- decision records supported,
- files/modules observed,
- commands run,
- test or regression results,
- generated artifact or fixture diffs,
- runtime observations when applicable,
- compatibility consumers affected,
- known limits,
- reviewer or certifier,
- retention location.

Evidence types:

- Inventory evidence: lists artifacts, owners, consumers, duplicates, gaps, and uncertainty.
- Contract evidence: serialized fixtures, generated artifact diffs, compatibility analysis, and stale-generation checks.
- Authority evidence: semantic owner maps, duplicate computation scans, restored projection fields, and downstream leakage regressions.
- Transport evidence: command classification, unknown-field preservation, null/empty preservation, error-envelope preservation, and mirror allowlists.
- State evidence: ownership matrix entries, synchronization graphs, cache ownership, mutation ownership, and stale-response behavior.
- Runtime evidence: failure reproduction, failure scope, partial data behavior, event-stream behavior, telemetry, and performance measurements.
- Mechanism evidence: regression behavior, failure message quality, drift detection, recovery path, and mechanism lifecycle status.
- Acceptance evidence: downstream consumer validation, compatibility obligations, rollback readiness, and baseline updates.

Evidence storage rules:

- Active milestone evidence belongs under `.agents/milestones/` or `.agents/certification/`.
- Durable evidence standards belong in `docs/architectural-evidence.md`.
- Evidence packages may reference generated outputs and test artifacts, but the durable baseline must summarize the evidence so future readers do not need transient build output.
- Certification without evidence is incomplete, even if tests pass.

## Rollback and Stop Rules

Large architectural migrations must have explicit rollback rules before implementation starts.

Stop a migration slice when:

- verification becomes non-deterministic,
- generated artifacts cannot be reproduced,
- a consumer silently loses data,
- runtime errors cross the intended boundary,
- semantic authority moves downstream,
- compatibility consumers are discovered without a migration path,
- or implementation requires redefining an invariant outside governance.

Rollback options, in preferred order:

- Revert the slice before it is accepted.
- Re-enable a documented compatibility layer.
- Disable the new generated consumer and restore the verified manual consumer.
- Quarantine the failing mechanism with owner, reason, affected invariant, and retirement criteria.
- Split the slice into smaller inventory, mechanism, migration, and removal slices.

Rollback is complete only when:

- the prior verified behavior is restored,
- any temporary compatibility path is regression-protected,
- the decision record explains why rollback happened,
- and the next attempt has a narrower slice or stronger evidence.

## Acceptance Hierarchy

Every milestone and implementation slice must pass through four levels:

1. Completion: planned code, tests, generated artifacts, docs, and evidence exist.
2. Certification: the milestone's own exit criteria and regression evidence prove the architectural claim.
3. Acceptance: downstream consumers still work, compatibility obligations are satisfied, rollback rules are documented, and no unresolved blocker remains.
4. Baseline Update: durable `docs/` reference material, capability matrix, decision records, and verification commands reflect the accepted state.

Do not treat "tests pass" as acceptance. Passing tests are required for certification, but acceptance also requires compatibility, documentation, rollback, and baseline updates.

## Architectural Capability Matrix

Maintain `docs/architectural-capabilities.md` as a living matrix. Each capability row must identify where the capability is introduced, protected, certified, and documented.

Initial matrix:

| Capability | Introduced | Protected | Certified | Reference Documentation |
| --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, 0.3 | 9 | `docs/architectural-mechanisms.md` |
| Canonical contract Oracle | 0.2 | 0.3, 1.2, 6 | 9 | `docs/contracts.md` |
| Architectural regression framework | 0.3 | 6 | 9 | `docs/architectural-mechanisms.md` |
| Architectural decision governance | 0.4 | 0.4, 6 | 9 | `docs/architecture-decision-governance.md` |
| Architectural evidence | 0.4 | 0.4, 6, 9 | 9 | `docs/architectural-evidence.md` |
| Canonical contract model | 1.1 | 1.2, 6 | 9 | `docs/contracts.md` |
| Generated contract ecosystem | 1.2 | 1.2, 6 | 9 | `docs/contracts.md` |
| Passive transport | 1.3 | 1.3, 6 | 9 | `docs/transport.md` |
| Semantic authority inventory | 2.1 | 2.2, 6 | 9 | `docs/authority.md` |
| Semantic authority restoration | 2.2 | 2.2, 6 | 9 | `docs/authority.md` |
| Presentation normalization | 2.3 | 2.3, 5.3, 6 | 9 | `docs/frontend-architecture.md` |
| State ownership | 3.1 | 3.2, 3.3, 6 | 9 | `docs/frontend-architecture.md` |
| Feature ownership | 3.2 | 3.2, 4.1, 6 | 9 | `docs/frontend-architecture.md` |
| Shared resource/action framework | 3.3 | 3.3, 6 | 9 | `docs/frontend-architecture.md` |
| Controller architecture | 4.1 | 4.1, 6 | 9 | `docs/frontend-architecture.md` |
| Workspace isolation | 4.2 | 4.2, 6 | 9 | `docs/frontend-architecture.md` |
| Runtime isolation | 4.3 | 4.3, 8, 6 | 9 | `docs/frontend-architecture.md` |
| Projection taxonomy | 5.1 | 5.1, 6 | 9 | `docs/projections.md` |
| Authority taxonomy | 5.2 | 5.2, 6 | 9 | `docs/authority.md` |
| Presentation taxonomy | 5.3 | 5.3, 6 | 9 | `docs/frontend-architecture.md` |
| Architectural mechanisms | 6 | 6 | 9 | `docs/architectural-mechanisms.md` |
| Structural debt elimination | 7 | 7, 6 | 9 | `docs/reference-architecture.md` |
| Runtime observability and performance architecture | 8 | 8, 6 | 9 | `docs/architectural-mechanisms.md` |
| Reference architecture baseline | 9 | 9, 10 | 10 | `docs/reference-architecture.md` |
| Reference architecture publication | 10 | 10 | 10 | Published reference package |

## Implementation Slicing Model

Milestones define architectural capabilities. Implementation happens through slices. A slice must be small enough to complete, certify, accept, and roll back independently.

Each slice must include:

- slice objective,
- owning capability,
- affected files/modules,
- authority and state ownership impact,
- compatibility impact,
- rollback path,
- verification commands,
- durable documentation updates if the slice changes architecture.

Default slice sequence:

1. Inventory slice.
2. Mechanism or contract slice.
3. Pilot migration slice.
4. Consumer migration slice.
5. Regression hardening slice.
6. Compatibility retirement slice.
7. Certification and baseline slice.

Large milestone slice examples:

| Milestone | Suggested slices |
| --- | --- |
| 1.2 Generated contract ecosystem | IR/schema pilot; TypeScript generation pilot; generated mock/fixture pilot; stale artifact verification; low-risk read-model migration; mutation-result migration; shell metadata verification; manual type retirement |
| 1.3 Passive transport | command classification; shared forwarding helpers; error/null/status preservation; low-risk read command migration; write command migration; push/execution special-case resolution; streaming passivity; mirror removal |
| 2.2 Authority restoration | eligibility fields; severity/diagnostic fields; recovery/retry fields; certification/health fields; explainability adapters; continuity interpretation; Git/execution semantics; regression hardening |
| 3.2 Feature ownership | repository selection/dashboard; artifact workspace; operational context; continuity; execution and Git; decisions; governance; reasoning; workflow; root cleanup |
| 3.3 Shared resources/actions | primitive definition; repository read hooks; selected-repository resources; action primitive; execution actions; decision actions; reasoning resources; workflow resources; raw `setData` retirement |
| 4.1 Controller architecture | repository controller; artifact controller; execution controller; decision controller; governance controller; reasoning controller; workflow controller; controller communication; root cleanup |
| 4.2 Workspace isolation | workspace shell boundary; repository workspace; execution workspace; decisions workspace; governance workspace; reasoning workspace; continuity/operational-context workspace; navigation isolation |
| 4.3 Runtime isolation | backend error envelope; transport error preservation; resource failure model; workspace error boundaries; partial data model; absence semantics; streaming resilience; corrupt repository degradation |
| 7 Structural debt elimination | contract debt; shell mirror debt; authority debt; projection debt; resource debt; presentation debt; dead endpoints/services; docs cleanup |

## Phase 0 Execution Guardrails

Phase 0 intentionally installs the operating model before architectural transformation. Because it includes verification, Oracle, regression, and governance, it must be managed as a sequence of small foundation slices rather than one large prelude.

Rules:

- Phase 0 work must not migrate production architecture except where required to make verification truthful.
- Each Phase 0 milestone must produce at least one executable mechanism or durable governance artifact.
- Do not block all later work on perfect coverage; block later work only on gaps that would make the next milestone unsafe.
- Quarantine unknown or broken verifiers with owner, reason, risk, and retirement criteria rather than expanding Phase 0 indefinitely.
- Phase 0 is complete when later milestones can safely observe contract drift, authority drift, regression drift, and ungoverned decisions.

Phase 0 slice order:

1. Verification inventory and immediate repair.
2. Contract surface inventory and fixture pilot.
3. Regression framework skeleton and first drift checks.
4. Decision governance, evidence model, and rollback policy.
5. Phase 0 certification and baseline documentation.

## Core Invariants

- Backend domain services compute semantic meaning.
- Projections expose authoritative meaning; they do not create new meaning.
- Contracts describe externally observable projection shape and evolve through a single canonical authority.
- Transport preserves request, response, status, null/empty, and error semantics without becoming a domain participant.
- TypeScript clients, hooks, resources, controllers, and React components consume and render authoritative facts.
- React may map typed semantic enums to colors, icons, labels, grouping, and accessibility text; it may not infer eligibility, severity, health, retryability, recovery, certification, recommendation rank, or lifecycle legality from weak strings.
- Every mutable state has one owner.
- Feature controllers own feature resources, actions, refresh, loading, error, and view model construction.
- Workspaces compose controllers and local interaction flow; the application root composes workspaces.
- Runtime failures are typed, scoped, observable, and recoverable at the smallest valid boundary.
- Architecture rules are enforced by executable mechanisms, not by documentation alone.

## Milestone 0.1: Restore Structural Verification

(See ./milestones/m0.1-structural-verification.md)

## Milestone 0.2: Establish the Canonical Contract Oracle

(See ./milestones/m0.2-contract-oracle.md)

## Milestone 0.3: Install the Architectural Regression Framework

(See ./milestones/m0.3-regression-framework.md)

## Milestone 0.4: Establish Architectural Decision Governance

(See ./milestones/m0.4-decision-governance.md)

## Milestone 1.1: Define the Canonical Contract Model

(See ./milestones/m1.1-canonical-contract-model.md)

## Milestone 1.2: Build the Generated Contract Ecosystem

(See ./milestones/m1.2-generated-contracts.md)

## Milestone 1.3: Make Transport Passive

(See ./milestones/m1.3-passive-transport.md)

## Milestone 2.1: Inventory Semantic Authority

(See ./milestones/m2.1-semantic-authority-inventory.md)

## Milestone 2.2: Restore Semantic Authority

(See ./milestones/m2.2-semantic-authority-restoration.md)

## Milestone 2.3: Normalize Presentation

(See ./milestones/m2.3-presentation-normalization.md)

## Milestone 3.1: Establish the State Ownership Matrix

(See ./milestones/m3.1-state-ownership.md)

## Milestone 3.2: Restore Feature Ownership

(See ./milestones/m3.2-feature-ownership.md)

## Milestone 3.3: Create the Shared Resource and Action Framework

(See ./milestones/m3.3-resources-actions.md)

## Milestone 4.1: Establish Controller Architecture

(See ./milestones/m4.1-controllers.md)

## Milestone 4.2: Isolate Workspaces

(See ./milestones/m4.2-workspaces.md)

## Milestone 4.3: Establish Runtime Isolation

(See ./milestones/m4.3-runtime-isolation.md)

## Milestone 5.1: Normalize Projection Taxonomy

(See ./milestones/m5.1-projection-taxonomy.md)

## Milestone 5.2: Normalize Authority Taxonomy

(See ./milestones/m5.2-authority-taxonomy.md)

## Milestone 5.3: Normalize Presentation Taxonomy

(See ./milestones/m5.3-presentation-taxonomy.md)

## Milestone 6: Install Architectural Mechanisms

(See ./milestones/m6-architectural-mechanisms.md)

## Milestone 7: Eliminate Structural Debt

(See ./milestones/m7-structural-debt.md)

## Milestone 8: Validate Runtime Architecture and Performance

(See ./milestones/m8-runtime-performance.md)

## Milestone 9: Certify and Baseline the Architecture

(See ./milestones/m9-architecture-baseline.md)

## Milestone 10: Publish the Reference Architecture

(See ./milestones/m10-reference-architecture-publication.md)

## Cross-Milestone Mechanism Matrix

Mechanisms are reused across milestones. Maintain this matrix in `docs/architectural-mechanisms.md` and update it whenever a mechanism is introduced, strengthened, weakened, retired, or replaced.

| Mechanism | Introduced | Used By | Protects |
| --- | --- | --- | --- |
| Verification baseline | 0.1 | all milestones | build, compiler, test, and CI trustworthiness |
| Contract Oracle | 0.2 | 1.1, 1.2, 1.3, 7, 9, 10 | contract authority and serialization drift |
| Golden contract fixtures | 0.2 | 1.2, 1.3, 7, 9 | response shape, compatibility, generated consumers |
| Architectural regression framework | 0.3 | all architecture-changing milestones | invariant drift and failure quality |
| Decision governance | 0.4 | all milestones | ungoverned architecture change |
| Architectural evidence packages | 0.4 | all certifications and acceptances | decisions, mechanisms, certification, traceability |
| Rollback policy | 0.4 | migration and removal slices | reversible migrations and compatibility fallback |
| Capability matrix | 0.4 | all milestones, 9, 10 | capability lifecycle visibility |
| Generated contract toolchain | 1.2 | 1.3, 2.x, 3.x, 4.x, 7, 9 | manual contract drift |
| Transport passivity helpers | 1.3 | 2.x, 4.3, 7, 8, 9 | shell semantic drift and error/status loss |
| Authority regression | 2.2 | 2.3, 5.x, 6, 7, 9 | downstream semantic computation |
| Presentation vocabulary | 2.3 | 4.x, 5.3, 7, 9 | duplicate or inferential presentation mappings |
| State ownership matrix | 3.1 | 3.2, 3.3, 4.x, 7, 9 | duplicate mutable state |
| Resource/action framework | 3.3 | 4.x, 4.3, 8, 9 | duplicated loading, mutation, stale-response, refresh mechanics |
| Controller boundaries | 4.1 | 4.2, 4.3, 7, 8, 9 | root orchestration creep and component-owned behavior |
| Workspace boundaries | 4.2 | 4.3, 8, 9 | cross-workspace coupling and unscoped composition |
| Runtime isolation boundaries | 4.3 | 8, 9 | failure amplification, partial data loss, untyped absence |
| Projection taxonomy | 5.1 | 5.2, 6, 7, 9 | projection/authority/renderer role confusion |
| Authority taxonomy | 5.2 | 6, 7, 9 | mixed semantic responsibilities and ambiguous naming |
| Presentation taxonomy | 5.3 | 6, 7, 9 | presentation role drift and UI responsibility mixing |
| Mechanism lifecycle model | 6 | 7, 8, 9, 10 | drift detection, recovery, governance, and retirement |
| Structural debt removal protocol | 7 | 9, 10 | deletion before replacement |
| Runtime observability | 8 | 9, 10 | unmeasured runtime behavior and unsafe optimization |
| Reference baseline | 9 | 10 | reusable architecture consistency |

## Cross-Milestone Test Matrix

Use test coverage proportional to risk:

- Backend domain services: semantic authority, eligibility, lifecycle, diagnostics, recovery, projection, certification, and compatibility derivation.
- Backend endpoints: route shape, status semantics, error envelopes, null/empty handling, serialization, and contract fixtures.
- Contract oracle/generation: fixture drift, generated artifact freshness, deterministic output, manual edit detection, compatibility versioning.
- Shell: passivity helpers, error preservation, unknown field relay, null/empty relay, command classification allowlist, route mapping verification.
- TypeScript API clients: command names, argument shape, generated type use, transport error handling, boundary violation preservation.
- Hooks/resources/actions: stale response guards, repository switch cleanup, loading/error status, mutation refresh policy, invalidation, owner declaration.
- Controllers: orchestration ownership, view model derivation, action sequencing, error/loading aggregation, cross-controller contracts.
- Workspaces: isolated loading/error/failure, local navigation, controller composition, communication boundaries.
- Presentation: typed semantic mapping, no string-based semantic inference, accessibility labels, shared explainability rendering.
- Runtime: error boundaries, partial data preservation, streaming resilience, concurrency, cache invalidation, telemetry, performance guardrails.
- E2E: one representative repository path across repository selection, workspace, workflow, decision lifecycle, execution, Git workflow, operational-context review, governance, reasoning, continuity, and failure recovery.

## Milestone Definition of Done

A milestone is done only when:

- It preserves the target architecture chain.
- It leaves the application buildable.
- It updates or adds tests for every changed architectural boundary.
- It updates contract fixtures or generated artifacts when response shape changes.
- It removes or quarantines duplicate authority, duplicate state, duplicate contract, or duplicate presentation created by the milestone.
- It documents compatibility exceptions with owner and retirement criteria.
- It includes verification evidence for the commands relevant to the milestone.
- It stores an evidence package for each architecture-affecting decision, mechanism, certification, acceptance, or rollback.
- It updates durable architecture documentation when the architecture changes.

## Final Definition of Done

The plan is complete when Command Center can continuously answer:

- Did authority move?
- Did ownership change?
- Did contracts drift?
- Did projections drift?
- Did semantics move downstream?
- Did state duplicate?
- Did transport gain responsibility?
- Did presentation infer meaning?
- Did runtime failures cross boundaries?
- Did a mechanism weaken or disappear?
- Is the evidence sufficient to trust the answer?

The answer must come from executable mechanisms and certification evidence, not from manual inspection alone.
