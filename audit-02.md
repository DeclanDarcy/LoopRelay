# Architecture Convergence Regeneration Audit

Date: 2026-07-12

Branch: `merge-4`

Observed HEAD: `61602a62` (`Complete Eval full-chain certification evidence`)

Purpose: planning input for regenerating `architecture-convergence-roadmap.md`

## 1. Audit posture and source precedence

This audit records the current merged codebase, not the state that existed when
`ctx.md` was written. It uses the following precedence:

1. Current production code and active composition.
2. Current schema, tests, certification harness, and retained local certification evidence.
3. Accepted merge ADRs under `docs/architecture/decisions/`.
4. `architecture-convergence-roadmap.md` for owner-ratified architectural intent and the
   accepted milestone boundary.
5. `ctx.md` as a useful M7-era handoff, but not as operational authority.

`ctx.md` correctly identifies the accepted M0-M7 commits and much of the M7 architecture. It
predates the merge resolutions, logical-v9 schema convergence, prompt-dispatch redesign,
recovery additions, production-certification system, Codex 0.144.x compatibility work, and
fixture hardening. Its instructions about agent cadence, questions, foreground execution, and
artifact durability are historical process advice, not constraints on the regenerated roadmap.

The same caution applies to comments and documentation embedded in the code: several still
describe M6/M7 behavior that the accepted merge ADRs and current implementation subsequently
changed.

## 2. Non-negotiable milestone boundary

The last completed milestone in the **architecture convergence program** is M7, Runtime
Authority. No presence of later contracts, no component test, and no production-certification
result promotes M8 or a later architecture milestone to complete.

| Architecture milestone | Accepted commit | Audit disposition |
|---|---|---|
| M0 Architecture Constitution | `9f6418f5` | Completed before merge |
| M1 Workspace State Authority | `8c0b11a4` | Completed before merge; current implementation has later merged extensions |
| M2 Evaluation Authority | `87c97444` | Completed before merge; vocabulary conflict now requires re-ratification |
| M3 Product Authority | `ab10e06b` | Completed before merge; current observer/persistence split is broader |
| M4 History Authority | `b1b9aa8a` | Completed before merge; current schema adds evidence sets and projection effects |
| M5 Policy Authority | `96d41f44` | Completed before merge; policy is now v4 and configuration separation is incomplete |
| M6 Prompt Authority | `45053775` | Completed before merge; accepted ADR-0004/0009 changed its composition model |
| M7 Runtime Authority | `10dd9494` | Last completed architecture milestone |
| M8 onward | none | Not completed; some foundations exist and some behavior is production-wired |

There is also a **production certification roadmap** with milestones numbered 1-15. Those
numbers are a separate namespace. Local evidence currently passes certification M1-M14 and is
16/17 at certification M15, but this does not mean architecture-convergence M8-M14 are complete.

## 3. Change history after M7

The current branch is not simply M7 plus fixture tests. Since `10dd9494`, 299 tracked files have
changed, with roughly 36,629 insertions and 6,257 deletions. The important branch events are:

- `0a7dc6c4`: merged `architecture-convergence` into `merge-4`.
- `d8a7e50a`: merged `add-fixtures` into `merge-4`.
- `6f5df67f`: resolved the architectural merge and incorporated M7-era runtime work.
- `656e36b7`: hardened exact Codex compatibility-fixture authority.
- `76fb6ba6`: hardened runtime certification campaigns and several production seams exposed by
  the fixtures.
- `61602a62`: completed the retained Eval full-chain certification evidence.

Eleven accepted ADRs now govern the merge. The most consequential deltas from the old roadmap
are:

- logical schema identity/family/version and canonical v9 convergence;
- explicit separation of raw configuration from resolved policy;
- logical history/evidence authority with SQLite as the current mechanism;
- prompt templates plus versioned prompt-policy profiles composed before hashing;
- recommendations as causal evidence evaluated by Policy Authority;
- a single-attempt transition runtime separated from recovery and effects;
- a required prompt-dispatch gateway with durable pre-dispatch facts/lifecycle;
- a thin typed application boundary.

These ADRs are accepted merge inputs. Where they contradict the old roadmap, regeneration must
either incorporate them or explicitly supersede them; silently restoring the old wording would
reopen resolved conflicts.

## 4. Current solution and deployable topology

`LoopRelay.slnx` contains 12 source projects and 12 test projects.

The supported public executable remains `LoopRelay.Cli`. `LoopRelay.Plan.Cli` and
`LoopRelay.Roadmap.Cli` still compile, and retain 33 and 224 C# source files respectively, but
their `Program.cs` files only print retirement messages directing users to the unified CLI.
Their bodies remain executable specification and test surfaces, not supported entry points.

