<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M12 -->
# M12 — Import Gateway Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M12
- **Name:** Import Gateway
- **Implementation role:** Import Gateway
- **Roadmap position:** 13 of 22
- **Short description:** Close one explicit one-way boundary for every supported owner workspace: read-only detect and preview, map domain identities, import transactionally, verify semantic fidelity, persist a receipt, mark legacy state non-authoritative, and run canonical-only afterward.
- **Primary outcome:** Each enumerated owned format either imports with accepted fidelity and durable receipt or fails with an actionable ambiguity/conflict report; runtime never falls back or dual-writes after import, and exhausted adapters retire.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M12), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0001-logical-schema-v9](../../docs/architecture/decisions/0001-logical-schema-v9.md), [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Close one explicit one-way boundary for every supported owner workspace: read-only detect and preview, map domain identities, import transactionally, verify semantic fidelity, persist a receipt, mark legacy state non-authoritative, and run canonical-only afterward.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Logical-v9 compatibility convergence, a legacy-continuity importer, compatibility operation tables, and retained readers exist, but the complete owned workspace portfolio, preview/conflict semantics, and adapter retirement are unresolved.
- Hard prerequisites: M11 Workspace Storage Authority.
- Not yet architecturally closed: Read-only source detection, Import preview, Transactional one-way import, Semantic verification/retirement.

## 6. Runtime / System State After

- Each enumerated owned format either imports with accepted fidelity and durable receipt or fails with an actionable ambiguity/conflict report; runtime never falls back or dual-writes after import, and exhausted adapters retire.
- Enforceable permanent property: Close one explicit one-way boundary for every supported owner workspace: read-only detect and preview, map domain identities, import transactionally, verify semantic fidelity, persist a receipt, mark legacy state non-authoritative, and run canonical-only afterward.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Read-only source detection | Import Gateway | Owned legacy workspaces | Filesystem/database source | Version, portfolio match, conflicts, unsupported facts | Detection performs no writes | Preview/operator |
| Import preview | Import Gateway | Detected source | Parsed domain facts and target schema | Identity mapping and semantic delta | Ambiguity is explicit; no guessing | Interaction/application |
| Transactional one-way import | Import Gateway | Approved preview | Source facts, mapping, canonical target | Canonical facts plus import receipt | All-or-nothing target and canonical-only marker | Kernel/storage |
| Semantic verification/retirement | Import Gateway | Completed import | Source/target logical projections | Fidelity verdict and adapter exhaustion evidence | Accepted portfolio runs without fallback | M21 |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Import Gateway | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Import Gateway rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Portfolio detector adapters | Recognize only enumerated owned formats read-only | No state | Inputs and facts supplied by legacy readers | detection contracts | Typed collaboration boundary; no adjacent-owner semantics | legacy readers | format/non-mutation tests |
| Import preview mapper | Build typed domain identity/conflict map | Preview fact | Inputs and facts supplied by detector/canonical schema | preview schema | Typed collaboration boundary; no adjacent-owner semantics | detector/canonical schema | ambiguity tests |
| Import transaction service | Commit canonical facts and operation lifecycle atomically | Import facts/receipt | Inputs and facts supplied by M11/M1 | import command | Typed collaboration boundary; no adjacent-owner semantics | M11/M1 | rollback/retry tests |
| Semantic verifier | Compare logical source and target meaning | Verification evidence | Inputs and facts supplied by read models | domain comparator | Typed collaboration boundary; no adjacent-owner semantics | read models | fidelity tests |
| Fallback guard | Reject runtime access to marked legacy sources | canonical-only marker | Inputs and facts supplied by observer/kernel | runtime source-selection contract | Typed collaboration boundary; no adjacent-owner semantics | observer/kernel | no-fallback tests |

## 10. Repository and File Impact

- `LegacyContinuityWorkspaceImporter.cs` and logical artifact providers
- compatibility/import tables and canonical stores
- new portfolio adapters/preview contracts under Core or Orchestration
- application import commands/read model
- legacy readers removed when last portfolio case passes

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Detect/preview/import/verify results expose source kind, conflicts, mapping, receipt, and required action.
- Runtime APIs accept canonical source only after completed import.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Detection and preview are side-effect-free.
- Import receipt is committed only after target semantic verification.
- Adapters are ingress-only and never implement runtime reads/writes.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Detection result | Import Gateway | request | Canonical ledger or declared authoritative artifact | immutable | detection ID | portfolio/version/conflict validation | rerun | Preview |
| Import preview | Import Gateway | approval scope | Canonical ledger or declared authoritative artifact | immutable | preview ID/source fingerprint | complete mapping/no guesses | re-preview on source change | Interaction/import |
| Import operation/receipt | Import Gateway | permanent | Canonical ledger or declared authoritative artifact | append-only lifecycle | operation/receipt ID | semantic verification | M9 recovery | Read model |
| Canonical-only marker | Workspace State Authority | workspace lifetime | Canonical ledger or declared authoritative artifact | monotonic | workspace/import receipt | target valid | restore from receipt | Runtime guard |

## 14. Lifecycle and State Transitions

```text
Undetected -> Detected -> Previewed -> Approved -> Started -> Verified -> Completed -> Legacy non-authoritative -> Adapter retired
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
| Hard prerequisite | M11 Workspace Storage Authority |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M13 Workflow Catalog; M16 compatibility projection; M17–M19 canonical-only feature runs |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Unknown portfolio format | detector | unsupported; no writes | owner enumerates or rejects format | source fingerprint | Deterministic fault injection plus public-result regression |
| Ambiguous identity mapping | preview validator | HumanDecisionRequired; no import | M10 response then new preview | conflict report | Deterministic fault injection plus public-result regression |
| Crash mid-import | transaction/journal | rollback or RecoveryRequired | M9 reconcile | operation phase | Deterministic fault injection plus public-result regression |
| Fidelity mismatch | semantic verifier | fail import completion | correct mapper; retry new plan | domain diff | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Import is one-way | Roadmap §§0.1, 3, 5 and M12 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No dual write or runtime fallback | Roadmap §§0.1, 3, 5 and M12 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Detection/preview are read-only | Roadmap §§0.1, 3, 5 and M12 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No fabricated historical evidence | Roadmap §§0.1, 3, 5 and M12 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Receipt follows semantic verification | Roadmap §§0.1, 3, 5 and M12 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | adapter detection unit tests |
| Integration | preview/conflict contract tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | transaction rollback/restart tests; portfolio fidelity integration tests; canonical-only runtime tests |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- pre-unification roadmap
- partial planning artifacts
- decision sessions
- numbered histories
- completion archives
- ambiguous/conflicting mixed workspace
- malformed source

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Detect and preview one owned legacy workspace without changing it, import into a fresh canonical target, compare domain projections and receipt, restart through the normal runtime, and prove disabling/removing the legacy reader changes no behavior.

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
| 1. Contract closure | Make Import Gateway boundaries explicit | Typed inputs, outputs, states, validators | M11 Workspace Storage Authority | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Each enumerated owned format either imports with accepted fidelity and durable receipt or fails with an actionable ambiguity/conflict report; runtime never falls back or dual-writes after import, and exhausted adapters retire.
Dependencies satisfied for: M13 Workflow Catalog; M16 compatibility projection; M17–M19 canonical-only feature runs.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

## Open Implementation Questions

- Owner must enumerate the actual owned workspace portfolio and decide ambiguous/conflicting source cases; generation is not blocked because the gateway fails closed for unenumerated formats.

<!-- END GENERATED: milestone=M12 -->
