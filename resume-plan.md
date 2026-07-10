# Codex Resume Recovery Implementation Plan

## Goal

Implement restart-safe Codex decision-session continuity. LoopRelay must resume the previously active Codex thread when that operation is legal and succeeds. When a classified, recovery-eligible failure prevents resume, LoopRelay must create one replacement thread, reconstruct the best safe context available, preserve the original thread and its ancestry, and atomically make the replacement authoritative only after recovery is verifiably complete.

The implementation must also repair the demonstrated `experimentalApi` negotiation defect. A deterministic protocol, configuration, authentication, permission, or programming failure must stop visibly; it must never be converted into a fresh or recovered replacement.

## Authority and Scope

`resume-audit.md` is the architectural authority for this plan. The implementation should use the ownership boundaries, lifecycle, persistence facts, failure analysis, source inventory, and bounded runtime questions established there rather than reopening the audit.

The first supported recovery owner is the cross-process Execute decision session. One-shot agents and process-local Plan, execution, review, projection, and completion sessions keep their current lifetimes. Provider-neutral contracts, journal records, operation-profile evidence, and lineage must not encode Codex-only assumptions so that later providers or recovery mechanisms can be added without replacing this design.

No temporary resume subsystem, second active-session store, raw-transcript replay path, or string-matching error policy is part of this plan.

## Non-Negotiable Invariants

1. The same provider thread ID is a resumed original; any different ID is a replacement.
2. `thread/resume` is attempted only with a captured `SessionContinuityProfile` whose resume operation and parameter contract make every emitted field legal.
3. A deterministic protocol failure, including an `experimentalApi`/`excludeTurns` incompatibility, is recorded as `ProtocolRepairRequired`; no replacement is started.
4. Automatic replacement is limited to a classified resume-handshake failure for which LoopRelay proves that no `turn/start` was submitted.
5. The original active pointer remains unchanged until the replacement identity, lineage, recovery evidence, and required context acceptance are durable.
6. Replacement creation, context injection, and decision turns are journaled before their first possibly side-effecting request. An unknown outcome blocks blind retries.
7. Active-session scope is a stable repository and Execute-lifecycle identity, not the process-random `Repository.Id` or a database-wide singleton.
8. Recovery content is versioned, normalized, bounded, sanitized, digestible, and labeled with its completeness and omissions. Raw rollouts are never injected or copied into SQLite wholesale.
9. Recovery state and active-pointer writes are correctness-critical. They fail closed on storage errors; they do not use the current fail-open warning behavior.
10. Transition run, LoopRelay session, provider thread, provider turn, recovery attempt, lineage node, and produced artifact are durably joinable.
11. A process restart reconciles the existing nonterminal attempt or turn before it starts another provider operation.
12. Production output, persisted evidence, and telemetry distinguish `ResumedOriginal`, native replacement, reconstructed replacement, repository-only replacement, clean session, and failed-closed outcomes.
13. Every replacement side effect is authorized by one persisted, versioned `RecoveryPlan`; mechanism changes require a new linked attempt.
14. `DecisionSessionRecoveryCoordinator` contains no provider, source-precedence, mechanism-ranking, or journal-transition branches.

## Target Architecture

### Ownership

| Concern | Owner | Required repository boundary |
| --- | --- | --- |
| JSON-RPC framing, initialize exchange, error parsing, method execution, and process teardown | `LoopRelay.Agents` Codex adapter and `AgentRuntime` | Extend the existing app-server transport; do not parse Codex protocol in the CLI |
| Provider-neutral operation support and resume/read/write/fork results | `LoopRelay.Agents` | A `SessionContinuityProfile` and typed operation contracts beside `IAgentRuntime` and session models |
| Recovery state-machine rules | `LoopRelay.Orchestration.Primitives.RecoveryJournal` | A pure architectural primitive that validates transitions and produces the next state/events; it performs no I/O and knows no Codex types |
| Recovery persistence | `LoopRelay.Orchestration.Primitives.IRecoveryStore` | SQLite implementation using the shared workspace database, compare-and-swap row versions, lineage, plans, sources, turns, and atomic activation |
| Recovery planning and execution | `LoopRelay.Orchestration.Primitives.RecoveryRuntime` | Provider-neutral implementation boundary over the journal, store, planner, mechanism catalog, and source catalog |
| Stable workspace and schema migration ownership | `LoopRelay.Core` | Evolve `LoopRelayWorkspaceDatabase` from create-and-stamp behavior to ordered transactional migrations |
| Decision-session lifecycle integration | `LoopRelay.Cli.Services.Decisions` | A thin `DecisionSessionRecoveryCoordinator` translates the current Execute scope/run into a `RecoveryRuntimeRequest` and applies the returned session/outcome; it does not branch on mechanisms or drive journal transitions |
| Recovery mechanism/source implementations and envelope assembly | Registered implementations behind Orchestration contracts | Codex-native operations stay in Agents; repository/thread-read/rollout reconstruction implementations are composed by the CLI without leaking into the coordinator/runtime |
| Repository-owned context and artifact materialization | Existing `LoopArtifacts`, product records, projections, and transition evidence | Extend existing stores and products; do not create a parallel artifact model |
| Operator output and telemetry | Unified CLI console/status plus existing telemetry sinks | Structured continuity events derived from journal state; transcript bodies never enter telemetry |

`DecisionSession` remains the session-lifecycle owner, but its procedural resume catch is replaced with the coordinator. The call chain is `DecisionSessionRecoveryCoordinator -> RecoveryRuntime -> RecoveryJournal/IRecoveryStore/IRecoveryPlanner/IRecoveryMechanismCatalog/IRecoverySourceCatalog`. `AgentRuntime` reports provider-operation facts and never chooses recovery policy. `TransitionRuntime` supplies run correlation and consumes the resulting continuity metadata; it does not read Codex content.

### Protocol Compatibility, Supported Codex Versions, and Capability Contract

Treat supported Codex releases as an explicit certified set, not an open-ended semantic-version range. The initial production set is exactly `0.142.5`, because that is the only audited binary, and it enters the checked-in compatibility manifest only during Milestone 9 certification. Additional versions may be added only after the complete version fixture passes for that exact app-server schema digest. Versions absent from the manifest produce an `Unknown` continuity profile and fail closed for resume/read/write/fork recovery operations.

Model provider behavior as a `SessionContinuityProfile`, not a bag of `CanX` booleans. The profile contains operation-support descriptors for:

- `ResumeSupport`;
- `ForkSupport`;
- `ConversationReadSupport`;
- `ConversationWriteSupport` for seeding a replacement;
- `ConversationImportSupport` and `ConversationExportSupport` (initially `Unknown`; no implementation is added in this feature);
- `PartialReadSupport` and its verified-boundary semantics;
- `DeterministicIdentifiers` and provider idempotency/reconciliation guarantees;
- `MaximumRecoverableContext`, source of that value, and input/output constraints.

Each operation descriptor has tri-state `Supported`, `Unsupported`, or `Unknown` plus protocol/schema version, parameter support, side-effect class, result/fidelity contract, partial-result behavior, limits, reconciliation strategy, and evidence. `excludeTurns` is a resume-parameter feature inside `ResumeSupport`, not a top-level capability. The immutable profile also carries:

- provider and CLI/server version;
- executable/protocol identity and canonical app-server schema digest;
- client capabilities offered during `initialize`;
- server capabilities returned, when present;
- compatibility-manifest entry and fixture identity;
- negotiation timestamp and evidence source;
- any structured rejection that narrowed an operation or parameter;
- a canonical profile digest.

Resolve operation-support evidence in this order:

1. Local safety invariants and an explicit server rejection may only narrow support.
2. An explicit server `Unsupported` result overrides a compatibility manifest.
3. An exact certified version plus schema digest may promote a server-omitted value from `Unknown` to the certified value.
4. A version string without the matching schema/fixture evidence never promotes support.
5. Conflicting evidence leaves the operation or parameter `Unknown` and stops it.

For certified `0.142.5`, initialize offers `capabilities.experimentalApi = true`, and `thread/resume` includes `excludeTurns = true` only when `ResumeSupport.Parameters.ExcludeTurns` is `Supported`. If that parameter is `Unsupported`, omit it; if it is `Unknown`, do not guess. A structured invalid-request or invalid-params response to a request authorized by the profile invalidates or narrows the affected operation for the attempt and is a deterministic compatibility failure.

### Structured Resume Result

Replace `AgentSessionResumeException` as the policy signal with `SessionResumeResult`. Keep raw provider messages only as redacted diagnostics.

