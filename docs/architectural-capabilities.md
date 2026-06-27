# Architectural Capabilities

This matrix records architectural capabilities as they become observable, protected, certified, and documented during the post-MVP architecture program.

| Capability | Introduced | Protected | Certified | Reference Documentation | Status |
| --- | --- | --- | --- | --- | --- |
| Structural verification | 0.1 | 0.1, future architectural regression framework | 0.1 local command-line baseline | `docs/architectural-mechanisms.md` | Certified locally with quarantines |
| Canonical contract Oracle | 0.2 | Repository dashboard golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, repository workspace golden fixture comparison, consumer verification, artifact freshness verification, request-boundary verification, cross-family repeatability evidence, workflow projection field inventory, workflow fixture field classification, workflow instance golden fixture comparison, workflow TypeScript consumer verification, workflow request-boundary verification, workflow artifact freshness verification, workflow local certification, milestone certification review, milestone acceptance baseline, and procedural change workflow | Repository dashboard pilot certified locally as of Slice 0018; request-boundary extension verified in Slice 0019; repository workspace fixture verified in Slice 0020; repository workspace consumer verification verified in Slice 0021; repository workspace artifact freshness verified in Slice 0022; repository workspace request-boundary verification verified in Slice 0023; repository workspace pilot certified locally as of Slice 0024; two-family repeatability checkpoint recorded in Slice 0025; workflow projection inventory recorded in Slice 0026; workflow fixture field classification recorded in Slice 0027; workflow instance fixture comparison verified in Slice 0028; workflow TypeScript consumer verification verified in Slice 0029; workflow request-boundary verification verified in Slice 0030; workflow artifact freshness verified in Slice 0031; primary workflow projection pilot certified locally as of Slice 0032; three-family repeatability checkpoint recorded in Slice 0033; scoped milestone certification review recorded in Slice 0034; scoped milestone acceptance baseline recorded in Slice 0035 | `docs/contracts.md` | Accepted and baselined as the Phase 0 Contract Oracle foundation with explicit deferrals; repository dashboard, repository workspace, and primary workflow projection pilots are locally certified; full contract-surface coverage and generated contract lifecycle remain later work |
| Architectural regression framework | 0.3 | Backend architecture-test namespace, mechanism catalog, fixture-wiring meta-regression, architectural invariant catalog, catalog metadata regression, regression taxonomy, taxonomy metadata regression, ownership matrix, severity model, ownership/severity metadata regression, architectural drift model, drift metadata regression, regression UX specification, failure-message metadata regression, architectural confidence model, confidence metadata regression, regression lifecycle model, lifecycle metadata regression, regression architecture specification, specification metadata regression, frontend architecture-test area, frontend discoverability metadata regression, shell command-family classification, shell mirror inventory, and shell classification metadata regression | Initial M0.3 skeleton verified locally in Slice 0036; invariant catalog guard verified locally in Slice 0037; regression taxonomy guard verified locally in Slice 0038; ownership/severity guard verified locally in Slice 0039; drift model guard verified locally in Slice 0040; regression UX guard verified locally in Slice 0041; architectural confidence guard verified locally in Slice 0042; regression lifecycle guard verified locally in Slice 0043; regression architecture specification guard verified locally in Slice 0044; frontend regression skeleton verified locally in Slice 0045; shell regression classification verified locally in Slice 0046; milestone certification recorded in Slice 0047 | `docs/architectural-mechanisms.md`, `docs/shell-transport-classification.md` | Certified as a framework-complete Phase 0 architectural regression foundation with explicit enforcement deferrals; broad invariant enforcement remains later milestone work |
| Architectural decision governance | 0.4 | Decision governance reference document, architectural evidence model, decision record template, decision class catalog, mechanism lifecycle approval rules, emergency exception rules, rollback policy, baseline update policy, backend governance metadata guard, architecture-regression bypass guard, shell Rust struct classification guard, active governance artifact structure/link guard, referential governance claim guard, authority/projection-like file name governance guard, compatibility-structure governance guard, milestone certification review, and milestone acceptance baseline | Opening governance definition guard verified locally in Slice 0050; architecture-regression bypass guard verified locally in Slice 0051; shell Rust struct classification guard verified locally in Slice 0052; active governance artifact validation verified locally in Slice 0053; referential governance validation verified locally in Slice 0054; authority/projection watchlist governance verified locally in Slice 0055; compatibility-structure governance added in Slice 0056; scoped milestone certification recorded in Slice 0057; scoped milestone acceptance baseline recorded in Slice 0058 | `docs/architecture-decision-governance.md`, `docs/architectural-evidence.md`, `docs/shell-transport-classification.md`, `docs/authority-projection-governance-watchlist.md`, `docs/compatibility-structure-governance.md` | Accepted and baselined as the Phase 0 architectural decision governance foundation with explicit enforcement limitations; full source-change detection, historical corpus validation, compatibility correctness, and later architecture capabilities remain future work |

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

