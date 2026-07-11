# Loop Relay Continuous Production Certification System Roadmap

## Purpose

This roadmap defines the safest implementation sequence for constructing a continuous production certification system for Loop Relay. It converts the architectural audit into capability milestones; it does not prescribe code, data structures, APIs, file formats, or implementation mechanics. Milestone specifications created from this roadmap must preserve that distinction.

The target is continuous, explainable confidence in the published `LoopRelay.Cli` using real Codex sessions, real disposable repositories, production workflow definitions and prompts, canonical SQLite state, real local Git topology, production session postures, interruption and restart, independent oracles, and idempotent closure.

Fixtures are one reusable source of certification evidence, not the system's architectural center. The target is not a large collection of miniature application repositories. Fixture repositories remain deliberately tiny. Workflow, persistence, provider, Git, fault, configuration, recovery, and oracle complexity belongs to the certification system and its scenarios.

## Architectural baseline and scope

The roadmap starts from the audited 2026-07-10 baseline of 1,576 passing tests, five skipped live Codex approval/protocol checks, and no failures. That component suite remains a prerequisite and regression layer; it is not counted as proof of assembled production behavior.

The production system under test is the unified `LoopRelay.Cli`. Its active authority includes:

- the four workflow identities `TraditionalRoadmap`, `EvalRoadmap`, `Plan`, and `Execute` defined from `CanonicalWorkflowDefinitionSketches`;
- the declared `TraditionalRoadmap -> Plan -> Execute` and `EvalRoadmap -> Plan -> Execute` chains;
- the canonical transition runtime, resolver, gates, effects, and SQLite persistence in `LoopRelay.Orchestration.Primitives`;
- real Codex one-shot, persistent read-only, warm, permission-scoped, decision, execution, and local transition postures;
- repository-owned `.agents` material, canonical `.LoopRelay/persistence/looprelay.sqlite3` state, local `.LoopRelay/evidence`, external Codex continuity evidence, and Git publication effects;
- production completion, archive, projection, permission, telemetry, and recovery composition.

Retained Roadmap and Plan services remain useful comparison and migration authorities, but direct execution of retired entry points never counts as public-path certification. Where a richer retained service and the active unified composition differ, the fixture platform must expose the difference and the product must either close it or declare the narrower public contract. The platform must never silently award confidence for inactive wiring.

## Roadmap invariants

These invariants shall never be violated by any milestone, milestone specification, implementation choice, exception, or later roadmap revision.

1. **Repository/scenario orthogonality.** Stable repository content and transient workflow, persistence, provider, Git, fault, configuration, and oracle conditions remain independently owned and identifiable.
2. **Independent certification authority.** Loop Relay does not schedule, observe, aggregate, or judge its own certification as the sole authority.
3. **Oracle independence.** Generated artifacts, model prose, checked boxes, transition success, and completion decisions require an independent deterministic backstop.
4. **Production-path validation.** Production confidence comes from the published unified CLI, active production composition, real authorities, and real Codex where applicable. Lower evidence tiers remain explicitly labeled.
5. **Deterministic lifecycle.** Case creation, composition, validation, reset, execution, interruption, recovery, replay, evidence capture, retention, and cleanup are predictable and case-scoped.
6. **Composability.** Reusable bases and compatible overlays are preferred to copied repositories and one-off scenario implementations.
7. **Minimal repository complexity.** Repository content contains only the smallest capability and independent acceptance signal needed to expose the production obligation.
8. **Fail-closed uncertainty.** Ambiguous authority, provider side effect, recovery evidence, schema/profile support, or oracle result cannot silently pass.
9. **Coverage monotonicity.** Covered obligations, evidence levels, denominators, and residual gaps remain visible; progress cannot be manufactured by deleting or weakening them.
10. **Confidence before expansion.** A later milestone may not assume a behavior, authority, seam, oracle, recovery path, or provider capability that an earlier dependency has not established. Unproven prerequisites block or explicitly narrow the later milestone.
11. **Recovery from the first live slice.** Live coverage cannot expand while restart, cancellation, uncertain side effects, and duplicate-work prevention remain outside the evaluated lifecycle.
12. **Containment and privacy.** No certification run may escape its disposable filesystem, process, Git, network, credential, or evidence-retention authorities.

When a proposed shortcut conflicts with an invariant, the invariant wins. The roadmap must be revised transparently if the invariant itself is no longer appropriate; a milestone specification may not waive it locally.

## Roadmap decisions

These decisions remove ambiguity that would otherwise infect every later milestone.

1. **Independent certification authority.** The fixture harness is a separate certification authority around Loop Relay. It owns case composition, execution scheduling, controlled faults, reset, independent observation, oracle evaluation, result aggregation, evidence retention, and coverage accounting. Loop Relay remains the system under test.
2. **EvalRoadmap remains distinct.** `EvalRoadmap` plans evaluation work; it is not assumed to execute its generated DAG. Harness execution and verdicts are independent from model-authored EvalRoadmap artifacts. Any future production eval executor must have an explicit handoff contract and is certified as another product capability.
3. **Three stable identities.** A Fixture Repository identifies the immutable tiny capability and acceptance signal. A Fixture Scenario identifies runtime conditions and overlays. A composed case identifies the exact repository/scenario materialization. Any behavior-bearing change invalidates the corresponding identity and prior evidence.
4. **Orthogonal scenario ownership.** Workflow/artifact, persistence, Git, interruption, provider, configuration/permission, and oracle overlays declare the authorities they alter, their prerequisites, compatibility, precedence, invalidation, and reset obligations. Incompatible or ambiguous compositions fail before Loop Relay runs.
5. **Production-derived coverage.** Coverage denominators come from active workflow definitions, products, gates, effects, persistence schema, prompt assets, execution postures, provider profiles, failure vocabularies, known-risk records, and supported platform/Git topologies. Fixture count and test count are not coverage measures.
6. **Independent oracle rule.** No model-authored artifact, transition success, checked checkbox, or Loop Relay certification decision is sufficient by itself. Every pass has a deterministic independent backstop based on repository truth, state, graph, structure, allowed effects, or protocol facts. A model-assisted semantic judge may add diagnostic signal but cannot be the sole pass/fail authority.
7. **Versioned evidence identity.** A result binds repository version, scenario and overlay versions, oracle and normalizer versions, active workflow/prompt identities and hashes, database schema, Loop Relay build, settings, platform, Git identity, Codex binary and app-server schema digest, model, effort, and evidence tier.
8. **Fail-closed provider policy.** An installed Codex profile without certified required capabilities is provider-incompatible, not a product pass or ordinary flake. Gated operations are not attempted. Resume, read, write, fork, context capacity, and reconciliation claims remain separate capabilities.
9. **Persistent cross-process truth.** Any fact required by a later transition after a process boundary must be recoverable from an authoritative durable source or the workflow must block with a precise recovery action. Process-local warm sessions and boundary writers cannot be treated as durable evidence.
10. **Observable chain boundaries.** Workflow exit, product transfer, downstream entry, and chain-run identity must survive process restart and be independently observable before full-chain certification begins.
11. **Explicit storage contract.** `storage init`, `import`, `export`, `sync`, and `verify` receive documented public semantics. Certification measures the active unified path against those semantics, including non-mutation, stale/conflict/corruption handling, and the deliberate scope of any narrower behavior.
12. **Isolated Git authority.** Local disposable parent and `.agents` remotes are the default publication topology. No fixture may write or push outside authorities created for that case. Ordinary-directory and nested-repository/submodule `.agents` shapes remain distinct scenarios.
13. **Containment precedes unrestricted work.** Live `danger-full-access` authoring and execution do not enter routine certification until workspace, process, network, credential, remote, and cleanup boundaries are independently proven.
14. **Singular, resumable closure.** Completion has one observable owner for policy decision, archive state, synthesis, roadmap-context update, canonical close, and continuity retirement. A failure at any step produces a discoverable resumable or explicitly fail-closed state. A second invocation after close performs no model work.
15. **Obligation-driven combinations.** Exhaustive combinations are reserved for security, authority-conflict, duplicate-side-effect, and closure invariants. Pairwise or representative combinations are used elsewhere only when the coverage ledger proves no authority interaction is hidden.
16. **Evidence tier honesty.** Component, replay, live-transition, and live-chain/recovery evidence are reported separately and never substituted for one another.

Numeric budgets, repetition counts, flake thresholds, retention windows, release-blocking evidence levels, and supported platform/profile lists are governance decisions established through measured evidence in the milestones below. Until set, uncertainty remains visible and cannot be normalized into a pass.

## Milestone dependency topology

The roadmap has one deliberate critical path. Each arrow means the successor may consume only obligations proven at the predecessor's exit; it may not reinterpret planned work as established confidence.

```mermaid
flowchart LR
    M1["M1 Trusted lifecycle"] --> M2["M2 Public CLI and authority"]
    M2 --> M3["M3 Live Codex and containment"]
    M3 --> M4["M4 Interruption and recovery"]
    M4 --> M5["M5 Plan certification"]
    M5 --> M6["M6 Execute continuity"]
    M6 --> M7["M7 Git and publication"]
    M7 --> M8["M8 Persistence lifecycle"]
    M8 --> M9["M9 TraditionalRoadmap"]
    M9 --> M10["M10 EvalRoadmap"]
    M10 --> M11["M11 Completion and closure"]
    M11 --> M12["M12 Failure and oracle closure"]
    M12 --> M13["M13 Traditional full chain"]
    M13 --> M14["M14 Eval full chain"]
    M14 --> M15["M15 Continuous certification"]
```

The linear ordering is intentional: it favors diagnosability and risk retirement over throughput. Work discovery may occur ahead of the critical path, but no milestone may claim completion, raise a confidence level, or serve as a release dependency before all predecessor exit criteria and architectural obligations are satisfied.

## Architectural seams under retirement

Seams are tracked roadmap objects, not incidental risks hidden inside milestones. “Retired” means the active public path has the named independent evidence; code presence or component tests alone do not change seam status.