| Outcome | Meaning | Coordinator action |
| --- | --- | --- |
| `SuccessfulResume` | The original provider ID was loaded and no turn was submitted | Record `ResumeSucceeded`; keep the same active lineage |
| `RetryableFailure` | A typed transient transport/provider failure occurred at a proven pre-turn stage | Retry within the same attempt using the persisted deterministic schedule; fail closed after exhaustion |
| `DeterministicProtocolFailure` | An operation/parameter gate, protocol schema, method, field, or local protocol-integration contract is incompatible | Record `ProtocolRepairRequired`; never create a replacement |
| `UnavailableSession` | Trusted provider/storage evidence proves the exact original session is absent or archived in an unsupported location | Evaluate policy-approved replacement sources |
| `CorruptedState` | The active-state document or exact provider session source is present but fails its versioned integrity/parser contract | Preserve diagnostics; recover only from the last verified boundary and only if policy permits |
| `UnknownOutcome` | The operation or its classification cannot be proven | Record `UnknownOutcome`; stop automatic continuation and reconcile |

The six outcomes above are the minimum resume taxonomy. Add explicit non-recovery outcomes for authentication, local configuration, permission, persistence/storage, cancellation, post-turn failure, and local programming failure rather than folding them into a protocol or unavailable-session result. Every one fails closed and is preserved in `FailureClassification`.

The result includes operation stage, provider method, request/correlation ID, JSON-RPC code and cloned `error.data`, process exit and bounded stderr facts, continuity-profile digest, attempt count, timeout/cancellation distinction, whether the request was written/acknowledged, and the invariant `TurnSubmitted = false` for any result eligible for replacement.

Classification must not inspect error-message text. Use structured JSON-RPC fields, local operation/parameter/schema gates, exact thread-ID storage evidence, parser integrity results, and transport state. If the installed Codex version exposes only ambiguous `-32600` text and no independent exact-session evidence resolves it, return `UnknownOutcome` rather than guessing that the session is unavailable.

### Stable Scope and Correlation Identity

Add a stable `workspace_id` in `workspace_metadata`; generate it once during schema preparation and reuse it across CLI invocations. Continue using `Repository.Id` only for live registry isolation.

Define `DecisionSessionScopeId` as the canonical SHA-256 digest of:

- stable workspace ID;
- workflow identity `Execute`;
- active `PreparedEpic` causal identity;
- active `ExecutablePlan` causal identity;
- session role `Decision`;
- scope-contract version.

Make the `PreparedEpic` and `ExecutablePlan` causal identities deterministic from their canonical content hashes and producer identities rather than from process-local evidence strings. The invocation mode (`DefaultChained`, `ForcedEvalChain`, `ForcedTraditionalChain`, or `BoundedExecute`) is provenance, not part of the scope: restarting the same Execute lifecycle through another legal entry mode may resume it, while a new epic/plan produces a new scope.

Modify prompt execution so `TransitionRuntime` passes a `PromptExecutionRequest` containing the already-persisted run ID, workflow, stage, transition, rendered prompt, input snapshot hash, root invocation mode, and request metadata. This replaces the current `IPromptExecutor.ExecuteAsync(definition, prompt, ...)` signature and gives the decision lifecycle the correlation identity before any session or turn work starts.

### Recovery Journal and Lineage State Machine

Use one provider-neutral `RecoveryJournal` with compare-and-swap transitions. Required statuses and restart behavior are:

| Status | Durable invariant | Restart action |
| --- | --- | --- |
| `Pending` | Attempt and original reference exist; original remains active | Reconcile any uncertain resume request, otherwise continue the classified pre-turn attempt |
| `ProtocolRepairRequired` | Deterministic incompatibility captured; no replacement exists | Terminal failed-closed result |
| `ResumeSucceeded` | Original provider ID verified; active pointer unchanged | Terminal success |
| `RecoveryPreparing` | Canonical plan, eligibility/ranking evidence, sources, expected completeness, and optional envelope digest are durable | Revalidate plan/profile/source digests, then continue; do not send a replacement request yet |
| `ReplacementCreating` | Persisted before a fresh-create or native-fork request | Reconcile by provider evidence before any retry |
| `ReplacementCreated` | Replacement identity and parent lineage are durable but inactive | Validate native history or proceed to context injection |
| `ContextInjectionPending` | Context marker/digest and turn intent are durable before submission | Read/reconcile the replacement thread; submit only if absence is proven |
| `RecoveryCompleted` | Context/history is verified and active pointer/authoritative lineage switched in one transaction | Terminal success; downstream work uses replacement |
| `RecoveryFailed` | Known terminal failure; original pointer and evidence remain | Terminal failed-closed result |
| `UnknownOutcome` | A provider side effect may have happened | Reconcile only; never blindly create/fork/inject again |

Allowed transitions are monotonic and explicit. `UnknownOutcome` can leave that state only when reconciliation proves a specific successor or known failure. A side-effect-free transient retry increments a sub-attempt counter on the same record. A second replacement is always a new linked attempt, not a retry.

Each lineage node stores provider session reference, scope, parent and root IDs, mechanism, completeness, source/profile/plan digests, creation/activation/retirement timestamps, and authoritative-continuation state. A successful resume creates no child edge. Fresh initial sessions use `Fresh`; intentional decision transfer uses `PlannedTransfer`; recovered replacements use `NativeFork`, `PublicProjection`, `RolloutSalvage`, or `RepositoryOnly`. The same active-pointer and lineage transaction serves all of them.

Every recovery attempt stores its ID and journal schema version; provider; repository/workflow/epic/stage/transition/run scope; original and optional replacement references; status and row version; structured failure; trigger and recovery mechanism; source locations/digests/boundaries, normalizer version, completeness, and omissions; continuity-profile and recovery-plan digests; idempotency key and provider request/correlation IDs; retry counters; redacted diagnostics; and created, updated, and completed timestamps. These are durable domain fields, not facts reconstructed from log text.

`RecoveryJournal` owns the legal state graph and invariants, not coordination. Given the current attempt plus a typed command such as `RecordResumeFailure`, `RecordPlan`, `RecordReplacementCreated`, `RecordContextAccepted`, `CompleteAndActivate`, or `RecordUnknownOutcome`, it returns a validated next state and domain events. `RecoveryRuntime` is the only production caller allowed to apply those commands and persist them through `IRecoveryStore`.

### Recovery Runtime and Recovery Plan

`RecoveryRuntime` is the provider-neutral implementation boundary. For a request, it:

1. loads and reconciles any nonterminal attempt before doing new work;
2. asks the continuity runtime to resume the original and records the structured result through the journal;
3. stops or performs persisted transient retries when replacement is ineligible;
4. asks `IRecoverySourceCatalog` for verified source descriptors without choosing a mechanism;
5. passes the failure, scope, `SessionContinuityProfile`, policy, budget, and sources to `IRecoveryPlanner`;
6. persists the returned canonical `RecoveryPlan` and digest in `RecoveryPreparing` before a replacement operation;
7. resolves the plan's exact mechanism/version from `IRecoveryMechanismCatalog`, then executes, reconciles, and validates it;
8. drives journal transitions and the store's atomic activation transaction;
9. returns a typed runtime result to the coordinator.

`RecoveryPlan` is an immutable, versioned policy decision with:

- plan ID, schema version, planner version, policy version, and canonical digest;
- `Mechanism`: selected mechanism identity/version and eligibility/ranking evidence;
- `Sources`: ordered source descriptors, required source digests, verified boundaries, and normalizer versions;
- `Envelope`: optional canonical `RecoveryEnvelope` descriptor/digest;
- `ActivationStrategy`: `ReuseOriginal`, `EagerCreateAndInject`, or `NativeClone`;
- `ValidationStrategy` and unknown-outcome reconciliation strategy;
- `ExpectedCompleteness` and explicit allowed omissions;
- required `SessionContinuityProfile` digest and operation constraints;
- idempotency identity, retry ceiling, and failure behavior.

`IRecoveryPlanner` is pure and deterministic: the same canonical planning input produces the same plan digest. It evaluates mechanism eligibility, applies the configured ranking, and selects exactly one mechanism. Once `ReplacementCreating` is reached, the runtime never silently replans. If a known terminal failure permits another mechanism, it closes the attempt and creates a new linked attempt/plan; an uncertain result remains `UnknownOutcome`.

### Pluggable Recovery Mechanisms and Source Precedence

Define `IRecoveryMechanism` with stable identity/version plus `EvaluateEligibility`, `ExecuteAsync`, `ReconcileAsync`, and `ValidateAsync`. Register mechanisms through `IRecoveryMechanismCatalog`; the coordinator and runtime contain no native-fork/reconstruction/repository `if` chain. Initial implementations are:

- `NativeForkRecoveryMechanism`;
- `ThreadReadReconstructionMechanism`;
- `RolloutReconstructionMechanism`;
- `RepositoryReconstructionMechanism`.