`LoopRelay.Certification` is now a real executable in the solution. It is an authority around the
published CLI, not a workflow inside LoopRelay. It creates disposable repositories and runtime
overlays, invokes the public CLI, applies independent observations/oracles, retains scrubbed
evidence, and cleans up cases unless retention is requested.

The production composition is still highly concentrated:

| File | Current size | Architectural significance |
|---|---:|---|
| `UnifiedCliComposition.cs` | 6,355 lines | Composition plus many prompt/product/effect/feature adapters and execution bodies |
| `LoopRelayWorkspaceDatabase.cs` | 2,144 lines | Schema inspection, convergence, migration, and physical manifest |
| `RepositoryObserver.cs` | 1,523 lines | Filesystem, persistence projection, compatibility, Git, storage, and workflow observation |
| `CanonicalWorkflowDefinitionSketches.cs` | 806 lines | Production catalog authority despite the provisional name |
| `TransitionRuntime.cs` | 631 lines | Canonical single-attempt lifecycle |

The target authority boundaries exist as types, but `UnifiedCliComposition` remains a major
convergence hotspot and still contains behavior that belongs to policy, completion, recovery,
effect, and workflow-feature authorities.

## 5. Current production route

The public route is:

```text
Program
  -> CLI argument/configuration loading
  -> UnifiedCliComposition.CreateProduction
  -> CanonicalCliApplicationService (ILoopRelayApplication)
  -> WorkflowChainRunner
  -> WorkflowController
  -> TransitionRuntime (one authorized attempt)
  -> TransitionEffectCoordinator
  -> repository re-observation
```

Production composition performs fail-closed catalog validation at startup and constructs:

- `RepositoryObserver` and `WorkflowResolver`;
- all four workflow definitions and both chains;
- `PromptDispatchGateway` over canonical prompt fact/lifecycle stores;
- `TransitionRuntime` with candidate, receipt, gate, evidence, attempt, freshness, and atomic
  commit stores;
- `TransitionEffectCoordinator` over durable effect-intent state;
- `WorkflowController` and `WorkflowChainRunner`;
- durable chain-boundary evidence and workflow-instance recording.

The route is materially stronger than the M7 handoff description. In particular, prompt facts
are no longer optional gateway deposits written at the transport threshold. Current architecture
persists the immutable rendered prompt and `Planned`/`Authorized` dispatch lifecycle before the
provider may be called.

The application boundary is only partially converged. Typed request/result/status contracts
exist, and `UnifiedCliRunner` is mostly a renderer/exit-code adapter. However:

- `CanonicalCliApplicationService` still contains command dispatch, migration, direct storage
  SQL, run-loop policy, status queries, completion cleanup, and output formatting concerns;
- recovery request record types exist but the CLI parser exposes no recovery commands;
- the status service directly constructs `SqliteRecoveryStore`, reads persistence snapshots,
  and queries prompt/effect state rather than consuming a complete canonical read model;
- the application outcome collapses all specific cannot-proceed reasons into one
  `CannotProceed` value plus exit code 4.

M20 should therefore not be treated as pre-completed by ADR-0011's contract skeleton.

## 6. Workflow catalog and product topology

The active catalog has production-derived denominators of:

- 4 workflows;
- 2 chains and 4 chain boundaries;
- 24 stages;
- 46 transitions;
- 26 distinct product identities;
- 136 distinct gate obligations;
- 56 declared effects across 7 categories;
- 5 active execution postures in the current denominator;
- 44 distinct prompt identities and 50 compiled `.prompt` assets.

The chains remain:

```text
TraditionalRoadmap -> Plan -> Execute
EvalRoadmap        -> Plan -> Execute
```

Both roadmap workflows converge on `PreparedEpic` and `MilestoneSpecificationSet`. Plan converges
on `ExecutablePlan`, `OperationalContext`, `ExecutionDetails`, `ExecutionMilestoneSet`, and
`ExecutionReadiness`. Execute terminates at `CertifiedCompletion`.

The catalog now includes declared clean-input surfaces for selected disk-reading transitions and
has a broader runtime-outcome vocabulary (`EffectsPending`, `RecoveryRequired`,
`InputInvalidated`, `ConcurrentStateConflict`, `HumanDecisionRequired`, provider incompatibility,
and compatibility import). It still has important limitations:

- output surfaces are not first-class catalog contracts;
- blocking commit and required-asynchronous push are not structurally auto-inserted from output
  surfaces;
