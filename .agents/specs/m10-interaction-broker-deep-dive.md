<!-- BEGIN GENERATED: source=.agents/epic.md version=3.0 milestone=M10 -->
# M10 — Interaction Broker Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M10
- **Name:** Interaction Broker
- **Implementation role:** Interaction Broker
- **Roadmap position:** 11 of 22
- **Short description:** Introduce a durable typed request/response lifecycle for every required human action, including category policy, validation, timeout/default semantics, application commands/queries, and restart-safe resumption without workflow console reads.
- **Primary outcome:** Outstanding interactions are persisted before presentation, status names exact request identity and allowed response schema, responses are validated/idempotent, and the clean-input offer-to-commit becomes the first production request.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/epic.md`](../epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M10), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0011-thin-application-boundary](../../docs/architecture/decisions/0011-thin-application-boundary.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Introduce a durable typed request/response lifecycle for every required human action, including category policy, validation, timeout/default semantics, application commands/queries, and restart-safe resumption without workflow console reads.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains the supported model unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner once production routing and parity are proven.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- Request-shaped outcomes and HumanDecisionRequired vocabulary exist, but requests, responses, timeouts, and presentation are not a complete durable production path.
- Hard prerequisites: M9 Recovery Coordinator; M1/M4 durable state and evidence.
- Unavailable before this milestone: Durable interaction request, Validated response, Timeout/default policy, Clean-input offer as architecturally closed capabilities.

## 6. Runtime / System State After

- Outstanding interactions are persisted before presentation, status names exact request identity and allowed response schema, responses are validated/idempotent, and the clean-input offer-to-commit becomes the first production request.
- Enforceable permanent property: Introduce a durable typed request/response lifecycle for every required human action, including category policy, validation, timeout/default semantics, application commands/queries, and restart-safe resumption without workflow console reads.
- Capabilities still assigned to later milestones remain unavailable and must not be advertised.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Durable interaction request | Interaction Broker | Required human action | Category, question, response schema, lineage, policy | Outstanding request identity | Presentation never precedes persistence | CLI/read model |
| Validated response | Interaction Broker | Outstanding request | Typed response and responder context | Accepted response fact or typed rejection | Late/invalid/duplicate responses cannot corrupt progression | Recovery/kernel |
| Timeout/default policy | Interaction Broker | Outstanding request category | Resolved category policy and clock evidence | Expired/defaulted/human-action-required outcome | No hidden default | Recovery/read model |
| Clean-input offer | Interaction Broker | Interactive dirty declared input | Git observation and invocation posture | Offer-to-commit request or headless system fault | Headless never auto-commits or waits indefinitely | Kernel/effects |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Interaction Broker | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Interaction Broker rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | Effect result never becomes a state claim without receipt |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Recovery classifies/plans before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Interaction request store | Persist requests/responses and legal lifecycle | Interaction ledger facts | Inputs and facts supplied by M1/M4 | request/query/respond contracts | Typed collaboration boundary; no adjacent-owner semantics | M1/M4 | lifecycle/idempotency tests |
| Interaction broker | Create, validate, and resolve typed interactions | No alternate workflow state | Inputs and facts supplied by policy/store/recovery | broker contract | Typed collaboration boundary; no adjacent-owner semantics | policy/store/recovery | category and restart tests |
| Interaction policy resolver | Resolve deadline/default/isolation per category | Policy resolution facts | Inputs and facts supplied by M5 policy | category policy contract | Typed collaboration boundary; no adjacent-owner semantics | M5 policy | no-hidden-default tests |
| Application/CLI adapter | List/show/respond without owning semantics | No domain state | Inputs and facts supplied by M20/read model | typed command/query results | Typed collaboration boundary; no adjacent-owner semantics | M20/read model | parser/rendering contract tests |

## 10. Repository and File Impact

- new interaction contracts/services under orchestration primitives
- workspace schema/store additions
- application boundary request/response contracts
- CLI parser/formatter adapters
- interaction and dirty-input tests

Expected tests remain in the matching `tests/LoopRelay.*.Tests/` projects. Generated runtime/certification cases stay under `.tmp/certification/`; durable product files remain on their roadmap-defined surfaces.

## 11. Public Contracts

- `interaction list/show/respond`-equivalent typed use cases expose stable IDs and schemas; exact CLI spelling is owned by M20 contracts.
- Status required actions always include request identity and response requirements.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Broker persists before presentation and resumes through Recovery/Kernel.
- Duplicate response with same semantic value is idempotent; conflicting duplicate is rejected.
- Workflow components never read stdin/console directly.

- Required causal writes complete before the boundary they authorize.
- Retried coordination is idempotent; retries of uncertain external work require recovery authorization.
- Rebuildable projections consume authoritative facts and never write back implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Interaction request | Interaction Broker | until resolved/expired/cancelled | Canonical ledger or declared authoritative artifact | append-only lifecycle | request ID | schema/category/causality | discover on restart | CLI/read model |
| Interaction response | Interaction Broker | permanent | Canonical ledger or declared authoritative artifact | immutable | response ID | request/schema/deduplication | replay resolution | Kernel |
| Category policy | Policy Authority | policy scope | Canonical ledger or declared authoritative artifact | immutable resolution | policy ID | explicit timeout/default/isolation | re-resolve as new policy | Broker |

## 14. Lifecycle and State Transitions

```text
Required -> Persisted -> Presented -> Responded / Expired / Defaulted / Cancelled -> Validated -> Resume authorized
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
| Hard prerequisite | M9 Recovery Coordinator |
| Inherited capability | M1/M4 durable state and evidence |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit component suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | M11 storage mutation decisions; M14 interact stage; M16 required-action projection |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Presentation before persistence | ordering test | do not present | persist then render | request correlation | Deterministic fault injection plus public-result regression |
| Invalid/late response | schema/lifecycle validator | typed rejection; request unchanged | submit valid response or replan | validation errors | Deterministic fault injection plus public-result regression |
| Duplicate conflicting response | idempotency key | reject conflict | operator resolves via policy | original/duplicate IDs | Deterministic fault injection plus public-result regression |
| Headless dirty input | invocation posture | typed system fault; no commit | clean input externally | declared surface diagnostic | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Every human action has durable request identity | Roadmap §§0.1, 3, 5 and M10 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Responses are schema-validated | Roadmap §§0.1, 3, 5 and M10 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No workflow console reads | Roadmap §§0.1, 3, 5 and M10 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Headless dirty input never auto-commits | Roadmap §§0.1, 3, 5 and M10 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No hidden timeout/default | Roadmap §§0.1, 3, 5 and M10 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | request lifecycle unit tests |
| Integration | restart/late/duplicate response integration tests |
| Contract | Public/internal contracts in §§11–12, including typed outcome preservation |
| Regression | category policy tests; interactive/headless clean-input tests; application contract tests |
| Replay/rebuild | Restart at each durable boundary; authoritative facts rebuild projections without semantic drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative catalog/workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- valid offer-to-commit
- headless dirty surface
- invalid response schema
- late response after expiry
- duplicate same/conflicting response
- restart with outstanding request

- Cross-component fixture: production-like repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every state boundary in §14.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. Invoke an interactive transition with dirty declared input, observe a persisted offer-to-commit request in status, restart, submit a valid response once and then duplicate it, and prove one resolution/effect path; repeat headless and verify typed fault without mutation.

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
| 1. Contract closure | Make Interaction Broker boundaries explicit | Typed inputs, outputs, states, and validators | M9 Recovery Coordinator | Invalid/ambiguous inputs fail before mutation |
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

Stable handoff: Outstanding interactions are persisted before presentation, status names exact request identity and allowed response schema, responses are validated/idempotent, and the clean-input offer-to-commit becomes the first production request.
Dependencies satisfied for: M11 storage mutation decisions; M14 interact stage; M16 required-action projection.
Remaining limitations stay owned by their named future milestones. Any temporary adapter is one-way, observable, and removed at its declared gate; unresolved risks transfer with durable evidence rather than hidden state.

## Open Implementation Questions

- Owner must rule timeout/default per request category, isolation guarantee depth, and whether trust evidence is an audit product.

<!-- END GENERATED: milestone=M10 -->