| Architectural seam | Baseline status | Retirement milestone | Evidence required for retirement |
|---|---|---:|---|
| Production definitions are activated from `CanonicalWorkflowDefinitionSketches` | Open | 2 | Production-derived discovery, public-path selection, and automatic drift-to-uncovered behavior |
| Workflow-boundary evidence is process-local despite canonical chain storage | Open | 2 | Boundary and transfer evidence survives restart without duplicate advancement |
| Active storage behavior is narrower than retained Roadmap persistence services | Open | 2 and 8 | Explicit public semantics plus complete active-wiring, authority, and non-mutation evidence |
| Repository observer and commit services disagree on ownership of dirty-tree facts | Open | 2 and 7 | Public resolution and real Git-effect evidence name and verify the correct authority |
| Live provider approval and posture behavior is not certified | Open | 3 | Current-profile live protocol, authority, containment, and adversarial permission evidence |
| Warm planning continuity depends on process memory | Open | 5 | Restart after `WriteExecutablePlan` yields durable safe revision or a precise recoverable block |
| Warm execution/handoff continuity and successor facts depend on process memory | Open | 6 | Restart after `ExecuteImplementationSlice` preserves or safely exposes every `GenerateHandoff` dependency |
| Real `.agents` and parent publication topology is uncertified | Open | 7 | Isolated real Git graph, remote, failure, retry, and containment evidence |
| Completion composition does not prove all available archive/evidence components are active | Open | 8 and 11 | Active-wiring evidence and one singular resumable closure story |
| EvalRoadmap planning has no independent executable-evaluation handoff | Open | 10 | Model-authored planning and harness-owned execution/verdict authority are explicitly separated and traceable |
| Completion spans multiple non-atomic effects with archive/rerun/index/context hazards | Open | 11 | Independent truth, interruption recovery, singular ownership, and zero-work idempotent rerun |
| Recovery coverage is representative rather than systematic | Open | 12 | Every maintained boundary/failure obligation has required live, replay, fail-closed, or explicit incompatibility evidence |
| Public assembly lacks complete live-chain evidence | Open | 13 and 14 | Both declared chains pass published-CLI restart, authority, closure, and rerun certification |
| Certification is a campaign rather than a continuously governed system | Open | 15 | Tiering, budgets, drift invalidation, platform/profile governance, retention, and release gates operate continuously |

A seam stays open if its retirement evidence expires, is invalidated by production drift, or is downgraded. The confidence ledger must then return its dependent obligations to uncovered or to the lower surviving evidence level.

## Non-negotiable milestone gates

Every milestone must satisfy all of these gates in addition to its own exit criteria.

- **Production path:** claims about Loop Relay behavior originate at the published unified CLI or are explicitly labeled as lower-tier evidence.
- **Repository/scenario independence:** no milestone creates repository copies merely to encode transient state, provider behavior, Git topology, or interruption timing.
- **Oracle independence:** the system under test is never its own sole evaluator.
- **Deterministic lifecycle:** create, compose, validate, reset, execute, interrupt, resume, replay, inspect, retain, and clean up are predictable and case-scoped.
- **Containment:** no undeclared filesystem write, process, credential, network effect, or Git push escapes disposable case authorities.
- **Fail closed:** ambiguous state, provider side effect, authority conflict, unsupported schema/profile, or untrusted recovery evidence cannot pass.
- **Coverage monotonicity:** existing obligations and evidence remain visible; a milestone cannot lower an evidence level, delete a denominator, or convert uncovered work into an exclusion without review.
- **Confidence-ledger continuity:** each milestone's opening ledger equals its predecessor's closing ledger plus reviewed production drift, and the milestone cannot close until its ledger delta and residual uncovered set are published.
- **Diagnostic sufficiency:** every pass, fail, block, wait, cancel, stall, ambiguous result, provider incompatibility, fixture drift, and oracle drift has enough retained evidence to explain its classification.
- **Cost proportionality:** full-chain model turns are reserved for chain questions; branch, failure, parser, and state questions use the cheapest evidence tier that can prove the obligation.
- **Privacy:** retained evidence contains no credentials, hidden reasoning, unsafe rollout content, or uncontrolled external paths.

## Confidence and coverage ledger

The coverage ledger is the roadmap's primary progress artifact. Milestones are valuable only insofar as they retire architectural obligations or raise their evidence strength. The ledger reports an uncovered set and an achieved evidence level for each dimension. An aggregate percentage may summarize but may not hide a zero in recovery, provider capability, authority interaction, isolation, or closure.

| Evidence level | Meaning | Permitted claim |
|---|---|---|
| 0 — Uncovered | No executable evidence | No confidence claim |
| 1 — Deterministic component | Contract, parser, store, or local transition evidence | The isolated deterministic behavior works |
| 2 — Replay | Scrubbed provider/output/state evidence without the current provider | Parsing, normalization, re-observation, or recovery-source behavior works for the recording |
| 3 — Live transition | Production transition, real provider where applicable, composed case, and independent oracles | The current production transition is reachable and behaves correctly for the case |
| 4 — Live chain/recovery certification | Published CLI, current provider, real authorities, restart/recovery, and rerun | The production capability is certifiable end to end |

Coverage dimensions and denominators are:

- every active production workflow transition and route-specific alternative;
- every declared chain, selection mode, boundary, bounded stop, and terminal route;
- every relevant transition, transport, session, effect, and post-persistence recovery boundary;
- every SQLite/filesystem domain and supported authority/operation shape;
- every classified process, prompt, product, permission, Git, storage, provider, evaluation, certification, and archive failure;
- every provider operation, parameter, result, capability gate, and reconciliation claim used by production;
- every behavior-bearing artifact, state domain, side effect, and terminal claim with a suitable oracle;
- every interaction among filesystem, SQLite, Git, provider, in-memory state, and configuration;
- every one-shot, persistent, warm, scoped, decision, read-only, unrestricted, and local posture;
- every product, lifecycle, storage representation, effect category, and ordering contract;
- every supported platform and Git topology that can change behavior.

The coverage ledger records the exact evidence satisfying each obligation. A success path does not cover its failure or recovery path; touching an artifact does not cover its oracle; replay does not cover a live provider; and duplicated cases add no credit unless they add a new obligation, evidence level, authority interaction, platform, or meaningful semantic variation.

Every milestone opens and closes with a confidence-ledger checkpoint. The update contains:

- the opening obligation set and evidence level by architectural dimension;
- new obligations discovered from production definitions, schemas, prompts, provider profiles, issue records, or milestone learning;
- obligations newly satisfied and the exact evidence that satisfies them;
- obligations whose evidence level increased, without erasing the lower-tier provenance;
- obligations invalidated, expired, downgraded, excluded, or still uncovered, with owner and reason;
- seams and risks retired, reopened, or still depended upon;
- the residual uncovered set that constrains the next milestone.

Ledger evolution is expressed as obligation-set movement, not decorative percentages or test counts. A milestone may increase the denominator and still be successful when it discovers real production surface; hiding that discovery to preserve an apparent completion rate is invalid. The successor's opening ledger must equal the predecessor's closing ledger plus explicitly reviewed production drift.

## Milestone sequence

### Milestone 1 — Trusted case lifecycle and baseline canary

#### Objective

Establish the smallest independently trustworthy certification loop: compose one immutable tiny repository with one deterministic scenario, run a non-mutating public CLI behavior, independently observe every affected authority, issue a versioned result, reset, and reproduce the same normalized outcome.

#### Architectural rationale

No later live result is meaningful until case identity, isolation, reset, normalization, evidence, and coverage attribution are trustworthy. This milestone retires base-state leakage and oracle circularity before model cost and production mutations are introduced. It is a vertical slice because it certifies a real public `status`/verification behavior rather than delivering only internal harness infrastructure.

#### Capabilities introduced

- Separate identities and ownership for Fixture Repositories, Fixture Scenarios, composed cases, runs, and retained evidence.
- Immutable null, text-only, tiny executable/library/CLI, intentionally failing, malformed, and already-satisfied repository classes, introduced only as needed rather than multiplied eagerly.
- Composable overlay contracts for workflow/artifact, persistence, Git, interruption, provider, configuration/permission, and oracle state, including compatibility and precedence validation.
- Deterministic materialization and cleanup of repository files, SQLite and sidecars, `.LoopRelay` evidence/telemetry, Git refs/remotes, provider home/session state, environment, and processes.
- A versioned normalization contract for paths, separators, case, line endings, timestamps, generated IDs, Git identities, SQLite ordering, archive indices, provider identities, token/quota/timing values, and allowed prose variability.
- A versioned run-result and evidence inventory that distinguishes product regression, provider regression, environment failure, fixture drift, oracle drift, blocked state, and unsupported capability.
- Independent exact, structural, invariant, state, and graph oracle entry points; semantic judgment remains diagnostic until deterministic backstops exist.
- Privacy scanning, scrubbing, retention classification, and concise normalized diffs.
- A production-derived coverage ledger seeded from current workflows, prompts, products, gates, effects, schema domains, postures, provider capabilities, issue records, and known risks.
- A deterministic public-CLI canary proving repository selection, status/verification observation, evidence capture, cleanup, and repeated reset without Codex.

#### Architectural obligations satisfied

- Certification evidence has stable identity, independent ownership, deterministic reset, and complete containment before any live provider claim is accepted.
- Repository complexity is separated from runtime scenario complexity, so later coverage can expand without repository proliferation or base-state leakage.
- Every future confidence claim has a production-derived ledger obligation and at least one declared independent oracle path.

#### Dependencies

- The audited repository and current component-test baseline.
- Stable access to the published unified CLI and active production definitions.
- A declared workspace root within which all case state is disposable.

#### Architectural risks retired

- Repository/scenario conflation, duplicated fixtures, ambiguous overlay ownership, base-state leakage, silent fixture drift, unversioned normalization, semantic oracle circularity, privacy leakage, coverage-by-count, and aggregate-coverage masking.

#### Production confidence gained

The platform can prove that it knows exactly what it ran, what changed, what did not change, what evidence supports the verdict, and that a second materialization begins from the same state. This is the trust root for every later live claim.

#### Architectural coverage expanded

- Fixture lifecycle and reset.
- Public CLI discovery and non-mutating observation.
- Exact/structural/state/invariant oracle foundation.
- Result identity, privacy, and coverage-denominator discovery.
- Null/minimal repository and missing-state outcomes.