`IRecoverySource` is separate: sources return immutable verified content/descriptors, while mechanisms define how a replacement is created, seeded, reconciled, validated, and activated. `ThreadReadRecoverySource`, `RolloutSalvageRecoverySource`, and `RepositoryContinuationRecoverySource` are registered through `IRecoverySourceCatalog`. Each reconstruction mechanism requires repository-authoritative context as its base and declares the additional source it consumes.

Eligibility and ranking policy is:

1. Stop on deterministic, authentication, permission, storage, cancellation, post-turn, or unknown failures.
2. Retry only typed transient pre-turn failures with a persisted bounded schedule. Derive any jitter from the attempt ID so the schedule is reproducible.
3. Ask every registered mechanism for eligibility using the captured profile, failure class, source integrity, scope match, budget, and reconciliation support.
4. Rank only eligible mechanisms using the versioned policy. Native fork cannot be eligible until Milestone 9 certification promotes its exact operation profile; method recognition alone is insufficient.
5. Select one plan. If no mechanism is eligible, fail closed with the collected eligibility evidence.
6. Permit `RepositoryReconstructionMechanism` only when the original identity/scope is known, all mandatory repository sources are verified, and the loss is surfaced as `RepositoryOnly`.
7. Keep `ReplacementClean` representable for an explicit operator policy, but do not register it as an automatic resume-recovery mechanism.

For reconstruction, source ranking is deterministic: exact `thread/read` public projection first; exact rollout salvage only when the certified parser establishes a valid boundary and thread-read is unavailable or intentionally incomplete; repository-only when no verified Codex content is usable. Do not use the cwd/start-time rollout heuristic. Native fork is a mechanism, not a content source, and its rank is profile/policy data established in Milestone 9 rather than procedural orchestration.

### Canonical Recovery Envelope

Introduce `RecoveryEnvelope` schema version 1 and a canonical serializer/hash. It contains:

- recovery attempt, original lineage, scope, workflow/stage/transition/run, and invocation provenance;
- structured resume failure class without secret-bearing raw text;
- current operational context, projection digest/content, relevant plan/details/product hashes, repository revision and bounded status, and accepted handoff/decision boundaries;
- ordered public conversation records with source sequence, original role/item type, sanitized text or summary, truncation flag, and digest;
- bounded file/tool-effect summaries needed to explain current repository state;
- source descriptors, normalizer versions, valid boundaries, omissions, redaction counts, corruption/partial-tail facts, completeness, token estimate, and envelope digest;
- an explicit preamble stating that the new thread is a replacement and naming `FullPublic`, `Selective`, `Summary`, or `RepositoryOnly` completeness.

Normalization rules are deterministic:

- preserve public user/agent order and turn boundaries;
- summarize file changes and tool activity into typed, bounded facts;
- deduplicate event/message representations and accepted repository artifacts by canonical digest;
- normalize repository paths and label external paths;
- never replay historical system/developer text as current authority;
- discard malformed data after the last verified boundary and record the omission;
- exclude encrypted/hidden reasoning, credentials, environment dumps, raw binary/base64, unresolved tool IDs, approval IDs, oversized tool output, stale instructions, and unsupported provider metadata;
- omit uncertain secret-bearing records rather than partially leaking them.

Use `IAgentTokenEstimator` for deterministic estimates. The effective input budget is the certified model context limit multiplied by the existing hard transfer threshold, minus the current governing prompt/projection, mandatory repository context, and a configured output reserve. If the server does not report a certified context limit or mandatory content cannot fit, automatic reconstruction fails closed. Selection keeps mandatory provenance/repository context first, then the public records after the latest accepted correlated artifact boundary, then earlier public records in deterministic newest-window order while rendering the selected records chronologically. Do not introduce a new model-generated summarizer; `Summary` means existing accepted repository summaries such as handoffs or operational deltas.

Add `RecoverDecisionSessionContext.prompt` as the single context-injection prompt. It includes a stable attempt/envelope marker, declares the material as recovered evidence rather than new instructions, forbids tool work, and requests a bounded acknowledgement. Acceptance requires a completed turn and provider/read evidence containing the exact marker and digest; assistant wording alone is not authoritative.

### Active Pointer, Turn, and Artifact Atomicity

The workspace database is canonical; `.agents` files remain repository-owned materializations.

- A recovered fresh replacement is created eagerly: send `thread/start`, capture the new provider thread ID, and stop before `turn/start`. This makes `ReplacementCreated` durable before `ContextInjectionPending` and requires a continuity-runtime create operation distinct from the existing lazy fresh open.
- Before a decision proposal, insert a `decision_session_turns` row linked to run, lineage, LoopRelay session, provider thread, and input hash.
- Persist request-write, accepted provider-turn ID, terminal status, and unknown-outcome boundaries through the existing turn-progress seam.
- After a completed proposal, use one SQLite transaction to store the raw output/hash, append the `loop_history` decision record with producer correlations, update router accounting on the active pointer, and mark the turn `OutputCommitted`.
- Materialize `.agents/decisions/decisions.md` from that committed record, verify its hash, and mark `ArtifactMaterialized`. Filesystem failure leaves an idempotently repairable database state rather than causing another model turn.
- `TransitionRuntime` records its normal raw-output evidence after the executor returns. On restart, the executor first looks for a committed turn for the same run/input hash and rehydrates `PromptExecutionResult` instead of submitting again.
- Replacement activation uses one separate SQLite transaction: verify expected active row version, insert/finish lineage, mark the attempt `RecoveryCompleted`, make the replacement authoritative, and update the active pointer. The in-memory replacement is not returned to `DecisionSession` before this commit succeeds.

Failures after `turn/start` submission are decision-turn reconciliation cases, not resume recovery. Planned transfer records a `PlannedTransfer` successor intent before closing the old process and activates the new lineage only after its seed/proposal commit. Execute completion retires the scoped active pointer only after `CertifiedCompletion` is durably observed. Failed turns and process disposal never delete lineage.

### Observable Vocabulary

Use a closed set of public outcomes:

- `ResumedOriginal`
- `ReplacementNativeFork`
- `ReplacementRecoveredFull`
- `ReplacementRecoveredPartial`
- `ReplacementRepositoryOnly`
- `ReplacementClean`
- `ProtocolRepairRequired`
- `RecoveryFailed`
- `UnknownOutcome`
- `ContinuityDisabled`

The CLI must never print “resumed” for a different provider ID. `PromptExecutionResult.Metadata`, canonical transition evidence, status output, SQLite/JSONL telemetry, and local diagnostics carry outcome, attempt/lineage IDs, old/new provider IDs, scope/run, profile/plan/mechanism/source/envelope digests, completeness, omissions, counts, durations, and redaction totals. They do not carry recovered transcript bodies or secret values.

## Persistence and Schema Evolution

The current workspace database schema is version 2; `DecisionSessionResumeState` is document schema version 1. Evolve the database to version 3 using ordered, transactional migrations rather than running a latest `CREATE TABLE IF NOT EXISTS` script and unconditionally stamping the version.

### Version 3 Tables and Changes

| Table/change | Purpose |
| --- | --- |
| `workspace_metadata.workspace_id` | Stable repository identity generated once |
| `session_continuity_profiles` | Immutable provider/version/schema/operation-support/constraint evidence keyed by profile digest |
| `decision_session_scopes` | Stable Execute scope, product causal identities, role, lifecycle and timestamps |
| `decision_session_lineage` | Provider session nodes, parent/root ancestry, mechanism, completeness and authority |
| `decision_session_active` | One row per non-retired scope with active lineage, router accounting, policy/projection digests and row version |
| `session_recovery_attempts` | Recovery Journal record, status, failure, selected plan/profile, idempotency and diagnostics |
| `session_recovery_plans` | Immutable canonical `RecoveryPlan`, planner/policy/mechanism versions, activation/validation strategy, expected completeness and digest |
| `session_recovery_sources` | Ordered source descriptors, verified bounds, normalizer, digest, completeness and omission metadata; no raw rollout body |
| `decision_session_turns` | Durable request/turn/output/materialization state and unknown-outcome reconciliation data |
| `session_transition_correlations` | Explicit join among transition run, lineage, recovery attempt, LoopRelay session and provider turn |
| `decision_session_legacy_imports` | One-time import/quarantine result and digest for old SQLite/JSON state |
| `loop_history` columns | Nullable producer run, lineage, provider thread/turn, and recovery-attempt references for migrated rows; required for new decision rows |
| `session_telemetry_events` columns | Provider thread, lineage, transition run, recovery attempt, event type and continuity outcome |

Add foreign keys and indexes for scope/status, parent lineage, provider session identity, nonterminal recovery attempts, transition run, and provider turn. Use canonical JSON only for versioned compound evidence; identities, statuses, timestamps, row versions, and join keys remain first-class columns.

### Required Transactions

