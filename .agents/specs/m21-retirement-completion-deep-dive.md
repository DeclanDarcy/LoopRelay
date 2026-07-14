<!-- BEGIN GENERATED: source=.agents/specs/epic.md version=3.0 milestone=M21 -->
# M21 — Retirement completion Deep Dive

Status: Open

## 1. Milestone Summary

- **Identifier:** M21
- **Name:** Retirement completion
- **Implementation role:** Canonical architecture retirement gate
- **Roadmap position:** 22 of 22; final milestone
- **Short description:** Remove every exhausted legacy body, import adapter, compatibility fallback, provisional bridge, direct table/effect/recovery path, unowned prompt/asset, dead declaration, and stale supported-behavior claim so exactly one canonical architecture remains and deletion changes no supported behavior.
- **Primary outcome:** All north-star metrics reach target: one boundary, catalog, kernel, logical store, effect protocol, recovery protocol, completion authority, and read model; zero duplicate/zero owners, direct required effects, workflow-local persistence/retry/recovery, legacy-only behavior, unowned assets, or unevidenced public claims.

## 2. Normative Basis

| Authority class | Sources | Use |
|---|---|---|
| Roadmap authority | [`.agents/specs/epic.md`](epic.md) §§0, 2.1, 3, 5, 6, 7, 8, 9 (M21), 10–12, Appendix A | Status, ownership, dependency order, permanent property, convergence and acceptance |
| Architectural authority | [0006-conflict-resolution-by-authority](../../docs/architecture/decisions/0006-conflict-resolution-by-authority.md), [0007-persistence-lineage-evidence-and-projection](../../docs/architecture/decisions/0007-persistence-lineage-evidence-and-projection.md), [0011-thin-application-boundary](../../docs/architecture/decisions/0011-thin-application-boundary.md) | Accepted cross-authority contracts; an ADR is target authority where the roadmap says it supersedes older implementation |
| Implementation authority | Current production code and active composition in the repository; paths in §10 | Concrete seams and executable contracts |
| Supporting context | current tests and certification baseline in roadmap §6 | Evidence and historical intent only; never a competing authority |

## 3. Objective

Remove every exhausted legacy body, import adapter, compatibility fallback, provisional bridge, direct table/effect/recovery path, unowned prompt/asset, dead declaration, and stale supported-behavior claim so exactly one canonical architecture remains and deletion changes no supported behavior.

## 4. Non-Goals

- Do not implement capabilities assigned to later milestones or claim their architectural closure.
- Do not add workflow-specific orchestration, recovery, effect, storage, or rendering authority.
- Do not introduce concurrency; linear first-eligible progression remains supported unless the roadmap explicitly changes it.
- Do not retain a bridge as a permanent second owner after routing and parity.
- Do not treat human-facing documentation, planning, governance, or reports as implementation deliverables.

## 5. Runtime / System State Before

- M17–M20 establish canonical feature and application parity, but temporary adapters, retained executable specifications, dead assets, and plausible second change locations may remain until a deliberate final deletion gate.
- Hard prerequisites: M20 Application Boundary convergence; M17–M19 accepted feature-body deletion gates.
- Not yet architecturally closed: Legacy-free production graph, Asset/declaration closure, Adapter exhaustion retirement, End-state metric enforcement.

## 6. Runtime / System State After

- All north-star metrics reach target: one boundary, catalog, kernel, logical store, effect protocol, recovery protocol, completion authority, and read model; zero duplicate/zero owners, direct required effects, workflow-local persistence/retry/recovery, legacy-only behavior, unowned assets, or unevidenced public claims.
- Enforceable permanent property: Remove every exhausted legacy body, import adapter, compatibility fallback, provisional bridge, direct table/effect/recovery path, unowned prompt/asset, dead declaration, and stale supported-behavior claim so exactly one canonical architecture remains and deletion changes no supported behavior.
- Later milestone capabilities remain unavailable until their own acceptance.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance criteria | Downstream consumers |
|---|---|---|---|---|---|---|---|
| Legacy-free production graph | Canonical architecture retirement gate | Supported behavior families | Accepted canonical routes and parity evidence | Single-owner dependency graph | Every behavior has one change location | Operators/developers |
| Asset/declaration closure | Canonical architecture retirement gate | Runtime prompts/assets/declarations | Reachability and owner registries | Only owned reachable assets | Unowned runtime/generated assets equal zero | Build/runtime |
| Adapter exhaustion retirement | Canonical architecture retirement gate | One-way import/bridge adapters | Portfolio exhaustion and no-fallback evidence | Deleted adapters/fallbacks | Removal changes no supported behavior | Canonical runtime |
| End-state metric enforcement | Canonical architecture retirement gate | Whole production architecture | Machine-derived ownership/reachability/evidence metrics | All roadmap §10 targets | Any regression blocks release | Owner acceptance |

