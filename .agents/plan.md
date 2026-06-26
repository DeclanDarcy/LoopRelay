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

Objective: make every existing verifier trustworthy before changing architecture.

Implementation tasks:

- Inventory all verifiers: .NET build/test, TypeScript build, ESLint, Vitest, Playwright, Rust build/test, endpoint tests, characterization tests, architecture tests, and any CI jobs.
- Classify each verifier as healthy, broken, disabled, skipped, partial, deprecated, or unknown.
- Repair TypeScript verification, including `tsc -b`, project references, path/module resolution, incremental build output, and strictness failures that hide shape drift.
- Verify C#, TypeScript, and Rust compilers independently.
- Compare local, IDE, Tauri, release, and CI verification paths. Add or repair CI only after local commands are deterministic.
- Inventory tests and classify maintained, obsolete, duplicate, flaky, disabled, and missing.
- Create an architectural verification matrix covering authority, projection, contract, transport, state, composition, explainability, navigation, workflow, decisions, execution, reasoning, continuity, and governance.
- Create a verifier dependency graph showing what each verifier consumes, produces, and protects.
- Improve failure quality where failures are silent, misleading, or unactionable.

Required outputs:

- Verification inventory.
- Verification health report.
- Compiler health report.
- Type-system recovery report.
- Test integrity report.
- Architectural verification matrix.
- Verification dependency graph.
- Local-vs-CI consistency report.
- Structural verification certification.

Exit criteria:

- Every compiler executes or is explicitly quarantined.
- TypeScript structural verification runs reliably.
- Build paths are consistent enough to support architectural refactoring.
- Every architectural verifier is cataloged.
- Broken verification is repaired or quarantined with owner and retirement criteria.

## Milestone 0.2: Establish the Canonical Contract Oracle

Objective: create a single observation point for cross-boundary contract truth.

Implementation tasks:

- Inventory every cross-boundary contract: backend DTOs, endpoint requests/responses, Tauri commands, TypeScript types, hooks, mocks, fixtures, generated or manual docs, and tests.
- Define the canonical contract authority for Command Center as the serialized external shape of backend-owned projections and command results under the backend JSON configuration.
- Define boundary taxonomy: domain, projection, contract, serialization, transport, resource, controller, presentation, persistence, runtime.
- Define serialization rules for identifiers, enum strings, dates, nulls, optional fields, collections, ordering, polymorphism, naming, versioning, and compatibility fields.
- Add golden serialized fixtures for representative contracts: repository dashboard, repository workspace, workflow projection, execution summary/status, decision lifecycle eligibility, decision proposal browser, governance snapshot, reasoning graph/report, continuity diagnostics, and error envelope.
- Add recursive contract comparison tests that compare backend serialization against fixtures and fail on unreviewed drift.
- Add an oracle change workflow: contract identity, version, compatibility classification, fixture update, consumer regeneration, and review evidence.
- Identify all parallel truths across C#, Rust, TypeScript, mocks, fixtures, docs, and tests.

Required outputs:

- Canonical contract inventory.
- Oracle specification.
- Boundary taxonomy.
- Serialization architecture.
- Contract lifecycle model.
- Consumer taxonomy.
- Oracle dependency graph.
- Parallel truth matrix.
- Versioning strategy.
- Oracle-centric verification model.
- Oracle certification report.

Exit criteria:

- Every contract has an identified authority and consumer set.
- Contract lifecycle and versioning rules exist.
- Golden fixture comparison can detect backend serialization drift.
- No ambiguity remains about where contract authority lives.

## Milestone 0.3: Install the Architectural Regression Framework

Objective: turn architectural principles into executable regressions.

Implementation tasks:

- Catalog architectural invariants for authority, projection, contract, transport, state, composition, layering, explainability, runtime, recovery, governance, execution, reasoning, continuity, diagnostics, navigation, workspace, and interaction.
- Define regression categories and choose the appropriate mechanism for each: C# reflection tests, endpoint integration tests, serialized contract comparison, TypeScript/Vitest source scans, ESLint rules, Rust helper tests, Playwright characterization, or script-based source scans.
- Add an architecture test namespace in backend tests and a characterization/regression area in UI tests.
- Define ownership for backend, frontend, shell, cross-layer, oracle, generated artifacts, build, and CI regressions.
- Define severity: advisory warning, local build failure, CI failure, compatibility warning, or release blocker.
- Define drift models for new authorities, duplicate authorities, transport responsibility growth, projection impurity, contract replication, state duplication, composition growth, dependency cycles, and semantic leakage.
- Require failure messages to explain architectural intent and remediation.
- Add coverage and confidence reporting based on evidence quality rather than percentage counts.

Required outputs:

- Architectural invariant catalog.
- Regression taxonomy.
- Regression architecture specification.
- Regression ownership matrix.
- Regression severity model.
- Architectural drift model.
- Regression UX specification.
- Regression lifecycle model.
- Architectural confidence model.
- Regression framework certification.

Exit criteria:

- Every invariant has at least one planned executable regression.
- Regression ownership and severity are explicit.
- Architectural drift is formally modeled.
- Future milestone work has a regression framework to extend.

## Milestone 0.4: Establish Architectural Decision Governance

Objective: define how architectural decisions are proposed, evaluated, accepted, reversed, and published before structural transformation begins.

Implementation tasks:

- Create `docs/architecture-decision-governance.md` with decision roles, evidence requirements, acceptance levels, rollback rules, compatibility criteria, and publication requirements.
- Define the decision record template for `.agents/decisions/`, including invariant affected, capability affected, evidence, alternatives, compatibility impact, regression impact, rollback path, accepted baseline updates, and supersession rules.
- Define the architectural evidence model in `docs/architectural-evidence.md`, including evidence package schema, evidence type taxonomy, retention rules, traceability rules, and certification standards.
- Define decision classes: new authority, new projection, contract change, compatibility exception, regression weakening, generated artifact exception, transport exception, state ownership change, controller/workspace boundary change, runtime failure scope change, and reference architecture change.
- Define required evidence by decision class. Examples: contract fixture diff, authority inventory entry, state ownership matrix entry, passivity regression, consumer list, compatibility retirement condition, runtime failure reproduction, or benchmark/telemetry evidence.
- Define approval rules for adding, weakening, or retiring architectural mechanisms.
- Define emergency exception rules for release-blocking issues, including maximum duration, owner, compensating regression, and required follow-up certification.
- Define rollback triggers and rollback evidence requirements for migrations.
- Define how accepted architecture decisions update durable docs and the architectural capability matrix.
- Add regressions or review checks that prevent ungoverned architecture changes: new authority-like names, new projection-like names, manual generated edits, new shell response mirrors, disabled regressions, and new compatibility fields without decision records.

Required outputs:

- Architectural decision governance document.
- Decision record template.
- Architectural evidence model.
- Decision class catalog.
- Evidence requirement matrix.
- Compatibility exception policy.
- Regression weakening policy.
- Emergency exception policy.
- Rollback policy.
- Baseline update policy.
- Decision governance regression suite.
- Decision governance certification report.

Exit criteria:

- Every architecture-affecting change type has explicit decision rules.
- Compatibility and rollback are governed before broad migration starts.
- Evidence packages are required for decisions, certification, acceptance, and rollback.
- Decision records can be traced into durable architecture documentation.
- Architecture changes can no longer be accepted by implementation alone.

## Milestone 1.1: Define the Canonical Contract Model

Objective: define what a contract is and how it relates to authority, projection, serialization, transport, and consumers.

Implementation tasks:

- Define a contract as the canonical externally observable representation of an authoritative projection or command result.
- Classify contract categories: public projection, internal projection, command request, command response, event, notification, streaming event, persistence, configuration, diagnostics, health, and certification.
- Define projection-to-contract rules, including when one projection may expose multiple contracts and when aggregation contracts are allowed.
- Define ownership for shape, semantics, serialization, compatibility, versioning, evolution, and deprecation.
- Define normalization rules for identifiers, enums, dates, optional values, collections, names, metadata, ordering, evidence, diagnostics, and compatibility fields.
- Define allowed and forbidden transformations at each boundary.
- Classify consumers as generated, derived, observational, transforming, testing, presentation, or compatibility.
- Produce canonical examples using current repository, workflow, decision, execution, reasoning, continuity, governance, health, and certification projections.

Required outputs:

- Contract definition specification.
- Contract taxonomy.
- Projection-contract relationship model.
- Contract ownership matrix.
- Normalization rules.
- Boundary semantics.
- Consumer model.
- Evolution and compatibility models.
- Contract identity model.
- Governance model.
- Canonical contract examples.

Exit criteria:

- Future contract generation has an unambiguous architectural foundation.
- Every existing contract category can be classified.
- Consumers know what they may and may not own.

## Milestone 1.2: Build the Generated Contract Ecosystem

Objective: replace parallel hand-maintained contract truths with deterministic generated or mechanically verified artifacts.

Implementation tasks:

- Define generated artifact categories: TypeScript types, Rust shell contract shapes or opaque command metadata, mocks, fixtures, docs, OpenAPI/JSON schema if useful, test contracts, and future SDKs.
- Add a contract intermediate representation if direct generation from backend serialization metadata is too brittle.
- Implement deterministic generation for TypeScript contract types under `src/CommandCenter.UI/src/contracts/generated` or an equivalent generated namespace.
- Replace or wrap hand-written types in `src/CommandCenter.UI/src/types/*` with generated exports, leaving compatibility aliases only where needed.
- Generate or mechanically verify Tauri command metadata and route maps instead of maintaining domain response mirrors in Rust.
- Generate or contract-verify `src/CommandCenter.UI/src/devTauriMock.ts` fixtures.
- Add stale generated artifact detection to local verification and CI.
- Add guardrails that generated files are not manually edited.
- Migrate consumers incrementally, starting with low-risk read models, then mutation results, then complex workflow/decision/execution models.

Required outputs:

- Generated artifact catalog.
- Generation architecture and intermediate representation.
- Generator taxonomy.
- Consumer lifecycle model.
- Determinism specification.
- Generation verification model.
- Ownership model.
- Manual artifact retirement plan.
- Generation toolchain.
- Generator evolution model.
- Canonical generated examples.

Exit criteria:

- At least one complete backend projection flows through oracle fixture, generated TypeScript, generated/verified mock, and consumer test.
- Generation is deterministic and stale artifacts fail verification.
- Manual consumer artifacts have an explicit migration or retirement path.

## Milestone 1.3: Make Transport Passive

Objective: ensure the Tauri shell carries commands and responses without owning domain shape or meaning.

Implementation tasks:

- Classify every Tauri command in `src/CommandCenter.Shell/src/main.rs` as opaque read, opaque write, shell-owned, request-shaped, streaming, or compatibility.
- Define allowed shell-owned responsibilities: backend startup/shutdown, executable discovery, readiness polling, native dialogs, command-to-route mapping, HTTP method selection, request serialization, response relay, error envelope preservation, filesystem-safe launch mechanics, and Tauri integration.
- Define forbidden shell responsibilities: projection field lists, lifecycle eligibility, workflow/governance/execution/decision state, certification interpretation, health interpretation, retry semantics, push outcome classification, and domain empty-state meaning.
- Replace domain-shaped response deserialization with `serde_json::Value` for opaque read/write commands.
- Keep Rust request DTOs only where the shell legitimately shapes command parameters into backend request bodies.
- Create shared Rust forwarding helpers for GET, POST, PUT, DELETE, JSON body, query parameters, empty body, null body, and streaming/event routes.
- Preserve backend error envelopes, boundary violations, HTTP status, null, arrays, unknown fields, enum strings, identifiers, dates, and nested objects.
- Remove or quarantine special cases such as `push_execution` returning success for a conflict response unless backend explicitly defines that result contract.
- Add passivity regressions proving unknown fields survive, null survives, arrays survive, backend error envelopes survive, status remains observable, and domain mirrors are absent outside an allowlist.

Required outputs:

- Shell responsibility specification.
- Tauri command classification matrix.
- Response passivity specification.
- Request shaping specification.
- Transport error preservation model.
- HTTP status semantics specification.
- Null/empty response contract.
- Shell mirror retirement plan.
- Route mapping governance model.
- Transport passivity regression suite.
- Streaming transport passivity model.
- Shell boundary certification.

Exit criteria:

- Every shell command is classified.
- Domain-shaped response mirrors are removed or explicitly listed as temporary exceptions.
- Passivity regressions guard the shell boundary.
- Backend error and null/empty semantics are preserved through Tauri.

## Milestone 2.1: Inventory Semantic Authority

Objective: map where meaning is computed before moving it.

Implementation tasks:

- Inventory semantic concepts: workflow lifecycle, governance lifecycle, decision lifecycle, execution lifecycle, reasoning confidence/materialization, continuity taxonomy, health, diagnostics, certification, Git eligibility, prompt transparency, recovery, projection grouping, severity, tone, action eligibility, blocking/advisory findings, recommendations, confidence, and operational-context interpretation.
- For each concept identify canonical owner, current implementation owner, projection owner, consumers, tests, duplicates, and uncertainty.
- Scan C#, Rust, and TypeScript for semantic terms such as `can`, `eligible`, `blocked`, `warning`, `error`, `severity`, `tone`, `score`, `rank`, `confidence`, `status`, `health`, `certified`, `passed`, `retryable`, `recoverable`, `valid`, `diagnostic`, `finding`, `recommendation`, `decision`, `workflow`, and `governance`.
- Classify each occurrence as authoritative computation, projection mapping, passive rendering, presentation mapping, duplicate semantic computation, compatibility projection, or false positive.
- Audit shell leakage, TypeScript client leakage, hook leakage, adapter leakage, React leakage, projection purity, and explainability adapters.
- Define the boundary between valid presentation mapping and invalid semantic inference.
- Map authority regression coverage.

Required outputs:

- Semantic concept catalog.
- Authority ownership matrix.
- Semantic computation scan report.
- Authority boundary classification matrix.
- Duplicate authority matrix.
- Downstream semantic leakage report.
- Projection authority classification.
- Explainability authority report.
- Presentation mapping boundary specification.
- Authority regression coverage matrix.
- Authority confidence report.
- Authority restoration input pack.

Exit criteria:

- Every semantic concept has an identified or explicitly unknown canonical owner.
- Every duplicate or downstream semantic computation is documented.
- Restoration can proceed without guessing.

## Milestone 2.2: Restore Semantic Authority

Objective: compute every semantic fact exactly once at its owning authority.

Implementation tasks:

- Prioritize authority leaks by release risk, execution impact, lifecycle eligibility, certification/health misclassification, explainability inference, and presentation-only inconsistency.
- Add missing typed semantic fields to owning backend projections. Candidate fields include `severity`, `impact`, `blocksExecution`, `isRecoverable`, `isRetryable`, `eligibility`, `reasonCode`, `diagnosticKind`, `findingStatus`, `healthState`, `materializationRecommendation`, and `authoritySource`.
- Do not add CSS tones, visual colors, or UI-only concepts to backend projections.
- Move eligibility decisions for execution launch, handoff review, operational-context review/promotion, decision actions, governance transfer/recovery, workflow continuation/recovery, commit, push, retry, and recovery into backend-owned projections where missing.
- Remove UI hardcoded lifecycle state sets and weak-string semantic inference from `App.tsx`, feature components, hooks, `lib/status.ts`, `lib/navigation.ts`, and explainability adapters.
- Restructure projection services that compute verdicts into explicit authorities such as `Assessment`, `Evaluation`, `Policy`, `Rules`, `Classifier`, or `Verdict`, then make projections consume their results.
- Ensure compatibility fields derive from structured authoritative fields.
- Install regressions against React semantic helpers, adapter inference, shell reinterpretation, lifecycle state-set reintroduction, severity string matching, health fallback computation, and hidden projection authorities.

Required outputs:

- Authority restoration priority matrix.
- Backend semantic projection completion report.
- Downstream semantic removal report.
- Authority-bearing projection restructuring report.
- Eligibility authority restoration report.
- Diagnostic authority restoration report.
- Explainability authority restoration report.
- Continuity authority restoration report.
- Recovery authority restoration report.
- Authority restoration regression suite.
- Authority compatibility strategy.
- Authority restoration certification report.

Exit criteria:

- Prioritized leaks are fixed or quarantined.
- Consumers render authoritative facts instead of deriving them.
- Backend projections expose required semantic state.
- Regression guards prevent reintroduction.

## Milestone 2.3: Normalize Presentation

Objective: make presentation a deterministic rendering of authoritative semantics.

Implementation tasks:

- Inventory every semantic value reaching UI presentation.
- Centralize semantic-to-presentation mappings for severity, health, certification, diagnostics, eligibility, findings, evidence, recommendations, workflow, governance, execution, reasoning, continuity, recovery, Git, and operational context.
- Define one presentation vocabulary for tone, icon, label, badge, tooltip, grouping, accessibility, and status text.
- Replace duplicated tone/icon/status mappings with total mappings from typed semantic values.
- Forbid string matching that creates semantic meaning.
- Normalize shared explainability components as presentation-only components for evidence, diagnostics, findings, health, certification, eligibility, alternatives, constraints, uncertainty, and recommendations.
- Define domain presentation wrappers that may frame timelines, graphs, comparisons, grouping, and navigation but may not compute meaning.
- Add accessibility rules for status text, ARIA, keyboard interaction, contrast, screen reader labels, and status announcements.
- Add regressions that prevent new tone tables, severity mappings, icon mappings, health renderers, eligibility renderers, and diagnostic renderers outside the canonical presentation layer.

Required outputs:

- Presentation semantic inventory.
- Presentation mapping catalog.
- Presentation vocabulary specification.
- Presentation mapping rules.
- Explainability presentation specification.
- Domain wrapper specification.
- Presentation consistency matrix.
- Accessibility presentation model.
- Presentation regression suite.
- Presentation governance model.
- Presentation compatibility strategy.
- Presentation normalization certification.

Exit criteria:

- Every semantic value has one canonical presentation mapping.
- Presentation mappings originate from typed values.
- Shared explainability is presentation-only.
- Accessibility is part of the presentation model.

## Milestone 3.1: Establish the State Ownership Matrix

Objective: identify every state object, owner, lifecycle, mutation authority, and synchronization path.

Implementation tasks:

- Inventory backend, shell, frontend, hook, controller, workspace, dashboard, execution, workflow, governance, decisions, reasoning, continuity, diagnostics, certification, repository, navigation, interaction, draft, and transient state.
- Classify every state object as authoritative, derived, cached, ephemeral, draft, or synchronization state.
- Map canonical owner, current owner, consumers, mutation authority, refresh authority, persistence, synchronization, lifecycle, deletion, and recovery.
- Identify duplicate mutable owners, manual reconciliation, duplicate caches, and state that can be recreated.
- Inventory refresh, polling, mutation refresh, optimistic update, cache invalidation, repository switch, execution completion, and workflow update synchronization paths.
- Define cache owner, invalidation, consistency, freshness, and lifetime for every cache.
- Build a dependency graph from authority to owner to derived state to presentation.

Required outputs:

- Complete state inventory.
- State taxonomy.
- State ownership matrix.
- Duplicate ownership report.
- Derived state report.
- Synchronization architecture report.
- Cache architecture specification.
- Mutation ownership matrix.
- State lifecycle catalog.
- State dependency graph.
- State ownership confidence report.
- State restoration input pack.
- State ownership certification.

Exit criteria:

- Every mutable state has an owner or explicit unknown status.
- Duplicate ownership and synchronization paths are understood.
- State restoration can proceed deterministically.

## Milestone 3.2: Restore Feature Ownership

Objective: move feature state, hooks, actions, refresh, errors, and loading out of the root and into feature owners.

Implementation tasks:

- Define feature boundaries for repository dashboard, workspace, workflow, governance, decisions, execution, reasoning, continuity, operational context, health, diagnostics, certification, artifacts, Git, and navigation-adjacent feature state.
- Define what a feature controller/container owns: data hooks, local feature state, loading/error state, mutations, refresh coordination, derived feature view model, actions, and child presentation props.
- Define what `App.tsx` may own: selected repository identity, active primary tab, global shell/navigation state, command palette, global app error boundary, and workspace composition.
- Move feature-specific draft state, mutation flags, refresh chains, and action handlers out of `App.tsx` one feature at a time.
- Define explicit cross-feature coordination contracts for shared repository identity, invalidation signals, shared backend projections, navigation requests, and refresh requests.
- Move feature-specific errors and loading states into feature containers.
- Preserve presentation props initially; decompose components only after ownership boundaries stabilize.
- Add ownership regressions preventing direct feature hook imports in `App.tsx`, root-owned feature mutation state, raw `setData` crossing feature boundaries, feature error funneling through global root state, large tab prop contracts, and undeclared cross-feature refresh.

Required outputs:

- Feature boundary catalog.
- Feature controller specification.
- Root composition specification.
- Feature state migration matrix.
- Feature action ownership matrix.
- Refresh ownership model.
- Cross-feature coordination model.
- Feature error/loading ownership model.
- Feature presentation boundary specification.
- Feature ownership regression suite.
- Incremental migration strategy.
- Feature ownership certification report.

Exit criteria:

- `App.tsx` composes workspaces instead of owning feature internals.
- Major features own their own state, actions, refresh, errors, and loading.
- Cross-feature coordination is explicit.

## Milestone 3.3: Create the Shared Resource and Action Framework

Objective: centralize repeated resource mechanics while preserving feature ownership.

Implementation tasks:

- Inventory every resource-loading pattern in `src/CommandCenter.UI/src/hooks/*`: repository-keyed reads, selected-repository reads, active session hooks, eligibility hooks, polling hooks, artifact content hooks, dashboard/workspace hooks, diagnostics hooks, certification hooks, list hooks, and detail hooks.
- Inventory every mutation/action pattern: commit, push, retry, recover, transfer, save, rotate, generate, refine, resolve, archive, supersede, promote, dismiss, refresh, capture, and materialize.
- Introduce shared resource primitives in the UI for key, enabled condition, fetcher, empty value, status, data, error, refresh, reset, stale guard, invalidation, lifecycle, and owner.
- Introduce shared action primitives for identity, enabled condition, executor, pending state, error state, result, success/failure handlers, affected resources, refresh policy, and concurrency policy.
- Standardize repository switches, rapid refreshes, overlapping mutations, cancellation, ignored late responses, replacement, queuing, and disallow rules.
- Standardize mechanical error handling while keeping backend domain meaning authoritative.
- Define invalidation and refresh policy for every mutation.
- Migrate hooks incrementally without changing public hook APIs at first.
- Remove raw `setData` escape hatches last.
- Add regressions that resource hooks use shared primitives, stale guards exist, actions use shared action primitives, resources declare owners, derived resources are disposable, and the root does not manually reconcile feature resources.

Required outputs:

- Resource pattern inventory.
- Action pattern inventory.
- Resource primitive specification.
- Action primitive specification.
- Resource concurrency policy.
- Resource error model.
- Refresh/invalidation model.
- Feature resource integration specification.
- Derived resource specification.
- Resource framework migration strategy.
- Resource framework regression suite.
- Shared resource framework certification report.

Exit criteria:

- Repeated loading/error/stale/mutation mechanics are centralized.
- Feature ownership remains local.
- Hook migration has a tested, incremental path.

## Milestone 4.1: Establish Controller Architecture

Objective: separate feature orchestration from presentation composition.

Implementation tasks:

- Define controllers as owners of feature orchestration, resources, actions, coordination, refresh, lifecycle, loading/error aggregation, and view model construction.
- Inventory target controllers for repository, workspace, workflow, governance, decisions, execution, reasoning, continuity, operational context, diagnostics, certification, artifacts, Git, and health.
- Define stable presentation APIs: view model, callbacks, interaction state, and no direct resources/transport/refresh internals.
- Define view models as disposable, deterministic, derived, presentation-ready data that never own semantics or mutation.
- Move coordination sequences such as commit -> refresh execution -> refresh dashboard -> refresh workflow into owning controllers.
- Define cross-controller communication through explicit contracts, shared resources, backend projections, published events, or composition requests.
- Define controller lifecycle: creation, initialization, resource loading, refresh, mutation, recovery, disposal, repository switch, navigation change, and feature activation.
- Add regressions preventing business logic in components, hook orchestration in `App.tsx`, cross-feature refresh in `App.tsx`, large presentation prop contracts, resource loading in presentation, backend clients in presentation, and hidden controller coupling.

Required outputs:

- Controller architecture specification.
- Feature controller matrix.
- Controller responsibility matrix.
- Presentation API specification.
- View model specification.
- Controller coordination model.
- Controller communication model.
- Root composition model.
- Controller lifecycle model.
- Controller regression suite.
- Controller migration strategy.
- Controller architecture certification.

Exit criteria:

- Every major feature has a defined controller boundary.
- Presentation components render view models and emit intents.
- Controllers own orchestration without becoming semantic authorities.

## Milestone 4.2: Isolate Workspaces

Objective: make every major workspace an independently composable and testable architectural boundary.

Implementation tasks:

- Define a workspace as the complete interaction surface for a bounded capability, not just a tab.
- Inventory workspaces: repository dashboard, workflow, governance, decisions, execution, reasoning, continuity, operational context, diagnostics, certification, health, and artifacts.
- Define workspace inputs: repository identity, application context, global services, controller instances, and navigation state.
- Define workspace outputs: user intents, workspace events, navigation requests, refresh requests, and invalidation requests.
- Make workspaces compose controllers and local layout; they must not compute backend semantics or invoke transport directly.
- Define workspace lifecycle: created, initialized, activated, deactivated, repository changed, refreshed, suspended, resumed, disposed.
- Define workspace-local loading, errors, failures, and partial data.
- Define communication rules that forbid shared mutable workspace state, direct controller access across workspaces, hidden refresh chains, and presentation coupling.
- Add regressions preventing controller orchestration in `App.tsx`, cross-workspace controller calls, shared mutable workspace state, duplicated workspace composition, and hidden refresh coordination.

Required outputs:

- Workspace architecture specification.
- Workspace inventory.
- Workspace boundary specification.
- Workspace composition model.
- Workspace lifecycle model.
- Workspace isolation specification.
- Workspace communication model.
- Workspace navigation architecture.
- Workspace presentation boundary.
- Workspace isolation regression suite.
- Workspace isolation migration strategy.
- Workspace isolation certification report.

Exit criteria:

- Application root composes workspaces.
- Workspaces compose controllers.
- Workspace communication is explicit and regression-protected.

## Milestone 4.3: Establish Runtime Isolation

Objective: ensure failures, loading states, partial data, stale data, and degraded subsystem behavior remain localized, typed, observable, and recoverable.

Implementation tasks:

- Define runtime failure classes: backend exception, validation failure, domain failure, repository missing/corrupt, file locked/access denied, persistence failure, projection build failure, transport unavailable, timeout, malformed response, empty response, Tauri invoke failure, streaming disconnect, render exception, resource load failure, stale response, null/partial/contradictory state, and unavailable optional data.
- Define failure scopes: field, resource, feature, workspace, repository, application, shell, and backend process.
- Create a single backend exception envelope model for all endpoints, including typed category, HTTP status, boundary violation, domain/infrastructure classification, file IO classification, and unhandled exception observability.
- Ensure shell and TypeScript transport preserve backend envelopes and distinguish transport failures from backend/domain failures.
- Add workspace error boundaries with render isolation, resource failure surfaces, partial data support, recovery affordance, diagnostic visibility, reset behavior, repository switch reset, and error persistence policy.
- Make feature controllers classify resource failure, action failure, optional data unavailable, required data unavailable, invalid state, stale response ignored, and recovery required.
- Preserve successful partial resources when optional projections fail.
- Standardize absence semantics for null, empty collection, empty object, missing field, omitted compatibility field, unavailable, not applicable, not projected, failed to load, permission denied, repository absent, and session absent.
- Define retry and recovery boundaries: generic transport retry in resource/action framework, domain recovery in backend projections, workspace reset separate from repository recovery, backend restart separate from workflow recovery.
- Define streaming/event resilience: disconnect, parse failure, missed event refresh policy, ordering, duplication, and non-corruption of resource state.
- Add runtime isolation regressions for backend envelopes, optional endpoint failure, workspace render failure, root boundary as last resort, null/empty behavior, transport status preservation, stale response protection, streaming containment, and corrupt repository degradation.

Required outputs:

- Runtime failure taxonomy.
- Failure scope matrix.
- Backend exception boundary specification.
- Transport error boundary specification.
- Workspace error boundary specification.
- Feature failure isolation model.
- Partial data/degraded projection specification.
- Absence semantics specification.
- Recovery/retry isolation model.
- Streaming resilience model.
- Runtime isolation regression suite.
- Runtime isolation certification report.

Exit criteria:

- Failures propagate only as far as their scope requires.
- Backend and transport error semantics are structured and preserved.
- Workspaces and features degrade locally where possible.

## Milestone 5.1: Normalize Projection Taxonomy

Objective: make `Projection` mean one thing: a derived read model that exposes authoritative meaning without creating new meaning.

Implementation tasks:

- Define projection categories: domain projection, aggregation projection, compatibility projection, diagnostic projection, certification projection, health projection, UI view/presentation model, and artifact renderer.
- Inventory all `Projection`-named and projection-like artifacts across backend, middle, shell, UI, tests, and docs.
- Classify each by current name, actual role, owner, consumers, authority dependency, semantic computation, rendering side effects, disposability, and recommended category.
- Define projection purity rules: projections may select, expose, aggregate, summarize, include evidence/rationale/source/freshness/unavailable state, and derive compatibility strings from structured fields.
- Forbid projections from computing lifecycle legality, recommendation rank, quality/burden score, severity from text, health verdict, retryability, execution blocking, operational-context parsing unless parser authority, diagnostic interpretation, or markdown rendering side effects.
- Define naming rules: `Projection`, `Assessment`, `Evaluation`, `Classifier`, `Policy`, `Rules`, `Renderer`, `ArtifactRenderer`, `DocumentRenderer`, `ViewModel`, `PresentationModel`, `Request`, `Response`, `CommandResult`, and generated contract names.
- Pay special attention to artifact projection services and UI projection helpers that may actually be renderers or presentation models.
- Add regressions detecting projection services that invoke rule engines directly, compute semantic flags, render artifacts, create compatibility strings outside authority, or expose fields with no authoritative source.

Required outputs:

- Projection definition specification.
- Projection taxonomy specification.
- Projection inventory matrix.
- Projection purity rules.
- Projection ownership model.
- Projection lifecycle model.
- Projection naming specification.
- Projection misclassification matrix.
- Projection compatibility strategy.
- Projection taxonomy regression suite.
- Projection glossary.
- Projection taxonomy certification report.

Exit criteria:

- Every projection-like artifact is classified.
- Misclassified artifacts have migration targets.
- Projection role confusion is regression-tested.

## Milestone 5.2: Normalize Authority Taxonomy

Objective: give every semantic responsibility one explicit architectural role, owner, lifecycle, and vocabulary.

Implementation tasks:

- Define roles: authority, assessment, evaluation, classification, policy, rules, projection, aggregation, compatibility, renderer, view model, presentation model, coordinator, controller, resource, action, contract, transport, diagnostics, certification, health, and recovery.
- For each role define what it owns, may perform, must not perform, consumes, produces, and depends on.
- Inventory major services and helpers across decisions, workflow, execution, reasoning, continuity, decision sessions, middle projections, backend endpoints, UI controllers, explainability, and resource framework.
- Classify artifacts by current name, actual role, correct role, primary/secondary responsibilities, and role purity.
- Define role purity rules and relationship graph.
- Define naming suffixes and evolution rules for splitting or merging roles.
- Add regressions preventing projection performing assessment, renderer computing semantics, controller becoming authority, policy inside presentation, assessment inside transport, compatibility becoming authority, and aggregation redefining meaning.

Required outputs:

- Authority role catalog.
- Authority responsibility matrix.
- Authority classification matrix.
- Role purity specification.
- Authority relationship graph.
- Authority lifecycle model.
- Authority naming specification.
- Mixed responsibility matrix.
- Authority evolution model.
- Authority taxonomy regression suite.
- Authority glossary.
- Authority taxonomy certification report.

Exit criteria:

- Every major architectural artifact can be classified.
- Mixed-role artifacts are known and transitional or intentional.
- Naming reflects responsibility.

## Milestone 5.3: Normalize Presentation Taxonomy

Objective: define presentation roles, layering, responsibilities, lifecycle, naming, and purity.

Implementation tasks:

- Define presentation roles: workspace, feature surface, view, layout, panel, section, region, widget, component, renderer, view model, presentation model, presentation adapter, interaction pattern, navigation, status presentation, badge, timeline, graph, comparison, inspector, dialog, overlay, command palette, and dashboard.
- Build a presentation layer graph: application -> workspace -> feature surface -> view -> panel -> section -> widget -> renderer, with overlays, interactions, and navigation as orthogonal concerns.
- Define what presentation may do: layout, grouping, ordering, formatting, visualization, accessibility, interaction, animation, transition, navigation, and responsiveness.
- Forbid presentation from semantic computation, contract interpretation, eligibility inference, recommendation scoring, lifecycle rules, authority ownership, transport, and persistence.
- Define interaction taxonomy for commands, actions, review, approval, selection, filtering, navigation, editing, confirmation, inspection, comparison, timeline navigation, and drill-down.
- Define composition rules, lifecycle, naming suffixes, and mixed responsibility migration paths.
- Add regressions preventing workspaces loading data directly, renderers owning state, widgets fetching resources, panels computing semantics, layouts owning interaction, navigation owning domain lifecycle, interactions owning authority, and view models becoming authorities.

Required outputs:

- Presentation role catalog.
- Presentation responsibility matrix.
- Presentation layer graph.
- Presentation purity specification.
- Presentation relationship model.
- Interaction taxonomy.
- Presentation composition specification.
- Presentation lifecycle model.
- Presentation naming specification.
- Presentation mixed responsibility matrix.
- Presentation taxonomy regression suite.
- Presentation taxonomy certification report.

Exit criteria:

- Every presentation artifact has a role or migration target.
- Presentation remains semantically passive.
- Future UI work has a common vocabulary.

## Milestone 6: Install Architectural Mechanisms

Objective: give every architectural invariant a complete lifecycle: defined, observed, verified, enforced, recovered, evolved, and retired.

Implementation tasks:

- Unify all invariants from prior milestones into one catalog.
- For every invariant define owner, affected layers, dependencies, current enforcement, and missing lifecycle stages.
- Map each invariant to a protecting mechanism: oracle, generation, contract fixture comparison, authority regression, transport passivity regression, projection taxonomy regression, state ownership regression, controller/workspace regression, runtime isolation regression, presentation regression, documentation validation, and certification.
- Define unified drift detection for authority, contract, transport, projection, ownership, presentation, controller, runtime, dependency, and layering drift.
- Define recovery guidance for each violation class.
- Build a mechanism interaction graph showing prerequisites and failure propagation.
- Define governance over changing invariants, adding mechanisms, retiring mechanisms, weakening regressions, bypassing generators, and introducing compatibility exceptions.
- Integrate architectural evidence packages into mechanism lifecycle, so every mechanism can name the evidence proving its current status.
- Define architectural confidence based on enforcement strength: runtime verification and regression are stronger than documentation and manual review.
- Add regressions protecting the mechanisms themselves: disabling regressions, bypassing oracle, bypassing generators, adding manual contracts, introducing second authorities, bypassing controller boundaries, and weakening runtime isolation.
- Produce a self-stabilization analysis identifying remaining convention-based gaps.

Required outputs:

- Architectural invariant catalog.
- Mechanism matrix.
- Architectural lifecycle model.
- Drift detection architecture.
- Architectural recovery model.
- Mechanism interaction graph.
- Architectural governance model.
- Architectural evidence integration model.
- Mechanism evolution model.
- Architectural confidence framework.
- Mechanism regression suite.
- Architectural self-stabilization report.
- Architectural mechanism certification report.

Exit criteria:

- Every invariant has a protecting mechanism or explicit gap.
- Mechanism lifecycle and governance are defined.
- Architectural drift is observable and recoverable.

## Milestone 7: Eliminate Structural Debt

Objective: remove obsolete architecture only after replacements are structurally enforced.

Implementation tasks:

- Inventory removable debt: manual TypeScript contracts, Rust response mirrors, duplicate schemas, duplicate authority, duplicate projections, duplicate resource mechanics, duplicate presentation mappings, transport wrappers, compatibility routes, obsolete controllers/workspaces, dead interfaces, dead services, dead endpoints, dead hooks, dead renderers, transitional infrastructure, and obsolete docs.
- Classify every item as safe removal, protected compatibility, needs migration, still active, unknown, blocked, or false positive.
- Verify every replacement is implemented, verified, regression-protected, and certified before removal.
- Analyze compatibility structures and decide retain, deprecate, or remove.
- Detect dead architecture by references, consumers, tests, and historical purpose.
- Remove only debt whose replacement is protected.
- Measure simplification: authorities reduced, resource frameworks unified, tone tables unified, shell mirrors removed, manual contracts removed, interfaces/services/hooks/renderers removed, dependency/coupling reduced.
- Validate canonical architecture after removal.
- Clean durable documentation so it reflects the current architecture and removes obsolete guidance.
- Add a prevention matrix explaining which mechanism prevents each removed debt class from returning.

Required outputs:

- Structural debt inventory.
- Debt classification matrix.
- Replacement verification report.
- Compatibility analysis.
- Dead architecture report.
- Architectural simplification report.
- Architectural purity report.
- Removal regression report.
- Documentation cleanup report.
- Architectural complexity report.
- Debt prevention matrix.
- Structural debt certification report.

Exit criteria:

- No duplicate architecture remains without justification.
- Every compatibility structure has owner and retirement criteria.
- Removed artifacts have verified replacements.
- Complexity reduction is measurable.

## Milestone 8: Validate Runtime Architecture and Performance

Objective: make runtime behavior observable, measurable, scalable, and governed by architecture.

Implementation tasks:

- Inventory runtime subsystems: startup, backend process lifecycle, repository initialization, workspace activation, controller activation, resource loading, mutation pipeline, refresh, invalidation, streaming, rendering, background tasks, diagnostics, telemetry, profiling, memory, caching, scheduling, and concurrency.
- Define runtime lifecycle from application startup through shell, backend, repository, workspace, controller, resources, presentation, interaction, and shutdown.
- Add observability for startup, repository switch, workspace transition, controller lifecycle, resource lifecycle, mutation lifecycle, refresh lifecycle, cache lifecycle, streaming lifecycle, error lifecycle, and recovery lifecycle.
- Define architecture-aware performance metrics: startup time, repository switch time, workspace switch time, controller activation time, resource latency, mutation latency, refresh latency, render cost, stream latency, memory, cache hit rate, invalidation cost, and interaction latency.
- Inventory concurrency: concurrent refresh, concurrent mutation, repository switch, streaming, cancellation, background processing, controller activation, ordering, isolation, synchronization, and determinism.
- Validate cache ownership, invalidation, rebuild, discard, sharing, freshness, staleness, redundancy, and memory.
- Assess scalability for many repositories, controllers, resources, workspaces, streaming events, diagnostics, history, large operational context, large reasoning graph, and large decision inventory.
- Define telemetry that measures architecture rather than incidental implementation.
- Add regressions ensuring performance fixes do not introduce semantic authority, global caches, shared mutable state, transport ownership, controller bypass, or presentation ownership.

Required outputs:

- Runtime architecture inventory.
- Runtime lifecycle model.
- Runtime observability model.
- Performance architecture model.
- Concurrency architecture.
- Runtime cache validation report.
- Scalability architecture report.
- Architectural telemetry model.
- Optimization philosophy.
- Runtime regression suite.
- Runtime evolution model.
- Runtime architecture certification report.

Exit criteria:

- Runtime behavior is observable and measurable.
- Optimization is evidence-driven and cannot bypass architecture.
- Runtime evolution is governed.

## Milestone 9: Certify and Baseline the Architecture

Objective: validate the complete architecture as one coherent system and produce the durable reference baseline.

Implementation tasks:

- Validate consistency across authority, projection, contract, transport, resource, controller, workspace, presentation, runtime, regression, governance, documentation, and mechanisms.
- Validate every invariant end-to-end: definition, observation, verification, enforcement, recovery, and evolution.
- Exercise every mechanism and prove it catches the drift class it is meant to prevent.
- Build traceability from architectural principle to mechanism to implementation to runtime behavior to regression to documentation.
- Validate evidence packages for every accepted capability, including sufficiency, freshness, reproducibility, known limits, and retention location.
- Reconcile durable documentation with implementation, runtime, taxonomy, governance, and mechanisms.
- Extract the stable architectural model into reference documentation, separating permanent/generalizable concepts from Command Center-specific implementation details.
- Review evolution readiness for new domains, workspaces, contracts, authorities, presentation surfaces, runtime mechanisms, and generated consumers.
- Create an architectural risk register with likelihood, impact, mitigation, and protecting mechanism.
- Assess long-term maintainability, mechanism overhead, governance burden, developer experience, incremental evolution, regression maintenance, and documentation maintenance.
- Freeze the reference baseline: diagrams, taxonomy, mechanisms, invariants, contracts, layering, relationships, glossary, and implementation guidance.

Required outputs:

- Architectural consistency report.
- End-to-end invariant validation.
- Architectural mechanism validation.
- Architectural evidence validation.
- Architectural traceability matrix.
- Architecture documentation validation.
- Reference architecture extraction.
- Architecture generalization report.
- Evolution readiness report.
- Architectural risk register.
- Maintainability assessment.
- Reference architecture baseline.
- Architecture certification report.

Exit criteria:

- Implementation, runtime behavior, tests, and documentation agree.
- Mechanisms are proven effective.
- Evidence is sufficient, current, traceable, and retained.
- Remaining risks are explicit.
- The reference architecture baseline is complete.

## Milestone 10: Publish the Reference Architecture

Objective: separate the Command Center implementation baseline from a consumable reference architecture package for future systems.

This milestone does not introduce new implementation architecture. It packages, validates, and publishes the accepted baseline from Milestone 9.

Implementation tasks:

- Split Command Center-specific implementation details from general reference architecture guidance.
- Publish a reference architecture package under `docs/reference/` or an equivalent durable documentation structure.
- Include canonical diagrams for the layer chain, authority flow, contract flow, transport flow, resource/controller/workspace flow, runtime failure flow, and mechanism lifecycle.
- Include capability lifecycle guidance: introduction, protection, certification, baseline, publication, evolution, and retirement.
- Include decision governance guidance and templates suitable for future systems.
- Include architectural evidence guidance and examples suitable for future systems.
- Include implementation adoption guidance: minimum viable Oracle, minimum passive transport, minimum authority inventory, minimum state ownership matrix, and minimum regression framework.
- Include migration guidance for existing systems and greenfield guidance for new systems.
- Include anti-patterns and recovery guidance: duplicate authority, manual contract truth, transport-owned semantics, root-owned feature state, presentation inference, unscoped runtime failure, and cleanup without replacement.
- Define versioning for the reference architecture package.
- Add publication validation: links, examples, glossary consistency, capability matrix consistency, and traceability back to the certified Command Center baseline.
- Define ownership for future updates to the reference package.

Required outputs:

- Published reference architecture package.
- Command Center-specific implementation appendix.
- General adoption guide.
- Decision governance template set.
- Architectural evidence template set.
- Capability lifecycle guide.
- Architecture anti-pattern and recovery guide.
- Reference architecture versioning policy.
- Publication validation report.
- Reference ownership model.
- Reference architecture publication certification.

Exit criteria:

- Future systems can consume the reference package without reading Command Center implementation history.
- Command Center-specific and general guidance are separated.
- The published package traces back to certified implementation evidence.
- Reference ownership and evolution rules are explicit.

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