#### Exit criteria

- Repeated materialize-run-reset cycles yield the same normalized canary result and base hash.
- Overlay conflicts and undeclared authority mutations fail before Loop Relay execution.
- Cleanup proves no remaining child process, provider state, database sidecar, Git ref, remote mutation, or file outside the case boundary.
- The result identity binds every behavior-bearing version listed in the roadmap decisions.
- Privacy checks reject seeded secret, environment-dump, base64, hidden-reasoning, and external-path leakage cases.
- The coverage ledger enumerates current denominators and reports uncovered obligations without manual omission.
- The existing component suite remains green and is labeled evidence level 1 rather than promoted to live certification.

### Milestone 2 — Public CLI, resolution, chain-boundary, and storage contract certification

#### Objective

Certify the deterministic production control surface before real model output is allowed to complicate diagnosis.

#### Architectural rationale

Default/forced/bounded selection, status, unblock, storage behavior, re-observation, exit codes, and workflow boundary evidence are the public frame around every live transition. Current active storage behavior is narrower than retained Roadmap services, Git dirty-state observation is shallow, and chain-boundary evidence is process-local. Those ambiguities must be visible and resolved before full workflows are trusted.

#### Capabilities introduced

- Published-executable coverage for default, forced Eval, forced Traditional, bounded Eval, Traditional, Plan, and Execute invocations.
- Status coverage for fresh, active, resumable, blocked, waiting, failed, cancelled, stalled, completed, ambiguous, corrupt, unsupported, and no-eligible-transition states.
- Exact exit-code coverage for `0`, `1`, `2`, `3`, `4`, and `130` where contractually applicable.
- Gate and resolution observation for entry, input, output, exit, product transfer, downstream entry, successor eligibility, automatic re-observation, bounded stopping, and the 32-transition guard.
- Durable workflow-boundary and chain-run evidence that remains observable across restart.
- Public `unblock` behavior for recoverable, non-recoverable, and stage-less blockers, including continuity ancestry and operator evidence.
- An explicit public contract for storage init/import/export/sync/verify and production-path evidence of actual behavior.
- Non-mutating verification and precise reporting for missing/empty storage, authority shape, stale export, conflict, corruption, unsupported schema, unresolved reference, and partial transaction.
- Public observation of clean, dirty, detached, non-Git, ordinary `.agents`, and nested `.agents` facts, while distinguishing resolver facts from later commit/change evidence.
- Drift detection when active workflow definitions, products, gates, effects, prompts, commands, schema domains, or exit vocabulary change without corresponding coverage obligations.

#### Architectural obligations satisfied

- Public command, workflow-selection, gate, exit, and storage semantics are observable before model behavior is introduced.
- Workflow-boundary confidence no longer depends on process-local evidence, and bounded work cannot silently cross an uncertified boundary.
- Active unified behavior is distinguished from richer retained services, preventing inactive architecture from contributing production confidence.

#### Dependencies

- Milestone 1 case lifecycle, normalization, independent state observation, and coverage ledger.
- A product decision for each public storage command and chain-boundary authority.

#### Architectural risks retired

- Implementation coupling, active/retained authority confusion, storage split-brain hidden by one-sided observation, process-local boundary evidence, dirty-tree false assumptions, verification mutation, legacy artifacts falsely advancing canonical workflows, and bounded commands accidentally starting downstream work.

#### Production confidence gained

Operators can trust the published CLI's deterministic selection and safety behavior, including what it deliberately does not do. Later model failures can be separated from command routing, authority, or chain-boundary failures.

#### Architectural coverage expanded

- Cross-workflow and CLI coverage.
- Storage authority and synchronization entry points.
- Workflow chain, gate, transition-selection, and terminal-state coverage.
- Filesystem/SQLite/Git/configuration authority interactions that do not require a live provider.

#### Exit criteria

- Every public command and invocation mode has a composed public-executable case and expected exit/status contract.
- A restart immediately before and after a workflow boundary preserves one authoritative boundary story and never duplicates product transfer.
- Storage commands either meet the declared domain semantics or fail closed with the mismatch recorded; richer inactive services do not earn public-path credit.
- Verification is byte-for-byte non-mutating for filesystem and database authorities.
- Legacy `execution-prompt.md` and compatibility artifacts cannot satisfy canonical Plan/Execute entry on their own.
- New or changed production definitions and schema domains automatically appear as uncovered coverage obligations.

### Milestone 3 — Live Codex compatibility, posture, and containment certification

#### Objective

Establish real Codex as a safe, versioned certification target and close the five skipped live approval/protocol gaps before broad semantic workflow execution.

#### Architectural rationale

A semantically valid artifact created through the wrong sandbox, approval, network, model, effort, working directory, or session lifetime is not a successful production result. The current compatibility fixture certifies a narrow 0.142.5 resume/read identity, not workflow semantics or live approvals. Provider and containment truth must be independently certified first.

#### Capabilities introduced

- Disposable authenticated `CODEX_HOME` handling with explicit analytics, secrets, and retention boundaries.
- Exact Codex binary/version and app-server schema identity, model/effort/settings identity, and fail-closed unsupported-profile classification.
- Live certification of process launch, JSON-RPC initialize/start/resume/read gates, streaming, tool events, approval requests, cancellation, teardown, and provider thread/turn correlation.
- Live observed assertions for planning authoring, planning review, operational one-shot, scoped artifact operation, decision, execution, and deterministic local postures: model, effort, sandbox, network, approval policy, working directory, allowed effects, and lifetime.
- Approval protocol cases for precise allowed targets, disallowed targets, wrapper commands, redirection, mutating safe commands, force-push configuration, indirect-shell policy, and path-like content/patch text that is not a path request.
- One low-cost live one-shot transition canary with prompt/source identity, structural product validation, raw evidence, telemetry, independent acceptance, and idempotent cleanup.
- Telemetry-on/off coverage for canonical SQLite events and JSONL compatibility output, including best-effort telemetry failure and caller cancellation.
- Process and transport adversaries: missing/wrong executable, nonzero exit, stderr/long-output floods, malformed/noisy/duplicate/truncated frames, missing IDs, absent token usage, estimator fallback, and process-tree teardown.
- Privacy-safe scrubbed protocol/replay evidence that never claims current live-provider certification.

#### Architectural obligations satisfied

- Every production provider and posture claim is backed by current live protocol and side-effect evidence or an explicit incompatibility.
- Unrestricted and permission-scoped sessions are contained before they may serve later workflow certification.
- Replay, constructed session specifications, and historical compatibility records cannot substitute for current live-provider authority.

#### Dependencies

- Milestones 1 and 2.
- Authenticated provider access, explicit live-test policy, and isolated credential handling.
- A certified profile or a deliberate provider-incompatible result.

#### Architectural risks retired

- Replay substitution, provider/profile drift ambiguity, permission-protocol mismatch, unsafe unrestricted sessions, network/credential leakage, false path declines, hard-deny bypass false confidence, hung approvals, and incomplete child-process cleanup.

#### Production confidence gained

The current Codex binary can be proven compatible—or safely rejected—before Loop Relay attempts gated operations. Each production posture is observable as an actual provider/process behavior, not merely a constructed session specification.

#### Architectural coverage expanded

- Provider capabilities and profile gates.
- All execution posture categories.
- Process/transport/session registry and telemetry.
- Permission/configuration/security issue scenarios `001`, `002`, `003`, `007`, `008`, `009`, `010`, and the deferred unrestricted execution trust boundary.

#### Exit criteria

- The five previously skipped live checks have an explicit pass, product defect, provider incompatibility, or environment classification; none remain silently skipped in certification reporting.
- Unsupported version/schema cases emit no gated protocol calls.
- Actual live frames and side effects prove every posture's declared authority.
- Adversarial approval cases produce exact expected decisions without writes outside declared targets.
- Cancellation and abnormal transport termination leave no live process or registry ownership leak.
- Retained evidence passes privacy scanning and contains no credentials, private session content, or hidden reasoning.

### Milestone 4 — Controlled interruption and first live recovery slice

#### Objective

Make interruption, restart, replay, and duplicate-side-effect prevention part of the platform before expanding workflow coverage.

#### Architectural rationale

Repository snapshots cannot reproduce timing-dependent death, response loss, cancellation, or partial effects. The canonical runtime has rich durable states, but supporting evidence writes can diverge and the correct authority varies by boundary. Recovery must be demonstrated with a controlled live slice now, not appended after happy paths proliferate.

#### Capabilities introduced

- A scenario-owned fault boundary capable of proving that interruption occurred at a named transition, transport, persistence, effect, Git, or archive boundary without embedding timing logic in fixture repositories.
- The full conceptual boundary vocabulary from pre-resolution through post-boundary transfer, including request write-started, submitted, accepted, provider turn identified, partial output, terminal result, raw-output persistence, interpretation, validation, ordered effects, completion persistence, re-observation, and chain transfer.
- Recovery classifications for safe retry, exact resume, committed-output materialization, effect verification/application, operator unblock, cancellation, fail-closed unknown side effect, and non-recoverable corruption.
- A first low-cost live prompt transition exercised before submission, after acceptance, after provider completion, during an effect, and after completion persistence.
- Duplicate provider-turn and duplicate-effect detection bound to transition/input identity.
- Normalized recovery plan, attempt, source, marker, lineage, correlation, durable-state, blocker, and status evidence.
- Replay tiers for parser, rollout projection, normalization, recovery-source selection, and re-observation, explicitly separated from live evidence.
- Recovery-source privacy, ordering, budget, digest, omission, truncation, and untrusted-marker checks.

#### Architectural obligations satisfied

- Recovery is part of the certified lifecycle before live workflow coverage expands.
- The system can distinguish safe retry, exact resume, post-validation effect recovery, and uncertain-side-effect refusal without duplicating work.
- Timing-dependent failures have an authority independent from fixture repository contents and are proven at the intended boundary.

#### Dependencies

- Live provider and containment certification from Milestone 3.
- Durable transition and chain observation from Milestone 2.

#### Architectural risks retired