- most effects are workflow-authored explicit declarations;
- `WorkflowTransitionDefinition.Validators` remains a string list with no clear active owner;
- the catalog is still named `Sketches`, is constructed repeatedly, and is code-only rather than
  an immutable versioned catalog object with one stable catalog identity;
- progression remains mostly linear and first-eligible; parallelism and effect conflict are
  explicitly unsupported future obligations.

These gaps keep architecture M13 and M14 open even though the catalog and kernel foundations are
extensive and full-chain fixtures are green.

## 7. Single-attempt transition lifecycle

`TransitionRuntime` now implements the ADR-0008 shape more closely than the old roadmap text. One
authorized attempt performs:

1. definition resolution and input-product resolution;
2. input-gate evaluation;
3. prompt-context construction and causal input snapshot;
4. transition-run/attempt authorization and attempt-intent persistence;
5. read receipt and consumed-input manifest persistence;
6. rendered prompt composition and required gateway preparation;
7. transition start and dispatch-intent boundary evidence;
8. exactly one provider dispatch;
9. normalized raw-output persistence;
10. interpretation and candidate registration;
11. product validation and output-gate evaluation;
12. input-freshness revalidation;
13. one SQLite transaction for promoted products, state projection, attempt completion, gate
    evidence, lifecycle evidence, and effect intents;
14. return with either a terminal non-success or `EffectsPending`.

An exception after dispatch intent becomes durable is treated as an unknown provider outcome and
returns `RecoveryRequired`; it is not blindly retried. Recovery attempts reuse the logical
transition-run identity and mint a new attempt identity only when a persisted recovery plan
authorizes that action.

Important residual seams:

- `WorkflowChainRunner` always supplies `FreshAttemptAuthorization`; transition recovery plans
  are not a universal public progression route.
- Run and policy-resolution writes in `CanonicalCliApplicationService` are caught and ignored as
  best effort even though ADR-0008 says required causal persistence fails closed.
- A fresh workflow-instance identity is created whenever a non-completed workflow is invoked;
  restart/re-entry semantics need explicit validation against durable instance lineage.
- The controller executes pending effects immediately in the same public invocation and then
  re-observes. This is not yet the durable background/reconciliation model described for M8.

## 8. Effect architecture: implemented foundation, M8 still open

The merged code has real effect infrastructure:

- effect intents are inserted atomically with transition state;
- intents have stable IDs, ordering, and idempotency keys derived from transition run plus effect;
- effect state changes append records;
- thrown effect calls become `Unknown` and require reconciliation;
- the controller refuses to call a transition complete until the coordinator reports required
  effects complete;
- real publication preflights `.agents` topology and treats post-preflight exceptions as unknown.

However, the permanent M8 property is not yet established:

- there is no general worker/restart path that discovers and reconciles pending intents;
- `TransitionEffectCoordinator` walks intents immediately and stops on the first non-success;
- push is not modeled as required-asynchronous work distinct from blocking local commit;
- commits/pushes are not auto-generated from declared output surfaces;
- `UnifiedEffectExecutor` contains workflow-specific branching and also updates workflow/stage
  projections, so effect execution and progression authority are not fully separated;
- when a transition has multiple effects, its helper paths can repeat broader product/state
  materialization while returning the record for only the requested effect;
- completion certification performs archive and commit/push work inside the prompt execution body
  before the catalog's archive effect is coordinated;
- some feature services still perform Git or filesystem mutation directly.

Certification proves important publication and unknown-outcome scenarios, but it does not change
the architecture milestone disposition. M8 must converge these foundations into one durable,
reconciled protocol and remove direct mutations from feature execution bodies.

## 9. Recovery and continuity: substantial implementation, incomplete authority

There are two related recovery domains:

1. Generic transition-boundary recovery (`TransitionRecoveryPlan`, classifier, coordinator,
   boundary journal, recovery authorization).
2. Decision-session continuity (`RecoveryRuntime`, `SqliteRecoveryStore`, sources, mechanisms,
   lineage, active state, turns, attempts, and envelopes).

The decision subsystem is deeply implemented and production-wired for decision sessions. Sources
include exact thread read, Codex rollout salvage, and repository continuation. Mechanisms include
resume, reconstruction, and native fork, selected by resolved recovery policy.

Codex continuity is fail-closed by exact server version and app-server schema digest. Checked-in
scrubbed profiles exist for 0.142.5, 0.144.0, and 0.144.1. For all three, exact resume,
`excludeTurns`, and read are promoted as supported. Conversation write, fork, and maximum
recoverable context remain unknown because their stronger safety/reconciliation claims were not
certified. Implemented reconstruction/fork mechanisms therefore remain policy/profile gated.

