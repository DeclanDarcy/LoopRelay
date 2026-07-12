<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M8 -->
# M8 — Effect Coordinator Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M8
- **Name:** Effect Coordinator
- **Implementation role:** Effect Coordinator
- **Roadmap position:** 9 of 22; first open milestone after baseline ratification
- **Short description:** Close external mutation authority around one durable scanner, ordered coordinator, executor, and reconciliation protocol so every required mutation is idempotent, restart-discoverable, receipted, and settled before dependent progression or closure.
- **Primary outcome:** All required Git, filesystem, publication, archive, export, and projection mutations flow through one coordinator; local commits block the step, required pushes are asynchronous but block closure, and unknown effects reconcile before retry.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M8), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md), [0008-single-attempt-runtime-and-recovery-coordinator](../../docs/architecture/decisions/0008-single-attempt-runtime-and-recovery-coordinator.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Close external mutation authority around one durable scanner, ordered coordinator, executor, and reconciliation protocol so every required mutation is idempotent, restart-discoverable, receipted, and settled before dependent progression or closure.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains the supported model unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner once production routing and parity are proven.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Atomic effect intents, stable IDs/idempotency keys, ordering, append-only states, unknown classification, immediate in-process coordination, topology preflight, and effect-aware outcomes exist; workers, restart scanning, asynchronous pushes, structural auto-effects, and direct-mutation retirement remain.
- Hard prerequisites: Post-merge baseline ratification; M7 Runtime Authority; M1/M4 durable state and evidence.
- Unavailable before this milestone: Durable effect work discovery, Idempotent execution, Unknown reconciliation, Commit/push semantics as architecturally closed capabilities.

## 6. Runtime / System State After

- All required Git, filesystem, publication, archive, export, and projection mutations flow through one coordinator; local commits block the step, required pushes are asynchronous but block closure, and unknown effects reconcile before retry.
- Enforceable permanent property: Close external mutation authority around one durable scanner, ordered coordinator, executor, and reconciliation protocol so every required mutation is idempotent, restart-discoverable, receipted, and settled before dependent progression or closure.
- Capabilities still assigned to later milestones remain unavailable and must not be advertised.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Durable effect work discovery | Effect Coordinator | Planned/started/pending/failed/stalled/unknown intents | Canonical effect journal | Ordered work lease/selection | Restart discovers every unsettled required effect | Kernel, recovery |
| Idempotent execution | Effect Coordinator | One semantic external mutation | Intent, key, preconditions | Receipt plus observed postcondition | Repeated coordination performs at most one semantic mutation | All feature bodies |
| Unknown reconciliation | Effect Coordinator | Possibly completed effect | Durable attempt evidence and independent observation | Settled result or RecoveryRequired | No unknown is treated as not-started | M9, completion |
| Commit/push semantics | Effect Coordinator | Declared output surfaces | Catalog surface and repository topology | Blocking commit + required-async push intents | Closure waits for required pushes | M13–M19 |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Effect Coordinator | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Effect Coordinator rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | Effect result never becomes a state claim without receipt |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Recovery classifies/plans before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Effect journal scanner | Lease and order unsettled intents across restart | Intent/lease state | Inputs and facts supplied by canonical store | effect query/lease contracts | Typed collaboration boundary; no adjacent-owner semantics | canonical store | restart/concurrency tests |
| Effect coordinator | Drive legal states and dependency ordering | Effect lifecycle facts | Inputs and facts supplied by scanner/executors/reconciler | ITransitionEffectCoordinator | Typed collaboration boundary; no adjacent-owner semantics | scanner/executors/reconciler | state-machine tests |
| Typed executors | Perform one semantic mutation for Git/filesystem/archive/export/projection | External target only; no orchestration state | Inputs and facts supplied by infrastructure adapters | executor result/receipt | Typed collaboration boundary; no adjacent-owner semantics | infrastructure adapters | idempotency and fault tests |
| Effect reconciler | Compare receipts/postconditions to independent observation | Reconciliation facts | Inputs and facts supplied by observer/executors | typed reconciliation contract | Typed collaboration boundary; no adjacent-owner semantics | observer/executors | unknown/partial tests |
| Output-surface effect synthesizer | Derive commit/push intents structurally | No durable state before attempt transaction | Inputs and facts supplied by M13 declarations | catalog-to-effect contract | Typed collaboration boundary; no adjacent-owner semantics | M13 declarations | coverage tests |

## 10. Repository and File Impact

- `Runtime/TransitionEffectCoordinator.cs`
- canonical effect stores and workspace schema
- new executor/reconciliation modules under orchestration/infrastructure
- feature mutation sites in CLI/Completion/legacy bodies
- effect and certification tests

Expected tests remain in the matching `tests/LoopRelay.*.Tests/` projects. Generated runtime/certification cases stay under `.tmp/certification/`; durable product files remain on their roadmap-defined surfaces.

## 11. Public Contracts

- Application results expose pending/unknown/failed required effects with stable identities.
- No feature API reports publication or closure before the corresponding receipt/postcondition.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Intent insertion shares the authoritative state transaction; execution occurs afterward.
- One intent represents one semantic mutation and has one idempotency key.
- Unknown always enters reconciliation before an executor may repeat.

- Required causal writes complete before the boundary they authorize.
- Retried coordination is idempotent; retries of uncertain external work require recovery authorization.
- Rebuildable projections consume authoritative facts and never write back implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Effect intent | Effect Coordinator | until terminal | Canonical ledger or declared authoritative artifact | append-only lifecycle | effect ID + idempotency key | category/order/preconditions | scanner resumes | recovery/read model |
| Execution receipt | Effect Coordinator | permanent | Canonical ledger or declared authoritative artifact | immutable | receipt ID | exact postcondition/correlation | independent reconcile | certification |
| Lease | Effect Coordinator | bounded worker ownership | Durable coordination record | renewable/expiring | intent + worker | compare-and-set | expiry returns to scanner | coordinator |

## 14. Lifecycle and State Transitions

```text
Planned -> Started -> Pending / Completed / Failed / Stalled / Cancelled / Unknown; Unknown -> Reconciling -> Completed / RetryAuthorized / HumanActionRequired
```

| Transition rule | Trigger | Preconditions | Result | Failure/evidence |
|---|---|---|---|---|
| Enter | Typed request or discovered durable work | Hard prerequisites and authority are unambiguous | Initial durable fact/state | Reject before side effects; diagnostic names missing prerequisite |
| Advance | Validated current evidence | Fresh inputs, legal prior state, required writes available | Next durable state and evidence | Preserve prior state and append failure evidence |
| Settle | Terminal observation/postcondition | Required effects and evidence complete | Typed terminal result | Unknown/partial transfers to Recovery, not success |

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

Startup fails closed on invalid composition. Normal operation follows the sequence above. Failure preserves the last durable boundary. Recovery starts from ledger evidence. Completion/shutdown writes terminal evidence with a non-cancelled token when caller cancellation has already fired.

## 16. Dependency Closure

| Classification | Dependency |
|---|---|
| Hard prerequisite | Post-merge baseline ratification |
| Inherited capability | M7 Runtime Authority |
| Inherited capability | M1/M4 durable state and evidence |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit component suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M9 Recovery Coordinator; M11 Storage Authority; M13 structural effects; M14 kernel; M15 completion |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Crash after mutation before receipt | restart scanner sees Started/Unknown | reconcile; do not repeat | observe postcondition then settle | intent/key/target diagnostic | Deterministic fault injection plus public-result regression |
| Duplicate coordination | idempotency check | return existing receipt | none | duplicate-attempt metric | Deterministic fault injection plus public-result regression |
| Push unavailable | executor result | required async remains pending; closure blocked | retry under policy | remote/ref diagnostic | Deterministic fault injection plus public-result regression |
| Order dependency violated | coordinator assertion | do not execute dependent intent | settle predecessor first | dependency graph | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Every required external mutation is an effect | Roadmap §§0.1, 3, 5 and M8 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Effects are not state claims | Roadmap §§0.1, 3, 5 and M8 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Unknown is not not-started | Roadmap §§0.1, 3, 5 and M8 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Required effects settle before closure | Roadmap §§0.1, 3, 5 and M8 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| One semantic mutation per intent | Roadmap §§0.1, 3, 5 and M8 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | state-machine unit tests |
| Integration | executor contract/idempotency tests |
| Contract | Public/internal contracts in §§11–12, including typed outcome preservation |
| Regression | restart at every effect boundary; independent postcondition integration tests; M8/M11/M13/M14 certification scenarios |
| Replay/rebuild | Restart at each durable boundary; authoritative facts rebuild projections without semantic drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative catalog/workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- local commit success
- remote push unavailable
- mutation succeeds then receipt write fails
- duplicate scanner lease
- partial ordered closure plan
- independent nested `.agents` repository

- Cross-component fixture: production-like repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every state boundary in §14.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Plan a local commit and required push, crash after the commit but before its receipt, restart the scanner, reconcile the existing commit without creating another, then leave push pending until the remote is available and prove closure remains disallowed.

**Execution checks:** invoke the published production composition where practical; inspect typed result, canonical ledger facts, independent repository observation, and process exit semantics.

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
- Machine-generated test results for all §19 categories and failure boundaries.
- Durable ledger/effect/recovery receipts whose identities appear in the public result or read model.
- Independent repository/provider observation agreeing with claimed state.
- Applicable deterministic and live certification campaign outputs from roadmap §6; green campaigns do not substitute for owner/authority closure.
- Baseline obligation changes mapped to executable tests or certification evidence.

## 23. Implementation Plan

| Step | Purpose | Deliverables | Dependencies | Completion criterion |
|---|---|---|---|---|
| 1. Contract closure | Make Effect Coordinator boundaries explicit | Typed inputs, outputs, states, and validators | Post-merge baseline ratification | Invalid/ambiguous inputs fail before mutation |
| 2. Durable state closure | Persist authoritative facts and legal transitions | Store/schema adapters and atomic operations | Step 1, M1/M4 | Restart reproduces the same classification/state |
| 3. Production routing | Route the supported application path through the owner | Composition wiring and typed result propagation | Steps 1–2 | No supported request bypasses the owner |
| 4. Failure/recovery closure | Implement/reconcile every §17 boundary | Failure evidence and recovery handoff | Step 3; M8/M9 where available | Unknown/partial/cancelled remain distinct and replayable |
| 5. Alternate-path retirement | Remove the superseded production behavior after parity | Deleted or unreachable competing bodies/assets | Steps 3–4 | Deletion changes no supported behavior |
| 6. Acceptance closure | Execute §19–§22 checks | Reproducible machine evidence | Steps 1–5 | Permanent property and inherited invariants pass |

For accepted M0–M7 specifications, these steps are preservation gates only; the accepted implementation is not replayed. Any remaining convergence is performed by its named later owner.

## 24. Parallel Work Opportunities

| Lane | Scope | Owner type | Dependencies | Synchronization point | Integration risk |
|---|---|---|---|---|---|
| Contracts/validators | Typed models, legal transitions, deterministic validation | Domain/core engineer | Normative decisions | Before store and composition integration | Contract drift if merged late |
| Persistence/replay | Schema adapters, atomic writes, reconstruction fixtures | Persistence engineer | Stable contracts | Production routing integration | False evidence or migration drift |
| Production/certification | Composition, public outcomes, failure fixtures | Integration engineer | Contracts plus store | Acceptance demonstration | Green behavior may hide alternate authority |

Shared-state changes and production routing serialize at the synchronization point; do not parallelize competing schema or composition owners.

## 25. Risks and Mitigations

| Class | Risk | Impact / likelihood | Earliest detection | Mitigation | Fallback |
|---|---|---|---|---|---|
| Architectural | Bridge becomes permanent second owner | High / medium | Composition/reachability tests | Route one owner, name retirement gate | Keep adapter one-way and fail closed |
| Data | Required fact and outward action diverge | High / medium | Fault injection at boundary | Atomic fact/intent then reconcile | RecoveryRequired; never fabricate success |
| Integration | Typed outcome collapses at application/CLI | High / medium | Contract matrix | Preserve discriminated outcomes and exit meaning | Reject generic mapping |
| Testing | Green campaign misses duplicate authority | High / medium | Static ownership/reachability checks | Pair behavior evidence with closure inspection | Block acceptance |
| Performance | Unbounded ledger/catalog scan | Medium / low | Smoke metrics | Stable indexes, bounded queries, resume cursor | Defer optimization until measured |
| Security | Untrusted files/provider text influence policy or mutation | High / medium | Validation and permission tests | Typed schemas, hash-covered prompts, resolved policy, effect allowlists | Fail closed |

## 26. Observability and Diagnostics

- Structured logs include workspace, root run, workflow instance, transition run, attempt, session/turn, and owner-specific identities when present.
- Metrics count state transitions, rejections, unknown/recovery-required outcomes, duplicate coordination, and latency without treating counters as authority.
- Health checks report dependency availability and unsettled required work without mutating state.
- Diagnostic/read-model claims link to durable evidence, effect, recovery, interaction, or policy identities; secrets and provider payloads are scrubbed at external boundaries.

- Audit records preserve append-only lifecycle, policy, prompt, effect, recovery, interaction, and completion correlations appropriate to the milestone.
- Debug views are read-only projections of the canonical read model and expose source identities/unknown fields; they never query raw tables or repair state.
## 27. Performance and Scalability Considerations

- Baseline: one active run per workspace and linear first-eligible progression.
- Measure durable-query latency, scan cardinality, transaction duration, provider/effect wait time, and restart reconstruction time.
- Likely bottlenecks are the canonical SQLite ledger, repository hashing/observation, provider turns, and remote Git effects.
- Add indexes/batching only from measurements; concurrency, parallel scheduling, and cross-workspace scale remain deferred.

## 28. Security and Safety Considerations

- Treat repository files, configuration, imported data, provider output, and human responses as untrusted typed inputs.
- Enforce resolved permission/sandbox/network ceilings; recommendations and prompt payloads cannot elevate them.
- Journal destructive or external mutations as allowlisted effects with exact preconditions and postconditions.
- Hash and correlate evidence without exposing credentials; cancellation and crash paths must preserve terminal evidence.
- Fail closed on ambiguous authority, unsupported schema/profile, stale inputs, or unknown external outcome.

## 29. Documentation Updates

No human-facing documentation production is an implementation deliverable for this milestone. If a machine-consumed catalog, schema manifest, CLI schema, or generated contract registry changes, update it atomically with the executable contract and test it. The roadmap changes only when a durable ruling changes; that administrative update is outside the software implementation plan.

## 30. Exit Criteria

- All required components and typed public/internal contracts exist or are explicitly unavailable until their named milestone.
- Production routing uses the singular owner; no competing supported path remains.
- All success, failure, cancellation, unknown, pending, recovery, and required-action paths are covered.
- Unit, integration, contract, regression, replay, failure, performance-smoke, and acceptance checks pass.
- Certification evidence in §22 is reproducible and inherited invariants remain valid.
- No future milestone capability is falsely claimed.
- The roadmap owner accepts the milestone as architecturally closed, not merely implemented or green.

## 31. Transition to Next Milestone

Stable handoff: All required Git, filesystem, publication, archive, export, and projection mutations flow through one coordinator; local commits block the step, required pushes are asynchronous but block closure, and unknown effects reconcile before retry.
Dependencies satisfied for: M9 Recovery Coordinator; M11 Storage Authority; M13 structural effects; M14 kernel; M15 completion.
Remaining limitations stay owned by their named future milestones. Any temporary adapter is one-way, observable, and removed at its declared gate; unresolved risks transfer with durable evidence rather than hidden state.

<!-- END GENERATED: milestone=M8 -->
