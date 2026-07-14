<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 milestone=M17 -->
# M17 — Roadmap capability convergence Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M17
- **Name:** Roadmap capability convergence
- **Implementation role:** Canonical authorities; Roadmap convergence gate
- **Roadmap position:** 18 of 22
- **Short description:** Route Traditional and Eval roadmap intents exclusively through the canonical catalog, kernel, products, effects, recovery, interaction, and read model so both produce the same validated producer-neutral `PreparedEpic` and `MilestoneSpecificationSet` contracts.
- **Primary outcome:** Both producers satisfy identical downstream product/gate contracts under canonical authorities; no new/recovery work enters the retained body, and after owner acceptance the legacy Roadmap implementation and obsolete last-only assets/readers are deleted.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/specs/epic.md`](epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M17), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0004-canonical-prompt-composition](../../docs/architecture/decisions/0004-canonical-prompt-composition.md), [0008-single-attempt-runtime-and-recovery-coordinator](../../docs/architecture/decisions/0008-single-attempt-runtime-and-recovery-coordinator.md), [0009-canonical-prompt-dispatch-gateway](../../docs/architecture/decisions/0009-canonical-prompt-dispatch-gateway.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Route Traditional and Eval roadmap intents exclusively through the canonical catalog, kernel, products, effects, recovery, interaction, and read model so both produce the same validated producer-neutral `PreparedEpic` and `MilestoneSpecificationSet` contracts.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Both unified public routes, common downstream products, active catalog transitions, full-chain evidence, and the retained Roadmap body exist; feature-local state machines/readers/prompt framing and three registered Eval prompt stubs may remain.
- Hard prerequisites: M16 Canonical Read Model; M14 kernel and M13 catalog.
- Not yet architecturally closed: Traditional roadmap convergence, Eval roadmap convergence, Roadmap legacy retirement.

## 6. Runtime / System State After

- Both producers satisfy identical downstream product/gate contracts under canonical authorities; no new/recovery work enters the retained body, and after owner acceptance the legacy Roadmap implementation and obsolete last-only assets/readers are deleted.
- Enforceable permanent property: Route Traditional and Eval roadmap intents exclusively through the canonical catalog, kernel, products, effects, recovery, interaction, and read model so both produce the same validated producer-neutral `PreparedEpic` and `MilestoneSpecificationSet` contracts.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Traditional roadmap convergence | Canonical authorities; Roadmap convergence gate | Traditional intent | Project Context and roadmap inputs | Validated producer-neutral prepared products | Only canonical lifecycle executes | Plan |
| Eval roadmap convergence | Canonical authorities; Roadmap convergence gate | Eval intent | Hypotheses/dependencies/eval products | Same prepared product schemas | Downstream cannot distinguish producer except provenance | Plan |
| Roadmap legacy retirement | Canonical authorities; Roadmap convergence gate | Retained executable specification | Parity and live evidence | Deleted feature owner/assets with no behavior loss | No alternate roadmap state machine/reader/send site | M21 |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Canonical authorities; Roadmap convergence gate | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Canonical authorities; Roadmap convergence gate rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Roadmap catalog declarations | Express both producer lifecycles completely | Catalog definitions only | Inputs and facts supplied by prompts/products/gates | M13 contracts | Typed collaboration boundary; no adjacent-owner semantics | prompts/products/gates | coverage tests |
| Canonical roadmap handlers/validators | Build/interpret/validate producer products without progression ownership | Candidate products/evidence | Inputs and facts supplied by Prompt/Product/Evaluation | kernel handler contracts | Typed collaboration boundary; no adjacent-owner semantics | Prompt/Product/Evaluation | producer tests |
| Producer-neutral contract adapter | Normalize both routes to shared product schemas | No hidden state | Inputs and facts supplied by M3 | PreparedEpic/MilestoneSpecificationSet contracts | Typed collaboration boundary; no adjacent-owner semantics | M3 | cross-producer tests |
| Legacy parity/removal seam | Compare then delete retained body | No new state | Inputs and facts supplied by Roadmap.Cli | test-only executable-spec adapter | Typed collaboration boundary; no adjacent-owner semantics | Roadmap.Cli | parity/deletion tests |

## 10. Repository and File Impact

- canonical roadmap declarations/handlers in orchestration primitives
- Eval prompt assets and prompt catalog
- shared product validators and fixtures
- `src/LoopRelay.Roadmap.Cli/` retained body removed after acceptance
- Roadmap component/full-chain certification tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Traditional/Eval invocation modes return the same downstream product contract with producer provenance.
- No public route reaches a feature-local roadmap engine.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Producer-specific logic may create candidates but never own progression, persistence, effects, recovery, or prompt framing.
- Deletion follows canonical parity and owner acceptance.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Prepared roadmap products | Product Authority | versioned product lifecycle | Canonical ledger or declared authoritative artifact | append-only versions | product IDs | shared schema/gates | retry through kernel | Plan |
| Producer provenance | History/Product | permanent | Canonical ledger or declared authoritative artifact | immutable | source workflow/attempt IDs | causal validation | retained | Read model |
| Legacy parity evidence | Certification | until deletion gate | Canonical ledger or declared authoritative artifact | immutable run evidence | case ID | independent observation | rerun | M21 |

## 14. Lifecycle and State Transitions

```text
Intent selected -> Canonical roadmap workflow -> Shared products validated/promoted -> Plan boundary -> Legacy body unreachable -> Deleted
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
| Hard prerequisite | M16 Canonical Read Model |
| Inherited capability | M14 kernel and M13 catalog |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M18 Plan convergence |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Producer schemas drift | cross-producer contract test | fail boundary transfer | fix handler/schema | semantic diff | Deterministic fault injection plus public-result regression |
| Eval prompt stub invoked | asset validation/runtime test | fail closed | implement accepted intent or remove declaration by ruling | prompt identity | Deterministic fault injection plus public-result regression |
| Legacy body still reachable | route scan | block acceptance | remove route then body | call path | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Both roadmap intents converge on the same products | Roadmap §§0.1, 3, 5 and M17 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No feature-local progression/prompt framing | Roadmap §§0.1, 3, 5 and M17 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Downstream behavior is producer-neutral | Roadmap §§0.1, 3, 5 and M17 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Legacy deletion follows parity | Roadmap §§0.1, 3, 5 and M17 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | producer handler/validator unit tests |
| Integration | cross-producer contract integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | canonical-only route/recovery tests; Traditional/Eval live chain tests; post-deletion build regression |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- Traditional new roadmap
- Eval roadmap update
- forced/default selection
- same semantic prepared epic from both producers
- registered Eval stub invocation
- legacy route reachability

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Run Traditional and Eval fixtures through the published CLI, inspect identical shared product schemas and Plan entry gates with distinct provenance, then prove no canonical route references the retained Roadmap engine before deleting it and rerunning tests.

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
| 1. Contract closure | Make Canonical authorities; Roadmap convergence gate boundaries explicit | Typed inputs, outputs, states, validators | M16 Canonical Read Model | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Both producers satisfy identical downstream product/gate contracts under canonical authorities; no new/recovery work enters the retained body, and after owner acceptance the legacy Roadmap implementation and obsolete last-only assets/readers are deleted.
Dependencies satisfied for: M18 Plan convergence.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

## Open Implementation Questions

- Owner must rule full-roadmap generation intent for reserved `Planning/CreateNewRoadmap` before wiring or deleting that asset.

<!-- END GENERATED: milestone=M17 -->