Warm Plan and Execute continuity now have SQLite-backed checkpoints and exact-profile resume
paths, which is later than `ctx.md`. They still depend on feature-specific stores and prompt
executor state, and the generic recovery runtime is constructed inside the decision execution
body rather than composed once as a universal authority.

M9 remains open because:

- generic transition recovery is not exposed as the normal CLI resume/reconcile path;
- effect reconciliation is incomplete;
- cancellation salvage semantics remain unratified in the architecture roadmap;
- recovery classifications and operator actions still include generic `OperatorUnblock` language;
- recovery, warm-session continuity, and completion checkpoints are separate mechanisms without
  one cross-domain recovery read model;
- fork/read/write/capacity behavior is intentionally only partially certified.

## 10. Workspace state, persistence, history, and compatibility

The canonical database remains `.LoopRelay/persistence/looprelay.sqlite3`, but the current logical
model is materially broader than the M7 handoff.

Current constants are:

- schema identity: `looprelay.workspace-state`;
- schema family: `CanonicalWorkspace`;
- logical version: 9.

The schema source contains 64 `CREATE TABLE IF NOT EXISTS` declarations and 38 index declarations.
It covers the original roadmap/plan compatibility domains, canonical workflow state, causal spine,
read receipts, policy/prompt/runtime facts, continuity and recovery, effect intents, evidence sets,
compatibility import operations, projection effects, recommendations, profile evaluations, and
dispatch lifecycle.

Current schema behavior follows ADR-0001/0007:

- identity/family/version are interpreted together, not by numeric version alone;
- canonical v8 upgrades to canonical v9 in place;
- recognized partial v9 branch shapes converge transactionally to complete canonical v9;
- unknown or stamped-incomplete v9 fails closed;
- existing workspace identity is preserved;
- historical evidence not observed during migration remains null rather than fabricated;
- legacy continuity v3 enters through explicit compatibility import, not as canonical v3;
- convergence and migrations record dedicated receipts and a complete physical-shape fingerprint.

History is logically ledger-authoritative with SQLite as the current mechanism. Numbered `.agents`
history files are derived projections/compatibility inputs; a projection failure after ledger
commit is retryable and must not rewrite the fact. The current schema also gives a history fact one
stable evidence set containing typed evidence items.

Residual persistence/storage seams:

- `RepositoryObserver` consumes `CanonicalPersistenceReadModel` for canonical workflow state but
  still performs direct SQLite reads for compatibility and decision-resume observations;
- `CanonicalCliApplicationService` directly reads snapshots and writes metadata;
- the status/read model does not yet expose every policy, prompt, recovery, effect, import, and
  uncertainty fact through one stable public projection;
- storage `init/import/export/sync` primarily ensure schema and metadata; they do not yet implement
  the truthful import/export/round-trip authority described by architecture M11/M12;
- runtime migration is attempted before every command, including status, so observation is not
  universally read-only;
- compatibility tables and retained roadmap readers remain extensive and have not been retired.

## 11. Product and observation authority

The current observer explicitly separates filesystem-authoritative collaboration products from
ledger-authoritative system facts. Collaboration products are read from `.agents/**`; canonical
rows cannot substitute for missing files or mask present files. Read receipts carry commit,
per-file hashes, surface hash, validation result, and causal lineage. Input freshness is rechecked
before promotion, and drift can yield `InputInvalidated` or `ConcurrentStateConflict`.

`RepositoryObserver` now receives a persistence-owned projection and independently observes the
filesystem/repository. This is an important improvement over the pre-merge audit. It also retains
large compatibility-specific scans and domain heuristics. Product authority is therefore broadly
functional but not yet free of workflow-specific observation logic.

The Project Context contract is exactly nine files under `.agents/ctx`. The stale test repositories
that omitted this contract have now been corrected. The regenerated roadmap should treat the
nine-file input as part of the active public contract and continue requiring every fixture that
claims production behavior to materialize it explicitly.

## 12. Configuration, policy, recommendations, and runtime profiles

Current shipped configuration is `settings-v3`; operational policy is `policy-v4`. The default
document separates:

- `runtime.brain` model/effort configuration;
- supported Codex profile configuration;
- execution, decision-resume, recovery, and runtime operational policy;
- permission configuration.

`OperationalPolicyResolver` still resolves exactly three policy layers: built-in, workspace, and
invocation. Known ambient environment variables are translated to invocation overrides; explicit
`--policy` flags win. Unknown keys, malformed values, and duplicate same-precedence overrides
fail. Policy identity remains `pol_v1_` plus a truncated SHA-256 of canonical effective policy.

