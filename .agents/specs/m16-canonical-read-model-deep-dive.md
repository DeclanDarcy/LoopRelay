<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 milestone=M16 -->
# M16 — Canonical Read Model Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M16
- **Name:** Canonical Read Model
- **Implementation role:** Canonical Read Model
- **Roadmap position:** 17 of 22
- **Short description:** Provide every client one immutable evidence-linked projection of selection, workflow lineage, gates, warnings, freshness, policy/runtime profile, prompts/provider evidence, effects, recovery, interactions, completion, storage/import state, uncertainty, pending work, and required human action.
- **Primary outcome:** Application queries and observers consume one projection; renderers/exports remain pure, every displayed claim traces to stable evidence, status is read-only, and conflicts/uncertainty/actions are visible rather than collapsed.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/specs/epic.md`](epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M16), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0003-logical-history-authority](../../docs/architecture/decisions/0003-logical-history-authority.md), [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md), [0011-thin-application-boundary](../../docs/architecture/decisions/0011-thin-application-boundary.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Provide every client one immutable evidence-linked projection of selection, workflow lineage, gates, warnings, freshness, policy/runtime profile, prompts/provider evidence, effects, recovery, interactions, completion, storage/import state, uncertainty, pending work, and required human action.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Typed CLI snapshots, persistence projection, status output, and many domain stores exist, but application/observer assemble ad hoc queries, pending/unknown work can be invisible, and certification obligations are not linked at item level.
- Hard prerequisites: M15 Completion Authority; M12 Import Gateway.
- Not yet architecturally closed: Canonical operational snapshot, Resolution explanation, Required-action projection, Certification obligation linkage.

## 6. Runtime / System State After

- Application queries and observers consume one projection; renderers/exports remain pure, every displayed claim traces to stable evidence, status is read-only, and conflicts/uncertainty/actions are visible rather than collapsed.
- Enforceable permanent property: Provide every client one immutable evidence-linked projection of selection, workflow lineage, gates, warnings, freshness, policy/runtime profile, prompts/provider evidence, effects, recovery, interactions, completion, storage/import state, uncertainty, pending work, and required human action.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Canonical operational snapshot | Canonical Read Model | Workspace/client query | All authoritative domain projections | Immutable evidence-linked read model | Every claim has source identity and uncertainty | CLI/exports/operators |
| Resolution explanation | Canonical Read Model | Invocation/workflow selection | Alternatives, conflicts, gates, observations | Selected/ambiguous/waiting explanation | No console text is sole truth | Kernel/application |
| Required-action projection | Canonical Read Model | Pending effect/recovery/interaction/storage/completion | Domain facts | Typed action with exact identity | No invisible required action | Operator |
| Certification obligation linkage | Canonical Read Model | Catalog/schema/risk obligation | Campaign/test evidence identities | Credited or explicitly uncredited obligation | Dimension pass cannot silently imply item credit | Release view |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Canonical Read Model | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Canonical Read Model rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Canonical projection composer | Join owner-provided projections without interpreting raw tables | Rebuildable snapshot only | Inputs and facts supplied by domain projection interfaces | CanonicalReadModel query | Typed collaboration boundary; no adjacent-owner semantics | domain projection interfaces | claim/source tests |
| Domain projection adapters | Expose policy/prompt/effect/recovery/etc. facts | No new authority state | Inputs and facts supplied by canonical stores | typed submodels | Typed collaboration boundary; no adjacent-owner semantics | canonical stores | adapter contract tests |
| Resolution explanation builder | Explain alternatives/conflicts/current eligibility | Projection data | Inputs and facts supplied by kernel/catalog/observer | resolution submodel | Typed collaboration boundary; no adjacent-owner semantics | kernel/catalog/observer | ambiguity tests |
| Pure render/export adapters | Format snapshot only | No state | Inputs and facts supplied by application snapshot | renderer/export contracts | Typed collaboration boundary; no adjacent-owner semantics | application snapshot | purity/non-mutation tests |
| Obligation evidence linker | Associate executable evidence with stable obligations | Evidence links | Inputs and facts supplied by M13/certification | coverage query | Typed collaboration boundary; no adjacent-owner semantics | M13/certification | credit/no-credit tests |

## 10. Repository and File Impact

- `CanonicalPersistenceReadModel.cs` and new aggregate read-model contracts
- `ApplicationBoundaryContracts.cs` status/query types
- `UnifiedCliStatusFormatter.cs` made pure
- remove raw store queries from application and observer
- projection/traceability/certification tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- One status/query contract returns the complete canonical snapshot and stable evidence links.
- CLI renderers and exports accept a snapshot; they cannot access repository, persistence, recovery, or effects.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Each domain owner projects its facts; aggregate composer does not reinterpret ownership.
- Projection rebuild never writes or migrates.
- Unknown/missing evidence is represented explicitly, not filled with defaults.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Canonical snapshot | Canonical Read Model | query | Rebuildable or process-scoped | immutable/rebuildable | snapshot ID/source watermarks | source/evidence validation | rebuild | Clients |
| Domain subprojection | owning authority | fact revision | Rebuildable from authoritative facts | rebuildable | owner/source IDs | fact precedence | rebuild | Aggregate |
| Obligation evidence link | Certification/read model | catalog/evidence version | Canonical ledger or declared authoritative artifact | append/supersede | obligation + evidence IDs | exact scope/tier | relink | Release query |

## 14. Lifecycle and State Transitions

```text
Authoritative facts -> Owner projections -> Aggregate snapshot -> Pure rendering/export; source changes -> new snapshot
```

| Transition rule | Trigger | Preconditions | Result | Failure/evidence |
|---|---|---|---|---|
| Enter | Typed request or discovered durable work | Hard prerequisites and unambiguous authority | Initial durable fact/state | Reject before side effects; name missing prerequisite |
| Advance | Validated current evidence | Fresh inputs, legal prior state, required writes available | Next durable state/evidence | Preserve prior state and append failure evidence |
| Settle | Terminal observation/postcondition | Required effects/evidence complete | Typed terminal result | Unknown/partial transfers to Recovery, not success |

## 15. Execution Flow

```text
Production composition
  -> validate authority and prerequisites
  -> load authoritative facts/read-only observations
  -> validate inputs and persist causal intent
  -> execute the owner-specific operation
  -> atomically record state plus required effect intents
  -> reconcile effects/recovery as applicable
  -> project one typed result/read model