- Recovery as a late enhancement, timing encoded in repositories, duplicate submissions after unknown outcomes, final-files-only false confidence, partial-effect masking, supporting-evidence divergence, cancellation evidence loss, and replay mislabeled as live proof.

#### Production confidence gained

At least one real production transition can be interrupted and safely classified across the critical retry/resume/reconcile/effect boundary classes. The platform can prove both recovery and deliberate refusal to recover when side effects are ambiguous.

#### Architectural coverage expanded

- Recovery boundary and durable-state coverage.
- Provider-turn correlation and unknown-outcome coverage.
- Recovery plan/source/lineage oracles.
- Cancellation, cleanup, and public status/unblock after a partial live run.

#### Exit criteria

- Each first-slice interruption is evidenced at the intended boundary rather than inferred from final state.
- Pre-submission retry does not reuse uncertain output; post-acceptance ambiguity never resubmits without reconciliation; post-validation recovery never reruns the prompt unnecessarily.
- Exactly one terminal classification exists for each transition run, and ordered effects cannot be silently duplicated.
- Status and unblock expose the durable recovery action and continuity ancestry after restart.
- Replay reproduces low-cost parsing/state behavior but is reported below live evidence.

### Milestone 5 — Plan authoring and scoped-operation certification

#### Objective

Certify a complete stage-targeted Plan capability with real Codex: producer-agnostic entry, same-thread authoring/revision, independent read-only review, deterministic operational context, permission-scoped details/milestones, rollback, restart, and Execute-entry products.

#### Architectural rationale

Plan combines materially different postures and currently depends on an in-memory warm session between `WriteExecutablePlan` and `RevisePlan`. It is the smallest production workflow that exposes warm continuity, read-only review, scoped approvals, projection freshness, rollback, and a rich downstream product contract. Seeding validated upstream products avoids spending roadmap turns while preserving the public Plan entry gate.

#### Capabilities introduced

- Plan entry from both TraditionalRoadmap and EvalRoadmap products without branching on producer identity.
- Real plan creation, structural/semantic capability coverage, allowed-write enforcement, prompt/source identity, and invalid/missing/empty/unrelated-output classification.
- Adversarial projection generation/reuse, Project Context nine-file contract, prompt-hash and causal freshness, invalid projection, and manifest migration coverage.
- Real persistent read-only review with attempted-mutation detection and teardown.
- Same-thread warm revision and a defined safe restart result between write and revision. Any required continuity fact is durable or the public CLI provides a precise recoverable block; silent process-local dependency is not accepted.
- Deterministic initial operational-context seed and invalidation/regeneration after plan change.
- Details collection/refinement and milestone extraction under precise approval scoping, declared inputs/outputs, path-like-content negatives, mutation transaction, rollback, reduced-count supersession, strict checkbox, zero-file, and zero-checkbox outcomes.
- Execute-entry verification for the exact five-product contract, causal identities, freshness, bounded stop, and partial-stage resume.
- Repetition evidence and acceptable semantic route sets for model-authored Plan prose.

#### Architectural obligations satisfied

- Plan revision continuity no longer silently depends on process memory across a public restart boundary.
- Plan products are independently valid, fresh, causally bound, and producer-neutral before Execute may consume them.
- Scoped mutations prove exact authority and rollback, so unrestricted prompt success cannot masquerade as valid Plan progress.

#### Dependencies

- Milestones 1–4.
- Valid minimal PreparedEpic and MilestoneSpecificationSet seeds from both producers.
- Live scoped-approval certification and a product decision for cross-process warm Plan continuity.

#### Architectural risks retired

- Warm Plan restart loss, prompt success mistaken for product success, stale projection reuse, unrestricted writes replacing scoped operations, path-content false declines, rollback gaps, producer coupling at Plan entry, and brittle prose goldens.

#### Production confidence gained

A real Codex Plan run can transform validated roadmap products into validated Execute-entry products while preserving authority, posture, continuity, freshness, rollback, and restart semantics.

#### Architectural coverage expanded

- All Plan stages except final Git publication, which is certified in Milestone 7.
- Warm, persistent read-only, scoped, one-shot/local, and cross-process session interactions.
- Project Context, projection, plan, review, operational context, details, milestones, and readiness artifacts.

#### Exit criteria

- Both roadmap producer types reach the same Plan entry and output product contracts.
- A real write-review-revise run proves expected thread reuse and teardown.
- Restart after plan write produces a safe, durable, deterministic next action and never loses or silently fabricates review context.
- Scoped operations mutate only declared files, roll back invalid output, and retain exact approval evidence.
- A plan change invalidates dependent projection/context products as declared.
- The bounded Plan run stops before Execute and yields independently validated Execute-entry products.

### Milestone 6 — Execute decision, implementation, handoff, and continuity certification

#### Objective

Certify the core Execute loop with real Codex and real restarts, from readiness through decision continuity, implementation slice, handoff, operational continuity, progress/stall accounting, and pre-completion state.

#### Architectural rationale

Execute contains the highest concentration of cross-process and provider risk. Decision sessions have deep SQLite-backed recovery, while the implementation/handoff pair and several repository facts are process-local. This milestone proves one bounded implementation capability before publication and completion amplify side effects.

#### Capabilities introduced

- Readiness validation for missing, malformed, stale, partial, blocked, and valid Plan products.
- Fresh decision scope, exact-ID resume, continuation, planned transfer, recommendation validation/binding, projection injection once, persisted accounting restoration, scope causal identity, stale-scope rejection, and retirement preparation.
- Provider-backed failed-resume classification, resume-disabled behavior, bounded retry, unavailable/corrupt session handling, and no silent replacement.
- Committed decision output rehydration without resubmission and fail-closed unresolved submitted/accepted/unknown turns.
- Context restoration for decision projection, operational context, handoff, repository sources, budgets, markers, digests, omissions, truncation, sanitization, and capacity failure.
- One real implementation slice with exact model/effort, bounded allowed diff, deterministic repository acceptance signal, milestone-only progress, no-change behavior, unrelated-write detection, command failure, and cancellation.
- Same-thread `ExecuteImplementationSlice`/`GenerateHandoff` behavior and a defined safe restart result after work but before handoff. Changed paths, milestone counts, repository-slice baseline, completion/recovery evidence, and every other successor dependency are durable or produce a precise recoverable block.
- Handoff rotation, decision retirement, operational delta creation/archive, context evolution, and history sequencing.
- Durable progress/no-progress accounting, exact stall threshold behavior across invocations, and operator recovery after correction.
- Non-implementation candidate discovery, cached disposition, semantic confirmation, explicit HITL ownership, allowed auxiliary file, forbidden file, and review-failure classification.

#### Architectural obligations satisfied

- Execution continuity, handoff generation, and successor-required facts have durable restart semantics rather than hidden process-memory prerequisites.
- Repository truth and the independent acceptance signal, not generated decisions or handoff prose, determine implementation progress.
- Decision turns, scopes, lineage, recovery, stall, and HITL behavior are correlation-safe and cannot duplicate semantic work after restart.

#### Dependencies

- Milestones 1–5.
- Execute-ready tiny repository with one independent acceptance signal and one strict milestone.
- Product decisions for work/handoff restart and all process-local Execute facts.

#### Architectural risks retired

- Duplicate decision turns, stale or mismatched scope, committed-but-unmaterialized output loss, unknown provider side-effect replay, implementation/handoff warm-session loss, false progress from artifacts, lost baseline/count facts, context privacy leakage, and non-durable stall accounting.

#### Production confidence gained

The central production loop can make a decision, change a tiny real repository, describe the observed work, preserve or safely block across restart, and distinguish substantive, milestone-only, no-op, unrelated, and non-implementation outcomes.

#### Architectural coverage expanded

- Execute readiness, decision planning, implementation, handoff, continuity artifacts, non-implementation review, and stall stages.
- Decision continuity profiles, scopes, lineage, active pointers, attempts, sources, turns, correlations, and accounting.
- Real repository diff and acceptance-signal oracles.

#### Exit criteria

- Fresh, warm, resumed, transferred, failed-resume, committed-output, and unresolved-turn cases have distinct durable outcomes and no duplicate semantic progress.
- Work and handoff share the intended session in-process; restart at their boundary has a safe public-path result.
- All successor-required facts survive restart or yield an actionable fail-closed state.
- Exact allowed files and semantic required change agree with the independent acceptance signal.
- Repeated no-op slices persist and reach the documented stall outcome; real or milestone-only progress resets the count.
- Scope invalidation follows epic/plan identity changes and no private recovery content enters retained evidence.

### Milestone 7 — Git publication, permission, and remote-side-effect certification

#### Objective

Certify Plan and Execute publication against real isolated Git repositories and remotes, including permission adversaries, failure salvage, dirty-state ownership, and parent/`.agents` topology.

#### Architectural rationale

Scripted Git runners cannot prove object graphs, upstream behavior, gitlinks, response-loss recovery, or confinement. Production publication assumes a nested `.agents` repository, while real repositories may track it as ordinary files. Networked unrestricted sessions make remote isolation a primary safety boundary.

#### Capabilities introduced

- Real local Git scenarios for no repository, clean/dirty/detached parent, `.agents` ordinary directory, nested repository/submodule, isolated upstreams, and divergent/unavailable remotes.
- Independent Git graph oracles for branches, upstreams, commit parents, trees, gitlinks/submodule pointers, reachability, and no-change publication.
- Plan `.agents` publication and parent gitlink recording through the public path.
- Execute `.agents` publication, source commit/push, bookkeeping exclusion, milestone-only progress, and abnormal-exit salvage.
- Controlled stranded commit, non-fast-forward, unavailable upstream, push response loss, `.agents` published but parent pointer missing, and retry/idempotency scenarios.
- Cross-checks between shallow repository observation and process-driven change/commit evidence so each Git fact has a named authority.
- End-to-end regression cases for every checked-in permission/publication issue and known-risk record.
- Proof that no branch, remote, credential, or file outside disposable case authorities changes.

#### Architectural obligations satisfied

- Publication confidence is based on real Git objects, refs, upstreams, gitlinks, and remote effects rather than scripted process output.
- Ordinary and nested `.agents` authority shapes have explicit, non-interchangeable production behavior.
- Publication retries and failures preserve a single explainable graph without external side effects or duplicated semantic commits.

