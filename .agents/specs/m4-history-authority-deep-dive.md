<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M4 -->
# M4 — History Authority Deep Dive

Status: Accepted at `b1b9aa8a`; projection consumers still converge later

## 1. Milestone Summary

- **Identifier:** M4
- **Name:** History Authority
- **Implementation role:** History Authority
- **Roadmap position:** 5 of 22; accepted boundary
- **Short description:** Preserve append-only logical history and evidence identity in the canonical ledger, keep numbered files as projections/import sources only, and order facts by ledger insertion rather than clocks or ULIDs.
- **Primary outcome:** Ledger-backed loop history, causal lineage, durable chain boundaries, retrievable evidence, append-only backfill, and projection demotion are accepted; M16 must make canonical consumers use the projection.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M4), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0003-logical-history-authority](../../docs/architecture/decisions/0003-logical-history-authority.md), [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | Accepted commit `b1b9aa8a`; current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Preserve append-only logical history and evidence identity in the canonical ledger, keep numbered files as projections/import sources only, and order facts by ledger insertion rather than clocks or ULIDs.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains the supported model unless the roadmap explicitly changes it.
- Do not reimplement or reopen the accepted milestone; change only what is required to preserve its property under the post-merge baseline.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- M3 created receipts, but loop history and some evidence remained file-backed or weakly linked and chain boundaries lacked durable facts.
- Hard prerequisites: M3 Product Authority.
- Unavailable before this milestone: Append-only logical history, Durable chain boundary evidence, Projection effect model as architecturally closed capabilities.

## 6. Runtime / System State After

- Ledger-backed loop history, causal lineage, durable chain boundaries, retrievable evidence, append-only backfill, and projection demotion are accepted; M16 must make canonical consumers use the projection.
- Enforceable permanent property: Preserve append-only logical history and evidence identity in the canonical ledger, keep numbered files as projections/import sources only, and order facts by ledger insertion rather than clocks or ULIDs.
- Capabilities still assigned to later milestones remain unavailable and must not be advertised.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Append-only logical history | History Authority | Execution and decision facts | Causal event plus evidence set | Immutable history fact and evidence identity | Corrections supersede; facts are not rewritten | Recovery, completion, read model |
| Durable chain boundary evidence | History Authority | Workflow handoff decisions | Root run, workflow instances, products | Boundary event | Each chain decision is replayable | Kernel, certification |
| Projection effect model | History Authority | Filesystem representation of facts | Committed fact/evidence plus effect intent | Retryable projection state | Fact precedes projection | Effects, exports |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | History Authority | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | History Authority rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | Effect result never becomes a state claim without receipt |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Recovery classifies/plans before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| History ledger store | Append and retrieve ordered facts | History/evidence sets | Inputs and facts supplied by M1 state authority | History interfaces | Typed collaboration boundary; no adjacent-owner semantics | M1 state authority | ordering and immutability tests |
| Boundary evidence store | Persist chain choices and transferred products | Boundary facts | Inputs and facts supplied by Workflow instances | Chain boundary contracts | Typed collaboration boundary; no adjacent-owner semantics | Workflow instances | restart/replay tests |
| History projection adapter | Materialize compatibility files only as effects | Projection effect state | Inputs and facts supplied by M8 coordinator | Projection contracts | Typed collaboration boundary; no adjacent-owner semantics | M8 coordinator | failure/retry tests |

## 10. Repository and File Impact

- canonical history/evidence tables and stores
- `LedgerLoopHistoryStore.cs`
- chain boundary persistence in orchestration primitives
- evidence retrieval and projection tests

Expected tests remain in the matching `tests/LoopRelay.*.Tests/` projects. Generated runtime/certification cases stay under `.tmp/certification/`; durable product files remain on their roadmap-defined surfaces.

## 11. Public Contracts

- History queries return stable fact/evidence identities and ledger order.
- Filesystem history files are never selected as a parallel runtime authority.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Each fact owns one stable evidence set; items retain distinct kinds.
- Projection failure cannot roll back or outrank the committed fact.
- Historical unobserved values remain null.

- Required causal writes complete before the boundary they authorize.
- Retried coordination is idempotent; retries of uncertain external work require recovery authorization.
- Rebuildable projections consume authoritative facts and never write back implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| History fact | History Authority | permanent | Canonical ledger or declared authoritative artifact | immutable/superseding | `hist_` | schema + causal lineage | ledger replay | recovery/read model |
| Evidence set/item | History Authority | permanent | Canonical ledger or declared authoritative artifact | append-only | stable evidence IDs | kind/schema/correlation | retain | cert/recovery |
| Projection effect | Effect Coordinator | until settled | Rebuildable from authoritative facts | state machine | effect identity | postcondition | reconcile/retry | compatibility readers |

## 14. Lifecycle and State Transitions

```text
Fact prepared -> Fact and evidence committed -> Projection intent planned -> Projection settled or recoverable
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
| Hard prerequisite | M3 Product Authority |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit component suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M5 policy provenance; M8 effect evidence; M9 recovery classification; M16 evidence-linked status |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Projection write fails | effect result | fact remains authoritative; effect pending | M8 retry/reconcile | effect receipt | Deterministic fault injection plus public-result regression |
| Out-of-order clock/ULID | ledger query | ignore external order | use insertion order | ordering assertion | Deterministic fault injection plus public-result regression |
| Legacy history malformed | import validator | do not fabricate fact | actionable import failure | source record diagnostic | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Facts are append-only | Roadmap §§0.1, 3, 5 and M4 | Regression boundary and later owner wiring | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Ledger insertion order is authoritative | Roadmap §§0.1, 3, 5 and M4 | Regression boundary and later owner wiring | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Every fact has stable evidence identity | Roadmap §§0.1, 3, 5 and M4 | Regression boundary and later owner wiring | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Projection never outranks fact | Roadmap §§0.1, 3, 5 and M4 | Regression boundary and later owner wiring | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | append/ordering unit tests |
| Integration | legacy backfill tests |
| Contract | Public/internal contracts in §§11–12, including typed outcome preservation |
| Regression | projection failure tests; chain-boundary replay tests |
| Replay/rebuild | Restart at each durable boundary; authoritative facts rebuild projections without semantic drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative catalog/workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- same-millisecond ULIDs
- legacy numbered history
- projection filesystem failure
- two-chain boundary trace
- evidence retrieval by content hash

- Cross-component fixture: production-like repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every state boundary in §14.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Append two facts with intentionally misleading timestamps, project one to the filesystem, fail the second projection, and show ledger order and both facts remain correct while only the second effect is pending.

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
| 1. Contract closure | Make History Authority boundaries explicit | Typed inputs, outputs, states, and validators | M3 Product Authority | Invalid/ambiguous inputs fail before mutation |
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
- The accepted property remains intact under post-merge baseline reconciliation; no re-acceptance is implied.

## 31. Transition to Next Milestone

Stable handoff: Ledger-backed loop history, causal lineage, durable chain boundaries, retrievable evidence, append-only backfill, and projection demotion are accepted; M16 must make canonical consumers use the projection.
Dependencies satisfied for: M5 policy provenance; M8 effect evidence; M9 recovery classification; M16 evidence-linked status.
Remaining limitations stay owned by their named future milestones. Any temporary adapter is one-way, observable, and removed at its declared gate; unresolved risks transfer with durable evidence rather than hidden state.

<!-- END GENERATED: milestone=M4 -->