1. **Begin attempt:** verify active scope/row version, insert `Pending`, and capture the original reference and continuity-profile evidence.
2. **Record recovery plan:** insert immutable plan/source/profile records, verify their canonical digests, and transition the attempt to `RecoveryPreparing` with the selected mechanism/version in one transaction.
3. **Record replacement identity:** transition `ReplacementCreating` to `ReplacementCreated`, insert the inactive child lineage, and record provider correlation.
4. **Complete recovery and activate:** verify the persisted plan and context/history validation result, compare-and-swap the old active row, mark the child authoritative, mark the old node superseded but retained, and complete the attempt atomically.
5. **Commit decision output:** store output/history/accounting/turn state and artifact producer correlation atomically before filesystem materialization.
6. **Retire scope:** after certified Execute completion, retire the active row and lineage in the same transaction as the lifecycle record.

Use `BEGIN IMMEDIATE`, foreign keys, and monotonically incremented row versions. Store methods return conflicts/corruption explicitly and never swallow SQLite, schema, or serialization errors.

### Legacy Migration

- Read schema v2 `decision_session_resume` and `.LoopRelay/decision-session.json` without deleting either first.
- Preserve the source digest, parse result, old thread ID when recoverable, document schema, and diagnostic in `decision_session_legacy_imports` within the migration transaction.
- Create a retained `LegacyUnscoped` lineage node for a valid old ID, but do not automatically bind it to the current Execute scope because the old record lacks epic/workflow identity. Surface `LegacyScopeUnverified` and fail closed unless deterministic current product evidence can prove the binding under an explicitly tested migration rule.
- Quarantine invalid/corrupt input as non-authoritative evidence; never silently clear it. Delete a valid legacy JSON export only after the database commit and only after its digest can be read back.
- Stop reading or writing `decision_session_resume` after migration. Remove `IDecisionSessionResumeStore`, `DecisionSessionResumeState`, `SqliteDecisionSessionResumeStore`, file/null implementations, and their DI/test doubles after the v3 migration path is covered.
- Update storage import/export/verification to include the new domains and to reject dangling lineage, multiple active rows for a scope, invalid state transitions, profile/plan/source digest mismatches, or unresolved nonterminal operations that are presented as complete.

## Deterministic Implementation Sequence

```text
M1 Protocol operation-profile and structured-result implementation
  -> M2 Stable scope and run correlation
      -> M3 RecoveryJournal, RecoveryPlan contracts, schema v3, active state, and lineage
          -> M4 Durable decision turns and restart reconciliation
              -> M5 Recovery sources, envelopes, and reconstruction mechanisms
                  -> M6 RecoveryPlanner, RecoveryRuntime, and coordinator integration
                      -> M7 Native fork mechanism implementation (disabled until certified)
                      -> M8 Workflow, lifecycle, CLI, and telemetry integration
                          -> M9 Compatibility certification, mechanism promotion, migration, and rollout
```

Milestones 1-8 implement and unit/integration-test one architecture without promoting unverified provider behavior. Milestone 9 owns all disposable live certification and writes the certified compatibility manifest. Native fork is always implemented behind `IRecoveryMechanism`, but its `ForkSupport` remains `Unknown` and it is ineligible until Milestone 9 promotion; a failed certification leaves reconstruction as the production mechanism without code branching or redesign.

## Milestone 1: Legal Protocol Negotiation and Structured Resume

### Objective

Make resume legal and observable, introduce the provider-neutral operation profile and structured result contracts, and ensure deterministic compatibility defects fail closed before any recovery implementation exists.

### Repository Changes

1. Add continuity contracts under `src/LoopRelay.Agents/Abstractions` and `Models/Sessions`:
   - `IAgentSessionContinuityRuntime` for negotiate, eager fresh-create, resume, read, write/seed, fork, and reconcile operations; read/write/fork remain profile-gated until their mechanisms are implemented;
   - `SessionContinuityProfile`, operation-support/constraint/evidence models, and digest serializer;
   - `SessionResumeResult`, `SessionResumeOutcome`, `SessionOperationStage`, structured error and transport progress;
   - provider-neutral session/content/fork references and results.
2. Extend `CodexAppServerMessage` to retain numeric error code, cloned `error.data`, complete response shape, method/request correlation, and parse-integrity status. Message text remains diagnostic-only.
3. Replace fixed initialize/resume frame builders in `CodexAppServerProtocol` with typed options. Serialize `capabilities.experimentalApi` and `excludeTurns` only from the negotiated/certified profile. The Milestone 9 harness may probe read/fork schemas; production read frames arrive in Milestone 5 and fork frames in Milestone 7.
4. Add `CodexCompatibilityManifest`, a canonical embedded manifest, schema-digest resolver, and `CodexSessionContinuityProfileResolver` under `Services/Codex/Compatibility`. Persist no inferred support from version alone.
5. Refactor `CodexAppServerSession.EnsureHandshakeAsync` to expose initialize evidence, request stage/progress, exact method result, and the resolved continuity profile. Add an eager fresh-create operation that completes `thread/start` and returns the thread ID with zero turns, plus bounded request timeouts distinct from cancellation.
6. Make `AgentRuntime` implement `IAgentSessionContinuityRuntime`. It remains responsible for launch, registry cleanup, and failed-process disposal, but returns `SessionResumeResult` rather than converting every eager-handshake exception to a recoverable exception.
7. Keep `IAgentRuntime.OpenSessionAsync` behavior for non-resuming sessions. Route decision resume through the new method. Deprecate `AgentSessionResumeException` and remove it after all callers/tests migrate.
8. Update `CodexAppServerProtocolTests`, `CodexAppServerMessageTests`, `CodexAppServerSessionTests`, `AgentRuntimeResumeTests`, and scripted app-server support to enforce offered-client negotiation, operation/parameter gates, profile narrowing, and structured failures.

### Tests and Exit Criteria

- Typed frame tests prove `experimentalApi` and `excludeTurns` are coupled through `ResumeSupport.Parameters.ExcludeTurns`.
- Profile-resolution tests prove explicit server evidence, exact manifest evidence, conflicts, and structured rejection precedence.
- Scripted transport tests prove successful same-ID resume, all required result classes, eager fresh creation with zero turns, timeout/cancellation distinction, and failed-process cleanup.
- Deterministic protocol errors expose method/code/profile digest and never open a fresh session; no classifier reads message text.
- Unsupported/unknown operations are not emitted.
- Ordinary fresh held-open and one-shot tests remain unchanged.

Milestone 1 is complete when the implementation and scripted contracts pass. Live version certification and manifest promotion occur only in Milestone 9.

## Milestone 2: Stable Scope and Transition Correlation

### Objective

Give all session work a durable Execute scope and the transition run identity before a provider request is sent.

### Repository Changes

1. In `TransitionRuntimeContracts.cs`, add `PromptExecutionRequest` and change `IPromptExecutor.ExecuteAsync` to receive it. Include run/workflow/stage/transition, rendered prompt, input snapshot hash, root invocation mode, and metadata.
2. In `TransitionRuntime.cs`, generate and persist the run as today, then pass the same run ID to the executor. Update every prompt executor and test double in CLI, Plan, Roadmap, Completion, and orchestration tests mechanically; non-decision executors otherwise keep their behavior.
3. In `WorkflowChaining.cs`, preserve the root `WorkflowInvocation` while selecting bounded internal workflow controllers. Add root invocation provenance to `WorkflowControllerRequest`/`TransitionRuntimeRequest` instead of losing `DefaultChained`, `ForcedEvalChain`, or `ForcedTraditionalChain` at the internal `BoundedExecute` conversion.
4. Add stable workspace-ID creation/read APIs to `LoopRelayWorkspaceDatabase` and stop using the process-random `Repository.Id` for persisted continuity scope.
5. Add `DecisionSessionScopeResolver` in `LoopRelay.Cli.Services.Decisions`. Resolve and validate `PreparedEpic` and `ExecutablePlan` canonical products, then compute `DecisionSessionScopeId` using the versioned canonical contract.
6. Update product creation in `UnifiedCliComposition` and canonical product persistence so `PreparedEpic` and `ExecutablePlan` causal identities are derived from canonical content/producer hashes. Reject missing, stale, ambiguous, or process-local causal identities at Execute entry.
7. Extend prompt-result metadata and canonical transition evidence contracts with optional session scope/run correlation fields without changing non-session transitions.

### Tests and Exit Criteria

- `TransitionRuntimeTests` prove the executor receives the persisted run ID and input hash.
- Workflow-chain tests prove default, `--eval`, `--traditional`, and bounded execute retain distinct root invocation provenance while resolving the same Execute policy.
- Workspace-ID tests prove stability across process-shaped reopen and isolation across repositories.
- Scope tests prove identical products yield the same scope, changed epic/plan content yields a new scope, mode alone does not change scope, and ambiguous/stale products block.
- Product persistence tests prove causal identity survives export/import and is not based on random IDs or evidence ordering.
- All executor implementations compile against one request contract; no decision-only overload remains.