#### Dependencies

- Milestones 1–6.
- Local disposable remote authority and proven containment.
- Declared canonical production expectations for ordinary `.agents` versus nested/submodule topologies.

#### Architectural risks retired

- Scripted-Git false confidence, external push risk, `.agents` topology inference, parent gitlink omission, bookkeeping misclassified as progress, dirty-tree ownership confusion, stranded publication, response-loss duplicate push, and known permission bypass regressions.

#### Production confidence gained

Loop Relay can publish real planning and implementation state into a verified Git graph, surface recoverable failures, and remain confined to disposable authorities.

#### Architectural coverage expanded

- Repository/Git authority interactions and effect ordering.
- Plan publication and Execute publication/commit stages.
- Permission, configuration, network, remote, branch, and topology failure modes.

#### Exit criteria

- Both supported `.agents` shapes have explicit public outcomes; unsupported shapes fail before unsafe mutation.
- Successful publication produces the expected object/upstream/gitlink graph, not merely a clean working tree.
- Lost-response and non-fast-forward cases preserve one intelligible local/remote story and do not duplicate semantic commits.
- Every high-severity permission/publication issue has active-public-path evidence and remains in the denominator until that evidence passes.
- Containment proves zero external writes and pushes.

### Milestone 8 — Comprehensive persistence and authority lifecycle certification

#### Objective

Expand the foundational storage checks into production-path certification of every persistence domain, authority shape, logical artifact, migration, synchronization, corruption, concurrency, and reset obligation.

#### Architectural rationale

Loop Relay distributes authority across SQLite, `.agents`, `.LoopRelay/evidence`, provider state, and Git. Direct store tests are strong but cannot prove active composition, archive wiring, or public command semantics. Comprehensive state oracles are required before completion and full-chain certification.

#### Capabilities introduced

- Normalized logical snapshots for schema/workspace metadata, sync markers, roadmap state and ledgers, lifecycle/split/projection/preparation manifests, transition journal, loop history, execution evidence, archives, workflow transactions, canonical workflow/stage/transition/product/gate/effect/blocker/recovery/chain rows, telemetry, legacy resume, and decision continuity domains.
- Canonical-row, missing-reference, corrupt-row, unsupported-version, export/import, migration, and schema-evolution coverage for every applicable domain.
- Filesystem-only legacy, canonical SQLite-only, matching mixed, stale export, conflicting dual change, corrupt database, partial transaction, and legacy-resume-conflict cases.
- Public import/export/sync round-trip behavior, scoped dependencies, export regeneration/deletion, optional versus required manifests, optimistic conflicts, concurrent writers, and byte-for-byte non-mutating verification.
- Logical resolution when exports are absent and explicit retained-filesystem versus migrated-domain authority.
- Path containment/traversal, case variation, newline/encoding, numbered-history gaps, live/rotated handoff/decision/recommendation/delta, `.LoopRelay/.gitignore`, telemetry rotation, and cleanup outside-root protection.
- Active-wiring evidence for chain-run rows, loop/execution evidence used by completion, and any archive SQLite materialization promised by the public contract.
- Faults in primary versus supporting evidence persistence with a declared authoritative terminal story.

#### Architectural obligations satisfied

- Every durable production claim can be reconstructed from a normalized, independently checked logical snapshot across all active authorities.
- Authority precedence, synchronization, migration, conflict, corruption, concurrency, and non-mutation semantics are explicit rather than inferred.
- New persistence or artifact surface automatically becomes uncovered instead of disappearing from snapshots, reset, archive, or privacy review.

#### Dependencies

- Milestones 1–7.
- Public storage semantics fixed in Milestone 2.
- Workflow, decision, publication, and telemetry artifacts produced by earlier live slices.

#### Architectural risks retired

- Storage split-brain, row/effect/journal inconsistency, orphaned references, silent verification repair, migration mutation, logical artifact loss, archive component assumed active when unwired, concurrency masking, and reset omissions.

#### Production confidence gained

Every durable claim made by the public CLI can be independently reconstructed across all authoritative domains, including conflict and failure states. A correct-looking filesystem can no longer hide incomplete canonical state.

#### Architectural coverage expanded

- Every schema v3 domain and supported legacy boundary.
- Storage commands and authority synchronization.
- Filesystem/logical artifact behavior and persistence failure modes.

#### Exit criteria

- The snapshot inventory fails when a new table, column, product representation, artifact path, enum, or lifecycle state lacks handling.
- Every domain has applicable healthy, corrupt/missing-reference, and evolution evidence.
- Public storage behavior matches its declared semantics, including stable round trip where promised and explicit non-operation where deliberately narrow.
- Conflicting authorities and incomplete transactions block mutation without changing either source.
- Reset removes database sidecars, telemetry, provider state, Git refs, and runtime evidence while preserving the immutable base.

### Milestone 9 — TraditionalRoadmap live authoring certification

#### Objective

Certify real Codex TraditionalRoadmap behavior from minimal context through prepared epic and milestone specifications, including alternative routes, longitudinal evolution, failure, and restart.

#### Architectural rationale

TraditionalRoadmap has the broadest discretionary authoring route surface. It should be expanded only after prompt/product oracles, recovery, persistence, and provider posture are trustworthy. Stage-targeted seeds keep cost low while testing production resolution and real prompts.

#### Capabilities introduced

- Completion-context bootstrap/update for absent, valid, stale, malformed, archived-evidence, prompt-failure, and missing-product outcomes.
- Selection with valid, malformed, stale, superseded, roadmap/context drift, retired-initiative exclusion, and explicit HITL evidence.
- Existing-epic audit for ready, insufficient, blocked, prompt-failure, and audit-evidence-without-false-blocker outcomes.
- Epic creation for valid promotion, blocked, ambiguous, structurally invalid, prompt-without-artifact, and lifecycle persistence outcomes.
- Realign, reimagine, retire, and split routes with stable identity, preserved prior state, fallback, deduplication, path containment, child ordering, family persistence, selection supersession, and promotion validation.
- Milestone deep dives for valid, zero, mismatched ownership, partial bundle, malformed checklist, and prompt/context failure.
- Exact convergence on `PreparedEpic` and `MilestoneSpecificationSet`, stale-provenance rejection, legacy-artifact exclusion, and bounded Plan entry.
- Longitudinal roadmap change, completion-context update, revised selection, next epic, and stale causal invalidation.
- Allowed route-set semantics where model discretion is legitimate, with route-specific structural/state oracles instead of prompt overfitting.

#### Architectural obligations satisfied

- TraditionalRoadmap model discretion is bounded by route-specific lifecycle, provenance, product, graph, and causal invariants.
- All accepted Traditional outputs converge on independently valid universal Plan-entry products without legacy false advancement.
- Longitudinal roadmap evolution preserves history and invalidates stale decisions rather than treating artifact presence as completion.

#### Dependencies

- Milestones 1–8.
- Minimal exact nine-file Project Context and stage-targeted Traditional seeds.
- Repetition and semantic-variation policy informed by prior live runs.

#### Architectural risks retired

- Prompt-contract-only confidence, discretionary-route brittleness, stale selection/projection reuse, split lineage corruption, false promotion, legacy false advancement, fixture overfitting, and longitudinal causal loss.

#### Production confidence gained

Real Codex can produce and evolve Traditional roadmap products through the active public composition, and every accepted route preserves lifecycle, provenance, graph, and Plan-entry invariants.

#### Architectural coverage expanded

- Every TraditionalRoadmap coverage area and route family.
- Roadmap artifacts, selection provenance, decision ledger, split graph, lifecycle, deep dives, and Plan transfer.

#### Exit criteria

- Every route-specific alternative is live-covered where deterministically inducible or remains explicitly uncovered with a justified route-set strategy.
- Prompt success never advances without a valid, causally bound product.
- Split/path adversaries never escape the fixture root or partially promote invalid families.
- A longitudinal case proves stale selection/projection invalidation after repository/context evolution.
- The bounded workflow produces producer-valid Plan entry and does not start Plan.

### Milestone 10 — EvalRoadmap planning and independent evaluation-graph certification

#### Objective

Certify EvalRoadmap’s real planning outputs and traceability graph while establishing an explicit independent handoff between model-authored evaluation plans and harness-owned executable evaluation results.

#### Architectural rationale

EvalRoadmap is serial planning today, not an executable evaluator. Treating its DAG as executed truth would create circular certification. Graph and semantic oracles must validate the planning chain, while the harness owns scheduling, retries, hypothesis verdicts, result aggregation, and certification-readiness evidence for fixture cases.

#### Capabilities introduced

- Intent discovery and selection for none, one, multiple, empty, malformed, and forced-without-usable inputs.
- Dependency inventory traceability, safe-failure dependencies, forbidden non-implementation dependencies, missing/invalid output, and refresh after repository change.
- Hypothesis one-to-many mapping, confirmation/falsification/inconclusive definitions, missing dependency coverage, and refresh state.
- Architectural catalog uniqueness, same-plane grouping, dependency/falsifiability separation, and unresolved ambiguity.
- Eval DAG node/edge traceability, acyclicity, topological layers, cycles, conflicts, missing edges, negative controls, and machine-gate obligations.
- Next-roadmap earliest unresolved frontier, boundedness, dependency order, hypothesis acceptance, and prohibition on invented implementation detail.
- Canonical active epic at `.agents/epic.md`—including the output of the current next-epic specification transition—and deep-dive products with evaluation traceability and no false implementation/certification claims or misrouting into milestone-specification paths.
- Refresh/evolution invalidation and producer-agnostic convergence into Plan.
- Harness-owned evaluation outcomes for pass/fail/block/inconclusive/not-run, evaluator unavailable, timeout/cancel, missing evidence, dependency block, negative-control false pass, conflict, flake, provider/quota failure, retry exhaustion, duplicate-side-effect prevention, and restart-surviving partial result.
- Individual evidence retention and aggregation that never erases underlying eval outcomes or confuses evaluator deficiency with product failure.

#### Architectural obligations satisfied