Execution recommendations are immutable causal evidence bound to a decision product, source
attempt/session/turn, model, effort, and rationale. Policy evaluates them as accepted,
constrained, rejected, ignored, stale, invalid, or unsupported and persists the effective runtime
profile. Runtime authorization resolves the durable evaluation/profile instead of consuming the
recommendation directly.

This authority is not fully converged:

- ordinary one-shot and several feature sessions still receive `BrainConfiguration` directly
  from settings through `AgentSpecs`;
- the execution fallback profile takes model/effort directly from that configured brain;
- provider capability evidence for adaptive execution is currently synthesized from all enum
  values and `XHigh`, not loaded from a durable observed provider-capability snapshot;
- runtime and prompt-policy identities used by the outer CLI run are provisional literals
  (`runtime_cli_application`, `prompt_policy_cli_application`);
- `CODEX_HOME` is still read ambiently inside decision-session execution;
- the configuration resolver records provenance for configured brain facts, but Policy Authority
  does not yet produce every role's complete session policy.

ADR-0002 and ADR-0005/0010 should therefore be treated as target contracts with partial production
implementation, not finished M5/M7 closure.

## 13. Prompt and runtime authority after the merge

The old roadmap's M6 ruling said implementation-first policy prose was template-owned and no
prompt-policy profile mechanism would exist. Accepted ADR-0004 and ADR-0009 deliberately chose a
different merged target: invariant templates and versioned prompt-policy profiles are distinct
inputs and are composed before hashing.

Current code follows the ADR direction:

- `PromptPolicyProfile` is a typed input;
- `CanonicalPromptComposer` composes template, policy profile, and consumed inputs;
- `PromptDispatchGateway.PrepareAsync` durably writes rendered bytes/hash and dispatch lifecycle
  before runtime dispatch;
- runtime dispatch loads content by prompt-fact identity;
- provider-visible text cannot be appended by the transport;
- multi-turn prompt dispatch records session/turn/prompt correlation;
- unknown post-start outcomes transfer to recovery.

Some old template-owned-policy tests now fail because the duplicated template section was removed.
This is not merely stale test wording: it exposes a direct contradiction between the old roadmap
and the accepted merge ADRs. Roadmap regeneration must explicitly record which decision governs
and update M6's historical/current-state explanation without pretending the original accepted
implementation never existed.

Runtime Authority also now includes:

- exact Codex compatibility identity probing;
- app-server v2 session continuity operations;
- normalized transport progress and provider turn identities;
- runtime prerequisite inspection before any production transition;
- telemetry, usage-limit retry, and input-wait decorators;
- capability/profile-gated resume/read/reconstruction/fork behavior.

Codex remains the only production provider. The provider-neutral interfaces remain useful, but no
multi-provider fallback behavior should be inferred.

## 14. Completion authority

The active completion path is stronger than the pre-fixture audit described. Production
composition now wires:

- non-implementation completion review;
- canonical prompt dispatch for completion prompts;
- SQLite execution evidence;
- `CompletedEpicArchiveService` with `SqliteCompletedEpicArchiveMaterializer`;
- a durable completion-certification checkpoint used by the following route transition;
- final warm-session/checkpoint retirement after certified workflow exit;
- zero-model/no-mutation behavior when `CertifiedCompletion` is already durable.

The live M11 and both full-chain campaigns provide strong evidence for archive closure and
idempotent rerun. Architecture M15 is still open because:

- the completion service still uses generic `Blocked` domain vocabulary and blocker evidence;
- completion review, certification, archive, Git commits/pushes, route interpretation, and final
  state are not yet one recoverable closure transaction owned only through effects;
- archive mutation occurs inside the completion execution body even though the catalog also
  declares an archive effect;
- direct `CommitGate` use remains inside completion handling;
- checkpoint state is stored as opaque `workspace_metadata` JSON rather than a dedicated canonical
  completion read model;
- partial-effect recovery must be unified with the generic recovery/effect protocol.

## 15. Vocabulary and doctrine conflicts that regeneration must resolve

The old roadmap bans generic `Blocked` everywhere and says blockedness is derived, never latched.
The current merged tree contains hundreds of blocker/block/unblock references, including active
completion outcomes, non-implementation review states, evidence directories, recovery actions,
README contract text, and retained legacy code.

Not all occurrences are architectural violations: prose such as “blocked because” can be clear,
and retained legacy bodies are intentionally still compiled. But active typed uses conflict with
the old ruling:

- `CompletionCertificationServiceOutcome.Blocked`;
- `NonImplementationCompletionReviewStatus.Blocked`;
- `TransitionRecoveryDisposition.OperatorUnblock`;
- `LoopOutcome.CompletionBlocked` in the retained execution loop;
- README claims about `unblock`, although the current CLI parser exposes no `unblock` verb.

The regenerated roadmap must distinguish natural-language explanation, compatibility vocabulary,
and canonical typed vocabulary. It should not copy the old absolute ban without reconciling the
accepted merged code, nor silently treat the current reintroduction as an owner-approved reversal.

## 16. Certification system and fixture hardening

The statement in the old architecture roadmap that there is “no certification runner” or fixture
corpus is now false.

The certification harness is runtime-generated rather than primarily a checked-in fixture tree:

- source: `src/LoopRelay.Certification`;
- deterministic/component tests: `tests/LoopRelay.Certification.Tests`;
- static Codex compatibility fixtures:
  `tests/LoopRelay.Agents.Compatibility.Tests/Fixtures`;
- disposable cases: `.tmp/certification/milestone-N/<case-guid>/`;
- retained evidence: `.tmp/certification/evidence`;
- fixture runtime settings: `CertificationFixtureSettings`;
- configured certification model/effort: `gpt-5.3-codex-spark`, `medium`;
- provider-turn timeout: 60 minutes.

Harness commands cover canary, milestones 2-15, platform certification, and a production-derived
coverage ledger. Live milestones require explicit Codex binary and auth paths. M13 is the
Traditional full chain; M14 is the Eval full chain. M15 aggregates release dimensions and does
not execute every campaign itself.

The current production-derived ledger contains 469 distinct obligations. Its denominator is
derived from workflows, stages, gates, transitions, prompts, postures, effects, products, chains,
prompt assets, known-risk records, and schema. The raw `ledger` command reports these as uncovered
except for optional canary credit; campaign-level evidence aggregation is performed separately by
M15. A future convergence step should connect obligation-level evidence credit to the durable
campaign evidence rather than relying only on dimension-level pass files.

Local retained evidence is ignored by git because `.tmp` is ignored; it is current runtime
evidence, not a durable repository artifact. That is appropriate for credentials/privacy but means
release provenance and cross-machine retention need an explicit external owner if the roadmap
expects durable certification history.

## 17. Current certification evidence

As observed on this branch:

- status canary and certification M2-M14 evidence files are present and passing;
- Windows x64 platform certification passes separator, line-ending, UTF-8, Git, path-length, and
  normalized-contract checks;
- Codex compatibility fixtures are current for the exact checked-in version/schema pairs;
- Eval full-chain M14 passes all 29 expected transitions, default and forced selection,
  both workflow boundaries, producer convergence, independent repository verification, Git
  publication, archive closure, traceability, idempotent rerun, process cleanup, and privacy scan;
- M14 observed zero additional sessions and no user-tree/Git mutation on the terminal rerun;
- M15 reports 16 of 17 release dimensions passing;
- the sole M15 gap is genuine Linux platform evidence (`platform-linux.latest.json` is absent);
  cross-platform contract agreement therefore remains false and M15 classification is 6.

This evidence is architecturally valuable and should be preserved in regenerated roadmap gates.
The now-green deterministic suite strengthens the baseline, but it must not be used to claim
unimplemented authority singularity.

## 18. Current build and test baseline

Verification performed for this audit:

```text
dotnet build LoopRelay.slnx --no-restore
  succeeded, 0 warnings, 0 errors

dotnet test LoopRelay.slnx --no-restore
  1,770 passed, 0 failed, 5 skipped, 1,775 total

dotnet test tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj
  415 passed, 0 failed, 0 skipped
```

Previously failing projects after reconciliation:

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `LoopRelay.Cli.Tests` | 415 | 0 | 0 |
| `LoopRelay.Plan.Cli.Tests` | 109 | 0 | 0 |
| `LoopRelay.Roadmap.Cli.Tests` | 473 | 0 | 0 |

All test projects passed. The five skipped tests are live Codex approval/posture checks in
`LoopRelay.Agents.Tests`; the separate static compatibility test project passed 4/4.

The 52 failures were reconciled rather than suppressed. The work included:

- updating stale template-owned policy, retained-history, schema-diagnostic, terminal-idempotency,
  stage/progression, and raw-attempt-versus-effect-coordination expectations;
- making production-like fixtures provide the nine-file Project Context, canonical workspace
  causality, independent `.agents` Git topology, and correct per-session turn indices;
