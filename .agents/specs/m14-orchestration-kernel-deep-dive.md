<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 milestone=M14 -->
# M14 — Orchestration Kernel Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M14
- **Name:** Orchestration Kernel
- **Implementation role:** Orchestration Kernel
- **Roadmap position:** 15 of 22
- **Short description:** Replace bridge mechanics in place with one universal product-driven lifecycle—observe, resolve, gate, interact, authorize, dispatch, interpret, validate, freshness-check, atomically commit state/effect intents, reconcile, recover, chain, and project—used by every transition.
- **Primary outcome:** Every supported transition enters the same evidence-complete lifecycle from the validated M13 catalog; re-entry preserves root/workflow/transition lineage, required writes fail closed, clients and feature runners cannot advance state, and no alternate production kernel remains.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/specs/epic.md`](epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M14), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0008-single-attempt-runtime-and-recovery-coordinator](../../docs/architecture/decisions/0008-single-attempt-runtime-and-recovery-coordinator.md), [0009-canonical-prompt-dispatch-gateway](../../docs/architecture/decisions/0009-canonical-prompt-dispatch-gateway.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Replace bridge mechanics in place with one universal product-driven lifecycle—observe, resolve, gate, interact, authorize, dispatch, interpret, validate, freshness-check, atomically commit state/effect intents, reconcile, recover, chain, and project—used by every transition.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Resolver, single-attempt runtime, controller, chain runner, atomic promotion/effect intent, boundary evidence, workflow-instance records, and green full-chain campaigns exist, but fresh-attempt authorization, restart lineage, feature sequencing, effect walking, and required writes are not universally closed.
- Hard prerequisites: M13 Workflow Catalog; M10 Interaction Broker; M8/M9 effect and recovery authorities.
- Not yet architecturally closed: Universal transition lifecycle, Stable progression/chaining, Restart/re-entry, Atomic promotion/effect intent.

## 6. Runtime / System State After

- Every supported transition enters the same evidence-complete lifecycle from the validated M13 catalog; re-entry preserves root/workflow/transition lineage, required writes fail closed, clients and feature runners cannot advance state, and no alternate production kernel remains.
- Enforceable permanent property: Replace bridge mechanics in place with one universal product-driven lifecycle—observe, resolve, gate, interact, authorize, dispatch, interpret, validate, freshness-check, atomically commit state/effect intents, reconcile, recover, chain, and project—used by every transition.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Universal transition lifecycle | Orchestration Kernel | Every catalog transition | Invocation, catalog, authoritative observation | Typed terminal/pending/recovery result with evidence | No lifecycle phase may be bypassed | All workflows |
| Stable progression/chaining | Orchestration Kernel | Workflow and chain instances | Promoted product identities and boundary evidence | Next transition/workflow or typed stop | Root run preserved; new workflow-instance per successor | Plan/Execute |
| Restart/re-entry | Orchestration Kernel | Interrupted kernel run | Durable causal spine/effects/recovery/interactions | Same logical progression without lost object | Restart at every boundary is deterministic | Operator/certification |
| Atomic promotion/effect intent | Orchestration Kernel | Validated fresh candidates | Attempt facts and generated effects | Committed products/state/effect intents | No prompt output directly completes state | Read model/effects |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Orchestration Kernel | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Orchestration Kernel rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Kernel coordinator | Interpret catalog lifecycle without feature branches | No hidden workflow state | Inputs and facts supplied by catalog/observer/services | kernel command/result | Typed collaboration boundary; no adjacent-owner semantics | catalog/observer/services | phase-order tests |
| Single-attempt runtime | Execute one already-authorized attempt | Attempt/candidate/evidence facts | Inputs and facts supplied by prompt/runtime/evaluation | TransitionRuntime contract | Typed collaboration boundary; no adjacent-owner semantics | prompt/runtime/evaluation | boundary fault tests |
| Workflow controller/resolver | Select first eligible transition and reobserve after cycles | Resolution/boundary facts | Inputs and facts supplied by catalog/read models | controller/resolver contracts | Typed collaboration boundary; no adjacent-owner semantics | catalog/read models | progression tests |
| Chain runner | Transfer promoted products and preserve root lineage | Workflow-instance/boundary facts | Inputs and facts supplied by catalog/controller | chain contracts | Typed collaboration boundary; no adjacent-owner semantics | catalog/controller | restart/chain tests |
| Kernel read-model adapter | Return canonical projection identity, not ad hoc rendering | No authority state | Inputs and facts supplied by persistence projection | M16 query contract | Typed collaboration boundary; no adjacent-owner semantics | persistence projection | claim traceability tests |

## 10. Repository and File Impact

- `Runtime/TransitionRuntime.cs` and contracts
- `Chaining/WorkflowChaining.cs`
- `Resolution/WorkflowResolver.cs` and `RepositoryObserver.cs` seams
- production composition and application boundary
- kernel/restart/full-chain tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Typed run command returns outcome, evidence, pending effects, required action, and suggested exit semantics.
- No client command can select a private progression path or mark a transition complete.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Attempt runtime has no retry/recovery/chaining policy.
- Controller reobserves canonical state after attempt/effect cycle.
- All causally required writes fail closed; optional diagnostics cannot substitute.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Root/workflow/transition/attempt lineage | Kernel via State Authority | run lifetime | Canonical ledger or declared authoritative artifact | append-only lifecycle | causal spine | parent/current-state checks | M9 | Read model |
| Kernel decision | Orchestration Kernel | observation cycle | Canonical ledger or declared authoritative artifact | immutable boundary event | decision ID | catalog/eligibility evidence | replay/recompute | Chain runner |
| Candidate/promotion transaction | Product/State authorities | attempt | Canonical ledger or declared authoritative artifact | atomic append/projection | product/attempt IDs | gates/freshness | recover prior state | Effects/read model |

## 14. Lifecycle and State Transitions

```text
Observe -> Resolve -> Gate -> Interact? -> Authorize -> Dispatch -> Interpret -> Validate -> Freshness -> Atomic commit -> Effects -> Recover? -> Chain -> Read model
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
| Hard prerequisite | M13 Workflow Catalog |
| Inherited capability | M10 Interaction Broker |
| Inherited capability | M8/M9 effect and recovery authorities |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M15 Completion Authority; M17 Roadmap convergence; M18 Plan convergence; M19 Execute convergence |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Required persistence fails | store boundary | abort/RecoveryRequired; no advance | repair/reconcile | failed fact kind | Deterministic fault injection plus public-result regression |
| Input changes before promotion | freshness validator | reject candidate promotion | new authorized attempt | receipt hash diff | Deterministic fault injection plus public-result regression |
| No eligible transition | resolver | typed waiting/terminal/ambiguous outcome | interaction/recovery or stop | resolution explanation | Deterministic fault injection plus public-result regression |
| Alternate feature runner reachable | reachability test | block acceptance | route/delete alternate | call graph | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| One production orchestration kernel | Roadmap §§0.1, 3, 5 and M14 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Every attempt is already authorized and single-dispatch | Roadmap §§0.1, 3, 5 and M14 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No prompt result directly changes authoritative completion | Roadmap §§0.1, 3, 5 and M14 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Reobserve after effects | Roadmap §§0.1, 3, 5 and M14 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Root run and causal lineage are stable | Roadmap §§0.1, 3, 5 and M14 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | phase-order and pure resolver unit tests |
| Integration | atomic runtime/effect integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | restart at every durable boundary; success/non-success contract matrix; Traditional and Eval live full-chain certification |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- each terminal/nonterminal runtime outcome
- interaction required
- unknown provider dispatch
- partial effects
- freshness conflict
- both chain boundary paths
- restart snapshot per phase

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Run both production chains through the published boundary with fault injection after each durable phase, restart each case, and prove identical lineage/outcome with no duplicate provider/effect work and no feature-specific runner invocation.

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
| 1. Contract closure | Make Orchestration Kernel boundaries explicit | Typed inputs, outputs, states, validators | M13 Workflow Catalog | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Every supported transition enters the same evidence-complete lifecycle from the validated M13 catalog; re-entry preserves root/workflow/transition lineage, required writes fail closed, clients and feature runners cannot advance state, and no alternate production kernel remains.
Dependencies satisfied for: M15 Completion Authority; M17 Roadmap convergence; M18 Plan convergence; M19 Execute convergence.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

<!-- END GENERATED: milestone=M14 -->