- Model-authored EvalRoadmap planning is structurally and causally certified but cannot become its own executable verdict authority.
- Harness scheduling, retries, evidence, hypothesis outcomes, aggregation, and readiness remain independently owned and restart-safe.
- Eval and Traditional producers converge on the same Plan contract while retaining complete source-to-milestone traceability.

#### Dependencies

- Milestones 1–9.
- Independent graph, semantic, state, and result authorities.
- A product/harness boundary decision for any future executable eval handoff.

#### Architectural risks retired

- Eval planning mistaken for execution, model-authored DAG as sole oracle, invented implementation claims, missing negative controls, lost individual verdicts, retry duplication, producer-specific Plan behavior, and future graph topology blindness.

#### Production confidence gained

Real Codex can transform a minimal eval intent into a valid, traceable, convergent planning graph, while executable fixture verdicts remain independently owned and explainable.

#### Architectural coverage expanded

- Every EvalRoadmap stage and artifact.
- Evaluation graph, traceability, refresh, failure/retry vocabulary, and certification-readiness handoff.
- Future branching/parallel topology obligations remain visible even while active execution is serial.

#### Exit criteria

- Every source statement used by the scenario is traceable through dependency, hypothesis, catalog, DAG, roadmap, epic, and milestone or is explicitly rejected.
- Cycle/conflict/negative-control cases cannot produce false readiness.
- Repository mutation invalidates and refreshes the correct causal descendants.
- Eval and Traditional products satisfy the same Plan entry contract.
- Harness verdicts remain independent from EvalRoadmap prose and preserve each underlying outcome across retry and aggregation.

### Milestone 11 — Completion, archive, recovery, and idempotent closure certification

#### Objective

Certify completion as a singular, independently verified, resumable production capability from repository truth through policy routing, archive, synthesis, context update, canonical close, continuity retirement, and no-work rerun.

#### Architectural rationale

Completion currently combines multiple prompts and mutations in one transition, has known partial-archive/index/rerun/context risks, and active composition omits available archive/evidence components. Closure is the highest false-confidence risk: polished certification prose and checked boxes can disagree with repository truth. It must be certified before full chains.

#### Capabilities introduced

- Independent discovery and verification of active epic, strict milestones, completion trigger, fresh Project Context, execution/non-implementation/blocker/evaluation evidence, archive association, and repository acceptance signal.
- Full completion, drift, and recommendation vocabulary: coherent close/close-with-follow-up and coherent non-close routes; partial, continue, reopen, gather-evidence, contradictory, unknown, and malformed outcomes.
- Deterministic parser/policy/router agreement with repository truth; model output cannot override negative independent evidence.
- Demonstrated route behavior for approval/close, rejection, partial certification, evidence gathering, and blocked/failure outcomes. Every non-close route either re-enters execution through an explicit production path or exposes a precise durable operator action; milestone exhaustion cannot become an unexplained dead end.
- Archive index gaps, nonnumeric entries, existing synthesis, collisions, copy/move set, SQLite history/evidence materialization, hashes, metadata, and before/after path resolvability.
- Controlled interruption during materialization, synthesis, completion-context update, canonical close, and continuity retirement, with one authoritative resumable/fail-closed archive state.
- Recovery with live inputs, copied inputs, metadata-only evidence, and absent metadata.
- Already-certified discovery after live artifacts are archived and a mandatory second invocation that opens no Codex session and leaves closed state unchanged.
- Active-wiring verification for SQLite archive materializer and execution-evidence participation; absent wiring is an explicit failing or narrower-contract result, not assumed coverage.

#### Architectural obligations satisfied

- Closure is one independently verified, singular, resumable authority rather than a collection of successful prompts and partial side effects.
- Repository truth can veto model-authored completion, and every non-close route has an explicit continuation or operator contract.
- Archive, synthesis, context update, canonical close, and continuity retirement survive interruption and converge on a zero-work idempotent rerun.

#### Dependencies

- Milestones 1–10.
- Comprehensive persistence snapshots and Git/archive observation.
- Product decision for singular closure ownership and public routing after non-close certification.

#### Architectural risks retired

- Model self-certification, checked-box false success, partial archive mutation, rerun failure, archive-index collision, partial completion-context update, split closure ownership, missing archived SQLite evidence, continuity left active after close, and no automatic path after non-close routes.

#### Production confidence gained

Loop Relay can close only when independent repository evidence agrees, recover or stop safely at every archive boundary, and remain idempotently closed on rerun.

#### Architectural coverage expanded

- Completion inputs, vocabulary, policy routes, archive shape, synthesis, context update, canonical `CertifiedCompletion`, and continuity retirement.
- Issues `004`, `005`, and `006` and all archive/completion known risks.

#### Exit criteria

- Every valid and deliberately invalid policy combination has an exact route and independent repository oracle.
- No close occurs when repository truth contradicts certification prose or milestone state.
- Every archive effect and SQLite association is discoverable after restart and path relocation.
- Interruptions at each closure side effect yield one authoritative recovery action and preserve required evidence.
- A completed rerun performs zero model/provider turns, makes no Git/archive mutation, returns success, and leaves continuity retired.

### Milestone 12 — Systematic failure, recovery, and oracle closure

#### Objective

Close the architectural failure and recovery matrix across every prompt-bearing transition class, local effect class, authority interaction, and terminal outcome before relying on full-chain smoke.

#### Architectural rationale

Earlier milestones establish recovery with representative vertical slices. Production certification requires systematic expansion: one happy path per workflow cannot prove cancellation, ambiguous side effects, partial persistence, provider loss, permission denial, Git failure, evaluator failure, or archive recovery. This milestone converts the maintained failure vocabulary into a release-visible denominator.

#### Capabilities introduced

- Boundary coverage from pre-resolution through post-workflow transfer for every materially distinct prompt/session/effect posture, using equivalence only when authority, side-effect, and recovery semantics are demonstrably identical.
- Recoverable cases for repaired context, corrected malformed output, canonical artifact restoration, projection regeneration, scoped rollback, incomplete split/promotion, stranded publication, missing parent pointer, changed implementation without handoff, handoff without publication, committed decision without artifact, pointer conflict, partial archive/context update, cancelled output, corrected stall, and usage limit after failure.
- Fail-closed cases for unsupported schema/profile, untrusted corrupt authority, ambiguous provider side effect, multiple fork children, causal mismatch, recovery marker mismatch, hard-deny violation, unresolved dual authority, and closed evidence contradicted by repository truth.
- Process death before request, after write, after acceptance, during output, after terminal response, and at each ordered effect; cancellation at all meaningful transport boundaries.
- Usage-limit wait/retry/cap, provider outage, malformed output, retry exhaustion, and no duplicated semantic progress.
- Cross-domain exact, structural, semantic, invariant, state, graph, workflow, persistence, protocol, Git, and repository-acceptance oracle coverage for every artifact and terminal claim.
- Repeated-run distributions, accepted route sets, flake classification, quarantine owner/expiry, and recertification triggers based on measured transition variance.
- Evidence retention differences for success, failure, flake, block, incompatibility, and privacy-sensitive runs.

#### Architectural obligations satisfied

- Every maintained production failure and recovery boundary is an explicit ledger obligation with evidence, incompatibility, or reviewed exclusion.
- Retry and recovery semantics prevent duplicate provider turns, effects, Git commits, archives, and semantic progress across all distinct posture classes.
- Oracle coverage is complete for every behavior-bearing artifact, authority state, side effect, and terminal claim required by full-chain certification.

#### Dependencies

- Milestones 1–11.
- Maintained failure and recovery vocabularies.
- Provider capabilities remain exact-profile gated; unsupported reconstruction/fork/reconciliation obligations stay visible rather than simulated as passes.

#### Architectural risks retired

- Happy-path coverage inflation, partial-transaction masking, permanent flake quarantine, quota/provider failures mislabeled as product regressions, failure evidence loss, recovery equivalence assumptions, and uncovered issue records disappearing from reporting.

#### Production confidence gained

The platform can explain how Loop Relay behaves at every architecturally meaningful failure boundary and prove that retries, resumes, effects, and operator actions are safe.

#### Architectural coverage expanded

- Complete failure-mode and recovery-boundary dimensions.
- Complete oracle classification for expected planning, runtime, review, completion, archive, telemetry, and Git artifacts.
- Live or explicitly unsupported provider recovery features, including reconstruction, fork, capacity, and reconciliation.

#### Exit criteria

- Every maintained failure class maps to at least one case and evidence level; exclusions are explicit, reviewed, and release-visible.
- Every prompt/effect class has the required safe-retry, uncertain-side-effect, and post-validation recovery evidence.
- No duplicate provider turn, Git semantic commit, archive, or ordered effect occurs under retry.
- Flake thresholds and rerun rules distinguish provider variance from product, fixture, environment, and oracle regressions.
- Quarantined cases retain an owner, expiry/recertification condition, and uncovered coverage impact.

### Milestone 13 — Traditional full-chain live certification

#### Objective

Certify the complete `TraditionalRoadmap -> Plan -> Execute` production chain through the published CLI with real Codex, real persistence, real Git publication, restart evidence, independent completion truth, and idempotent rerun.

#### Architectural rationale

Full-chain runs are expensive and hard to diagnose. They become valuable only after every constituent posture, authority, transition family, recovery class, and closure seam has targeted evidence. This milestone proves assembly and propagation rather than re-testing every branch.

#### Capabilities introduced

- A minimal Traditional full-chain smoke repository with exact Project Context, singular capability, deterministic acceptance signal, isolated Git topology, and no ambient network/package dependency.
- Default and forced Traditional selection, automatic progression, both workflow boundaries, product propagation, real implementation, publication, certification, closure, and rerun.
- Planned restarts on each side of both workflow boundaries and at one representative warm-session/implementation boundary.
- Full normalized evidence package spanning prompts/hashes, provider posture, transitions/gates/effects, SQLite, files, Git graph, telemetry, recovery, archive, acceptance signal, cost, and latency.
- Repetition sufficient to establish the chain's acceptable route distribution and detect prompt/provider drift.

#### Architectural obligations satisfied