- propagating stalled/failed/unknown effect outcomes through the coordinator and canonical state;
- correcting cancellation classification before subsequent cancellable I/O;
- exporting publication projections from the canonical workspace database rather than the retired
  database path; and
- materializing local-verification effects at their declared canonical evidence paths.

The regenerated roadmap should preserve this green, authority-aligned component suite as an entry
gate. Future failures must still be classified as stale legacy expectations, changed accepted
contracts, or production regressions; live certification and component evidence must be reconciled,
not averaged.

## 19. Statements from the old documents that are now stale

| Old statement | Current reality |
|---|---|
| No CI/certification runner/fixture corpus/durable proof bundle | A complete runtime-generated certification executable and evidence model now exist; external CI scheduling is still absent |
| Schema v9 only reflects M7 append-only runtime facts | Logical canonical v9 is now the union/convergence model for continuity, recovery, effects, recommendations, prompt dispatch, evidence sets, and compatibility |
| Rendered prompt fact is attempted-send evidence minted at transport | Current accepted gateway persists prompt fact and pre-dispatch lifecycle before provider dispatch |
| Prompt policy is template-owned; no profile mechanism | Accepted ADR-0004/0009 and current code use versioned prompt-policy profiles composed before hashing |
| Read/fork capability bits are absent | Continuity request/result contracts include read, seed, fork, and reconciliation; exact promoted support remains narrower |
| Warm Plan/Execute restart is entirely deferred | Feature-specific durable continuity checkpoints and exact-profile resume paths now exist, but are not yet universal recovery authority |
| Chain-boundary evidence is process-local | Active composition now writes canonical chain-boundary events; full restart semantics still need architectural completion |
| Completion omits SQLite archive/evidence components | Active composition now wires both materializer and execution-evidence store |
| Only Codex 0.142.5 compatibility fixture exists | 0.142.5, 0.144.0, and 0.144.1 exact fixtures exist |
| M7 test baseline is the current baseline | Post-hardening reconciliation is green: 1,770 pass / 0 fail / 5 skip |
| `unblock` is a supported public command | README says so, but the current CLI parser does not expose it |

## 20. Authority status for roadmap regeneration

| Authority | Current state | What remains before architectural closure |
|---|---|---|
| Workspace State | Canonical v9 identity/family/shape convergence; broad causal schema | Remove direct consumers of physical tables; settle migration-on-status/read-only semantics |
| Evaluation | Typed gates, specific runtime outcomes, freshness checks | Reconcile generic Blocked vocabulary and remaining workflow-local validation |
| Product | Filesystem/ledger split, receipts, freshness, candidates | Reduce observer heuristics; complete declared input/output surface contracts |
| History | Logical ledger, evidence sets, SQLite mechanism, projections | Finish projection/reconciliation ownership and canonical consumers |
| Policy | v4 operational policy and recommendation evaluation | Resolve every role/session profile; replace synthetic capability evidence and direct brain config |
| Prompt | Required composition/fact/dispatch lifecycle gateway | Ratify profile-vs-template decision in roadmap; eliminate residual feature-local framing/identities |
| Runtime | Exact Codex profiles, capability gates, normalized sessions/turns | Universal runtime-profile authorization and continuity semantics |
| Effects (M8) | Durable intents, ordering, idempotency key, immediate coordinator | Auto-insert commit/push, async/restart worker, reconciliation, eliminate direct mutations |
| Recovery (M9) | Generic transition plans plus rich decision recovery | Public universal recovery path, cancellation ruling, effect/completion integration |
| Interaction (M10) | Outcome/request-shaped types only | Durable requests/responses, clean-input offer-to-commit, restart and CLI path |
| Storage (M11) | v9 schema verification and narrow commands | Truthful domain import/export/sync/verify and interrupted mutation recovery |
| Import (M12) | Schema convergence and legacy continuity importer | Portfolio-wide preview/import/receipt/non-authoritative marking |
| Catalog (M13) | One active code catalog, validation, 46 transitions | Stable catalog identity/version, full surfaces/effects/recovery declarations, remove `Sketches` posture |
| Kernel (M14) | Single-attempt runtime, controller, chain runner | Universal recovery/effects/interaction lifecycle and removal of feature sequencing bodies |
| Completion (M15) | Active review/archive/evidence/checkpoint/terminal rerun | One recoverable closure plan through effects; vocabulary and partial-effect semantics |
| Read Model (M16) | Typed CLI snapshot and persistence projection | One complete evidence-linked query model; CLI/application stop direct querying |
| Roadmap/Plan/Execute (M17-M19) | Public unified routes plus retained bodies | Prove behavior parity, converge restart/effects, delete legacy bodies on acceptance |
| Application Boundary (M20) | Typed internal boundary and retired old entry points | Thin service/composition, public recovery/storage contracts, one query/command authority |
| Retirement (M21) | Declaration debris reduced; legacy retained | Delete accepted legacy bodies/adapters and verify one plausible change location |

