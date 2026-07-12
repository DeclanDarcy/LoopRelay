<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M13 -->
# M13 — Workflow Catalog Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M13
- **Name:** Workflow Catalog
- **Implementation role:** Workflow Catalog
- **Roadmap position:** 14 of 22
- **Short description:** Replace the provisional repeated catalog with one immutable, versioned, fail-closed declaration authority for every workflow, stage, transition, product, schema, gate, validator, prompt, policy, runtime capability, input/output surface, effect, recovery path, successor, and terminal outcome.
- **Primary outcome:** One stable catalog/version is constructed once; every supported workflow resolves entirely from declarations, output surfaces structurally generate commit/push effects, changed obligations map to evidence, and adding a workflow requires no kernel branch.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M13), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0004-canonical-prompt-composition](../../docs/architecture/decisions/0004-canonical-prompt-composition.md), [0008-single-attempt-runtime-and-recovery-coordinator](../../docs/architecture/decisions/0008-single-attempt-runtime-and-recovery-coordinator.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Replace the provisional repeated catalog with one immutable, versioned, fail-closed declaration authority for every workflow, stage, transition, product, schema, gate, validator, prompt, policy, runtime capability, input/output surface, effect, recovery path, successor, and terminal outcome.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- The production catalog already declares four workflows, two chains, 24 stages, 46 transitions, typed products/gates/effects/postures, some input surfaces, and startup validation; identity is provisional, construction repeats, output surfaces and structural effects are incomplete, and sequencing remains partly hidden.
- Hard prerequisites: M12 Import Gateway; M9 Recovery Coordinator.
- Not yet architecturally closed: Stable catalog identity/version, Complete transition declaration, Structural output effects, Catalog-derived obligation ledger.

## 6. Runtime / System State After

- One stable catalog/version is constructed once; every supported workflow resolves entirely from declarations, output surfaces structurally generate commit/push effects, changed obligations map to evidence, and adding a workflow requires no kernel branch.
- Enforceable permanent property: Replace the provisional repeated catalog with one immutable, versioned, fail-closed declaration authority for every workflow, stage, transition, product, schema, gate, validator, prompt, policy, runtime capability, input/output surface, effect, recovery path, successor, and terminal outcome.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Stable catalog identity/version | Workflow Catalog | All workflow declarations | Immutable declaration set | Validated catalog snapshot and identity | Same semantic catalog has same identity; changes version explicitly | Composition/kernel/certification |
| Complete transition declaration | Workflow Catalog | Every supported transition | Products/surfaces/gates/prompts/policy/capabilities/effects/recovery/successors | Executable immutable definition | No hidden feature sequencing or mutation | M14 |
| Structural output effects | Workflow Catalog | Declared output surface | Repository topology and effect policy | Generated local commit and required push definitions | Workflow authors do not repeat Git effects | M8/M18 |
| Catalog-derived obligation ledger | Workflow Catalog | Accepted catalog | All declared obligations | Stable coverage denominator | Every changed obligation maps to executable evidence | Certification/M16 |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Workflow Catalog | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Workflow Catalog rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Canonical catalog builder | Construct one immutable versioned snapshot | Catalog snapshot | Inputs and facts supplied by declaration modules | catalog lookup contract | Typed collaboration boundary; no adjacent-owner semantics | declaration modules | identity/repeat-construction tests |
| Declaration schema/types | Represent complete workflow intent | No runtime mutable state | Inputs and facts supplied by prompt/policy/capability types | workflow/stage/transition/product/effect/recovery contracts | Typed collaboration boundary; no adjacent-owner semantics | prompt/policy/capability types | schema contract tests |
| Fail-closed catalog validator | Validate references, surfaces, cycles, ownership, terminal paths | Validation result | Inputs and facts supplied by catalog snapshot | startup validator | Typed collaboration boundary; no adjacent-owner semantics | catalog snapshot | negative catalog corpus |
| Structural effect synthesizer | Generate commit/push obligations from output surfaces | Derived immutable declarations | Inputs and facts supplied by M8 effect categories | catalog derivation contract | Typed collaboration boundary; no adjacent-owner semantics | M8 effect categories | coverage/idempotency tests |
| Obligation enumerator | Derive machine-consumed coverage ledger | Obligation snapshot | Inputs and facts supplied by validated catalog/assets/schema | enumeration API | Typed collaboration boundary; no adjacent-owner semantics | validated catalog/assets/schema | stable denominator tests |

## 10. Repository and File Impact

- replace/rename `CanonicalWorkflowDefinitionSketches.cs`
- extend `WorkflowContracts.cs` and `WorkflowDefinitionValidator.cs`
- composition constructs one catalog snapshot
- prompt asset and certification obligation registries
- catalog validator/coverage tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Catalog query exposes stable identity/version and immutable workflow/chain definitions.
- Production startup fails closed with all validation errors before commands run.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Kernel receives definitions only from the validated snapshot.
- Validators are owned typed references, not ungoverned strings.
- Derived effects are deterministic and included in catalog/obligation identity.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Catalog snapshot | Workflow Catalog | process/version lifetime | Rebuildable or process-scoped | immutable | catalog ID/version | complete reference validation | load prior accepted version | Composition/kernel |
| Workflow declaration | Workflow Catalog | catalog version | Versioned source/build artifact | immutable | stable workflow/stage/transition IDs | schema/graph validation | reject whole catalog | Kernel |
| Coverage obligation | Certification catalog | catalog version | Canonical ledger or declared authoritative artifact | immutable derived fact | obligation key | owner/source/evidence mapping | regenerate deterministically | M16/certification |

## 14. Lifecycle and State Transitions

```text
Declarations assembled -> Identity calculated -> Validated fail-closed -> Activated once -> Queried immutably -> Superseded by explicit version
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
| Hard prerequisite | M12 Import Gateway |
| Inherited capability | M9 Recovery Coordinator |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M14 Orchestration Kernel; M15 completion declarations; M17–M19 feature convergence |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Dangling product/gate/effect reference | validator | startup fails | fix declaration | path-qualified errors | Deterministic fault injection plus public-result regression |
| Hidden sequencing branch remains | architecture test | block M13 closure | express declaration/remove branch | reachability result | Deterministic fault injection plus public-result regression |
| Duplicate catalog construction disagrees | identity test | fail composition | single snapshot injection | identity diff | Deterministic fault injection plus public-result regression |
| Output surface lacks effects | derivation coverage | fail validation | declare surface/policy | transition ID | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| One production workflow catalog | Roadmap §§0.1, 3, 5 and M13 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Catalog validation fails closed | Roadmap §§0.1, 3, 5 and M13 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| All disk reads/writes declare surfaces | Roadmap §§0.1, 3, 5 and M13 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Universal mechanics remain outside workflows | Roadmap §§0.1, 3, 5 and M13 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Catalog obligations are deterministic | Roadmap §§0.1, 3, 5 and M13 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | declaration/type unit tests |
| Integration | invalid-catalog corpus and startup integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | identity/version regression tests; structural effect coverage tests; obligation ledger determinism tests |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- current four workflows/two chains
- dangling dependency
- duplicate identity
- undeclared output surface
- unsupported capability
- cycle/unreachable terminal
- changed single obligation

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Build the production catalog once, print its identity and obligation count, resolve both chains, then introduce a fixture transition with an undeclared output/effect and prove startup fails before any workspace access.

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
| 1. Contract closure | Make Workflow Catalog boundaries explicit | Typed inputs, outputs, states, validators | M12 Import Gateway | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: One stable catalog/version is constructed once; every supported workflow resolves entirely from declarations, output surfaces structurally generate commit/push effects, changed obligations map to evidence, and adding a workflow requires no kernel branch.
Dependencies satisfied for: M14 Orchestration Kernel; M15 completion declarations; M17–M19 feature convergence.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

<!-- END GENERATED: milestone=M13 -->