## Architectural Decision Governance

Milestone 0.4 is introduced with durable governance definitions and an executable metadata guard.

Current scope:

- decision roles for proposer, authority owner, mechanism owner, compatibility owner, and certifier,
- decision class catalog covering new authority, new projection, contract change, compatibility exception, regression weakening, generated artifact exception, transport exception, state ownership change, controller/workspace boundary change, runtime failure scope change, and reference architecture change,
- approval rules for authority, projection, contract, compatibility, regression, generated artifact, transport, and UI-local semantic-preview decisions,
- mechanism lifecycle governance for adding, strengthening, weakening, quarantining, replacing, and retiring mechanisms,
- emergency exception rules with duration, owner, compensating regression, follow-up certification, and bounded scope,
- rollback policy and baseline update policy,
- architectural evidence package schema,
- evidence type taxonomy,
- evidence requirements by decision class,
- retention, traceability, certification, and acceptance standards,
- decision record template under `.agents/decisions/`,
- backend architecture metadata guard in `ArchitecturalDecisionGovernanceTests`,
- architecture-regression bypass guard for xUnit `Skip` and Vitest `.skip` or `.only` in architecture regression homes,
- shell Rust struct classification guard that keeps `src/CommandCenter.Shell/src/main.rs` aligned with `docs/shell-transport-classification.md`,
- active governance artifact structure/link guard for `.agents/decisions/decisions.md`, M0.4 governance slice evidence files, and decision-governance evidence links in `docs/architectural-mechanisms.md`.
- referential governance claim guard for active decisions, governance evidence, capability claims, and mechanism claims.
- authority/projection-like file name governance through `docs/authority-projection-governance-watchlist.md`.
- compatibility-structure governance through `docs/compatibility-structure-governance.md`.

Primary M0.4 evidence:

- `.agents/milestones/m0.4-governance-definition-slice-0050.md`
- `.agents/milestones/m0.4-regression-weakening-guard-slice-0051.md`
- `.agents/milestones/m0.4-shell-mirror-governance-slice-0052.md`
- `.agents/milestones/m0.4-active-governance-artifact-validation-slice-0053.md`
- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`
- `.agents/milestones/m0.4-authority-projection-watchlist-slice-0055.md`
- `.agents/milestones/m0.4-compatibility-structure-governance-slice-0056.md`

Slice 0050 verifies the governance document, evidence model, and decision-record template are present and populated. It does not certify M0.4 as complete and does not yet detect all ungoverned architecture-changing source edits.

Slice 0051 adds initial regression-weakening enforcement for disabled or focused architecture regression tests. It does not yet detect deleted tests, narrowed assertions, new shell response mirrors, compatibility fields, or invalid active governance artifacts.

Slice 0052 adds executable shell mirror governance by requiring every Rust struct in `src/CommandCenter.Shell/src/main.rs` to appear in the Rust Mirror Inventory and every inventory entry to remain present in code. It does not classify whether an allowed mirror is passive, compatibility, or shell-owned correctly beyond the existing inventory metadata.

Slice 0053 adds active governance artifact validation for the current decision checkpoint, M0.4 governance evidence slices, and decision-governance evidence links. It validates structure and reachable mechanism evidence links only; full historical reachability and decision-record schema enforcement remain later work.

Slice 0054 adds referential governance validation for active decisions, M0.4 governance evidence, capability claims, and mechanism claims. It verifies reachable evidence links and basic governance-artifact traceability without judging the substance of authorized decisions.

Slice 0055 adds a narrow authority/projection watchlist guard for source file names under `src/` and backend tests that contain `Authority` or `Projection`. It verifies inventory alignment only; semantic authority correctness and projection purity remain later milestone work.

Slice 0056 adds compatibility-structure governance for compatibility fields, routes, commands, and mirrors. It verifies lifecycle metadata and evidence reachability, and aligns bounded route and shell compatibility inventories; it does not certify compatibility correctness or retire any compatibility structure.

Slice 0057 records scoped M0.4 certification in `.agents/milestones/m0.4-decision-governance-certification-slice-0057.md`. It certifies the decision governance foundation, evidence requirements, rollback and compatibility governance, and initial executable governance detectors. It does not certify complete historical corpus validation, compatibility derivation correctness, passive transport, generated contracts, semantic authority restoration, or other later milestone claims.

Slice 0058 records scoped acceptance and baseline evidence in `.agents/milestones/m0.4-decision-governance-acceptance-baseline-slice-0058.md`. Milestone 0.4 is accepted as the Phase 0 architectural decision governance foundation with explicit deferrals. The accepted baseline governs future architecture-affecting work before acceptance, while later milestones remain responsible for strengthening source-change detection, compatibility correctness, transport passivity, generated contracts, semantic authority, state ownership, controller/workspace architecture, runtime isolation, CI enforcement, and release-path certification.