## 21. Regeneration constraints and recommended starting order

The regenerated architecture roadmap should start from M7 accepted, but it cannot simply copy the
old M8-M21 sequence unchanged. It should preserve the authority dependency logic while inserting
or explicitly front-loading current-state reconciliation.

The essential order is:

1. **Ratify the post-merge baseline.** Reconcile accepted ADRs with the old M2/M5/M6/M7 text,
   especially prompt-policy ownership, generic Blocked vocabulary, required prompt evidence,
   logical schema v9, and configuration-versus-policy boundaries.
2. **Preserve evidence agreement.** Keep the restored green component suite and current live
   certification contracts as mandatory entry gates for every regenerated milestone.
3. **Finish Effect Authority (architecture M8).** Existing durable intents are the dependency for
   recovery, truthful storage mutation, completion closure, and later workflow retirement.
4. **Finish universal Recovery, then Interaction (M9-M10).** Reuse the implemented recovery
   substrate, but move it out of decision-only/feature-specific paths and settle cancellation
   salvage before interaction and closure consume it.
5. **Converge Storage and Import (M11-M12).** Build on journaled effects/recovery and logical-v9
   compatibility; do not preserve label-only import/export commands as final behavior.
6. **Complete declarative Catalog and universal Kernel (M13-M14).** Move remaining feature
   sequencing, output surfaces, auto effects, recovery, and validation into stable declarations
   and universal mechanics.
7. **Complete Completion and Read Model (M15-M16).** Route closure exclusively through recovered
   effects and expose all pending/uncertain/actionable state through one query model.
8. **Converge Roadmap, Plan, and Execute bodies, then boundary and retirement (M17-M21).** Delete
   retained bodies only after the public route, green component suite, and current live fixtures
   prove the accepted behavior.

Production certification should run alongside these steps as an independent evidence program.
Each architecture milestone should name which production-derived obligations it adds or
invalidates, which certification tier must pass, and which lower-tier tests remain required. The
roadmap must never infer architecture completion solely from a campaign pass, but it should also
never accept an architectural refactor that silently invalidates current full-chain evidence.

## 22. Essential open decisions

The regenerated roadmap needs explicit owner decisions or explicit ADR carry-forward for:

1. Whether ADR-0004/0009 definitively supersede M6's no-profile/template-owned policy ruling.
2. Which typed uses of `Blocked` remain valid completion/compatibility language, and which must
   return to specific derived cannot-proceed outcomes.
3. Cancellation salvage across pre-dispatch, accepted/unknown provider work, validated output,
   partial effects, and partial completion closure.
4. Interaction request timeout/default/isolation/trust-evidence semantics.
5. Whether status is strictly read-only or is allowed to trigger schema migration/convergence.
6. The exact public semantics of storage import/export/sync and the owned legacy workspace
   portfolio.
7. Whether certification evidence remains local ephemeral `.tmp` data or has an external durable
   release-evidence authority.
8. How current static/live Codex profiles are retired and how a new exact profile becomes
   release-supported.
9. Completion block/failure/resume cleanup semantics under the unified effect/recovery protocol.
10. Full-roadmap generation intent, Plan restart behavior, and Execute first-run/review order from
    the original open-decision docket.

## 23. Regeneration success criterion

A regenerated `architecture-convergence-roadmap.md` will be credible only if it starts from the
current merged system and makes the following distinctions explicit:

- accepted milestone versus implemented foundation;
- production-wired behavior versus retained executable specification;
- deterministic component evidence versus replay, live transition, and live chain evidence;
- architectural authority versus current storage mechanism;
- accepted merge ADR versus stale pre-merge roadmap wording;
- a passing fixture campaign versus a green complete test baseline;
- compatibility support implemented in code versus capability support certified for an exact
  provider profile.

The codebase has advanced substantially beyond the M7 handoff and has unusually strong live
evidence. Its immediate risk is no longer lack of architecture. It is false convergence: treating
the coexistence of target contracts, feature-specific implementations, green campaigns, stale
tests, and retained legacy bodies as though one singular authority already exists. The regenerated
roadmap should use the merged foundations aggressively while requiring each remaining authority to
become the only durable place where its behavior can be changed.