The roadmap defines no separate capability IDs for these entries; names are preserved without inventing identifiers.

## 8. Architectural Responsibilities

| Boundary | Sole authority | Collaboration rule | Failure/validation authority |
|---|---|---|---|
| Behavior owned here | Canonical architecture retirement gate | Accept typed inputs and emit typed facts/results; do not absorb adjacent owners | Canonical architecture retirement gate rejects invalid state before outward action |
| Durable state | Workspace State Authority | Domain owner defines semantics; state authority owns atomic durability | Required persistence failure is fail-closed |
| External mutation | Effect Coordinator (M8) | Owner emits intent; coordinator executes/reconciles | No mutation claim without receipt/postcondition |
| Recovery | Recovery Coordinator (M9) | Owner exposes durable boundary evidence | Classify and persist plan before action |
| Client projection | Canonical Read Model (M16) | Clients render/query only | Projection cannot repair or outrank facts |

## 9. Components and Modules

| Component | Purpose/responsibilities | Owned state | Consumed state | Public contracts | Internal contracts | Dependencies | Tests required |
|---|---|---|---|---|---|---|---|
| Retirement reachability verifier | Enumerate production entrypoints/owners/assets/change locations | Generated analysis result only | Inputs and facts supplied by solution/catalog/composition | machine contract | Typed collaboration boundary; no adjacent-owner semantics | solution/catalog/composition | zero-target tests |
| Post-deletion build/test surface | Compile and execute canonical projects after physical deletion | No runtime state | Inputs and facts supplied by M17–M20 | solution/test contracts | Typed collaboration boundary; no adjacent-owner semantics | M17–M20 | full regression/platform tests |
| Fallback guards | Prove canonical-only source/routing at runtime | Canonical marker/evidence only | Inputs and facts supplied by M12/M20 | source selection contracts | Typed collaboration boundary; no adjacent-owner semantics | M12/M20 | negative compatibility tests |
| North-star metric evaluator | Calculate roadmap §10 metrics from executable graph/registries | Metric snapshot | Inputs and facts supplied by catalog/composition/evidence | release gate API | Typed collaboration boundary; no adjacent-owner semantics | catalog/composition/evidence | target tests |

## 10. Repository and File Impact

- delete accepted legacy Roadmap/Plan/Execute bodies and projects
- remove exhausted import/compatibility/provisional adapters and direct table/effect/recovery paths
- remove unowned prompts/assets/dead declarations
- remove stale repository claims that advertise unsupported behavior, as explicitly required by roadmap M21
- solution/project references, publish scripts, tests, and certification updated to canonical-only surfaces

Tests live in matching `tests/LoopRelay.*.Tests/` projects. Generated certification cases remain under `.tmp/certification/`; durable product files remain on declared surfaces.

## 11. Public Contracts

- Only the published canonical application/CLI surface remains.
- No compatibility or fallback behavior is implied unless still explicitly supported by M12 portfolio facts.

Public results preserve completed, waiting, failed, cancelled, stalled, ambiguous, recovery-required, effect-pending, human-decision-required, and specific cannot-proceed meanings where applicable.

## 12. Internal Contracts

- Deletion is accepted only after parity/retirement prerequisites.
- Metric evaluator derives from production graph; no manual pass override.
- Git history is recovery for accepted deletions.

- Causally required writes complete before the boundary they authorize.
- Repeated coordination is idempotent; uncertain external work requires recovery authorization before repeat.
- Rebuildable projections consume authoritative facts and never write implicit corrections.

## 13. Data and State Model

| State object | Owner | Lifecycle | Durability | Mutability | Identity | Validation | Recovery | Consumers |
|---|---|---|---|---|---|---|---|---|
| Ownership/reachability metric snapshot | Retirement gate | release candidate | Rebuildable or process-scoped | immutable generated evidence | commit/catalog/config identity | all targets exact | rerun | Acceptance |
| Import adapter exhaustion fact | Import Gateway | portfolio completion | Canonical ledger or declared authoritative artifact | monotonic evidence | adapter/portfolio IDs | all owned cases canonical-only | restore only by new authority decision | Retirement |
| Canonical production graph | Composition/Catalog | release lifetime | Rebuildable or process-scoped | immutable at process start | build/catalog identities | uniqueness validation | fail startup/build | Runtime |

## 14. Lifecycle and State Transitions

