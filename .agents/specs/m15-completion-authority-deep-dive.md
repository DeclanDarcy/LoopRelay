<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 milestone=M15 -->
# M15 — Completion Authority Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M15
- **Name:** Completion Authority
- **Implementation role:** Completion Authority
- **Roadmap position:** 16 of 22
- **Short description:** Create one typed evidence-complete completion decision and one durable ordered closure plan whose archive, context update, Git publication, checkpoint cleanup, and terminal-state mutations execute exclusively through M8 effects and recover through M9.
- **Primary outcome:** Certified, specific cannot-proceed, failed, cancelled, effect-pending, and recovery-required remain distinct from decision through persistence/read model/exit code; certified closure occurs only after all required effects settle and terminal rerun is zero-model/idempotent.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/specs/epic.md`](epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M15), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md), [0008-single-attempt-runtime-and-recovery-coordinator](../../docs/architecture/decisions/0008-single-attempt-runtime-and-recovery-coordinator.md), [0009-canonical-prompt-dispatch-gateway](../../docs/architecture/decisions/0009-canonical-prompt-dispatch-gateway.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Create one typed evidence-complete completion decision and one durable ordered closure plan whose archive, context update, Git publication, checkpoint cleanup, and terminal-state mutations execute exclusively through M8 effects and recover through M9.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Completion review/prompts, execution evidence, archive materializer, certification checkpoint, terminal cleanup, and idempotent zero-model rerun exist, but multiple services decide completion and feature bodies perform archive/Git/cleanup mutations directly.
- Hard prerequisites: M14 Orchestration Kernel; M8 Effect Coordinator; M9 Recovery Coordinator.
- Not yet architecturally closed: Typed completion decision, Durable closure plan, Certified settlement, Idempotent terminal rerun.

## 6. Runtime / System State After

- Certified, specific cannot-proceed, failed, cancelled, effect-pending, and recovery-required remain distinct from decision through persistence/read model/exit code; certified closure occurs only after all required effects settle and terminal rerun is zero-model/idempotent.
- Enforceable permanent property: Create one typed evidence-complete completion decision and one durable ordered closure plan whose archive, context update, Git publication, checkpoint cleanup, and terminal-state mutations execute exclusively through M8 effects and recover through M9.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Typed completion decision | Completion Authority | Terminal candidate | Products, gates, review/evidence, obstacle vocabulary | Certificate/failure/specific-cannot-proceed decision | Exactly one authority decides; reasons are preserved | Closure planner/read model |
| Durable closure plan | Completion Authority | Accepted certificate | Archive/context/publication/cleanup/terminal operations | Ordered effect plan | All required operations are explicit and recoverable | M8/M9 |
| Certified settlement | Completion Authority | Closure plan effects | Receipts and independent observations | Certified terminal fact | No required effect incomplete/unknown | Application/operator |
| Idempotent terminal rerun | Completion Authority | Already certified workspace | Terminal facts/effect receipts | Same terminal result, zero model sends/mutations | Second run is observational only | Certification |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Completion Authority | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Completion Authority rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Completion decision service | Evaluate canonical evidence and emit one typed decision | Completion decision/certificate facts | Inputs and facts supplied by kernel/products/evaluation | completion contract | Typed collaboration boundary; no adjacent-owner semantics | kernel/products/evaluation | outcome matrix tests |
| Closure plan builder | Translate certificate into ordered effect intents | Closure plan facts | Inputs and facts supplied by catalog/M8 | closure-plan contract | Typed collaboration boundary; no adjacent-owner semantics | catalog/M8 | ordering/coverage tests |
| Completion effect executors | Archive, context update, commits, pushes, cleanup, terminal projection | External targets only | Inputs and facts supplied by M8/infrastructure | typed executor receipts | Typed collaboration boundary; no adjacent-owner semantics | M8/infrastructure | idempotency/fault tests |
| Completion projection | Expose decision, plan, pending work, terminal evidence | Rebuildable projection | Inputs and facts supplied by canonical stores | M16 contract | Typed collaboration boundary; no adjacent-owner semantics | canonical stores | claim traceability tests |

## 10. Repository and File Impact

- `src/LoopRelay.Completion/` contracts/services
- completion seams in `UnifiedCliComposition.cs` and CLI execution services
- canonical completion/effect persistence
- remove direct `CommitGate`, archive, Git, and cleanup mutation sites
- completion and full-chain certification tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Completion command/result preserves the full typed outcome vocabulary and evidence IDs.
- Certified status is unavailable until required asynchronous pushes and every closure effect settle.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Decision service performs no external mutation.
- Closure plan is persisted before effect execution.
- Partial closure always hands durable state to M9; rerun reconciles rather than repeats.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Completion decision/certificate | Completion Authority | terminal candidate lifetime | Canonical ledger or declared authoritative artifact | immutable | completion ID | evidence/gate/vocabulary validation | supersede via new attempt only | Planner/read model |
| Closure plan | Completion Authority | until settled | Canonical ledger or declared authoritative artifact | immutable plan + effect states | closure ID | complete ordered operations | M9 | M8 |
| Certified terminal fact | Completion Authority/State | workspace run terminal | Canonical ledger or declared authoritative artifact | monotonic | certificate + closure receipt set | all required postconditions | read-only rerun | Application |

## 14. Lifecycle and State Transitions

```text
Candidate -> Evaluated -> CertifiedDecision / SpecificCannotProceed / Failed / Cancelled; CertifiedDecision -> ClosurePlanned -> EffectsPending -> Settled -> CertifiedTerminal
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
| Hard prerequisite | M14 Orchestration Kernel |
| Inherited capability | M8 Effect Coordinator |
| Inherited capability | M9 Recovery Coordinator |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M16 Canonical Read Model; M19 Execute closure; M20 typed exit mapping |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Archive succeeds, push fails | effect receipts | EffectsPending; not certified | M9/M8 resume push | closure/effect IDs | Deterministic fault injection plus public-result regression |
| Generic obstacle erases cause | contract test | reject decision mapping | map/ratify vocabulary | source outcome | Deterministic fault injection plus public-result regression |
| Cleanup runs before publication | closure validator | reject plan | fix ordering | dependency path | Deterministic fault injection plus public-result regression |
| Terminal rerun sends model | dispatch counter | fail acceptance | terminal short-circuit from facts | session/turn delta | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| One completion decision owner | Roadmap §§0.1, 3, 5 and M15 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Closure uses effects exclusively | Roadmap §§0.1, 3, 5 and M15 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Certified implies every required effect settled | Roadmap §§0.1, 3, 5 and M15 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Specific outcomes survive all layers | Roadmap §§0.1, 3, 5 and M15 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Terminal rerun is idempotent and zero-model | Roadmap §§0.1, 3, 5 and M15 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | decision vocabulary unit tests |
| Integration | closure ordering/effect integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | partial failure/recovery tests; exit/read-model contract tests; terminal rerun and live-chain certification |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- certified evidence set
- each specific obstacle
- cancel before/after decision
- archive success/push failure
- unknown Git effect
- already-certified workspace

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Drive Execute to a valid completion decision, fail the required push after archive/commit, verify EffectPending rather than Certified, restart and settle the push through recovery, then rerun and prove zero new sessions, effects, user-tree changes, or Git changes.

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
| 1. Contract closure | Make Completion Authority boundaries explicit | Typed inputs, outputs, states, validators | M14 Orchestration Kernel | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Certified, specific cannot-proceed, failed, cancelled, effect-pending, and recovery-required remain distinct from decision through persistence/read model/exit code; certified closure occurs only after all required effects settle and terminal rerun is zero-model/idempotent.
Dependencies satisfied for: M16 Canonical Read Model; M19 Execute closure; M20 typed exit mapping.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

## Open Implementation Questions

- Owner must rule typed obstacle mapping, partial-effect failure semantics, and resume/cleanup behavior before closure.

<!-- END GENERATED: milestone=M15 -->