```

Startup fails closed on invalid composition. Failure preserves the last durable boundary. Recovery starts from ledger evidence. Terminal evidence uses a non-cancelled token after caller cancellation has fired.

## 16. Dependency Closure

| Classification | Dependency |
|---|---|
| Hard prerequisite | M15 Completion Authority |
| Inherited capability | M12 Import Gateway |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M17 Roadmap convergence; M18 Plan convergence; M19 Execute convergence; M20 thin application queries |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Claim lacks evidence identity | projection validator | omit/mark unknown; fail acceptance | fix domain adapter | claim path | Deterministic fault injection plus public-result regression |
| Status triggers migration | byte non-mutation test | reject query implementation | route mutation to M11 command | changed-byte diff | Deterministic fault injection plus public-result regression |
| Conflicting owners disagree | aggregate conflict detection | surface ambiguity; no arbitrary winner | resolve at authority | source IDs | Deterministic fault injection plus public-result regression |
| Stale certification credit | catalog/evidence version check | mark uncredited | rerun/relink evidence | obligation version | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Read model is not authority | Roadmap §§0.1, 3, 5 and M16 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Every displayed claim traces to evidence or explicit unknown | Roadmap §§0.1, 3, 5 and M16 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Status is read-only | Roadmap §§0.1, 3, 5 and M16 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Required actions are never invisible | Roadmap §§0.1, 3, 5 and M16 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Renderers are pure | Roadmap §§0.1, 3, 5 and M16 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | projection composition unit tests |
| Integration | read-only status and production-query integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | claim traceability contract tests; ambiguity/pending-action regression tests; obligation credit tests |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- full active run snapshot
- unknown effect
- recovery plus interaction
- storage migration required
- completion partial closure
- selection conflict
- uncredited changed obligation

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Query a workspace containing a pending push, outstanding interaction, recovery plan, and migration-required source; prove one snapshot exposes every identity/required action, render it twice without byte changes, and trace each claim to a durable fact.

**Execution checks:** use the published production composition where practical; inspect typed result, canonical facts, independent observation, and exit semantics.

**Expected output:** The typed result preserves the milestone-specific outcome and exposes stable evidence/pending-action identities.

**Expected persisted state:** Authoritative facts and required effect/recovery/interaction intents exist at the last completed durable boundary; no later state is fabricated.

**Expected diagnostics:** Structured diagnostics name the causal identity, failed invariant or unmet prerequisite, and the next allowed action without leaking secrets.

**Expected failures:** The negative case described above returns its specific typed outcome, performs no unauthorized mutation, and remains restart-discoverable.

**Verification commands/checks:**

```powershell
dotnet build LoopRelay.slnx --no-restore
dotnet test LoopRelay.slnx --no-restore
# Run the lowest applicable deterministic/live campaign from roadmap §6.
```

**Expected result:** The permanent property is observable; persisted state matches independent observation; a second identical coordination is idempotent.

## 22. Certification Evidence

- Passing build and applicable component suites with no unexpected warnings.
- Machine-generated results for all §19 categories and failure boundaries.
- Durable ledger/effect/recovery receipts surfaced by stable identity.
- Independent repository/provider observation agreeing with claimed state.
- Applicable deterministic/live roadmap §6 campaigns; campaign success does not substitute for authority closure.
- Changed production obligations linked to executable tests/campaign evidence.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion criterion |
|---|---|---|---|---|
| 1. Contract closure | Make Canonical Read Model boundaries explicit | Typed inputs, outputs, states, validators | M15 Completion Authority | Invalid/ambiguous inputs fail before mutation |
| 2. Durable state closure | Persist authoritative facts/legal transitions | Store/schema adapters and atomic operations | Step 1, M1/M4 | Restart reproduces state/classification |
| 3. Production routing | Route supported application path through owner | Composition wiring and typed result propagation | Steps 1–2 | No request bypasses owner |
| 4. Failure/recovery closure | Implement/reconcile every §17 boundary | Failure evidence and recovery handoff | Step 3, M8/M9 | Unknown/partial/cancelled are distinct and replayable |
| 5. Alternate-path retirement | Remove superseded behavior after parity | Deleted/unreachable competing bodies/assets | Steps 3–4 | Deletion changes no behavior |
| 6. Acceptance closure | Execute §§19–22 | Reproducible machine evidence | Steps 1–5 | Permanent property and inherited invariants pass |

## 24. Parallel Work Opportunities

| Lane | Scope | Owner type | Dependencies | Synchronization point | Integration risk |
|---|---|---|---|---|---|
| Contracts/validators | Models, legal transitions, deterministic validation | Domain/core engineer | Normative decisions | Before store/composition integration | Contract drift |
| Persistence/replay | Schema adapters, atomic writes, reconstruction fixtures | Persistence engineer | Stable contracts | Production routing | False evidence/migration drift |
| Production/certification | Composition, public outcomes, failure fixtures | Integration engineer | Contracts + store | Acceptance demonstration | Green behavior hides alternate owner |

Serialize shared schema and production composition changes at integration.

## 25. Risks and Mitigations

| Class | Risk | Impact / likelihood | Earliest detection | Mitigation | Fallback |
|---|---|---|---|---|---|
| Architectural | Bridge becomes permanent second owner | High / medium | Reachability tests | Route one owner and name retirement gate | One-way fail-closed adapter |
| Data | Required fact and outward action diverge | High / medium | Boundary fault injection | Atomic fact/intent then reconcile | RecoveryRequired |
| Integration | Typed outcome collapses at application/CLI | High / medium | Contract matrix | Preserve discriminants/exit meaning | Reject generic mapping |
| Testing | Green campaign misses duplicate authority | High / medium | Ownership scan | Pair behavior evidence with closure checks | Block acceptance |
| Performance | Unbounded ledger/catalog scan | Medium / low | Smoke metrics | Index/bound queries from measurements | Defer concurrency |
| Security | Untrusted input influences policy/mutation | High / medium | Permission/validation tests | Typed schemas, resolved policy, effect allowlist | Fail closed |

## 26. Observability and Diagnostics

- Structured logs include workspace/root-run/workflow/transition/attempt/session/effect/recovery identities.
- Metrics count legal transitions, rejections, unknowns, duplicate coordination, and latency without becoming authority.
- Health checks report dependency availability and unsettled required work without mutation.
- Read-model diagnostics link claims to durable identities and scrub credentials/provider payloads.

- Audit records preserve append-only lifecycle, policy, prompt, effect, recovery, interaction, and completion correlations appropriate to the milestone.
- Debug views are read-only projections of the canonical read model and expose source identities/unknown fields; they never query raw tables or repair state.
## 27. Performance and Scalability Considerations

- Baseline is one active run per workspace and linear first-eligible progression.
- Measure durable-query latency, scan cardinality, transaction duration, repository hashing, provider/effect waits, and restart reconstruction.
- Optimize indexes/batches only from evidence; concurrency and parallel scheduling remain deferred.

## 28. Security and Safety Considerations

- Treat repository files, config, imports, provider output, and human responses as untrusted typed inputs.
- Enforce resolved permission/sandbox/network ceilings; advisory evidence cannot elevate them.
- Journal destructive/external mutations as allowlisted effects with exact pre/postconditions.
- Fail closed on ambiguous authority, unsupported schema/profile, stale inputs, or unknown external outcome.

## 29. Documentation Updates

No human-facing documentation production is an implementation deliverable. Machine-consumed catalogs, schema manifests, CLI schemas, or generated registries change atomically with executable contracts and tests. Roadmap administration remains outside the software implementation plan.

## 30. Exit Criteria

- All components and typed contracts exist or are explicitly unavailable until their named milestone.
- Production routing uses the singular owner; no competing supported path remains.
- All success/failure/cancellation/unknown/pending/recovery/action paths are covered.
- All §19 checks and §21 demonstration pass; §22 evidence is reproducible.
- Inherited invariants remain valid and no future capability is falsely claimed.
- Roadmap owner accepts architectural closure, not merely implemented/green behavior.

## 31. Transition to Next Milestone

Stable handoff: Application queries and observers consume one projection; renderers/exports remain pure, every displayed claim traces to stable evidence, status is read-only, and conflicts/uncertainty/actions are visible rather than collapsed.
Dependencies satisfied for: M17 Roadmap convergence; M18 Plan convergence; M19 Execute convergence; M20 thin application queries.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

## Open Implementation Questions

- Owner must decide whether `.tmp` certification evidence gains a durable external release-evidence owner and how exact provider profiles are promoted/retired.

<!-- END GENERATED: milestone=M16 -->