Milestone 2 is complete when a decision prompt can log one stable tuple of workspace/scope/run/workflow/stage/transition/invocation before opening Codex.

## Milestone 3: RecoveryJournal, RecoveryPlan, Schema v3, and Lineage

### Objective

Establish `RecoveryJournal` and `RecoveryPlan` as provider-neutral architectural primitives and replace overwrite-only resume state with one durable, scoped, transactional persistence model before adding fallback behavior.

### Repository Changes

1. Refactor `LoopRelayWorkspaceDatabase.EnsureSchemaAsync` into ordered migrations for new, v1, and v2 databases; set `CurrentSchemaVersion = 3` only after the v2-to-v3 transaction succeeds.
2. Add the v3 tables, columns, indexes, checks, and foreign keys listed in the schema section. Add canonical serializers for versioned profile/plan/source/diagnostic JSON.
3. Add provider-neutral `RecoveryJournal`, `RecoveryPlan`, `IRecoveryRuntime`, `IRecoveryPlanner`, `IRecoveryMechanism`, `IRecoverySource`, catalog contracts, records, and `IRecoveryStore` under `LoopRelay.Orchestration.Primitives`. Runtime/planner/mechanism implementations arrive in Milestones 5-7; their stable contracts and plan serialization are fixed here. `IRecoveryStore` exposes:
   - scoped active read with `Present`, `Absent`, `Corrupt`, and `Conflict` results;
   - begin attempt and compare-and-swap state transition;
   - immutable continuity-profile, recovery-plan, and source insertion;
   - inactive lineage insertion;
   - atomic `CompleteRecoveryAndActivateAsync`;
   - resume success, planned transfer, scope retirement, and nonterminal-attempt lookup;
   - decision-turn operations used by Milestone 4.
4. Implement the pure `RecoveryJournal` transition functions and `SqliteRecoveryStore` in Orchestration. The journal validates domain transitions; the store validates compare-and-swap and relational constraints without duplicating planning/orchestration policy.
5. Implement migration/quarantine of `decision_session_resume` and legacy JSON. Preserve recoverable identity and diagnostics; never auto-attach an unscoped legacy thread.
6. Update canonical storage verification/import/export and `LoopWorkspaceDatabase.HasUsableLoopHistoryDatabase` to use the migration service rather than a hard-coded `1`/current-version check.
7. Compose the new store in `UnifiedCliComposition`. Keep recovery disabled; at this milestone resume succeeds through the new active pointer or fails closed while retaining it.
8. Remove the old resume store/model implementations after migration tests prove their input formats. Do not write both old and new stores during rollout.

### Tests and Exit Criteria

- Migration tests cover empty, v1, v2, valid/corrupt schema-1 resume JSON, future-version rejection, interrupted migration rollback, and idempotent reopen.
- Pure journal tests cover every allowed and forbidden transition, emitted domain events, terminal immutability, and the absence of provider/I/O dependencies.
- Recovery-plan tests prove canonical serialization/digest, required activation/validation/reconciliation fields, planner/policy/mechanism versioning, and rejection of an unregistered mechanism version.
- Store tests cover row-version conflicts, stable idempotency keys, and nonterminal lookup.
- Transaction tests inject failure before/after each statement and prove active pointer, lineage authority, and journal completion never diverge.
- Concurrency tests prove two runtime instances cannot activate two replacements or create two active rows for one scope.
- Integrity tests prove dangling lineage, duplicate provider IDs, source/profile/plan digest mismatch, and multiple authoritative nodes fail verification.
- Durability errors propagate as failed-closed results; no catch converts them to “no resume state.”

Milestone 3 is complete when the journal and plan can be tested without `DecisionSession`, the original pointer can remain active beside a durable inactive replacement, and only the atomic completion API can switch authority.

## Milestone 4: Durable Decision Turns and Restart Reconciliation

### Objective

Close the current gaps between provider turn completion, resume/accounting state, decision history, live artifacts, and canonical transition evidence before a recovery turn is introduced.

### Repository Changes

1. Extend `AgentTurnResult`, Codex turn parsing, and `AgentTurnProgress` to expose provider turn ID and `WriteStarted`, `Submitted`, `Accepted`, `Terminal`, and `Unknown` boundaries.
2. Add a continuity-aware progress observer in `DecisionSession` that updates `decision_session_turns` using the run/input identity from `PromptExecutionRequest`.
3. Change `DecisionSession.RunAsync` to accept a decision execution context rather than only a cancellation token. Before proposal submission, create the turn row and correlation record.
4. Replace `PersistResumeStateAsync` plus `LoopArtifacts.PersistDecisionsAsync` ordering with one store operation that commits output, output hash, router accounting, correlated `loop_history`, and turn state.
5. Extend `ILoopHistoryStore`, `SqliteLoopHistoryStore`, and `LoopHistoryRecord` with producer correlations. New decision writes require them; migrated handoff/delta rows may remain null.
6. Materialize and verify live `decisions.md` after the database commit. Add an idempotent repair method that recreates the live file from the exact committed history record.
7. Before running a decision turn, query by run ID and input hash. Rehydrate a completed `PromptExecutionResult`, repair its artifact, or reconcile an accepted/unknown provider turn; never submit a second turn while the first outcome is uncertain.
8. Link the returned result to `canonical_transition_evidence`. Update transition persistence readers/tests so a run can join to its session turn and artifact sequence.
9. Replace transfer and failed-turn clears. Planned transfer records a successor intent and retains the old lineage; known failed/unknown turns retain the active pointer and require the corresponding reconciliation policy.

### Restart Matrix and Exit Criteria

Inject process failure at:

- before turn write;
- after write but before ack;
- after provider turn acceptance;
- after terminal output but before SQLite commit;
- after SQLite output commit but before live-file materialization;
- after materialization but before `TransitionRuntime` raw evidence;
- after raw evidence but before product/effect completion.

For every point, tests must prove one of: no work occurred and a retry is safe; one exact result is rehydrated; the live artifact is repaired without a new turn; or the run is `UnknownOutcome` and stops. Provider turn, history sequence, lineage, run, and artifact must be queryable without timestamp inference.

Milestone 4 is complete when restarting at any audited decision-output boundary cannot generate a duplicate proposal or lose the accepted output.

## Milestone 5: Recovery Sources, Normalization, and Bounded Content

### Objective

Implement recovery sources, canonical content processing, and the three reconstruction mechanisms behind the contracts established in Milestone 3. Live provider certification remains deferred to Milestone 9.

### Repository Changes

1. Add typed `thread/read` and conversation-write/seed frames/results to the Codex adapter. Gate them with `ConversationReadSupport` and `ConversationWriteSupport`; an uncertified profile leaves them ineligible in production.
2. Add exact thread-ID content resolution. Replace continuity use of `FileSystemCodexRolloutLocator` with a `CodexRolloutRepository` that validates `session_meta.id`, resolves sessions/archived sessions under the effective `CODEX_HOME`, and returns typed absent/corrupt/partial/permission results. Existing usage telemetry may keep its heuristic only for non-continuity metrics until it is migrated.
3. Add versioned thread-read and rollout fixture parsers under the Codex adapter. They produce provider-neutral ordered records and never expose encrypted reasoning as recoverable content.
4. Implement the recovery-source contracts from Milestone 3:
   - `ThreadReadRecoverySource`;
   - `RolloutSalvageRecoverySource`;
   - `RepositoryContinuationRecoverySource` backed by existing products, `LoopArtifacts`, projections, prompt-policy identity, and bounded repository status;
   - `RecoverySourceCatalog`, which enumerates source observations but does not select a mechanism.
5. Add `RecoveryEnvelope`, completeness/source/omission models, canonical serializer, normalizer, sanitizer, path normalizer, deduplicator, budget calculator, and builder under `LoopRelay.Cli.Services.Decisions.Recovery`.
6. Add `src/LoopRelay.Core/Prompts/RecoverDecisionSessionContext.prompt`; generated rendering accepts only the canonical envelope and marker.
7. Persist only descriptors, hashes, sanitized bounded envelope when policy requires restart reconstruction, and counts. Never persist raw rollout text or secret-bearing rejected records.
8. Implement and register `ThreadReadReconstructionMechanism`, `RolloutReconstructionMechanism`, and `RepositoryReconstructionMechanism`. Each declares stable identity/version, required operation-profile support and source kinds, expected completeness, activation/validation/reconciliation strategies, and eligibility evidence. They share canonical envelope/create/inject primitives rather than duplicating orchestration.
9. Add fixture corpora for public thread projections and rollouts. Fixtures must be synthetic, harmless, and scrubbed.

