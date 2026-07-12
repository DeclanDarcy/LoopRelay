<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M20 -->
# M20 — Application Boundary convergence Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M20
- **Name:** Application Boundary convergence
- **Implementation role:** Application Boundary and single composition root
- **Roadmap position:** 21 of 22
- **Short description:** Make every command/query enter one thin typed application boundary and one production composition root, moving migration, storage, recovery, status assembly, run-loop policy, completion cleanup, and formatting to their canonical owners while preserving specific outcomes through exit mapping.
- **Primary outcome:** CLI parsing/rendering is pure adaptation; all commands/queries use complete typed contracts, composition validates unique fail-closed dependencies, historical entry points/alternatives are deleted, and no client can reach stores or orchestration mechanics.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M20), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0011-thin-application-boundary](../../docs/architecture/decisions/0011-thin-application-boundary.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Make every command/query enter one thin typed application boundary and one production composition root, moving migration, storage, recovery, status assembly, run-loop policy, completion cleanup, and formatting to their canonical owners while preserving specific outcomes through exit mapping.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- `ILoopRelayApplication`, typed request/result/status contracts, one supported CLI executable, and retirement messages exist, but the application service owns SQL/migration/run-loop/status/cleanup/rendering concerns and recovery/storage contracts are incomplete.
- Hard prerequisites: M19 Execute capability convergence.
- Not yet architecturally closed: Complete typed use-case boundary, Pure CLI adaptation, Unique fail-closed composition, Specific exit mapping.

## 6. Runtime / System State After

- CLI parsing/rendering is pure adaptation; all commands/queries use complete typed contracts, composition validates unique fail-closed dependencies, historical entry points/alternatives are deleted, and no client can reach stores or orchestration mechanics.
- Enforceable permanent property: Make every command/query enter one thin typed application boundary and one production composition root, moving migration, storage, recovery, status assembly, run-loop policy, completion cleanup, and formatting to their canonical owners while preserving specific outcomes through exit mapping.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Complete typed use-case boundary | Application Boundary and single composition root | All run/status/storage/import/recovery/interaction/completion use cases | Typed requests | ApplicationCommandResult or immutable query snapshot | Every public operation has one contract | CLI/future clients |
| Pure CLI adaptation | Application Boundary and single composition root | Arguments and application results | Parsed values/snapshot | Rendering and suggested exit code | CLI performs no domain query/mutation/selection | Operator |
| Unique fail-closed composition | Application Boundary and single composition root | Production startup | Validated config/policy and owner dependencies | One application instance or typed composition failure | No alternate root or missing capability | All use cases |
| Specific exit mapping | Application Boundary and single composition root | Typed application result | Outcome discriminant and suggested semantics | Stable process exit | Specific reason is never collapsed | Automation |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Application Boundary and single composition root | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Application Boundary and single composition root rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Application contracts | Define complete commands, queries, results, evidence/action fields | No state | Inputs and facts supplied by all owner APIs | ILoopRelayApplication models | Typed collaboration boundary; no adjacent-owner semantics | all owner APIs | contract matrix tests |
| Thin application coordinator | Authorize/dispatch use cases to owners and project results | No domain state | Inputs and facts supplied by kernel/storage/recovery/read model | application service | Typed collaboration boundary; no adjacent-owner semantics | kernel/storage/recovery/read model | delegation tests |
| Production composition root | Resolve config/policy/capabilities and construct one graph | Process-lifetime immutable graph | Inputs and facts supplied by all authorities | CreateProduction | Typed collaboration boundary; no adjacent-owner semantics | all authorities | missing/duplicate dependency tests |
| CLI parser/renderer/exit mapper | Translate and render only | No state | Inputs and facts supplied by application | argument/snapshot contracts | Typed collaboration boundary; no adjacent-owner semantics | application | purity/snapshot tests |
| Historical entrypoint retirement | Remove alternate CLIs/composition | No state | Inputs and facts supplied by parity evidence | solution publish surface | Typed collaboration boundary; no adjacent-owner semantics | parity evidence | post-deletion build tests |

## 10. Repository and File Impact

- `src/LoopRelay.Cli/Services/Application/ApplicationBoundaryContracts.cs`
- split/thin `UnifiedCliComposition.cs` and `UnifiedCliRunner.cs`
- `Program.cs`, CLI arguments, pure formatter
- delete `LoopRelay.Plan.Cli`/`LoopRelay.Roadmap.Cli` entrypoints and composition alternatives after parity
- application/CLI/certification tests

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- One typed command/query matrix covers run, status, storage, import, recovery, interaction, and completion.
- Application results carry outcome, evidence, warnings, pending effects, required actions, and suggested exit semantics.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Application coordinates use cases but never implements mechanics.
- Composition resolves raw config once, validates dependencies, and injects canonical owners.
- CLI adapters have no repository/database/provider dependencies.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Application request/result | Application Boundary | invocation | Canonical ledger or declared authoritative artifact | immutable | correlation ID | command schema/outcome completeness | retry via new invocation/recovery | CLI |
| Composition graph | Composition Root | process lifetime | Rebuildable or process-scoped | immutable | configuration/policy/catalog identities | uniqueness/capability validation | fail startup | Application |
| CLI snapshot/render | CLI | query/render lifetime | Rebuildable or process-scoped | immutable | snapshot/evidence IDs | pure formatting | rerender | Operator |

## 14. Lifecycle and State Transitions

```text
Arguments -> Typed request -> Application use case -> Canonical owner(s) -> Typed result/snapshot -> Pure rendering -> Suggested exit mapping
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
| Hard prerequisite | M19 Execute capability convergence |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M21 Retirement completion |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| CLI reaches store/kernel directly | dependency test | fail architecture acceptance | remove dependency | call graph | Deterministic fault injection plus public-result regression |
| Specific outcome collapsed | contract matrix | reject mapper | add discriminant mapping | source/result/exit | Deterministic fault injection plus public-result regression |
| Missing dependency | composition validation | fail before command | configure owner | typed missing capability | Deterministic fault injection plus public-result regression |
| Historical entrypoint works | publish/reachability test | block acceptance | delete project/entrypoint | binary list | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| One production application boundary | Roadmap §§0.1, 3, 5 and M20 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| One production composition root | Roadmap §§0.1, 3, 5 and M20 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| CLI is rendering-only | Roadmap §§0.1, 3, 5 and M20 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No client-owned state/effects/recovery | Roadmap §§0.1, 3, 5 and M20 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Typed outcomes survive exit mapping | Roadmap §§0.1, 3, 5 and M20 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | contract/exit mapping unit tests |
| Integration | production delegation/composition integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | CLI purity/snapshot tests; full command/query matrix; published CLI certification and post-deletion build |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- every command/query
- every typed outcome
- missing owner/capability
- duplicate composition
- status with all required actions
- retired executable invocation

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Exercise the full command/query matrix through the published CLI, assert each reaches the correct canonical owner and preserves result/exit semantics, scan dependencies to prove the CLI cannot access stores, and show historical binaries are absent.

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
| 1. Contract closure | Make Application Boundary and single composition root boundaries explicit | Typed inputs, outputs, states, validators | M19 Execute capability convergence | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: CLI parsing/rendering is pure adaptation; all commands/queries use complete typed contracts, composition validates unique fail-closed dependencies, historical entry points/alternatives are deleted, and no client can reach stores or orchestration mechanics.
Dependencies satisfied for: M21 Retirement completion.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

<!-- END GENERATED: milestone=M20 -->