- The assembled Traditional chain earns level-4 confidence from the public CLI rather than inheriting confidence from isolated stages.
- Both workflow boundaries, implementation truth, publication, closure, restart, and rerun agree on one causal production history.
- Full-chain evidence consumes only obligations already established by Milestones 1–12; it does not compensate for missing targeted recovery or oracle evidence.

#### Dependencies

- Milestones 1–12.
- All critical Traditional, Plan, Execute, Git, storage, provider, recovery, and closure dimensions have nonzero live evidence.

#### Architectural risks retired

- Production assembly gap, default selection gap, inactive-wiring assumptions, boundary propagation gap, full-chain prompt interaction surprises, and first-run-only closure confidence.

#### Production confidence gained

The current published system can take a tiny real Traditional intent from fresh repository state to independently proven, published, certified, and idempotently closed implementation.

#### Architectural coverage expanded

- Evidence level 4 for the Traditional chain, both boundaries, terminal close, planned restart, and rerun.

#### Exit criteria

- The default public invocation selects the intended chain and completes without direct internal-service invocation.
- Every boundary transfers only validated, causally bound products and survives the planned restart without duplicate work.
- Repository acceptance, SQLite, filesystem, Git, and provider evidence agree on one chain history.
- The second invocation opens no model session and makes no mutation.
- Cost, latency, token, and variation results fit the provisional certification budget or produce an explicit budget decision.

### Milestone 14 — Eval full-chain live certification

#### Objective

Certify the complete `EvalRoadmap -> Plan -> Execute` production chain with the same level of public-path, recovery, independent-oracle, publication, closure, and rerun evidence as the Traditional chain.

#### Architectural rationale

EvalRoadmap has different selection and planning artifacts but must converge on universal Plan products and the same Execute/closure authority. A separate chain milestone prevents a Traditional pass from masking Eval traceability, graph, refresh, or producer-convergence defects.

#### Capabilities introduced

- A minimal one-intent Eval full-chain smoke repository with a deterministic implementation acceptance signal and independent graph/semantic oracle.
- Default Eval selection from `.agents/evals`, forced Eval selection, serial planning progression, product convergence, Plan/Execute boundaries, implementation, publication, certification, closure, and rerun.
- Planned restarts around Eval-to-Plan and Plan-to-Execute boundaries.
- Full source-to-milestone traceability and evidence that model-authored planning never substitutes for harness verdicts.
- Repetition sufficient to characterize semantic variance without requiring prose equality.

#### Architectural obligations satisfied

- The assembled Eval chain earns level-4 confidence without conflating planning artifacts, fixture execution, or completion prose with independent verdicts.
- Eval-to-Plan producer convergence and both restartable workflow boundaries are proven through the same public authority as the Traditional chain.
- Complete traceability, implementation truth, publication, closure, and rerun agree on one causal production history.

#### Dependencies

- Milestones 1–13.
- EvalRoadmap planning and harness-evaluation separation proven in Milestone 10.

#### Architectural risks retired

- Eval full-chain assembly gap, planning/execution conflation, producer-specific Plan behavior, graph evidence lost at boundaries, and unchecked model-authored certification claims.

#### Production confidence gained

The current published system can take a minimal eval intent through traceable planning, convergent Plan entry, real implementation, independent certification, and idempotent closure.

#### Architectural coverage expanded

- Evidence level 4 for the Eval chain, both boundaries, source traceability, terminal close, restart, and rerun.

#### Exit criteria

- Default selection chooses Eval only under the declared intent conditions; missing/ambiguous intent fails or routes exactly as contracted.
- Eval products preserve complete traceability and satisfy universal Plan entry without producer branching.
- Full-chain repository, state, Git, provider, and archive evidence agree.
- Harness results remain independent from EvalRoadmap and completion prose.
- The second invocation performs no model work or mutation.

### Milestone 15 — Continuous, cross-platform production certification

#### Objective

Turn the proven fixture ecosystem into a sustainable release-certification service that detects architectural drift, controls cost and flake, preserves privacy, and scales to new workflows, schemas, providers, and platforms without reducing confidence.

#### Architectural rationale

One successful certification campaign establishes reachability, not durable production confidence. The final capability is continuous governance: the right tiers must run at the right cadence, external outages must be distinguishable, new production surface must expand denominators automatically, and legacy evidence must retire deliberately.

#### Capabilities introduced

- Evidence tiers for per-change hermetic checks, replay/protocol checks, low-cost live transitions, full-chain smoke, scheduled recovery, cross-platform topology, and release compatibility certification.
- Defined release blockers by dimension and evidence level, with explicit approval for exclusions and no aggregate masking.
- Measured cost, prompt size, token, latency, storage, retention, and human-review budgets by transition and chain.
- Repetition, retry, flake, quarantine, provider-outage, quota-aware scheduling, and recertification policies.
- Windows and supported Unix-like evidence for separators, case, line endings, UTF-8 console, shell/Git behavior, executable bits, filename case, path length, and normalized equivalence.
- Compatibility gates for Codex binary/schema/model/effort changes and separate semantic, prompt, transport, fixture, environment, and oracle drift classifications.
- Automatic coverage invalidation for changes to bases, overlays, workflows, products, effects, prompts, schemas, artifact paths, enums, provider capabilities, execution agents, failure vocabularies, and known-risk/issue records.
- Retention and retirement policies for successful, failed, flaky, blocked, incompatible, legacy-schema, old-prompt, and unsupported-profile evidence.
- Capacity for future non-linear/parallel workflows: concurrency, effect conflicts, shared `.agents`, Git merge behavior, quota coordination, ordering-independent oracles, and cancellation fan-out remain explicit uncovered obligations until production supports them.
- Coverage and diagnostic reporting that shows covered/uncovered sets, evidence ages, profile/platform identity, and concise state/file/SQLite/Git/semantic diffs.

#### Architectural obligations satisfied

- Production confidence remains current after workflow, schema, prompt, provider, platform, fixture, oracle, and failure-vocabulary evolution.
- Release decisions consume dimension-specific confidence-ledger state and cannot hide critical zeros behind aggregate success.
- Certification cost, flake, privacy, retention, platform, ownership, and future-topology obligations have durable governance rather than campaign-specific convention.

#### Dependencies

- Milestones 1–14.
- Measured live evidence sufficient to set budgets and thresholds.
- CI credential, platform, retention, and release-ownership decisions.

#### Architectural risks retired

- Quota-dependent noisy CI, permanent quarantine, silent provider drift, unreviewed golden/schema migration, stale fixtures, unowned recertification, cross-platform blind spots, unchecked new architecture, and evidence retention/privacy sprawl.

#### Production confidence gained

Production certification becomes repeatable, current, economical, explainable, and resistant to architectural drift. A release can state not just that tests passed, but which production obligations were certified live, replayed, component-checked, incompatible, excluded, or still uncovered.

#### Architectural coverage expanded

- Supported platforms and release cadences.
- Drift, governance, budget, retention, and future-evolution dimensions.
- Ongoing evidence level 4 maintenance for both production chains and critical recovery/provider obligations.

#### Exit criteria

- Tier schedules and release blockers are defined from measured cost/variance rather than guesswork.
- A provider outage, quota event, fixture drift, oracle drift, product regression, and environment failure produce different actionable results.
- A change to any production-derived denominator automatically invalidates or adds coverage rather than silently passing old evidence.
- Cross-platform normalized results agree on contractual behavior while preserving genuine topology differences.
- No critical dimension can report release-ready with zero required live evidence.
- Evidence retirement never removes an obligation; it returns that obligation to uncovered until recertified or explicitly de-supported.

## Required scenario rollout and milestone ownership

This table ensures the audit's opportunity inventory is not reduced to happy paths. A row identifies the first milestone that must make the scenario executable; later milestones may raise its evidence level.

| Scenario family | First owner | Required outcomes |
|---|---:|---|
| Fresh/null repository, minimal/malformed Project Context | 1–2 | correct default selection, missing-input block, no false progress, exact nine-file contract, no mutation |
| CLI modes, status, unblock, exit codes, guard | 2 | default/forced/bounded behavior; wait/block/fail/cancel/stall/ambiguous/close visibility |
| Filesystem/SQLite authority and public storage | 2, expanded 8 | missing, empty, SQLite-only, legacy-only, matching, stale, conflict, corrupt, unsupported, partial transaction, stable reset |
| Provider identity, process, posture, permissions, telemetry | 3 | certified/unsupported profile, live approval, exact authority, malformed transport, cancellation, privacy, cleanup |
| Transition interruption and replay | 4, completed 12 | safe retry, exact resume, committed-output reuse, effect recovery, unknown-side-effect block, no duplicates |
| Plan-ready, warm Plan, invalid output, scoped details/milestones | 5 | producer-neutral entry, same-thread revision, restart safety, precise approvals, rollback, valid Execute entry |
| Execute-ready, decision lifecycle, implementation/handoff, stall/HITL | 6 | fresh/resume/transfer/failure, bounded diff, acceptance signal, restart safety, no false progress |
| Publication and permission adversaries | 7 | ordinary/nested `.agents`, isolated upstreams, dirty/detached/divergent, stranded/lost response, graph correctness |
| Persistence domains and logical artifacts | 8 | normalized complete snapshot, references/hashes, migrations, concurrency, active-wiring truth |
| Traditional audit/create/split/realign/reimagine/retire | 9 | all valid/failure routes, lineage/lifecycle/provenance, longitudinal invalidation, Plan convergence |
| Eval intent/dependency/hypothesis/catalog/DAG/roadmap | 10 | traceability, cycles/conflicts/negative controls, refresh, bounded frontier, Plan convergence |
| Evaluation executor outcomes | 10, expanded 12 | pass/fail/block/inconclusive/not-run, dependency/retry/flake/provider distinctions, preserved individual verdicts |
| Completion/rejection/partial archive/already certified | 11 | coherent routes, independent truth, resumable archive, collision protection, continuity retirement, no-work rerun |
| Usage limit, malformed output, process death, provider loss | 12 | correct durable outcome, retry eligibility, operator action, cleanup, no duplicate progress |
| Traditional full chain | 13 | public default/forced chain, both boundaries, implementation, Git, close, restart, rerun |
| Eval full chain | 14 | intent selection, traceable convergence, both boundaries, implementation, Git, close, restart, rerun |
| Cross-platform/profile drift and continuous scheduling | 15 | normalized equivalence, exact incompatibility, budgets, recertification, drift-driven coverage invalidation |