### Tests and Exit Criteria

- Synthetic thread-read and rollout fixtures cover text, command/file/tool records, compaction, cancellation/failure, partial tails, missing metadata, duplicate events, unsupported items, and archived paths.
- Item ordering and intentional omissions have versioned golden contracts.
- Exact thread-ID resolution never chooses a same-cwd “latest” rollout.
- Each synthetic corruption/archive fixture has a typed result and last verified boundary; ambiguous middle corruption does not pass as full recovery.
- sanitizer tests remove credentials, auth headers/files, environment dumps, high-risk tool output, external absolute paths, binary/base64, hidden reasoning, and stale instructions without recording their values;
- envelope serialization/digest and selection are deterministic under shuffled source enumeration;
- an unknown `MaximumRecoverableContext` or insufficient output reserve makes textual mechanisms ineligible rather than guessing;
- oversized mandatory content refuses recovery, while selective/repository-summary cases expose exact omissions and completeness.
- mechanism contract tests prove each implementation reports eligibility independently, consumes only declared sources, produces the expected `RecoveryPlan` requirements, and can be resolved by identity/version through the catalog.
- catalog/planner-contract tests prove source or mechanism registration order cannot change canonical eligibility evidence.

Milestone 5 is complete when the source/content pipeline and all reconstruction mechanisms pass synthetic contract tests without `DecisionSession` or live provider certification.

## Milestone 6: RecoveryPlanner, RecoveryRuntime, and Coordinator Integration

### Objective

Implement provider-neutral planning/execution over the journal, store, sources, and mechanisms, then replace the catch-and-clear behavior with a thin decision-session integration.

### Repository Changes

1. Implement `RecoveryPlanner` as a pure deterministic policy engine over failure, scope, `SessionContinuityProfile`, source observations, budget, mechanism eligibility, and versioned ranking policy. It emits one canonical `RecoveryPlan`; it does not execute provider operations.
2. Implement `RecoveryRuntime` in Orchestration. It owns resume/retry, source discovery, planner invocation, plan persistence, mechanism resolution/execution/reconciliation/validation, journal transitions, and atomic activation. It uses `IRecoveryStore`; it never reaches into `DecisionSession`.
3. Add a thin `DecisionSessionRecoveryCoordinator` in `LoopRelay.Cli.Services.Decisions.Recovery`. It builds `RecoveryRuntimeRequest` from the current scope/run/session inputs, calls the runtime, and converts the typed result to the existing `DecisionSession` lifecycle result. It contains no source precedence, mechanism selection, or journal transition code.
4. Change `DecisionSession.OpenOrResumeSessionAsync` to:
   - resolve scope and nonterminal journal work first;
   - read the scoped active pointer without clearing it;
   - begin `Pending` before resume;
   - consume the structured result;
   - restore router accounting only for `SuccessfulResume`;
   - return explicit continuity metadata with the session.
5. Implement the failure matrix in planner/runtime policy:
   - retry typed transient pre-turn failures within the same attempt;
   - record `ProtocolRepairRequired` for deterministic protocol/config/auth/permission/programming failures;
   - enter recovery only for verified unavailable or policy-eligible corrupt sources;
   - stop on cancellation, post-turn, storage failure, scope ambiguity, untrusted content, or unknown outcome.
6. Persist the selected plan in `RecoveryPreparing`; resolve its exact registered mechanism/version; then allow that mechanism to drive `ReplacementCreating`, eager `thread/start`, inactive lineage insertion, `ContextInjectionPending`, marker reconciliation, validation, and the runtime's atomic completion call.
7. Atomically complete/activate the replacement, then return it with `seeded = true`. The next decision proposal follows the existing warm path; current governing prompt policy and pending handoff remain the next-turn delta.
8. On activation failure, close the in-memory replacement, retain the original pointer and inactive child, and return failed closed. Never continue on an uncommitted child.
9. Add recovery outcome, plan, mechanism, profile, source, and completeness metadata to `PromptExecutionResult` immediately so transition evidence can distinguish the path before the full telemetry milestone.
10. Replace the old DecisionSession fallback tests with planner, runtime, mechanism, coordinator, activation, and restart tests using fake continuity runtime/store/source/mechanism implementations.

### Exit Criteria

- pure planner tests prove deterministic eligibility/ranking and identical plan digests for identical inputs regardless of catalog enumeration order;
- runtime tests prove the persisted plan, not procedural branches, determines the mechanism, activation strategy, validation, and restart behavior;
- unavailable original plus verified full public content selects `ThreadReadReconstructionMechanism`, yields one `ReplacementRecoveredFull` lineage, and submits one context turn;
- selective, summary, and repository-only cases yield their exact labels and omission manifests;
- deterministic `experimentalApi`, malformed invocation, auth, config, storage, cancellation, and unknown failures create no replacement and preserve the original pointer;
- replacement create/context failures leave the original active and the journal reconcilable;
- restart from every nonterminal journal status resumes/reconciles the same attempt and never creates a second child;
- restart after `RecoveryPreparing` reloads and revalidates the persisted plan rather than replanning; post-side-effect failure never changes mechanisms in place;
- coordinator tests prove it only maps request/result data and remains unchanged when mechanism registrations/ranking change;
- a different thread ID is never reported or persisted as resumed;
- no arbitrary raw transcript, system/developer instruction, secret, or oversized tool result reaches `turn/start`.

Milestone 6 is complete when reconstructed continuity runs entirely through `RecoveryRuntime`/`RecoveryPlan`/`IRecoveryMechanism`, the coordinator is mechanism-agnostic, and the old catch/clear/fresh path no longer exists.

## Milestone 7: Native Fork Mechanism Implementation

### Objective

Implement native fork behind the same operation profile, planning, runtime, mechanism, journal, and store boundaries as reconstruction. Keep it ineligible in production until Milestone 9 certifies an exact provider profile.

### Repository Changes

1. Add typed `thread/fork`, fork-result, child-metadata, and fork-reconciliation operations to `IAgentSessionContinuityRuntime`/Codex adapter. `ForkSupport = Unknown` prevents emission outside tests.
2. Implement `NativeForkRecoveryMechanism` with stable identity/version and the same `EvaluateEligibility`, `ExecuteAsync`, `ReconcileAsync`, and `ValidateAsync` contract as reconstruction mechanisms.
3. Require `ForkSupport = Supported`, stable parent/child identity semantics, a declared fidelity contract, and a deterministic unknown-response reconciliation strategy for eligibility. The mechanism does not infer support from method recognition or CLI version.
4. Persist `ReplacementCreating` before fork, reconcile an uncertain response, insert the inactive child lineage, validate the reported parent/effective-history digest, and request atomic activation without context injection.
5. Emit `ReplacementNativeFork`, never `ResumedOriginal`, and retain source/child descriptors plus profile/plan/mechanism digests.
6. Register the mechanism in the same catalog. Ranking remains policy data consumed by `RecoveryPlanner`; neither runtime nor coordinator changes when fork is registered.

### Tests and Exit Criteria

- Contract tests prove `Unknown`/`Unsupported` fork profiles are ineligible and issue no provider request.
- Synthetic supported-profile tests prove the planner can rank fork before or after reconstruction solely from versioned policy and mechanism evidence.
- Scripted app-server tests prove request/response parsing, stable parent/child validation, source mismatch rejection, zero implicit context turn, and `ReplacementNativeFork` terminology.
- Unknown-response tests prove zero/one/multiple child reconciliation results map to known failure/specific child/`UnknownOutcome` without a second fork.
- Runtime/journal restart tests prove the same persisted plan/mechanism version is reconciled after `ReplacementCreating` or `ReplacementCreated`.
- Coordinator tests remain unchanged when the native mechanism is registered.
- No production compatibility-manifest entry or ranking promotion is written in this milestone.

Milestone 7 is complete when native fork is a fully tested but profile-gated plugin and the unmodified runtime/coordinator can execute it from a synthetic supported `RecoveryPlan`. Live fidelity/reconciliation certification and production promotion occur only in Milestone 9.

## Milestone 8: Workflow Lifecycle, Modes, Observability, and Operator Controls

### Objective

Integrate continuity into the existing unified orchestration and make every outcome visible and supportable without changing bounded workflow semantics.

### Repository Changes

