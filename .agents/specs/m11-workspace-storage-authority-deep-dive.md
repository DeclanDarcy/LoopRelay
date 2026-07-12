<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M11 -->
# M11 — Workspace Storage Authority Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M11
- **Name:** Workspace Storage Authority
- **Implementation role:** Workspace Storage Authority
- **Roadmap position:** 12 of 22
- **Short description:** Close verify, init, export, sync, and storage-facing import coordination around one truthful, recoverable owner: verification is byte- and semantically read-only, every mutation has an explicit plan executed through effects/recovery, and command names/results match actual behavior.
- **Primary outcome:** Storage operations have domain-correct typed contracts; status/verify never repairs, mutation starts only from unambiguous inspection plus explicit authorization, export round-trips semantically, and direct application SQL is gone.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M11), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0001-logical-schema-v9](../../docs/architecture/decisions/0001-logical-schema-v9.md), [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md), [0011-thin-application-boundary](../../docs/architecture/decisions/0011-thin-application-boundary.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Close verify, init, export, sync, and storage-facing import coordination around one truthful, recoverable owner: verification is byte- and semantically read-only, every mutation has an explicit plan executed through effects/recovery, and command names/results match actual behavior.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Canonical-v9 inspection/convergence, receipts/fingerprints, narrow storage commands, and effect foundations exist, but verification/migration boundaries and labels are misleading, application code issues direct SQL, and interrupted mutation semantics are incomplete.
- Hard prerequisites: M8 Effect Coordinator; M10 Interaction Broker; M1 Workspace State Authority.
- Not yet architecturally closed: Read-only verification, Explicit storage mutation, Semantic export round-trip, Storage command truthfulness.

## 6. Runtime / System State After

- Storage operations have domain-correct typed contracts; status/verify never repairs, mutation starts only from unambiguous inspection plus explicit authorization, export round-trips semantically, and direct application SQL is gone.
- Enforceable permanent property: Close verify, init, export, sync, and storage-facing import coordination around one truthful, recoverable owner: verification is byte- and semantically read-only, every mutation has an explicit plan executed through effects/recovery, and command names/results match actual behavior.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Read-only verification | Workspace Storage Authority | Workspace schema/storage health | Path and bytes plus schema manifest | Typed health/compatibility result | Repeated verify changes neither bytes nor semantics | Application, read model |
| Explicit storage mutation | Workspace Storage Authority | Init/converge/export/sync | Validated operation plan and interaction authorization | Effect/recovery plan plus receipts | No mutation begins under ambiguous authority | M12, operator |
| Semantic export round-trip | Workspace Storage Authority | Canonical logical state | Export contract/schema | Portable export plus semantic fingerprint | Reimport-equivalent comparison passes | Import Gateway |
| Storage command truthfulness | Workspace Storage Authority | Public storage use cases | Typed request | Domain-correct result and exit meaning | Labels never claim work not performed | M20 CLI |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Workspace Storage Authority | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Workspace Storage Authority rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Storage inspector/verifier | Inspect bytes, identity/family/version/shape without writes | No mutable state | Inputs and facts supplied by workspace database/filesystem | StorageVerificationResult | Typed collaboration boundary; no adjacent-owner semantics | workspace database/filesystem | byte non-mutation tests |
| Storage operation planner | Validate and persist explicit mutation plans | Storage operation facts | Inputs and facts supplied by interaction/effects | init/export/sync plan contracts | Typed collaboration boundary; no adjacent-owner semantics | interaction/effects | ambiguity/interruption tests |
| Storage operation executors | Execute planned mutations only through effects | External/database target under effect protocol | Inputs and facts supplied by M8/M9 | typed executor/receipt | Typed collaboration boundary; no adjacent-owner semantics | M8/M9 | idempotency/recovery tests |
| Semantic exporter/comparator | Serialize logical state and verify round-trip equivalence | Export artifact/evidence | Inputs and facts supplied by M1/M12 | versioned export schema | Typed collaboration boundary; no adjacent-owner semantics | M1/M12 | round-trip/corruption tests |

## 10. Repository and File Impact

- `src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs`
- storage contracts in application/orchestration projects
- remove direct SQL/migration branches from `CanonicalCliApplicationService`
- effect executors and storage integration tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Typed verify/init/export/sync requests and results; import delegates source interpretation to M12.
- `status` and `verify` report required migration without performing it under the roadmap-recommended ruling.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Inspection interfaces are read-only by construction.
- Mutation planner persists intent before M8 execution; M9 owns interruption recovery.
- Observer consumes a storage projection, not raw tables.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Storage inspection | Workspace Storage Authority | request | Canonical ledger or declared authoritative artifact | immutable result | operation correlation | identity/family/version/shape | rerun safely | Application/read model |
| Storage operation plan | Workspace Storage Authority | until settled | Canonical ledger or declared authoritative artifact | immutable plan + append-only state | operation ID | unambiguous source/target/preconditions | M9 | M8 executor |
| Export package | Workspace Storage Authority | portable artifact lifetime | Durable versioned artifact | immutable/versioned | export ID/fingerprint | semantic validation | reimport/compare | M12/operator |

## 14. Lifecycle and State Transitions

```text
Uninspected -> Inspected -> Healthy / ActionRequired / Unsupported / Corrupt; explicit action: Planned -> Effecting -> Verified -> Completed / RecoveryRequired
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
| Hard prerequisite | M8 Effect Coordinator |
| Inherited capability | M10 Interaction Broker |
| Inherited capability | M1 Workspace State Authority |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M12 Import Gateway; M16 storage projection |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Verify mutates bytes | before/after byte hash | fail test and result | remove write path | changed-path diagnostic | Deterministic fault injection plus public-result regression |
| Unsupported/corrupt shape | read-only inspection | typed rejection; no mutation | explicit compatible import or repair outside runtime | shape/fingerprint | Deterministic fault injection plus public-result regression |
| Interrupted mutation | operation/effect journal | RecoveryRequired | M9 reconcile/resume | operation/effect IDs | Deterministic fault injection plus public-result regression |
| Export loses semantics | round-trip comparator | reject completion | fix schema/serializer | semantic diff | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Verification never mutates | Roadmap §§0.1, 3, 5 and M11 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Command label equals performed behavior | Roadmap §§0.1, 3, 5 and M11 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Mutation requires unambiguous authority and explicit plan | Roadmap §§0.1, 3, 5 and M11 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Direct application SQL is zero | Roadmap §§0.1, 3, 5 and M11 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Export equivalence is semantic, not file-format coincidence | Roadmap §§0.1, 3, 5 and M11 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | pure verifier unit tests |
| Integration | byte/semantic non-mutation integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | interrupted-operation recovery tests; export round-trip/migration tests; public command contract tests |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- healthy canonical-v9
- v8 migration-required
- recognized partial-v9
- unknown/corrupt v9
- interrupted init/sync
- full semantic export with null historical evidence

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Hash every workspace file, run verify/status twice, prove identical bytes and an ActionRequired result for v8, then explicitly plan convergence through effects, interrupt it, recover, export, and prove semantic round-trip equivalence.

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
| 1. Contract closure | Make Workspace Storage Authority boundaries explicit | Typed inputs, outputs, states, validators | M8 Effect Coordinator | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Storage operations have domain-correct typed contracts; status/verify never repairs, mutation starts only from unambiguous inspection plus explicit authorization, export round-trips semantically, and direct application SQL is gone.
Dependencies satisfied for: M12 Import Gateway; M16 storage projection.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

## Open Implementation Questions

- Owner must rule whether `status` is strictly read-only or may initiate migration; roadmap recommendation and this blueprint default to strictly read-only.

<!-- END GENERATED: milestone=M11 -->
