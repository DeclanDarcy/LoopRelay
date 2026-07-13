<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 milestone=M19 -->
# M19 — Execute capability convergence Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M19
- **Name:** Execute capability convergence
- **Implementation role:** Canonical authorities; Execute convergence gate
- **Roadmap position:** 20 of 22
- **Short description:** Route decision selection, implementation, handoff, publication, repository evaluation, stall handling, review, and certified completion exclusively through canonical authorities, preserving distinct outcomes and idempotent recovery across every provider and effect boundary.
- **Primary outcome:** Execute is restart-safe, effect/recovery/completion-authority driven, explainable from readiness to certified terminal state, performs no blind repeats, and after owner acceptance deletes `LoopRunner`, `ExecutionStep`, the legacy loop, and last-only consumers.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/specs/epic.md`](epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M19), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0005-execution-recommendations-as-evidence](../../docs/architecture/decisions/0005-execution-recommendations-as-evidence.md), [0008-single-attempt-runtime-and-recovery-coordinator](../../docs/architecture/decisions/0008-single-attempt-runtime-and-recovery-coordinator.md), [0009-canonical-prompt-dispatch-gateway](../../docs/architecture/decisions/0009-canonical-prompt-dispatch-gateway.md), [0010-execution-recommendations-as-causal-evidence](../../docs/architecture/decisions/0010-execution-recommendations-as-causal-evidence.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Route decision selection, implementation, handoff, publication, repository evaluation, stall handling, review, and certified completion exclusively through canonical authorities, preserving distinct outcomes and idempotent recovery across every provider and effect boundary.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Unified route, decision/warm recovery, implementation/handoff transitions, effect intents, completion/archive, and green full-chain campaigns exist; the legacy loop still owns progression and has the highest-risk recovery/policy/completion seams.
- Hard prerequisites: M18 Plan capability convergence; M15 Completion Authority.
- Not yet architecturally closed: Canonical decision routing, Recoverable implementation/handoff, Repository evaluation/stall handling, Certified Execute closure, Legacy loop retirement.

## 6. Runtime / System State After

- Execute is restart-safe, effect/recovery/completion-authority driven, explainable from readiness to certified terminal state, performs no blind repeats, and after owner acceptance deletes `LoopRunner`, `ExecutionStep`, the legacy loop, and last-only consumers.
- Enforceable permanent property: Route decision selection, implementation, handoff, publication, repository evaluation, stall handling, review, and certified completion exclusively through canonical authorities, preserving distinct outcomes and idempotent recovery across every provider and effect boundary.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Canonical decision routing | Canonical authorities; Execute convergence gate | ExecutionReadiness/current evidence | Decision products, governed recommendation/profile | Authorized next implementation transition | Decision agent output never owns policy/progression | Implementation |
| Recoverable implementation/handoff | Canonical authorities; Execute convergence gate | Authorized transition | Prompt/runtime/input surfaces | Validated candidates, effects, handoff facts | Unknown work reconciles before repeat | Next attempt/reviewer |
| Repository evaluation/stall handling | Canonical authorities; Execute convergence gate | Published implementation state | Git/product/evaluation evidence | Continue/review/specific cannot-proceed/stalled outcome | Stall is derived, not latched | Completion |
| Certified Execute closure | Canonical authorities; Execute convergence gate | Completed milestones/review | M15 decision and closure plan | Typed terminal/pending/recovery result | Every interruption boundary is idempotent | Application/operator |
| Legacy loop retirement | Canonical authorities; Execute convergence gate | Canonical parity evidence | Reachability and full-chain results | Deleted loop bodies/consumers | No alternate progression/policy fallback | M21 |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Canonical authorities; Execute convergence gate | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Canonical authorities; Execute convergence gate rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Execute catalog transitions | Declare decision/implement/handoff/evaluate/review/complete order | Catalog only | Inputs and facts supplied by Plan readiness/prompts | M13 definitions | Typed collaboration boundary; no adjacent-owner semantics | Plan readiness/prompts | coverage/order tests |
| Decision authorization adapter | Bind decision, recommendation evaluation, runtime profile, prompt and inputs | Authorization facts | Inputs and facts supplied by M5/M7 | ExecutionAuthorization | Typed collaboration boundary; no adjacent-owner semantics | M5/M7 | stale/ceiling tests |
| Implementation/handoff handlers | Interpret provider output into candidates only | Candidate/evidence facts | Inputs and facts supplied by M14 | kernel handler contracts | Typed collaboration boundary; no adjacent-owner semantics | M14 | unknown/cancel tests |
| Repository evaluator/stall classifier | Derive current state without latch | Evaluation facts | Inputs and facts supplied by M2/M16 | typed outcomes | Typed collaboration boundary; no adjacent-owner semantics | M2/M16 | stalled/no-change tests |
| Legacy parity/removal seam | Prove then remove loop progression and fallbacks | No new state | Inputs and facts supplied by CLI legacy services | test-only adapter | Typed collaboration boundary; no adjacent-owner semantics | CLI legacy services | reachability/deletion tests |

## 10. Repository and File Impact

- canonical Execute definitions/handlers
- decision/recommendation/runtime-profile stores and adapters
- repository evaluation and completion integration
- remove `LoopRunner.cs`, `ExecutionStep.cs`, legacy loop/policy fallbacks after acceptance
- Execute/full-chain/restart tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Execute run preserves waiting/failed/cancelled/stalled/recovery/effect/human/specific-cannot-proceed/certified results and IDs.
- No CLI/client method controls iteration or review order directly.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Decision recommendation is causal evidence only.
- Handlers produce candidates; kernel promotes and effects mutate.
- Completion is entirely M15; retries entirely M9.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Decision product/recommendation/evaluation | Product/Policy/History | decision version | Canonical ledger or declared authoritative artifact | immutable separate facts | causal IDs | freshness/profile ceilings | reevaluate/replan | Runtime |
| Implementation/handoff candidates | Product Authority | attempt | Canonical ledger or declared authoritative artifact | immutable candidates | product IDs | schemas/gates/freshness | new attempt | Evaluator |
| Execute progression facts | Kernel | run | Canonical ledger or declared authoritative artifact | append-only | causal spine | catalog order | M9 | Read model |
| Terminal completion | M15 | run terminal | Canonical ledger or declared authoritative artifact | immutable/monotonic | certificate/closure IDs | all effects settled | idempotent rerun | Application |

## 14. Lifecycle and State Transitions

```text
ExecutionReadiness -> Decision -> Authorization -> Implementation -> Handoff/publication -> Repository evaluation -> Continue / Stall / Review -> M15 completion -> Certified
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
| Hard prerequisite | M18 Plan capability convergence |
| Inherited capability | M15 Completion Authority |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M20 Application Boundary convergence |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Unknown provider/effect work | boundary evidence | RecoveryRequired; no repeat | M9 reconcile | attempt/dispatch/effect IDs | Deterministic fault injection plus public-result regression |
| Cancellation at partial effect | terminal/effect facts | Cancelled plus recovery plan as needed | salvage per ruling | preserved evidence | Deterministic fault injection plus public-result regression |
| Generic no-change latch | state scan/test | reject legacy progression | derive current stall outcome | current evidence | Deterministic fault injection plus public-result regression |
| Legacy policy fallback used | authorization assertion | fail closed | route M5/M7 | missing identity | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Execute progression is kernel/catalog-owned | Roadmap §§0.1, 3, 5 and M19 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Unknown work reconciles before repeat | Roadmap §§0.1, 3, 5 and M19 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Recommendations never authorize directly | Roadmap §§0.1, 3, 5 and M19 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Stall/cannot-proceed remain derived/specific | Roadmap §§0.1, 3, 5 and M19 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Completion uses M15 | Roadmap §§0.1, 3, 5 and M19 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | decision/evaluator unit tests |
| Integration | restart after every provider/effect integration boundary |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | cancellation/stall/outcome contract tests; both full-chain live campaigns; post-legacy-deletion regression |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- first execution
- continued implementation
- no-change/stall
- unknown dispatch
- partial publication
- cancel at each boundary
- specific completion obstacle
- already certified

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Run both chains from readiness, inject cancellation/unknown/failure after every provider and external-effect boundary, restart each, prove distinct outcomes and no duplicate work, reach certified completion, then prove the legacy loop is unreachable/deletable.

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
| 1. Contract closure | Make Canonical authorities; Execute convergence gate boundaries explicit | Typed inputs, outputs, states, validators | M18 Plan capability convergence | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Execute is restart-safe, effect/recovery/completion-authority driven, explainable from readiness to certified terminal state, performs no blind repeats, and after owner acceptance deletes `LoopRunner`, `ExecutionStep`, the legacy loop, and last-only consumers.
Dependencies satisfied for: M20 Application Boundary convergence.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

## Open Implementation Questions

- Owner must rule Execute first-run sequencing and review order; encode the ruling in M13 declarations, not feature branches.

<!-- END GENERATED: milestone=M19 -->