## Oracle and artifact policy

Milestone specifications must assign every behavior-bearing artifact and claim to at least one independent oracle class.

| Oracle class | Primary subjects | Policy |
|---|---|---|
| Exact | seed/reset hashes, prompt identity/hash, stable CLI fields/exit codes, allowed writes, schemas/enums, permission decisions, normalized rows, ordering, recovery digests, Git topology | Dynamic values are normalized under a versioned contract; raw model prose and raw database bytes are not exact goldens |
| Structural | roadmap/epic/plan/milestone/projection/evaluation/handoff/blocker/certification Markdown, JSON, telemetry, archive shape, status explanations | Check required/forbidden sections, types, cardinality, ownership, references, and paths without wording coupling |
| Semantic | eval traceability, plan capability coverage, handoff/repository agreement, completion/repository agreement, non-implementation classification, recovery-context sufficiency | Must be constrained by deterministic facts; a model judge is supplemental only |
| Invariant | valid-product advancement, gate ordering, one terminal state, effect order, containment, no duplicate turns/effects, causal identities, hashes, closure/rerun | Any violation is fail-closed regardless of prose quality |
| State/SQLite | workflow/stage/transition/product/gate/effect/blocker/recovery/chain, continuity, history, evidence, archive, telemetry, transaction | Compare normalized logical state and references, not database file bytes |
| Workflow/graph | selected chain, eligible/completed transitions, transfers, successors, Eval DAG, split family, recovery lineage | Reconstruct from independent observations and compare causal graph, not merely declared outputs |
| Repository/Git/protocol | acceptance signal, exact diff, refs/trees/remotes, provider frames/posture/lifetime | Required whenever the production claim depends on real implementation, publication, or provider authority |

Expected inventory includes the nine Project Context files; projections/manifests; roadmap completion context; selection/provenance; active epic; milestone specifications; split/lifecycle/decision state; transition journals; every EvalRoadmap artifact; plan, review, context, details, milestones, readiness; decisions/recommendation/history; handoffs/deltas; implementation evidence and repository diff; slice baselines; non-implementation evidence; completion claim/evaluation/blocker; archive synthesis/metadata; `CertifiedCompletion`; normalized SQLite/filesystem/Git/provider/telemetry/recovery snapshots; process cleanup; case identity; coverage claims; and cost/latency summary.

Golden outputs are limited to deterministic prompts, normalized status, schemas, fixture inputs, structured examples, and scrubbed capability evidence. Real Codex prose uses structural, semantic, and invariant contracts. Exemplars may aid diagnostics but may not become brittle pass/fail authorities.

## Cost and execution strategy

- Keep full-chain live coverage to the two minimum smoke chains until a new chain adds a distinct production obligation.
- Start branch and failure cases immediately before the transition under audit using already-validated upstream products.
- Prefer dependency-free text or tiny executable acceptance signals; add languages/frameworks only for a real platform/provider boundary.
- Choose overlay combinations for authority interactions and risks, not Cartesian completeness.
- Use deterministic/replay tiers broadly, low-cost live transitions selectively, scheduled recovery certification, and full-chain runs at release-relevant cadence.
- Reuse a live session only when session reuse is the behavior under test.
- Measure prompt growth, especially decision transfer and completion, and preserve individual transition cost/latency/token evidence.
- Retain normalized logical snapshots and bounded scrubbed evidence rather than complete disposable workspaces when diagnostics do not need them.
- Require human review for genuine semantic ambiguity or provider drift, never for normalized timestamps, IDs, paths, or incidental prose.

## Audit risk retirement traceability

The following audit seams are release-significant and cannot be declared closed merely because component code exists.

| Audited seam | Retirement milestone |
|---|---:|
| Production authority still named `CanonicalWorkflowDefinitionSketches` | 1–2: production-derived discovery and drift coverage |
| Warm Plan session lost between write and revision | 5 |
| Warm execution session and in-memory facts lost before handoff | 6 |
| Decision reconstruction/fork/reconciliation capability gaps | 3–4 and 12; unsupported capabilities remain visible until live-certified |
| In-memory workflow-boundary evidence despite SQLite chain records | 2 |
| Narrow unified storage verifier/commands versus richer retained services | 2 and 8 |
| EvalRoadmap planning versus absent executable eval architecture | 1 and 10 |
| Multi-effect completion and known archive/rerun/index/context hazards | 11 |
| Unrestricted production sessions and skipped live approvals | 3, 5–7 |
| Active completion composition omits archive/evidence components | 8 and 11 |
| Repository observer does not populate working-tree changes | 2 and 7 |
| No live prompt/product corpus | 5–6 and 9–11 |
| No cost, flake, repetition, or cross-platform baseline | 12–15 |

## Roadmap extensibility contract

This roadmap is expected to evolve as Loop Relay gains workflows, products, effects, providers, schemas, execution agents, platforms, and failure modes. New milestones or changes to existing milestones are valid only when they preserve the certification architecture.

A future milestone must:

- originate from a new or changed production-derived obligation, an open architectural seam, or evidence that invalidates the current dependency topology;
- preserve every roadmap invariant, especially repository/scenario orthogonality, independent certification, oracle independence, production-path validation, deterministic lifecycle, fail-closed behavior, and coverage monotonicity;
- be inserted only after all obligations it consumes are established and before every milestone that consumes its obligations;
- include objective, architectural rationale, capabilities introduced, architectural obligations satisfied, dependencies, risks retired, confidence gained, coverage expanded, and exit criteria;
- declare its opening confidence-ledger state, intended ledger delta, required evidence level, residual uncovered set, and effect on seam status;
- use a new repository only when capability semantics or the independent acceptance signal changes; runtime variation remains a scenario overlay;
- define independent oracles and recovery obligations before claiming live coverage;
- preserve lower-tier evidence provenance when raising confidence and return invalidated evidence to uncovered rather than deleting it;
- update scenario ownership, oracle/artifact policy, seam traceability, completion invariants, cost/tiering, and roadmap validation where affected.

Parallel milestone execution is allowed only when neither milestone consumes the other's obligations and shared authorities cannot weaken isolation or diagnostic causality. Removing or merging a milestone does not remove its obligations; they must be transferred with equivalent or stronger evidence or returned visibly to the uncovered set.

## Roadmap validation

The roadmap is itself subject to fail-closed validation. A revision is invalid if it:

- violates or locally waives a roadmap invariant;
- allows a later milestone to assume confidence not established by its dependency closure;
- reduces architectural confidence, evidence strength, or a coverage denominator without an explicit de-support decision and visible uncovered impact;
- removes or weakens independent certification or permits Loop Relay/model output to become its own sole oracle;
- replaces production-path or live-provider validation with mocked, component, replay, or historical evidence while retaining the stronger confidence claim;
- increases fixture coupling, repository complexity, base-state leakage, or repository duplication where compatible overlays suffice;
- hides an architectural seam inside implementation work, marks a seam retired without its named evidence, or fails to reopen a seam after evidence invalidation;
- drops a workflow transition, authority interaction, recovery boundary, provider capability, posture, failure class, known-risk record, schema domain, artifact, platform, or oracle obligation from the ledger without review;
- introduces a new production surface without a coverage, oracle, recovery, privacy, reset, retention, and ownership strategy;
- changes milestone ordering in a way that creates circular dependencies, consumes future confidence, or weakens the no-shortcuts rule;
- reports progress through fixture/test counts or aggregate percentages while critical uncovered sets remain concealed;
- introduces an architectural regression without a named retirement milestone and measurable exit evidence.

Every accepted roadmap revision must show invariant preservation, dependency-topology validity, confidence-ledger continuity, seam-status changes, audit/issue traceability, and completion-invariant impact. Failure to demonstrate any one of these makes the revision incomplete rather than implicitly acceptable.

## Roadmap completion invariants

The roadmap is complete only when all of the following remain simultaneously true:

- both declared production chains have current live level-4 certification through the published CLI;
- every active production workflow transition has public-path live certification, using real Codex for prompt-bearing behavior and production local execution for deterministic transitions;
- every execution posture has observed live authority, containment, session-lifetime, allowed-effect, and teardown evidence;
- every behavior-bearing authority interaction has an independent oracle for precedence, synchronization, conflict, invalidation, recovery, and containment;
- every relevant transition, transport, session, persistence, effect, Git, archive, and workflow-boundary recovery point has explicit safe-retry, resume/reconcile, effect-recovery, operator-action, or fail-closed evidence;
- every provider capability claim is supported by exact current-profile live evidence or is reported as an explicit incompatibility with the gated operation prevented;
- every persistence domain, Git effect, behavior-bearing artifact, and terminal claim has an appropriate exact, structural, semantic, invariant, state, graph, repository, Git, or protocol oracle;
- completion is independently true, resumable across partial side effects, and idempotent on rerun;
- every checked-in issue and known-risk record has corresponding active-public-path certification coverage until explicitly retired by passing evidence;
- every architectural seam under retirement is retired by its named evidence and automatically reopens if that evidence expires or is invalidated;
- no production-confidence claim depends solely on a mock, scripted runtime, direct retained-service invocation, replay, model self-evaluation, artifact presence, or component test;
- case creation/reset/cleanup is deterministic and confined, retained evidence is private and explainable, and no fixture repository carries runtime complexity that belongs in scenarios;
- the confidence ledger contains production-derived denominators, exact evidence, evidence levels, exclusions, and uncovered sets, with no release-blocking gap hidden by fixture count or aggregate percentages;
- continuous tiering, drift invalidation, budget, flake, retention, platform, and ownership policies keep certification current after the initial implementation campaign.

Completion is lost whenever one of these invariants becomes false; prior milestone completion does not grandfather stale confidence. At completion, Loop Relay is not merely tested by a large suite. It has a composable continuous production certification system that can explain what the current public executable, current Codex profile, current workflows, current persistence schema, and current Git/closure behavior have actually proven—and exactly what remains uncertain.