```text
Canonical parity accepted -> Alternate routes unreachable -> Assets/adapters exhausted -> Physical deletion -> Clean build/tests/certification -> Metrics at target -> Owner end-state acceptance
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
| Hard prerequisite | M20 Application Boundary convergence |
| Inherited capability | M17–M19 accepted feature-body deletion gates |
| Supporting infrastructure | Canonical workspace database, Git/filesystem observation, xUnit suites, certification harness |
| Explicitly unavailable | Later milestone authorities cannot be substituted by feature-local implementations |
| Enables | Program end-state acceptance |

## 17. Failure Modes

| Failure | Detection | Expected behavior | Recovery | Diagnostics | Test coverage required |
|---|---|---|---|---|---|
| Deleted body still has unique behavior | parity tests | stop deletion/acceptance | route behavior canonically first | failing case | Deterministic fault injection plus public-result regression |
| Dead/unowned asset remains | registry/reachability scan | fail metric | remove or assign real runtime owner | asset path | Deterministic fault injection plus public-result regression |
| Fallback silently activates | negative runtime fixture | fail closed | remove source selection | source diagnostic | Deterministic fault injection plus public-result regression |
| Platform evidence missing | release gate | classification remains incomplete | run required genuine platform campaign | platform evidence ID | Deterministic fault injection plus public-result regression |

## 18. Validation and Invariants

| Invariant | Source authority | Enforcement point | Failure behavior | Test strategy |
|---|---|---|---|---|
| Exactly one canonical architecture remains | Roadmap §§0.1, 3, 5 and M21 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| Deletion changes no supported behavior | Roadmap §§0.1, 3, 5 and M21 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| All roadmap §10 metrics meet exact targets | Roadmap §§0.1, 3, 5 and M21 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No unowned runtime asset | Roadmap §§0.1, 3, 5 and M21 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |
| No public claim lacks evidence identity | Roadmap §§0.1, 3, 5 and M21 | Owning service transaction/boundary | Typed failure; no false progress | Unit + integration + acceptance fixture |

## 19. Testing Strategy

| Category | Required coverage |
|---|---|
| Unit | ownership/reachability metric unit tests |
| Integration | canonical-only post-deletion integration tests |
| Contract | Public/internal contracts in §§11–12 and typed outcome preservation |
| Regression | full solution regression tests; required live/platform certification; negative fallback and unowned-asset tests |
| Replay/rebuild | Restart at each durable boundary; facts rebuild projections without drift |
| Failure path | Every §17 mode, required-write failure, cancellation, unknown and duplicate coordination |
| Performance smoke | Bounded scan/query and no duplicate external work on a representative workspace |
| Acceptance | §21 demonstration plus the lowest decisive roadmap §6 certification tier |

## 20. Fixtures and Test Data

- canonical Traditional and Eval full chains
- every retired legacy invocation
- all imported owned formats with adapters disabled
- unowned prompt sentinel
- duplicate-owner composition sentinel
- genuine Windows/Linux platform evidence

- Cross-component fixture: production-like Git repository with all nine Project Context files and independent nested `.agents` Git topology.
- Corruption fixture: missing/invalid durable row or receipt appropriate to this authority.
- Replay fixture: restart snapshot at every §14 boundary.

## 21. Acceptance Demonstration

**Setup and input:** Use a disposable production-like Git repository and canonical workspace fixture. On the deletion candidate, enumerate the production graph and assert every §10 metric at target, build/test the reduced solution, run both full chains and required platform campaigns, invoke every former route to prove absence, and confirm adapter-disabled imported workspaces run canonical-only.

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
| 1. Contract closure | Make Canonical architecture retirement gate boundaries explicit | Typed inputs, outputs, states, validators | M20 Application Boundary convergence | Invalid/ambiguous inputs fail before mutation |
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

Roadmap M21 explicitly requires removal of stale documentation that claims obsolete supported behavior. This is a deletion/parity condition, not production of new human-facing documentation. Machine-consumed manifests, registries, solution files, and command schemas must be updated atomically with deletion and tested.

## 30. Exit Criteria

- All components and typed contracts exist or are explicitly unavailable until their named milestone.
- Production routing uses the singular owner; no competing supported path remains.
- All success/failure/cancellation/unknown/pending/recovery/action paths are covered.
- All §19 checks and §21 demonstration pass; §22 evidence is reproducible.
- Inherited invariants remain valid and no future capability is falsely claimed.
- Roadmap owner accepts architectural closure, not merely implemented/green behavior.

## 31. Transition to Next Milestone

Stable handoff: All north-star metrics reach target: one boundary, catalog, kernel, logical store, effect protocol, recovery protocol, completion authority, and read model; zero duplicate/zero owners, direct required effects, workflow-local persistence/retry/recovery, legacy-only behavior, unowned assets, or unevidenced public claims.
Dependencies satisfied for: Program end-state acceptance.
Remaining limitations stay with named future milestones; temporary adapters are one-way, observable, and retired at their gate.

<!-- END GENERATED: milestone=M21 -->