1. Compose the coordinator, `RecoveryRuntime`, `RecoveryPlanner`, `IRecoveryStore`, journal, source/mechanism catalogs, continuity-profile manifest, and recovery telemetry in `UnifiedCliComposition`; remove the null decision console and use the unified CLI output/error writers.
2. Flow the root invocation provenance from Milestone 2. Apply the same Execute policy for default, `--eval`, `--traditional`, and bounded `execute`; bounded eval/traditional/plan never initialize decision recovery.
3. Add continuity metadata to `PromptExecutionResult`, canonical transition evidence, raw-output evidence, product producer correlations, and workflow status projections.
4. Extend existing session telemetry records/sinks with typed continuity events rather than creating another state store. Compose telemetry in production and use exact provider thread IDs instead of cwd/time guesses.
5. Update `UnifiedCliStatusFormatter` and status loading to show active scope/lineage, original/replacement ancestry, last terminal recovery, unresolved attempt/turn, completeness, and required operator action. Show IDs/digests/counts, not content.
6. Emit the observable vocabulary through `ILoopConsole`. Deterministic protocol output explicitly says that no replacement was started; repository-only recovery explicitly says the original conversation was unavailable.
7. Update planned transfer to use lineage/successor intent and update Execute completion handling to retire the active scope after `CertifiedCompletion`. Verify a later epic cannot attach the old thread.
8. Replace `LoopRelay_DECISION_RESUME` semantics:
   - `0`/`false` disables resume and automatic replacement but preserves all active/journal state;
   - if an active pointer exists, return `ContinuityDisabled` and stop instead of silently opening clean;
   - add a separate recovery-mechanism policy setting (`resume-only`, `reconstructed`, `certified`) for rollout; it gates completed mechanisms, not storage or protocol correctness;
   - clean replacement remains an explicit, separately named operator action and is never implied by disabling resume.
9. Update runtime prerequisite diagnostics to validate the installed version/schema manifest before Execute recovery work. Update `README.md` and `config/settings.default.json` only for these implemented operator settings and supported-version behavior.

### Tests and Exit Criteria

- CLI snapshots prove exact wording for resume, each replacement completeness, protocol failure, disabled continuity, failed recovery, and unknown outcome.
- Production composition tests prove warnings are not routed to `TextWriter.Null` and continuity/session telemetry is registered.
- Mode tests reach Execute from all four entry shapes, assert identical policy, and assert distinct invocation provenance; bounded non-Execute commands never touch continuity storage or Codex recovery.
- Status/storage verification tests expose nonterminal attempts and reject inconsistent authority/lineage.
- Transfer/restart/completion tests prove planned successor lineage, no early clear, retirement after certification, and no cross-epic resume.
- Telemetry security tests prove recovered bodies and secret values never appear in console, SQLite telemetry, JSONL, or failure messages.
- Kill-switch tests prove disabling continuity preserves the original ID and never starts a fresh replacement.

Milestone 8 is complete when an operator can determine exactly what happened and what remains active from CLI/status and durable state alone.

## Milestone 9: Certification, Profile Promotion, Migration, and Rollout

### Objective

Perform all disposable live/runtime certification in one milestone, promote only exact verified provider profiles and mechanism rankings, then ship the single continuity architecture incrementally without masking defects or maintaining dual persistence.

### Certification Infrastructure

1. Add `tests/LoopRelay.Agents.Compatibility.Tests`, include it in `LoopRelay.slnx`, and build a subprocess harness that accepts explicit versioned binary paths.
2. For every run, create a disposable repository and `CODEX_HOME`; generate experimental app-server schemas only into a disposable directory; never read, fork, or copy the user's real session store.
3. Produce versioned, scrubbed golden operation results and a canonical certification-evidence digest. The compatibility manifest is generated/reviewed from that evidence, not edited from memory.
4. Keep live compatibility tests out of ordinary hermetic unit-test assumptions: a release-certification command supplies the exact binaries, while ordinary solution tests validate the checked-in manifest and golden contracts.

### Certification Traceability

| Implementation milestone | Milestone 9 exit evidence |
| --- | --- |
| M1 protocol operation profile/results | C1 protocol/version/negotiation matrix |
| M2 stable scope/correlation | C4 mode and end-to-end correlation tests |
| M3 journal/plan/store/migration | C4 migration, CAS, transaction, lineage, and verification tests |
| M4 durable turns | C4 crash-injection and duplicate-prevention tests |
| M5 sources/envelopes/reconstruction mechanisms | C2 read/corruption/archive/context certification |
| M6 planner/runtime/coordinator | C2 plus C4 reconstructed-recovery/restart tests |
| M7 native fork mechanism | C3 fork fidelity and unknown-outcome reconciliation |
| M8 lifecycle/modes/observability | C4 workflow, CLI, telemetry, security, and completion tests |

### Certification C1: Protocol, Version, and Negotiation

For every candidate supported Codex binary, run the audit-bounded matrix:

- no client experimental capability plus `excludeTurns`;
- client experimental capability plus `excludeTurns`;
- no capability and no `excludeTurns`;
- capability plus `thread/read` and recognized `thread/fork` against disposable valid/invalid IDs;
- capture `codex --version`, app-server help, initialize response, experimental generated-schema digest, and source rollout hashes.

C1 exit criteria:

- the exact `0.142.5` fixture advertises `experimentalApi`, legally emits `excludeTurns`, resumes the valid original ID, and does not mutate its rollout;
- the old invalid field/capability combination maps to `DeterministicProtocolFailure`, records method/code/profile digest, and starts no fresh session;
- every promoted operation/parameter descriptor is supported by initialize evidence or an exact version/schema fixture;
- unsupported/unknown operations are not invoked;
- the promoted supported set and schema digests are explicit, and unlisted binaries fail closed for continuity;
- no production classification branches on error-message text.

### Certification C2: Read, Corruption, Archive, and Context Boundaries

Across every C1-supported profile:

- capture golden `thread/read` projections for text-only, command-heavy, file-edit, MCP, compacted, cancelled, failed, and partial sessions;
- run disposable rollout variants with missing newline, truncated tail, malformed middle, missing metadata, duplicate event, unsupported item, and sessions/archived placement;
- capture model identity and server-reported context/token metadata and compare it with the configured 256,000 assumption.

C2 exit criteria:

- operation profiles describe public item ordering, intentional omissions, partial-read semantics, and exact source locations;
- exact thread-ID resolution never chooses a same-cwd latest rollout;
- every corruption/archive fixture has a typed result and verified boundary; ambiguity never passes as full recovery;
- captured `MaximumRecoverableContext` and output reserve are sufficient for enabled textual mechanisms; absent evidence leaves them ineligible;
- the three reconstruction mechanisms' expected completeness, source requirements, and ranking evidence match the live fixtures;
- sanitizer, deterministic envelope, overflow refusal, and omission contracts pass against the captured public data without storing secrets.

### Certification C3: Native Fork Fidelity and Unknown-Outcome Reconciliation

Across every C1-supported profile:

1. Fork a valid harmless disposable source.
2. Prove a new durable ID and rollout, stable parent identity, no source mutation, no implicit new turn, and equivalent effective history/compaction.
3. Compare native effective history with the reconstruction plan for fidelity, security, size, and observability.
4. Drop the fork response after server processing, restart, and prove the exact child can be discovered uniquely from parent plus persisted attempt/correlation evidence.
5. Test missing, corrupt, partial, and archived source behavior established in C2.

C3 promotion criteria:

- `ForkSupport = Supported` only for an exact version/schema profile passing all checks;
- unknown-response reconciliation identifies zero or one exact child; multiple candidates become `UnknownOutcome`;
- source mutation and implicit-turn counts are zero;
- forked history is at least as complete as the selected reconstruction plan and does not bypass scope/policy freshness;
- the versioned ranking policy records whether fork ranks before or after reconstruction for that exact profile and its evidence digest.

Failure of any criterion leaves `ForkSupport` `Unknown`/`Unsupported`; `NativeForkRecoveryMechanism` stays registered but ineligible. No implementation milestone is reopened.

### Certification C4: System and Release

Run:

```powershell
dotnet test LoopRelay.slnx
dotnet test tests\LoopRelay.Agents.Compatibility.Tests\LoopRelay.Agents.Compatibility.Tests.csproj
dotnet test tests\LoopRelay.Agents.Tests\LoopRelay.Agents.Tests.csproj
dotnet test tests\LoopRelay.Orchestration.Primitives.Tests\LoopRelay.Orchestration.Tests.csproj
dotnet test tests\LoopRelay.Cli.Tests\LoopRelay.Cli.Tests.csproj
```

The compatibility command is supplied explicit certified binary paths and disposable homes. Release evidence records binary versions, schema/profile/plan/mechanism fixture digests, test result digests, and fixture identities without copying user session data.

C4 exit criteria:

- unit tests certify profile/result/journal/plan/mechanism/source/normalization/budget/policy contracts;
- persistence tests certify migrations, CAS, transactions, lineage, active identity, corruption preservation, and storage verification;
- compatibility tests certify the declared version set and all six audit-bounded runtime questions;
- crash-injection tests certify every resume, replacement, context, proposal, artifact, and active-switch boundary;
- end-to-end tests certify original resume, reconstructed recovery, optional native fork, unavailable source, corrupt source, duplicate prevention, completion retirement, and restart continuation;
- default, `--eval`, `--traditional`, and bounded execute pass the same recovery certification while bounded non-Execute modes remain unaffected;
- no deterministic defect test produces a replacement;
- no replacement is labeled resumed;
- no raw/secret recovery content appears in telemetry or diagnostics;
- fresh one-shot and process-warm nondecision workflows retain their established behavior.

### Profile and Policy Promotion

- Update the compatibility manifest only from passing C1-C3 evidence for the exact version/schema digest.
- Store operation support, parameter support, limits, fidelity, partial-read and reconciliation semantics in the profile; store mechanism priority in a separate versioned recovery-ranking policy.
- Never let a runtime probe mutate the checked-in profile or ranking policy. A structured production rejection may narrow an in-memory attempt and emit diagnostics, but promotion requires this milestone again.

### Rollout Sequence

1. **Protocol-only release:** after C1, ship the corrected negotiation, structured result, exact supported profile, and failed-closed behavior. Recovery policy remains `resume-only`.
2. **State-model release:** after the applicable C4 migration/persistence subset, enable schema v3, scoped active state, lineage, journal, plan persistence, and durable decision turns from M2-M4. Old stores are read only by one-time migration and never dual-written.
3. **Reconstructed recovery release:** after C2 and the reconstructed C4 scenarios, enable `reconstructed` for verified `UnavailableSession` and explicitly permitted corruption cases. Keep native fork ineligible.
4. **Certified mechanism release:** after C3 and fork C4 scenarios, `certified` may rank native fork for promoted profiles; other profiles continue with reconstruction or fail closed.
5. **Default enablement:** make `certified` the default only after all C4 evidence passes and staged telemetry shows no unresolved-attempt, plan-digest, or duplicate-child invariant violations.

Rollback changes only the mechanism policy to `resume-only`; it never downgrades the database, clears the active pointer, deletes lineage/plans, or reintroduces the old catch-and-fresh path.

Milestone 9 and the plan are complete only when C1-C4 evidence is repository-owned, the promoted profile/ranking policy exactly matches that evidence, and the acceptance criteria below all pass.

## Consolidated Repository File Map

The implementation is expected to touch the following existing areas and add files beside them. Exact type-per-file splitting may follow repository style, but ownership must not move across these project boundaries.

### `LoopRelay.Agents`

- Modify `IAgentRuntime.cs`, `IAgentSession.cs`, `AgentRuntime.cs`, `AgentSessionSpec.cs`, `AgentTurnResult.cs`, and turn-progress models.
- Modify `CodexAppServerProtocol.cs`, `CodexAppServerMessage.cs`, `CodexAppServerSession.cs`, and Codex session parsing/reader code.
- Add continuity runtime, `SessionContinuityProfile`, structured operation-result, Codex compatibility-manifest/profile resolver, exact rollout repository, thread-read/write/fork, and reconciliation types.
- Retire `AgentSessionResumeException.cs` after every caller and test uses the structured resume result.
- Update service registration and embed the certified compatibility manifest.

### `LoopRelay.Orchestration.Primitives`

- Modify `TransitionRuntimeContracts.cs`, `TransitionRuntime.cs`, `WorkflowChaining.cs`, canonical persistence models/stores, and workflow/product correlation contracts.
- Add provider-neutral `RecoveryJournal`, `RecoveryPlan`, `RecoveryRuntime`, planner, mechanism/source catalogs, scope, active state, lineage, recovery attempt/source, decision-turn, continuity-event, and `IRecoveryStore` contracts/implementations.
- Add `SqliteRecoveryStore` and canonical profile/plan/source serializers; keep the pure journal as the state-transition validator.
- Retire `IDecisionSessionResumeStore`, `DecisionSessionResumeState`, file/null resume services after schema migration coverage.

### `LoopRelay.Core`

- Refactor `LoopRelayWorkspaceDatabase.cs` into explicit migrations and add stable workspace identity.
- Add `RecoverDecisionSessionContext.prompt` and generated prompt coverage.
- Update shared logical/persistence verification contracts only as required for the new canonical domains.

### `LoopRelay.Infrastructure`

- Extend `RuntimeDiagnostic`/severity handling with structured continuity code, correlation IDs, and redacted evidence fields used by prerequisite and operator diagnostics.
- Reuse `ILoopConsole` for visible continuity events; do not introduce another console abstraction or place recovered content in diagnostic messages.

### `LoopRelay.Cli`

- Replace resume logic in `DecisionSession.cs`; remove `PersistResumeStateAsync` and clear-on-failure behavior.
- Replace `SqliteDecisionSessionResumeStore.cs` with composition of `SqliteRecoveryStore`/`IRecoveryStore`.
- Add the thin recovery coordinator plus registered repository/thread-read/rollout sources, reconstruction mechanisms, envelope/normalization/sanitization/budget services, and CLI composition adapters. Planning/runtime/journal policy remains in Orchestration.
- Modify `UnifiedCliComposition.cs`, `UnifiedCliRunner.cs`, `UnifiedCliStatusFormatter.cs`, `CliArguments.cs`/invocation provenance, `LoopArtifacts.cs`, loop-history stores/models, projection/prompt policy inputs, and completion/transfer integration.
- Extend session telemetry composition, records, SQLite/JSONL sinks, console output, and runtime prerequisites.
- Update settings/README only for shipped compatibility and operator controls.

### Tests and Solution

- Add `tests/LoopRelay.Agents.Compatibility.Tests` and disposable/golden fixtures; add the project to `LoopRelay.slnx`.
- Extend Agents unit tests for frames, parsing, continuity-profile negotiation, structured failures, read/write/fork, and transport progress.
- Extend Orchestration tests for run context, journal, plans, planner/runtime, mechanism/source catalogs, migrations, store CAS, lineage, transactions, and corruption.
- Replace current DecisionSession fallback/resume-store tests with coordinator, recovery-content, turn-journal, restart, mode, CLI, telemetry, and end-to-end certification tests.
- Extend storage, workflow chain, loop history/artifact, product causal identity, and production composition tests.

## Final Acceptance Criteria

- A valid active Codex thread resumes on every declared supported profile with the same provider ID and restored accounting.
- The `experimentalApi`/`excludeTurns` contract is legally negotiated and live-certified; a regression fails closed and starts no replacement.
- Resume results are structured and contain all required categories without string-based classification.
- `SessionContinuityProfile` models operation support, parameter constraints, fidelity, partial-read behavior, deterministic identifiers, reconciliation, and context limits without Codex-specific `CanX` policy branches.
- The active decision session is scoped to a stable workspace and Execute epic/plan boundary.
- `RecoveryJournal` is a provider/I/O-free state-machine primitive; `RecoveryRuntime` is its only production driver and `IRecoveryStore` is its persistence boundary.
- Every replacement is authorized by a durable canonical `RecoveryPlan` naming one mechanism/version, sources, envelope, activation, validation, reconciliation, expected completeness, profile, and idempotency identity.
- Recovery mechanisms are catalog-registered plugins; the planner ranks eligibility, the runtime executes the persisted plan, and the decision-session coordinator remains mechanism-agnostic.
- Original and replacement identities, complete ancestry, mechanism, activation point, provenance, completeness, diagnostics, continuity profile, recovery plan, source digests, and timestamps are durable and queryable.
- The original pointer survives replacement preparation and any failed/unknown replacement operation.
- Replacement activation, authoritative lineage, and journal completion are atomic and compare-and-swap guarded.
- Restarts reconcile nonterminal resume, fork/create, context, proposal, and artifact operations without duplicates.
- Recovery sources and pluggable mechanisms are selected by a persisted `RecoveryPlan` from the operation profile and versioned policy, not hard-coded branches in `DecisionSession` or its coordinator.
- Reconstructed context is normalized, sanitized, bounded, versioned, deterministic, and explicit about loss; arbitrary raw transcripts are never replayed.
- Native fork is selected only for exact certified profiles with proven fidelity and unknown-outcome reconciliation; otherwise the same architecture uses reconstruction.
- Decision output, history, active accounting, transition evidence, and repository materialization are correlated and restart-safe.
- Default, `--eval`, `--traditional`, and bounded execute share the same Execute continuity policy and preserve their invocation provenance; bounded non-Execute commands remain unaffected.
- Production CLI/status/telemetry reports every continuity outcome truthfully and never calls a replacement a resume.
- Legacy state is migrated or quarantined without early deletion, ambiguous scope attachment, or dual writes.
- Execute completion retires the active scope, planned transfer preserves lineage, and a later epic cannot inherit stale session state.
- Full unit, integration, persistence, compatibility, security, restart, migration, mode, and end-to-end certification passes with repository-owned evidence.
