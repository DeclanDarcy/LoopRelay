# Codex Resume Recovery Audit

## 1. Executive Summary

- **Proven — current behavior.** LoopRelay has many Codex-backed execution shapes, but only the Execute workflow's warm decision session is resumable across LoopRelay process restarts. The durable identity is one Codex app-server thread ID stored in the singleton SQLite row **decision_session_resume.id = 1** under **.LoopRelay/persistence/looprelay.sqlite3**. LoopRelay's own **SessionIdentity** is a new GUID for every open and is not the resumable identity.
- **Proven — current failure path.** On the first decision-session open in a process, **DecisionSession.OpenOrResumeSessionAsync** reads the stored thread ID and asks **AgentRuntime** to open it. **AgentRuntime** launches Codex app-server and eagerly performs initialize → initialized → thread/resume. Any typed resume failure causes **DecisionSession** to log “Starting fresh,” clear the singleton resume row, open a non-resuming session, and submit the ordinary fresh-session proposal prompt. It does not recover or inject the failed thread's conversation.
- **Proven — experimentalApi root cause.** LoopRelay always sends **excludeTurns: true** in thread/resume (**CodexAppServerProtocol.ThreadResume**, lines 59-67) but its initialize request sends only clientInfo and does not advertise **capabilities.experimentalApi** (**CodexAppServerProtocol.Initialize**, lines 23-32). Against the exact configured binary, Codex CLI 0.142.5, that sequence returns JSON-RPC error code -32600 with the exact message: **thread/resume.excludeTurns requires experimentalApi capability**. Advertising the capability or omitting excludeTurns advances to normal thread lookup. A valid historical LoopRelay thread resumed successfully after advertising the capability. The defect is therefore repository-owned protocol negotiation, not stale metadata, MCP, an SDK, authentication, or demonstrated version skew.
- **Proven — recoverability.** The repository does not persist the conversation, but Codex does. Historical LoopRelay threads were found under **%USERPROFILE%/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl**. Codex 0.142.5 can read a valid thread through app-server **thread/read** and can resume it without changing the rollout file. The CLI also exposes **codex fork**, and app-server recognizes **thread/fork**. Repository-owned operational context, handoffs, decisions, prompt assets, transition output, and repository state provide a lower-fidelity reconstruction path.
- **Strongly supported — recommended owner.** The narrowest current owner with enough information is the decision-session lifecycle boundary represented by **DecisionSession.OpenOrResumeSessionAsync**, preferably factored into a dedicated recovery coordinator. Protocol-specific detection/read/fork operations belong behind the Codex adapter; the decision-session owner must choose policy, assemble repository context, preserve workflow lineage, and commit the active identity. **AgentRuntime** alone lacks persistence, workflow, content, and provenance.
- **Strongly supported — recovery mechanism decision boundary.** First repair deterministic protocol defects; the experimentalApi error must not trigger content fallback. The planning phase must evaluate native Codex **thread/fork** as a candidate replacement mechanism when negotiated support exists and disposable validation demonstrates that it preserves more conversational fidelity and has safer failure semantics than reconstruction. Independently, **thread/read**, rollout salvage, and repository-authoritative artifacts can support a bounded, labeled recovery envelope at explicitly stated completeness. If only repository artifacts remain, recovery is partial. Never describe any different thread ID as resuming the original, and do not assume a fork-first ordering before the tradeoffs are certified.
- **High-risk gaps.** The current catch treats nearly every eager-handshake exception as recoverable; clears the only old pointer before replacement creation; has no first-class provider-neutral Recovery Journal or durable lineage; has no negotiated SessionCapabilities snapshot; can retain stale cross-epic state in the production unified CLI; has no transcript normalization, secret filtering, or size budget; and suppresses the fallback warning in production. Existing transition journaling is useful but is not linked to thread IDs or turn IDs.
- **Verdict.** **Ready with explicitly bounded runtime validation.** The current architecture and experimentalApi cause are sufficiently proven for implementation planning. Capability discovery/negotiation, native fork creation semantics and relative fidelity, compatibility across a declared Codex version range, corrupted-rollout behavior, and fork unknown-outcome handling still require disposable-fixture validation before implementation choices are finalized.

## 2. Scope and Audit Constraints

**Proven — inspected scope**

- Session abstractions, specs, identity types, registry, process launcher/runner, app-server transport, JSON-RPC builders/parsers, turn accumulation, one-shot execution, and teardown under **src/LoopRelay.Agents**.
- The production unified CLI entry, workflow composition, default/forced/bounded mode resolution, workflow chaining, decision/plan/execution session ownership, transition runtime, canonical transition persistence, loop artifacts, telemetry code, and SQLite schema.
- Decision resume state models, SQLite and legacy file stores, operational artifacts, prompt assets, transition evidence, repository observation, and completion/transfer cleanup paths.
- Focused tests for protocol frames, eager resume, decision fallback, resume stores, rollout location, workflow composition/chaining, CLI modes, transition persistence, and test doubles.
- Package manifests and lockfile equivalents, current environment flags, installed Codex binaries, current Codex config keys, Codex-local session storage, repository history, blame, and the commits that introduced resume.
- Runtime probes against the exact **CODEX_EXECUTABLE** configured for LoopRelay.

**Proven — constraints and non-actions**

- No existing source, configuration, test, prompt, plan, roadmap, or documentation file was modified.
- No production-like resume row was edited, no Codex session was deleted, no real replacement/fork was created, no dependency was changed, and no recovery was applied.
- Runtime probes used either a deliberately nonexistent thread ID or read/resumed a historical LoopRelay thread without starting a turn. File length and modification time were checked before and after the valid read/resume probes and were unchanged.
- The repository had no **.LoopRelay** directory at audit time, so there was no repository-local active resume row or telemetry database to correlate to the reported incident. This limits incident-specific provenance, but not the deterministic root-cause conclusion.
- Tests were inspected rather than run because the requested output constraint permits only **audit.md** and normal test execution may create or update build artifacts. The controlled app-server subprocess probes supplied direct compatibility evidence.
- The OpenAI documentation skill was consulted. Its normal manual helper was not run because it writes a cache file, and installing a missing docs MCP would modify Codex configuration, both outside the audit's write constraint. A narrow official-site search did not surface public app-server protocol documentation for these fields. Installed CLI help and live wire behavior are therefore the authoritative external evidence used here.

**Evidence labels**

- **Proven:** directly shown by current source, tests, history, local persisted data, or captured command output.
- **Strongly supported:** multiple concrete facts agree, but one non-mutating runtime experiment cannot prove the complete behavior.
- **Tentative:** plausible and relevant, but requires the validation named in section 19.

## 3. Current Codex Session Lifecycle

### 3.1 Session kinds

| Kind | Creation and lifetime | Identity | Persistence | Current production use |
| --- | --- | --- | --- | --- |
| One-shot | **AgentRuntime.RunOneShotAsync** launches **codex exec --json ... -**, sends one prompt on stdin, closes stdin, consumes JSONL, and disposes the process. | New LoopRelay **SessionIdentity** only; **IAgentSession.ThreadId** is null. | Output may become workflow evidence/artifacts; no resumable thread identity. | Eval, Traditional Roadmap, milestone, projection, completion, and other isolated prompts. **Proven:** **CodexAgentArgumentBuilder**, lines 55-79; **AgentRuntime**, lines 75-90. |
| Process-warm app-server | **OpenSessionAsync** launches **codex ... app-server --listen stdio://**. The first turn lazily initializes and starts a thread; later turns reuse it. | LoopRelay SessionIdentity plus Codex thread ID after handshake. | Usually no thread ID persistence; the session ends with the current process or transition group. | Plan authoring/revision, execution/handoff, review, and scoped artifact operations. **Proven:** **UnifiedCliComposition**, lines 1039-1107, 1125-1240, 1351-1430, 1494-1634. |
| Cross-process decision session | Same app-server transport, but the stored Codex thread ID is supplied as **ResumeThreadId** and the resume handshake is eager. | New LoopRelay SessionIdentity for each open; stable Codex thread ID on successful resume. | Singleton **DecisionSessionResumeState** is written after each successful proposal. | Execute workflow's GenerateDecision / TransferDecisionSession / ContinueDecisionSession transitions. **Proven:** **DecisionSession**, lines 73-128 and 161-237; **UnifiedCliComposition**, lines 1432-1473. |

### 3.2 End-to-end lifecycle

~~~text
Program
  -> CliArguments: choose DefaultChained / ForcedEvalChain / ForcedTraditionalChain / bounded mode
  -> UnifiedCliComposition.CreateProduction
     -> AddAgents -> AgentRuntime + AgentSessionRegistry + Codex launcher
  -> WorkflowChainRunner
     -> TransitionRuntime persists Started + prompt evidence
     -> UnifiedPromptExecutor chooses one-shot or held-open posture
        -> AgentSessionSpec assigns a new LoopRelay SessionIdentity
        -> EnvironmentAgentExecutableResolver resolves CODEX_EXECUTABLE
        -> CodexAgentArgumentBuilder builds exec or app-server arguments
        -> ProcessRunner starts the exact executable with redirected stdio/stderr
        -> CodexAppServerSession performs initialize -> initialized -> thread/start or thread/resume
        -> turn/start sends one text input; notifications determine output, usage, and terminal state
     -> Prompt output is interpreted, validated, effects applied, and transition completion persisted
  -> UnifiedPromptExecutor.DisposeAsync closes plan/execution/decision sessions
  -> AgentSessionRegistry/provider disposal kills any remaining child process
~~~

**Proven — creation and identity assignment.** Every **AgentSpecs** factory calls **SessionIdentity.New()** before launch. **AgentRuntime.OpenSessionAsync** registers a session by (**RepositoryId**, **SessionIdentity**) before handshake. Fresh app-server sessions do not receive a Codex thread ID until the thread/start response contains **result.thread.id**. The production CLI assigns **Repository.Id = Guid.NewGuid()** on each invocation (**CliArguments**, lines 140-147), so the registry namespace is process-invocation-specific even for the same filesystem repository.

**Proven — invocation.** Persistent arguments are:

~~~text
<CODEX_EXECUTABLE>
  --cd <repository>
  --sandbox <sandbox>
  --ask-for-approval <on-request|never>
  app-server
  --listen stdio://
~~~

One-shots use **exec --json --skip-git-repo-check --cd ... -c approval_policy="never" -c model_reasoning_effort="..." -**. There is no Codex SDK or MCP client library in project dependencies; LoopRelay directly launches the CLI and directly implements app-server JSON-RPC. **Proven:** **Directory.Packages.props**, **LoopRelay.Agents.csproj**, **CodexAgentProcessLauncher**, and **ProcessRunner**.

**Proven — success and failure detection.** A thread create/resume succeeds only if the JSON-RPC response has no error and contains **result.thread.id**. A turn succeeds only when **turn/completed.turn.status == "completed"**; interrupted maps to Canceled and every other terminal status maps to Failed. Protocol failure text is preferred over the bounded 8,192-character stderr tail. Stream end releases all pending request waiters with an IOException. **CodexAppServerSession**, lines 90-159, 236-279, 281-323, 474-487; **CodexAppServerTurnReader**, lines 165-184.

**Proven — retry and recovery.** There is no retry around session resume. **GatedAgentSession** contains a three-retry policy only for usage-limit turn failures, not opens, and it is not constructed by the production **UnifiedCliComposition**. Resume recovery is the single catch in **DecisionSession.OpenOrResumeSessionAsync**. Other app-server sessions fail their transition and close. One-shots are not retried.

**Proven — closure.**

- Plan authoring closes after RevisePlan, failure, cancellation, or executor disposal.
- Execution closes after GenerateHandoff, failure, cancellation, or executor disposal.
- Review/scoped sessions close in finally blocks after one turn.
- Decision transfer and failed decision turns call **CloseAsync(clearResumeState: true)**; ordinary executor disposal calls **CloseAsync(false)**, retaining the resume row.
- **AgentRuntime.CloseSessionAsync** deregisters and disposes the process; registry/provider disposal is the final safety net.

**Proven — handoff to later workflow steps.** Codex output is not itself the workflow authority. **TransitionRuntime** records raw output, interprets it, validates products, applies effects, and only then persists transition completion. Decision output is also persisted as **decisions.md** and numbered/SQLite history; execution consumes it and writes a handoff. **TransitionRuntime**, lines 99-298; **LoopArtifacts**, lines 43-76.

**Proven — process restart restoration.** Only DecisionSession restoration exists. The next process reads the singleton state, eagerly resumes its Codex thread, sets **seeded = true**, restores router accounting, and sends only the next handoff-derived proposal rather than the operational context. Plan and execution warm sessions cannot survive a process restart; workflow products and canonical transition records, not Codex threads, drive their restart behavior.

## 4. Current Resume Path

### 4.1 Authoritative call chain

1. **WorkflowChainRunner.RunAsync** selects or advances to Execute. Default and forced chains can arrive here after Traditional/Eval and Plan. **Proven:** **WorkflowChaining.cs**, lines 341-420.
2. **TransitionRuntime.RunAsync** persists a new random run ID in Started state before invoking the prompt executor. **Proven:** lines 28 and 84-103.
3. **UnifiedPromptExecutor.ExecuteAsync** routes GenerateDecision, TransferDecisionSession, and ContinueDecisionSession to **ExecuteDecisionSessionAsync**. **Proven:** **UnifiedCliComposition**, lines 603-607 and 902-910.
4. **ExecuteDecisionSessionAsync** lazily constructs one **DecisionSession** for the entire prompt-executor lifetime, supplying **SqliteDecisionSessionResumeStore**, **DecisionResumeComposition.IsEnabled()**, a null projection service, and a console backed by **TextWriter.Null**. It ignores the rendered prompt text and lets DecisionSession construct the actual Codex prompt. **Proven:** lines 811-820 and 1432-1454.
5. **DecisionSession.RunAsync** evaluates the router, optionally transfers, then calls **OpenOrResumeSessionAsync** when no in-memory session exists. **Proven:** lines 73-99.
6. On the first open only, **OpenOrResumeSessionAsync** reads state if **LoopRelay_DECISION_RESUME** is enabled. Later opens in the same process never resume persisted state. **Proven:** lines 161-175.
7. **SqliteDecisionSessionResumeStore.ReadAsync** selects **document_json** from the singleton row; it imports legacy **.LoopRelay/decision-session.json** only if SQLite has no row. It accepts only schema version 1 and a non-empty thread ID. **Proven:** lines 33-49, 103-173, 213-216.
8. **AgentSpecs.Decision(repository, state.ThreadId)** assigns a new LoopRelay SessionIdentity and carries the old Codex ID in **ResumeThreadId**. **Proven:** **AgentSpecs**, lines 55-63.
9. **AgentRuntime.OpenSessionAsync** launches app-server, registers the new LoopRelay session, and because ResumeThreadId is non-null, immediately calls **EnsureReadyAsync**. **Proven:** **AgentRuntime**, lines 17-57.
10. **CodexAppServerSession.EnsureHandshakeAsync** sends initialize, initialized, and thread/resume. The resume frame includes threadId, cwd, sandbox, approvalPolicy, and excludeTurns. **Proven:** lines 236-261.
11. A JSON-RPC error becomes **AgentSessionResumeException**; missing result.thread.id becomes the same typed exception. Any other non-cancellation exception in the eager handshake is wrapped as **AgentSessionResumeException** by AgentRuntime. The failed process is deregistered and disposed. **Proven:** **CodexAppServerSession**, lines 257-274; **AgentRuntime**, lines 38-56.
12. **DecisionSession** catches only the typed resume exception, writes a warning, clears the resume store, and opens a fresh Decision session. **Proven:** lines 193-219.
13. The fresh open is lazy: thread/start is not sent until **RunTurnAsync**. The prompt is constructed as a fresh seed: current operational context + first/next execution-agent prompt, with the latest handoff when present. **Proven:** **DecisionSession**, lines 130-159; **AgentRuntimeResumeTests.OpenSessionWithoutResumeIdStaysLazy**, lines 70-88.
14. After the proposal turn completes, DecisionSession marks itself seeded, updates routing cost, writes the replacement thread ID, then writes decision output to history/live artifacts. **Proven:** **DecisionSession**, lines 101-127 and 222-237.

### 4.2 Exact frames

Current initialize:

~~~json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"clientInfo":{"name":"LoopRelay","version":"0.1.0"}}}
~~~

Current resume:

~~~json
{"jsonrpc":"2.0","id":2,"method":"thread/resume","params":{"threadId":"<stored>","cwd":"<repo>","sandbox":"read-only","approvalPolicy":"never","excludeTurns":true}}
~~~

Current first post-resume turn:

~~~json
{"jsonrpc":"2.0","id":3,"method":"turn/start","params":{"threadId":"<resumed>","input":[{"type":"text","text":"<proposal prompt>","text_elements":[]}],"effort":"xhigh"}}
~~~

**Proven — success criteria.** Open returns only after thread/resume returns a thread ID. No turn has been submitted at this point, so the reproduced experimentalApi failure is definitively pre-turn and cannot have caused Codex or repository work.

**Proven — failure propagation.** The JSON-RPC numeric error code is discarded; **CodexAppServerMessage** retains only **error.message**. Resume exceptions carry unstructured text. This prevents robust classification and makes the DecisionSession catch broader than its comments imply.

**Proven — current recovery result.** **DecisionSessionTests.Run_FirstEntry_ResumeFails_WarnsClearsAndFallsBackToAFreshPrimedProcess** (lines 959-980) asserts two opens, first with the old ID and second without one, one clear call, and persistence of the new thread after a successful fresh proposal. It asserts operational context is present in the new prompt; it does not assert any prior conversation content.

## 5. Active Session Identity and Persistence

| Question | Current answer |
| --- | --- |
| Is “active” explicit? | **Partly, Proven.** In memory, **DecisionSession.session** is the active process object. Across restarts, the singleton resume row is treated as the active decision thread. There is no Active flag, state machine, or lineage record. |
| What is the authoritative ID? | **Proven.** For resumption, **DecisionSessionResumeState.ThreadId**, a Codex thread/session UUID. LoopRelay **SessionIdentity** is registry/telemetry identity only and changes on every open. |
| Scope | **Proven.** Physical scope is one row per repository database path. The interface comment says “per repo, per epic,” but no repository ID, epic ID, workflow ID, invocation ID, stage, agent, mode, user, or run ID is stored. |
| Multiple active/resumable sessions? | **Proven.** The live registry can hold multiple sessions keyed by repository ID + LoopRelay session ID. Cross-restart persistence can represent only one decision thread per repository. Plan/execution threads may be live but are not resumable by LoopRelay. |
| Survives restart? | **Proven, best effort.** The SQLite row survives. Store reads/writes/clears swallow non-cancellation errors, so durability failure does not fail the workflow. |
| When is LoopRelay SessionIdentity committed? | **Proven.** It is created before process launch and registered before handshake. A failed resume removes it from the live registry. |
| When is Codex thread identity committed? | **Proven.** Fresh thread ID exists after thread/start, but DecisionSession does not persist it until a proposal turn completes successfully. Resume state is therefore a last-successful-proposal pointer, not a just-created-thread pointer. |
| Stale/superseded handling | **Proven.** Transfer and failed proposal clear the row; a later successful proposal replaces it. Invalid schema/content is cleared. Projection staleness can clear it only when a projection service is supplied. |
| Production completion cleanup | **Proven gap.** **LoopRunner** clears the row at epic completion, but production composition is explicitly tested not to construct LoopRunner. No other production path clears the row on workflow completion. Unified composition also supplies **_projectionService: null**, disabling the stale-projection check. |
| Can failed resume destroy recovery identity? | **Proven.** Yes. The catch clears the old row before opening the fresh session. Fresh open is lazy, and replacement identity is not persisted until a successful proposal, leaving a window with neither old nor replacement ID. |

**Proven — conceptual mismatch.** “Session” refers to three different things:

1. LoopRelay **SessionIdentity**: a per-open registry/telemetry GUID.
2. Codex **ThreadId**: the actual resumable conversation identity.
3. DecisionSession: the orchestration object and cost-accounting lifetime.

The SQLite row persists only (2) plus DecisionSession accounting. Telemetry, when used, records (1) and a guessed rollout path but not (2). Transition state records a random run ID but neither (1) nor (2). No durable join exists among conversation, execution attempt, workflow transition, and artifacts.

**Strongly supported — stale-state risk.** Because the current unified CLI retains decision resume state on disposal, does not run LoopRunner's epic-completion clear, and disables projection freshness validation, a later Execute lifecycle in the same repository can treat an old decision thread as active. Repository products may often prevent that route, but the persistence model itself does not enforce epic/workflow scope.

## 6. Recoverable Session Content Inventory

| Source | Location / API | Content | Completeness | Durable After Failure | Replay Suitability | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Active resume row | **.LoopRelay/persistence/looprelay.sqlite3**, table **decision_session_resume**, id 1 | ThreadId, cost/accounting counters, SavedAtUtc, schema version | Identity only; no messages or rollout path | **Yes, until current catch clears it. Proven.** | Not replayable; essential lookup/provenance key | Singleton scope; fail-open I/O; cleared too early; no lineage |
| Legacy resume JSON | **.LoopRelay/decision-session.json** | Same **DecisionSessionResumeState** JSON | Identity only | Imported then deleted; invalid/corrupt files are deleted | Lookup only | Destructive read-on-invalid; no preserved diagnostics |
| Codex rollout JSONL | **%CODEX_HOME%/sessions/YYYY/MM/DD/rollout-*.jsonl**, default **%USERPROFILE%/.codex/sessions** | session_meta/base instructions; turn_context; developer/user/assistant messages; function/custom tool calls and results; encrypted reasoning; token/events | **Closest to full Codex-owned conversation. Proven from two LoopRelay-originator files.** It includes material not exposed by thread/read. | Yes for the reproduced failure; valid read/resume left file unchanged | Best as source for native Codex operations; direct replay requires a versioned parser and filtering | Provider-private format; huge tool results; paths/environment/secrets; encrypted reasoning; malformed tail; duplicated event/message views |
| Codex thread/read | app-server JSON-RPC **thread/read** with threadId and includeTurns | Thread metadata/path and public turn items | **Partial, Proven.** Observed valid thread returned 2 turns: 21 agentMessage, 3 fileChange, 2 userMessage, while its rollout also contained developer instructions, 112 function calls, 112 function outputs, and reasoning records. | Yes while Codex can read rollout | Good source for an ordered conversational projection; not a full replay | Does not preserve all roles/tool results; undocumented compatibility surface |
| Codex native fork | CLI **codex fork <SESSION_ID>**; app-server recognizes **thread/fork** | Codex-native cloned conversation with a new thread identity | **Potentially highest fidelity, not yet certified.** CLI help and invalid-ID protocol probe prove surface recognition; a valid fork was not created due audit constraints. | Depends on readable old rollout | Candidate replacement only when negotiated support and comparative validation justify it | May fail for missing/corrupt rollout; unknown ack can orphan forks; version compatibility; must capture forkedFromId |
| Repository operational context | **.agents/operational_context.md**; generated from plan when absent; may be updated during transfer | Canonical current execution context used to seed fresh decision process | Repository-owned task context, not conversation | Yes, committed/published through workflow | Directly suitable as current context | Can be stale, incomplete, or already summarized; no exact historical version linked to thread |
| Handoff history | Live **.agents/handoffs/handoff.md**; historical logical paths **handoff.NNNN.md**; SQLite **loop_history** kind Handoff when canonical | Execution-agent outputs consumed as later decision prompts | High fidelity for user-side decision inputs, but no mapping to Codex turn IDs | Yes after successful rotation/history write | Suitable in order after selecting the boundary belonging to the old thread | Live/history precedence; gaps permitted; may contain generated/stale paths |
| Decision history | Live **.agents/decisions/decisions.md**; historical **decisions.NNNN.md**; SQLite **loop_history** kind Decisions | Decision-session assistant proposal outputs | High fidelity for assistant decision products; not narration/tool history | Yes after **PersistDecisionsAsync** | Suitable as authoritative output artifacts, not as role-faithful transcript | State write occurs before artifact write; no thread/turn/run linkage; live file is retired after consumption |
| Operational deltas | Live **.agents/operational_delta.md**; historical **operational_delta.NNNN.md**; SQLite loop_history | Summary extracted from warm decision thread before transfer | Derived summary, not full conversation | Yes after successful rotation | Good compact fallback context | Only exists when transfer completed; deliberately lossy |
| Prompt assets/policy | **src/LoopRelay.Core/Prompts/GenerateSystemPromptForFirstExecutionAgent.prompt**, **GenerateSystemPromptForNextExecutionAgent.prompt**, default implementation-first policy, optional projection | Static instructions and templating used to build decision prompts | Reconstructs repository-owned prompt logic at current revision | Yes in source/git | Suitable only after pinning source revision and input versions | Current prompt may differ from one used by old thread; user/developer role semantics are flattened in app-server input |
| Transition input metadata | **canonical_transition_runs** and **canonical_transition_evidence** | Run/workflow/stage/transition, input hash, prompt evidence locator, durable states | Does not store actual rendered prompt text. For decision transitions, rendered prompt is not the actual prompt DecisionSession sends. | Yes | Diagnostic and duplicate guard input | No session/thread/turn join; evidence location may be logical only |
| Transition raw output | **canonical_transition_evidence.document_json**, event RawPromptOutputCaptured | **PromptExecutionResult**, including decision output after executor returns | Full returned assistant output for completed executor calls | Yes after prompt executor returns | Useful for completion/duplicate detection and recovery summary | Missing if crash occurs before raw-output record; contains output, not input transcript |
| Session telemetry code | SQLite **session_telemetry_events** and **.LoopRelay/telemetry/sessions.DATE.NNNN.jsonl** | LoopRelay SessionId, role, token counts, timings, guessed CodexLogPath | Metrics only; no prompts/responses and no Codex ThreadId | Would be durable if composed | Diagnostic only | **Proven production gap:** no construction of GatedAgentRuntime/SessionTelemetryComposition in unified production; resumed old rollout fails locator's “started after open” rule |
| Process stderr | In-memory last 8,192 chars in **AgentProcess.ErrorSnapshot** | CLI/app-server warnings and fatal diagnostics | Tail only | No; only copied into failure result/transition failure if propagation reaches it | Diagnostic only | Truncation; resume JSON-RPC error comes on stdout and numeric code is discarded |
| Repository/git state | Working tree, commits, submodule history, canonical products/effects | Durable effects and artifacts that prior sessions caused | Outcome evidence, not conversation | Yes | Essential for duplicate and stale-context checks | Cannot reconstruct reasoning or exact prompt/output order |

### 6.1 Required distinctions

1. **Full original Codex conversation.** Only the Codex rollout is close to full. It contains provider/runtime records beyond an ordinary role transcript and should not be treated as a stable import format.
2. **Repository-owned input context.** Operational context, handoffs, plan/details, prompt assets, and current repository state can reconstruct the task LoopRelay intended the decision session to perform. They do not reconstruct the conversation.
3. **Derived summaries/handoffs.** Operational deltas, handoffs, and decisions are durable, useful, and intentionally lossy.
4. **Execution artifacts.** Decision products, transition outputs, repository changes, commits, and workflow products prove work/outcomes, not dialogue.
5. **Diagnostic logs.** Telemetry, stderr, transition failures, and CLI output help classify failures but are unsuitable for replay.

**Proven — observed size/completeness evidence.** Two Codex rollouts with session_meta.originator **LoopRelay**, cwd **C:\kernritsu\LoopRelay**, and cli_version **0.142.5** were found. The larger was 1,196,853 bytes with 287 response_item records. Its function-call outputs totaled 728,566 characters with a maximum single output of 40,093 characters. Using LoopRelay's own deterministic estimator, the file size alone is roughly 299,000 tokens, already beyond the configured 256,000-token decision window before a new response. “Inject everything” is therefore not a safe general mechanism.

## 7. experimentalApi Root-Cause Investigation

### 7.1 Exact observed evidence

**Proven runtime command.** The subprocess was the exact binary resolved by LoopRelay:

~~~text
& $env:CODEX_EXECUTABLE --cd C:\kernritsu\LoopRelay --sandbox read-only --ask-for-approval never app-server --listen stdio://
~~~

Environment/runtime findings:

- **CODEX_EXECUTABLE** resolved to the npm-installed Codex binary under **C:\Program Files\nodejs\node_modules\@openai\codex\...\codex.exe**.
- That binary and shell-resolved **codex** both reported **codex-cli 0.142.5**.
- A second pnpm installation at version 0.120.0 was present on PATH, but LoopRelay does not PATH-resolve Codex; **EnvironmentAgentExecutableResolver** requires CODEX_EXECUTABLE. The probe used the configured 0.142.5 binary.
- Current **%USERPROFILE%/.codex/config.toml** had no experimentalApi/experimental_api/experimental-api key. **codex features list** had no feature named experimentalApi.

**Proven failing sequence.** With current LoopRelay initialize and a nonexistent thread ID:

~~~text
initialize response: success
thread/resume response:
{"error":{"code":-32600,"message":"thread/resume.excludeTurns requires experimentalApi capability"},"id":2}
process exit code after orderly stdin close: 0
~~~

**Proven controls.**

| Probe | Result |
| --- | --- |
| initialize omits capabilities; resume includes excludeTurns=true | -32600, “thread/resume.excludeTurns requires experimentalApi capability” |
| initialize includes capabilities.experimentalApi=true; resume includes excludeTurns=true; nonexistent ID | -32600, “no rollout found for thread id 00000000-0000-0000-0000-000000000000” |
| initialize omits capabilities; resume omits excludeTurns; nonexistent ID | Same normal “no rollout found” result |
| initialize includes capability; resume includes excludeTurns; valid historical LoopRelay ID | Success; returned the same thread ID with zero turns in the response; rollout length/mtime unchanged |

These controls prove validation fails on the unnegotiated **excludeTurns** field before thread lookup. They also prove thread/resume itself does not require experimentalApi and the old rollout can remain valid.

### 7.2 Emitter and triggering path

**Proven.** The message is emitted by Codex app-server 0.142.5 as a JSON-RPC response to request ID 2. LoopRelay parses it in **CodexAppServerMessage.Parse**, retains only **error.message**, and throws:

~~~text
AgentSessionResumeException:
Codex thread/resume failed: thread/resume.excludeTurns requires experimentalApi capability
~~~

The error occurs only on LoopRelay's resume path because thread/start does not carry excludeTurns. It occurs before any turn/start and does not depend on which persisted thread ID was supplied.

### 7.3 Repository source and history

**Proven.**

- **CodexAppServerProtocol.Initialize**, lines 23-32, sends no capabilities object.
- **CodexAppServerProtocol.ThreadResume**, lines 59-67, always sends excludeTurns=true.
- **CodexAppServerProtocolTests.InitializeFrameCarriesJsonRpcEnvelopeAndClientInfo**, lines 10-19, does not require capability negotiation.
- **ThreadResumeFrameMapsThreadIdCwdSandboxApprovalAndExcludeTurns**, lines 87-99, explicitly requires excludeTurns=true.
- Commit **97a1c572d (feat(agents): thread/resume app-server frame builder, 2026-07-04)** introduced excludeTurns and its serialization tests without changing initialize.
- The commit comment says the frame was “Verified against codex-cli 0.142.5,” but current 0.142.5 live behavior rejects the exact combined handshake. The repository tests use a scripted process that accepts resume without enforcing capability negotiation.

### 7.4 Root-cause conclusion

**Proven.** The strongest and sufficient root cause is:

> LoopRelay invokes the experimental Codex app-server protocol directly and sends the experimental thread/resume.excludeTurns field without first advertising the client capability capabilities.experimentalApi=true. Codex 0.142.5 deterministically rejects that request. DecisionSession then misclassifies the deterministic integration defect as a recoverable session failure, clears the active pointer, and starts fresh.

This is a repository-owned malformed protocol negotiation. The term experimentalApi names an initialize capability, not a Codex config feature flag and not a field LoopRelay currently serializes.

### 7.5 Alternative hypotheses eliminated

- **Stale/missing session metadata:** eliminated for this error; the guard fires before lookup and reproduced with a dummy ID.
- **Version skew:** not required; code, comments, and runtime target 0.142.5, and the same binary reproduced the error. Multiple installed versions are an operational risk, but CODEX_EXECUTABLE removes ambiguity in this run.
- **Codex SDK/wrapper defect:** eliminated; no Codex/OpenAI SDK dependency exists.
- **MCP/protocol integration:** MCP is not involved in initialize/thread/resume; persistent frames deliberately contain no mcpServers/tools properties.
- **Authentication/network/rate limit:** initialize succeeded, error was local request validation, and valid read/resume probes succeeded. Incidental plugin-catalog warnings on stderr did not affect the JSON-RPC result.
- **Malformed thread ID:** eliminated for the experimentalApi response by the control matrix.
- **User config/feature flag:** no matching config/feature exists; capability negotiation on initialize controls behavior.
- **Unsupported thread/resume surface:** eliminated for current binary; normal lookup and valid resume succeeded when the request was legal.

### 7.6 Remaining uncertainty

- **Tentative:** Whether older/newer supported Codex versions require, ignore, rename, or remove this capability/field combination. LoopRelay declares no minimum/maximum Codex compatibility range and performs no version/protocol capability check.
- **Tentative:** Native thread/fork behavior after a valid resume-specific failure. Method recognition and CLI support are proven; creating a real fork was prohibited in this audit.
- **Strongly supported:** The exact reported incident followed this path, because the requested error text is identical and the current code always emits the invalid pair. The incident's actual thread ID/error capture was not present in repository-owned state at audit time.

### 7.7 Negotiated capability architecture

**Strongly supported requirement.** Capability support must become an explicit, captured session contract rather than a set of hard-coded assumptions or inferences from **codex --version**. The live controls prove that a client capability declaration changes which request fields are legal. They do **not** prove that the initialize response exposes a complete server capability list, that method recognition implies safe recovery semantics, or that one successful 0.142.5 probe generalizes to other versions.

Introduce a provider-neutral **SessionCapabilities** snapshot with tri-state values (**Supported**, **Unsupported**, **Unknown**) and evidence provenance. For the Codex adapter, the minimum projection is:

| Capability | Meaning | Audited 0.142.5 evidence | Current confidence |
| --- | --- | --- | --- |
| **CanResume** | The adapter can issue a legal thread/resume for this initialized session | Valid historical resume succeeded after a legal initialize; resume without excludeTurns reached lookup | Supported for the audited invocation only |
| **CanReadThread** | The adapter can retrieve the supported public thread projection | Valid thread/read returned ordered turns and a path | Supported for the audited invocation only |
| **CanForkThread** | The adapter can create and reconcile a durable child with acceptable fidelity | CLI help and app-server method recognition are proven; no valid fork was created | **Unknown / uncertified for recovery policy** |
| **CanExcludeTurns** | thread/resume may legally include excludeTurns | Works only when the client advertises capabilities.experimentalApi in the audited controls | Conditionally supported with that negotiated client declaration |

The snapshot should also record provider, CLI/server version, protocol/schema digest, client capabilities offered, server capabilities returned if any, compatibility-fixture identity, negotiation time, and evidence source. The Codex adapter should derive it from the initialize exchange plus versioned compatibility evidence and safe schema/method checks; it must not infer support from a version string alone. Operations must be gated by this snapshot: **Unsupported** is never invoked, and **Unknown** fails closed unless an explicitly safe compatibility path exists. A structured method/field rejection must invalidate or narrow the affected capability for the attempt.

Persist the snapshot or its immutable digest with the session lineage and each recovery attempt. That makes a later recovery decision reproducible and prevents a restart or binary change from silently reusing assumptions negotiated by another process. Planning must define which capabilities are declared by the client, which are returned by Codex, which are established by compatibility certification, and how conflicts are resolved.

The minimal closing experiments are listed in section 19.

## 8. Resume Failure Taxonomy

Fallback is safe only after the failing operation is identified as **thread/resume**, the failure is classified, and the system proves no turn/start was submitted. The current generic **AgentSessionResumeException** does not carry enough structure.

| Failure Class | Evidence / Detection | Retry Resume | Context Fallback | Fail Closed | Rationale |
| --- | --- | ---: | ---: | ---: | --- |
| Session not found / rollout absent | JSON-RPC -32600 with normalized “no rollout found”; thread/read also unavailable | No, unless storage appearance is transient | Yes, **partial** repository-context replacement if policy allows | Visible if required recovery sources absent | Genuine loss of Codex-local identity; native fork will likely fail from same source |
| Stale session identifier | Stored ID differs from verified rollout/session metadata or belongs to wrong workflow boundary | No blind retry | After repairing/selecting state with provenance; otherwise partial fallback | Yes on ambiguity | Guessing another “latest” session can attach the wrong conversation |
| Missing resume metadata | No singleton row / empty ID / unreadable store | No | Clean or repository-context session only; cannot claim recovery of unknown conversation | Yes if objective requires old content | There is no original ID to recover |
| Invalid schema/corrupt resume JSON | Store validation failure | No | Only after preserving diagnostics and using other lineage evidence | Yes by default | Current stores destructively clear/delete unusable state, losing forensic evidence |
| experimentalApi error | Exact method/field capability error reproduced here | No until protocol repaired | **No** | **Yes** | Deterministic repository programming/configuration defect; fallback would hide that every resume is broken |
| Other malformed invocation | JSON-RPC invalid request/params, unknown field/method | No until code repaired | No | Yes | Same masking risk as experimentalApi |
| Unsupported resume capability / Codex protocol version | Method-not-found, capability/version mismatch, negotiated capability gate | Retry only after selecting a certified compatible invocation/version | Consider only mechanisms marked Supported in the captured SessionCapabilities; otherwise partial fallback only by policy | Yes by default | Must not pretend a deterministic incompatible installation is a missing session or infer alternatives from version alone |
| Authentication failure | initialize/turn auth error, credential status | After credential refresh and bounded backoff | No | Yes | New session will fail or create misleading state |
| Network/transient provider failure | Transport/provider error distinct from local rollout loading | Yes, bounded with jitter | No during transient window | Yes after exhaustion | Replacement does not solve provider availability |
| Rate limiting / usage limit | Structured turn/provider limit, reset time | Resume/open as needed; retry the turn after reset | No | Yes after existing retry policy | Not a session-content problem; production currently does not compose GatedAgentRuntime |
| Invalid local config / strict config failure | Process stderr/initialize error; deterministic on fresh and resume | After config repair | No | Yes | Fallback masks operator/config defect |
| Corrupted rollout | Parse/read error tied to exact path; partial JSONL boundary known | One retry only if concurrent write suspected | Explicit partial recovery from validated records + repository context | Yes if boundary/provenance uncertain | Never replay malformed tail or silently omit roles |
| Permission/storage failure | Access denied to CODEX_HOME or workspace DB | Retry after permissions repaired | No automatic fallback | Yes | Starting a new session may also be unable to persist and can discard lineage |
| App-server process crash before resume response | Stream ended; no turn/start submitted; process exit/stderr | Yes, bounded | Only after repeated classified crash and readable source | Visible after exhaustion | Resume is read-only, but crash cause may be deterministic |
| Cancellation | Caller cancellation token | No | No | Yes, as Cancelled rather than Failed | User intent must not create replacement work |
| Timeout during resume | Explicit resume request timeout and transport stage | Bounded retry | Only if request outcome is known read-only and policy permits | Yes on unknown transport outcome | Current code has no request timeout |
| Failure after replacement/fork accepted but before acknowledgement | Recovery journal says fork request sent, no response | Query/reconcile before any retry | No second fork until reconciled | Yes while unknown | Avoid orphan/duplicate replacement sessions |
| Repository-owned programming defect | Stack/type/serialization invariant, repeatable with fixture | No until fixed | No | Yes | Broad fallback would turn defects into silent behavior drift |
| Unclassified failure | No structured class/stage | No blind retry beyond safe transport retry | No | Yes | Visible escalation preserves evidence and prevents masking |

## 9. Fallback Architecture Options

### 9.1 Ownership layer options

| Layer | Information/authority available | Strength | Weakness | Verdict |
| --- | --- | --- | --- | --- |
| CLI command / Program | Mode, repository path, top-level exit | Can show user-visible outcome | Too far from thread ID, protocol stage, prompt content, and session object | Not owner; presentation only |
| Workflow chain/controller | Workflow and transition selection | Can stop/advance workflow | Does not own Codex session or resume store | Consume recovery outcome; not implement it |
| TransitionRuntime | Run ID, prompt lifecycle, durable transition states | Best place to supply run/correlation ID and enforce unknown-outcome policy | DecisionSession builds its own actual prompt; runtime lacks Codex details/content | Collaborator and journal source |
| UnifiedPromptExecutor | Transition definition and DecisionSession lifetime | Can connect transition run context to session owner | Current IPromptExecutor signature does not expose run ID; generic executor should not parse Codex rollouts | Composition boundary |
| **DecisionSession / dedicated decision-session lifecycle coordinator** | Old state, current handoff/context, seeded/accounting state, fresh prompt logic, active session, clear/write authority | Narrowest current layer able to decide replacement semantics and content | Needs structured Codex adapter and durable recovery store | **Recommended policy owner** |
| AgentRuntime | Spec, process, eager resume outcome, registry | Correct place to guarantee failed-process teardown and expose structured resume stage/result | Does not know persisted active state, workflow, artifacts, or recovery content | Transport owner, not fallback policy owner |
| Codex adapter/session | Exact JSON-RPC method, code, response, thread/read/fork | Correct place for negotiation, classification, and protocol operations | Provider-specific and lacks workflow authority | Required protocol collaborator |
| Resume persistence store | Old pointer and write/clear | Can make state transition atomic | Must not choose fallback policy or read Codex content | Durable state collaborator |
| Retry policy | Attempts/timing | Reusable for transient classes | Cannot classify content/provenance alone | Apply only after structured classification |

**Strongly supported recommendation.** Introduce a decision-session recovery coordinator at the existing **OpenOrResumeSessionAsync** boundary. It should:

- ask a Codex adapter for a structured resume result containing request stage, JSON-RPC code, normalized category, raw redacted message, and whether a turn was submitted;
- capture the negotiated **SessionCapabilities** snapshot and gate resume/read/fork/excludeTurns operations against it;
- read the old durable state without clearing it;
- choose retry/fail/fork/recovery-envelope policy using workflow/run context;
- obtain repository and Codex-owned recovery sources;
- create/reconcile a replacement through a durable **RecoveryJournal** attempt;
- atomically persist lineage and the new active pointer;
- return an explicit **ResumedOriginal** or **ReplacementWithRecoveredContext** result to DecisionSession.

Competing layers either lack content/persistence (AgentRuntime/Codex session) or lack protocol certainty (workflow/CLI).

### 9.2 Content-loading mechanisms

| Mechanism | Fidelity / roles / tools | Cost and context risk | Compatibility / failure | Testability / observability | Assessment |
| --- | --- | --- | --- | --- | --- |
| Repair negotiation, then resume original | Exact original semantics | Lowest; Codex owns context | Proven on 0.142.5 | Straightforward subprocess contract test | Required fix for experimentalApi; not fallback |
| **Native thread/fork** | Potentially highest fidelity; new ID with native history and fork provenance | Avoids textual re-injection; Codex manages history/compaction | Surface recognition is proven; valid fidelity, negotiated support, and failure semantics need disposable validation; fails if source unreadable | Good only if forkedFromId/path are captured and unknown outcomes reconciled | **Candidate for preferred replacement; planning must compare it with reconstruction before ordering** |
| thread/read → structured recovery envelope → thread/start | Preserves ordered public user/agent/file-change items, but not full developer/tool/reasoning records | Must budget, summarize, and label; deterministic normalization possible | Proven read works on 0.142.5; partial API view | Highly testable with fixtures and content digest | Strong Codex-owned reconstruction candidate |
| Parse rollout JSONL → structured recovery envelope | Can extract more roles/tool records than thread/read | Highest security/format/size risk; versioned parser required | Useful if thread/read fails but file is readable; private format | Fixture-heavy; must tolerate partial tail and redact | Secondary, explicitly versioned salvage path |
| Repository-owned continuation package | Operational context + relevant handoffs/decisions + current state/hashes | Bounded and stable; lower token cost | Works even when Codex rollout is gone | Very testable; already close to current fresh priming | Required last-resort partial recovery |
| Full raw transcript injected as one prompt | Roles/tool semantics flattened; includes non-conversation records | Observed sample exceeds configured context; secret risk | Technically possible through turn/start text only | Easy to send, hard to make correct | Reject |
| Replay prior messages one turn at a time | Changes model outputs and tool side effects; cannot replay assistant/tool roles through current turn/start builder | Expensive and nondeterministic | Current integration supports only text user input | Dangerous and misleading | Reject |
| Direct rollout-path import | Could be exact if supported | Unknown | No CLI/app-server option found in source/help; methods use thread ID | Not available from evidence | Not viable |
| Spawn **codex fork** TUI/CLI separately | Native fork surface | Process/UI/ack integration mismatch | CLI help proves recognition | Hard to integrate with held-open app-server and permissions | If native fork is selected, evaluate app-server thread/fork as the narrower integration surface |
| Summary-only injection | Explicitly lossy | Lowest cost | Always possible after summary exists | Good if source/digest/coverage recorded | Valid only as labeled partial recovery |

**Strongly supported constraints; mechanism ordering remains a planning decision.**

1. Do not trigger replacement for experimentalApi; repair the invocation and resume.
2. Build the eligible mechanism set from the captured **SessionCapabilities** and the classified failure; method recognition or CLI version alone is insufficient.
3. Evaluate native thread cloning/forking as the preferred recovery mechanism only when negotiated support exists and disposable tests show greater conversational fidelity and safer failure/reconciliation semantics than reconstruction. If chosen, record that it is a replacement.
4. Evaluate a deterministic, bounded recovery envelope from thread/read, versioned rollout salvage, and repository-authoritative artifacts as an independent candidate, including cases where fork exists but is less safe or observable.
5. If Codex content is unavailable, use a repository-only partial continuation package and expose the loss.
6. If the outcome is ambiguous, content is untrusted/oversized, or provenance cannot be established, fail closed.

## 10. Replacement Session Semantics

### 10.1 Definitions

- **Original session:** the Codex thread ID in active state before the resume attempt.
- **Resumed original session:** the same Codex thread ID successfully loaded; no replacement lineage edge.
- **Replacement session:** a different Codex thread ID created because classified recovery policy allowed continuation.
- **Recovered context:** the versioned content package used to establish replacement context. It may be native/forked, full public transcript, selective, summarized, repository-only, or absent.
- **Active session:** the one thread ID the decision-session lifecycle will use for future turns in this workflow scope.
- **Authoritative continuation:** the lineage node whose subsequent decisions/output are accepted by workflow state after replacement creation is durably committed.

### 10.2 Required journaled state transition

~~~text
RecoveryAttempt.Pending(original)                [active = original]
  -> ResumeSucceeded                             [active = original; terminal]
  -> ProtocolRepairRequired                      [active = original; visible stop]
  -> RecoveryPreparing(source, digest)           [active = original]
       -> ReplacementCreating                    [persist before side-effecting request]
            -> ReplacementCreated(original,new)  [new durable but inactive]
                 -> RecoveryCompleted            [validated native history; atomic switch]
                 -> ContextInjectionPending      [reconstructed replacement only]
                      -> RecoveryCompleted        [context accepted; atomic switch]
  -> RecoveryFailed                              [from a known failed nonterminal state]
  -> UnknownOutcome                              [from an uncertain side-effect boundary]
~~~

**Strongly supported.** These are durable **RecoveryJournal** states, not transient control-flow labels. The old active pointer must remain intact until the replacement is validated, its lineage is durable, any required context injection is accepted, and **RecoveryCompleted** is committed. If replacement creation or first context submission fails, the system must preserve both the original ID and recovery attempt state. It must not clear the original as the current catch does.

### 10.3 Required semantics

- A native fork is still a replacement, even though Codex preserved history.
- A text-injected replacement must begin with an explicit provenance header stating that resume failed, the new thread is not the original, and the following material is recovered context.
- The replacement becomes active only when **RecoveryCompleted** commits. For a native clone/fork this requires validated effective history and durable lineage; for reconstruction it requires accepted recovery context. **ReplacementCreated** alone never changes the active pointer.
- The original remains addressable in lineage and may become resumable later. It must not be deleted or silently superseded.
- Downstream decision outputs belong to the replacement lineage after activation. Prior outputs remain attributed to the original.
- A clean session without recovered context is a separate outcome and must not satisfy the stated fallback objective unless policy explicitly permits degraded continuation.

## 11. Duplicate-Execution and Partial-Outcome Risks

### 11.1 What current evidence can distinguish

| Failure point | Current evidence | Duplicate risk | Required safeguard |
| --- | --- | --- | --- |
| Before app-server process launch | Exception from launcher; no session registered | None | Fail/retry by class; do not alter active state |
| During initialize | Pending response/stream failure; no thread request necessarily sent | No Codex turn work; deterministic config/auth may recur | Record request stage and raw structured error |
| During thread/resume before response | Eager open; no DecisionSession turn can run concurrently | Resume is a read/load operation; no repository mutation from this attempt | Bounded retry is safe only while no turn/start is sent |
| Reproduced experimentalApi rejection | Exact JSON-RPC response before lookup | None from resume attempt | Fail closed and repair protocol; preserve old ID |
| After resume success, before next turn/start | **OpenSessionAsync** returned; session thread known | No new model work | Persist/emit ResumedOriginal before prompt |
| After turn/start write but before ack | **AgentTurnProgress** can distinguish write started/submitted/accepted for turns, but not durably | Unknown model/tool outcome | Never broaden session-resume fallback to turn failures; reconcile turn/rollout/repository state |
| After partial output/tool calls | In-memory notifications, possibly rollout and working-tree changes | Duplicate commands/mutations if rerun | Inspect rollout terminal event, transition evidence, working tree, and product gates |
| After completed decision turn but before resume-state write | Turn result exists only in process; no new active pointer | Duplicate proposal on restart | Durable turn acceptance/output record before advancing active state |
| After resume-state write but before decision artifact write | **Current order exists: state at line 112, decisions at line 116** | On restart, old thread resumes but workflow may still lack DecisionSet; another proposal can be generated | Make output/artifact/active-pointer commit coordinated or journal the gap |
| After decision artifact write but before TransitionRuntime raw-output record | Repository product may exist; transition run remains Started | Workflow observation may avoid rerun, but run history is incomplete | Reconcile product/content hash to started transition and close it idempotently |
| During thread/fork after server accepted but before caller received ID | No current support/journal | Duplicate/orphan forks if request is retried | Persist RecoveryAttempt before request; reconcile by parent ID/time/digest; do not blindly retry |
| During replacement recovery turn | New thread exists; context acceptance may be unknown | Multiple replacement nodes or duplicated proposal | Separate “replacement created” from “context accepted”; persist both |
| After repository mutation | Decision sessions are read-only, but a generic fallback layer might be reused by danger-full-access execution sessions | Duplicate/conflicting edits and commands | Scope fallback explicitly to decision-session resume; require stronger mutation reconciliation before any expansion |

**Proven — the current experimentalApi attempt is safe from duplicate work.** Resume verification is eager and happens before DecisionSession constructs or submits its proposal. The app-server rejected request ID 2, and no turn/start frame was sent.

**Proven — existing reusable safeguards.**

- **TransitionRuntime** persists Started before prompt execution and later records raw output, interpreted/validated states, effects, and completion.
- **canonical_transition_evidence** can contain full **PromptExecutionResult.RawOutput** after the executor returns.
- **LoopArtifacts** preserves numbered/SQLite decision and handoff history and uses live artifacts to decide whether decision/execution work is pending.
- Repository observation, product validation, working-tree change detection, commit gates, and milestone gates can detect effects outside the conversation.
- App-server turn progress distinguishes request write/submission/acceptance in memory.

**Proven limitations.**

- Transition run IDs, LoopRelay SessionIdentity, Codex ThreadId, Codex turn ID, decision history sequence, and recovery attempt are not durably joined.
- A new random transition run ID is generated on every **TransitionRuntime.RunAsync**; no idempotency key is supplied to Codex.
- Resume and fork request stages have no progress observer, timeout, acceptance journal, or reconciliation API in LoopRelay.
- **DecisionSessionResumeState** is written before decision artifacts and fail-open persistence can silently fail.
- Production unified composition does not use the legacy LoopRunner's explicit “pending decisions.md” sequencing or epic-completion resume clear as its runtime authority, though analogous canonical product gates may prevent some duplicates.

**Strongly supported policy boundary.** Automatic replacement is appropriate only for a classified resume-handshake failure with proven pre-turn status. Failures during or after turn submission must enter an unknown-outcome reconciliation path, not the resume fallback path.

## 12. Content Normalization and Context Boundaries

### 12.1 What may be transferred directly

- **Native fork, if selected after capability and fidelity validation:** no textual normalization; record old/new IDs, Codex-reported fork parent, rollout path/digest, negotiated capability snapshot, and compatibility evidence.
- **Repository-authoritative current context:** operational_context.md, the handoff selected for the pending decision, the latest accepted decision product when relevant, plan/details hashes, workflow/stage/transition identity, and repository revision/status.
- **Public conversational projection from thread/read:** ordered userMessage and agentMessage text, with fileChange items converted to bounded summaries. Preserve original order and turn boundaries.

### 12.2 What must be transformed

- Convert provider item variants into a versioned canonical record with sequence, original role/item type, source, timestamp if available, text, truncation status, and digest.
- De-duplicate rollout **event_msg.agent_message** from corresponding **response_item.message** records.
- Convert tool activity to summaries: tool name/category, command or target path if policy allows, terminal status, output digest, and a small redacted excerpt only when essential.
- Normalize absolute paths to repository-relative paths where possible and label external paths instead of replaying machine-specific locations.
- Detect and discard partial final JSONL records while preserving a “partial tail” fact and last valid sequence.
- Distinguish historical prompt instructions from current governing policy. Do not replay old developer/system instructions as current instructions; include their digest/version as provenance where necessary.
- Insert a replacement-session preamble that names recovery level: native, full public transcript, selective, summarized, or repository-only.

### 12.3 What must be excluded by default

- Encrypted reasoning and hidden/provider-only reasoning records.
- Authentication tokens, credentials, secrets, environment-variable dumps, auth files, and secret-bearing tool results.
- Raw binary/base64/image payloads and oversized file content.
- Full repository snapshots, repeated command output, dependency listings, and generated logs unless specifically essential.
- Tool call IDs or result references that the new thread cannot resolve.
- Approval request/response IDs tied to the old process.
- Provider-specific metadata that has no defined canonical meaning.
- Malformed records and unverified content after a corrupt boundary.
- Stale or conflicting developer/system instructions.

### 12.4 Size and budget evidence

**Proven.**

- Current **DecisionSessionRouterOptions.ModelContextWindowTokens** defaults to 256,000 and the hard transfer guard to 90 percent. The runtime does not expose the deployed model context window.
- LoopRelay's deterministic fallback estimator is **ceil(characters / 4)**.
- One observed LoopRelay rollout was about 1.20 MB, roughly 299,000 tokens under that estimator, before adding the replacement prompt or reserving output.
- That rollout contained 728,566 characters of tool output, most of which thread/read correctly did not return as public turn content.
- Current persisted **OccupancyTokens** is the most recent proposal's prompt + output, not a measured total history size, so it cannot budget a recovery package.

### 12.5 Safest canonical boundary

**Strongly supported recommendation.** The canonical non-fork recovery envelope should contain:

1. lineage/provenance header and failure classification;
2. exact workflow/repository boundary and content hashes;
3. current repository-authoritative operational context;
4. the ordered, bounded public conversation projection needed after that context boundary;
5. accepted decision/handoff artifacts not already represented;
6. concise tool/file-effect summaries needed to understand current repository state;
7. explicit omissions, corruption/truncation flags, token estimate, and source digest.

The envelope must reserve model output capacity and must choose among full public projection, selective projection, canonical summary, or refusal using: supported context window, estimated input size, source completeness, corruption state, secret scan result, artifact overlap, and duplicate-outcome status. There is no evidence supporting unconditional full replay.

## 13. Persistence and Schema Implications

### 13.1 Required durable state

| Datum | Why required | Current owner / recommended location |
| --- | --- | --- |
| Original failed thread ID | Preserve recoverable identity and audit trail | Existing decision resume domain; never clear before replacement commit |
| Replacement thread ID | Future active identity | Active decision-session pointer after activation |
| Lineage kind and parent | Distinguish resume, native fork, reconstructed replacement, clean replacement | New recovery/lineage record in workspace SQLite |
| Repository/workflow/stage/transition/run identity | Prevent cross-epic/session attachment and join orchestration evidence | TransitionRuntime supplies; recovery coordinator persists |
| Recovery attempt ID and status | Restart/idempotency/reconciliation | New recovery journal/table, not an overwrite-only JSON blob |
| Resume timestamps and attempt count | Retry policy and forensics | Recovery attempt |
| Structured failure stage/class/code | Safe classification | Codex adapter result persisted by recovery coordinator |
| Negotiated SessionCapabilities snapshot/digest | Prove that each operation/field was eligible and detect binary/protocol changes across restart | Session open result + RecoveryJournal attempt |
| Redacted raw error/digest | Diagnose unknown/version behavior | Recovery attempt diagnostics |
| Recovery source kind/location | Reproduce what was loaded | Recovery attempt; path may be external Codex rollout |
| Source content digest/version/boundaries | Prove exact package and detect changes | Recovery content descriptor |
| Completeness: native/full/partial/summary/repository-only | User-visible truth | Recovery content descriptor and transition metadata |
| Normalizer/schema version | Deterministic re-read and migration | Recovery content descriptor |
| Replacement creation and context-acceptance states | Safe active-pointer commit | Recovery attempt state machine |
| Duplicate/unknown-outcome classification | Prevent second fork/turn | Recovery attempt + transition evidence |
| Original later resumability | Do not destroy history | Lineage status |
| Authoritative continuation flag/time | Attribute downstream output | Active pointer/lineage transaction |

**Strongly supported schema direction.** Keep the singleton active pointer small, but add an append/upsert recovery-lineage domain and first-class **RecoveryJournal** rather than expanding **DecisionSessionResumeState** into an overwrite-only incident log. The active pointer should carry scope (at minimum repository stable identity, workflow/epic boundary, and active thread ID) and reference the current lineage node. A recovery attempt needs states that survive restart. Switching the pointer, completing the journal attempt, and marking replacement lineage authoritative should be one SQLite transaction.

**Strongly supported storage boundary.** Do not copy raw rollouts or large transcript text into the workspace database by default. Persist source path/API, hashes, public-normalized package metadata, completeness, and optionally a bounded sanitized envelope. Codex remains owner of its rollout. Repository SQLite remains owner of orchestration state and lineage.

### 13.2 First-class Recovery Journal

**Strongly supported architectural requirement.** Recovery must be modeled as a durable, provider-neutral state machine rather than re-created from procedural logs after a crash. A **RecoveryJournal** owns append/create, compare-and-swap transition, lookup by idempotency identity, and reconciliation of **RecoveryAttempt** records. The decision-session recovery coordinator owns policy and drives those transitions; provider adapters supply structured operation outcomes but do not decide them. This makes recovery restart-safe, idempotent, deterministically reconcilable, observable in telemetry, auditable, and extensible to providers whose external session identity is not a Codex thread ID.

The minimum durable record is:

| Field | Purpose |
| --- | --- |
| **Id** and **JournalSchemaVersion** | Stable attempt identity and migration boundary |
| **Provider** | Keep policy/journal semantics provider-neutral; Codex is one adapter |
| **Repository/Workflow/Epic/Stage/Transition/Run scope** | Prevent cross-scope attachment and join orchestration evidence |
| **OriginalSessionReference** | Provider-owned source identity; maps to Codex OriginalThreadId |
| **ReplacementSessionReference?** | Provider-owned child identity once known; maps to Codex ReplacementThreadId |
| **Status** and **RowVersion** | Durable state-machine position and compare-and-swap protection |
| **FailureClassification** | Structured reason recovery was entered or prohibited |
| **RecoveryMechanism** | Resume, native clone/fork, public projection, rollout salvage, repository-only, or clean replacement |
| **SourceDigest**, boundaries, completeness, normalizer version | Reproduce the selected context without storing unsafe raw content |
| **SessionCapabilitiesSnapshot/Digest** | Prove which operations and fields were negotiated/eligible for this attempt |
| **IdempotencyKey**, provider request/correlation IDs, attempt counters | Reconcile restarts and prevent duplicate replacement requests |
| **Created/Updated/Completed timestamps** and redacted diagnostics | Ordering, telemetry, audit, and operator diagnosis |

Required statuses and restart meanings are:

| Status | Durable invariant / restart action |
| --- | --- |
| **Pending** | Written before the resume operation; original pointer remains active. Reconcile any request whose send/response boundary is uncertain. |
| **ProtocolRepairRequired** | A deterministic protocol/capability defect was identified. No replacement is allowed; terminal for this attempt. A later repaired run creates a new linked attempt. |
| **ResumeSucceeded** | The original provider session was confirmed and remains active; terminal success for this attempt. |
| **RecoveryPreparing** | Automatic recovery was classified as safe; source selection and digesting are in progress; no replacement request may yet be sent. |
| **ReplacementCreating** | Persisted before the side-effecting create/fork request. On restart, reconcile before sending any second request. |
| **ReplacementCreated** | Replacement identity and parent lineage are durable, but the replacement is not yet authoritative. |
| **ContextInjectionPending** | A reconstructed replacement requires a context turn; persist before submission and reconcile acceptance/turn outcome before retry. Native clone/fork can bypass this state only after its effective history is validated. |
| **RecoveryCompleted** | Required context is accepted or native history is verified, and the active-pointer/lineage switch commits atomically; terminal success. |
| **RecoveryFailed** | Outcome is known to have failed; original pointer and evidence remain intact; terminal unless policy explicitly starts a new attempt. |
| **UnknownOutcome** | A provider operation may have succeeded. Stop automatic continuation and reconcile provider/session/rollout/transition evidence; never blindly create or inject again. |

Transitions must be monotonic and compare-and-swap guarded. A retry that is proven side-effect-free remains within the same attempt and increments a sub-attempt counter; a new replacement must not be represented as a retry. **UnknownOutcome** may advance only after reconciliation proves a specific later state or a known failure. Recovery startup must load nonterminal attempts before reading the active pointer and must resume/reconcile the journaled attempt rather than start a second one.

The journal solves the current early-clear defect by separating three facts: the original pointer, a nonterminal recovery attempt, and an authoritative replacement. The original is not deleted merely because recovery began. **ReplacementThreadId** can be persisted as soon as discovered without making it active, and a crash between creation, context acceptance, artifact persistence, and active switching remains deterministic and auditable.

### 13.3 Optional diagnostics

- Codex CLI version, app-server userAgent, negotiated **SessionCapabilities**, initialize capability set, method/field compatibility result.
- Old/new rollout paths, sizes, valid-record counts, last valid timestamp.
- Token estimate and selected budget policy.
- Redaction counts by category without secret values.
- thread/read item counts and omitted provider-item counts.
- Reconciliation observations and duration.

### 13.4 Current schema limitations

**Proven.**

- **decision_session_resume** stores opaque document_json and saved_at at id 1 only.
- **session_telemetry_events** has LoopRelay session_id but no Codex thread_id or workflow/run ID.
- **canonical_transition_runs/evidence** has run/workflow/stage/transition but no session/thread/turn/recovery ID.
- **loop_history** has artifact kind/sequence/content/hash but no producing thread/run.
- No foreign keys or transactions link these domains.

## 14. Mode and Workflow Integration

| Invocation | Current route | Does decision resume apply? | Desired fallback implication |
| --- | --- | --- | --- |
| Default, no flag | Select EvalRoadmap when eval intent exists, otherwise TraditionalRoadmap; chain to Plan then Execute | Yes, when chain reaches Execute decision transitions | Same classified policy in Execute; upstream one-shots remain unaffected |
| **--eval** | ForcedEvalChain → Plan → Execute | Yes, when chain reaches Execute | Same as default; provenance should record ForcedEvalChain |
| **--traditional** | ForcedTraditionalChain → Plan → Execute | Yes, when chain reaches Execute | Same as default; provenance should record ForcedTraditionalChain |
| Bounded **eval** command | EvalRoadmap only | No decision session | No resume fallback |
| Bounded **traditional** command | TraditionalRoadmap only | No decision session | No resume fallback |
| Bounded **plan** command | Plan only | No cross-process decision session | Warm plan thread is process-local only |
| Bounded **execute** command | Execute only | Yes | Same policy, with explicit bounded mode provenance |

**Proven.** **CliArguments**, lines 232-275, distinguishes flags (forced chains) from bounded workflow commands. **WorkflowChainRunner**, lines 351-420, advances completed unbounded workflows to downstream definitions and stops bounded invocations. All Execute decision transitions use the same **DecisionSession** instance; there is no mode-specific resume implementation.

**Strongly supported policy.** Automatic fallback eligibility should not differ among default, --eval, and --traditional once they reach the same Execute decision-session lifecycle. What must differ is recorded origin/provenance. A bounded roadmap or plan invocation has no active decision thread and must not perform this fallback.

**Proven — auto-chaining and output ownership.** TransitionRuntime persists each transition before/after prompt execution and workflow chaining re-observes the repository after each completed unbounded transition. Replacement output must therefore be attributed to the current Execute transition run and lineage node before product interpretation/effects. A replacement should not independently advance the workflow.

**Strongly supported — restart recovery.** On restart, recovery must first reconcile any in-progress recovery attempt and Started/PromptCompleted transition evidence, then decide whether to resume original, activate an already-created replacement, or fail. It must not reread the singleton and start a second replacement blindly.

## 15. Observability and User-Facing Semantics

### 15.1 Current behavior

**Proven.**

- Successful resume writes **Resumed decision session (thread ...)** through **ILoopConsole.Info**.
- Failed resume writes **Could not resume ... Starting fresh** through **ILoopConsole.Warn**.
- Production **UnifiedPromptExecutor** constructs its console with **TextWriter.Null** and returns no recovery metadata from **ExecuteDecisionSessionAsync**. Therefore both messages are suppressed in the production unified CLI.
- If fresh fallback succeeds, the transition can report Completed with metadata only for execute-decision-session and prompt evidence. It does not say a replacement was used.
- JSON-RPC numeric error code, method, request ID, CLI version, old/new lineage, and recovery source are not persisted.
- Session telemetry code contains no resume/fallback event type and is not composed in production.

### 15.2 Required observable distinctions

| Scenario | CLI/log wording | Persisted state / telemetry |
| --- | --- | --- |
| Normal resume success | “Resumed original decision session <id>.” | RecoveryJournal **ResumeSucceeded**; active ID unchanged |
| Transient retry | “Resume attempt N failed transiently; retrying at/after ...” | **Pending** sub-attempt count, class, next retry |
| experimentalApi / deterministic defect | “Resume rejected because LoopRelay/Codex protocol negotiation is incompatible; no replacement started.” | **ProtocolRepairRequired**, method/field/code/version/capability snapshot |
| Classified native fork | “Created replacement session <new> by forking recoverable history from <old>.” | Mechanism NativeFork; **ReplacementCreated** → **RecoveryCompleted** only after validation and active switch |
| Structured content recovery | “Created replacement <new> with full/partial/summarized recovered context from <old>.” | Completeness, boundaries, omissions, digest |
| Repository-only recovery | “Created replacement with repository-owned context; original conversation was unavailable.” | Partial/RepositoryOnly |
| Missing transcript | Visible degraded/fail-closed decision | Source unavailable and policy result |
| Replacement creation failure | “Original resume failed; replacement creation also failed; original remains active in recovery state.” | Old pointer retained; **RecoveryFailed** |
| Unclassified failure | “Resume failure was not classified; no fallback performed.” | Raw redacted error and diagnostics |
| Duplicate/unknown outcome | “Outcome uncertain; automatic continuation stopped for reconciliation.” | **UnknownOutcome**, observations, no second request |
| Eventual continuation | “Workflow continued on replacement lineage <id>.” | Downstream outputs linked to replacement |

**Required terminology.** Logs/state/UI must use separate values such as **ResumedOriginal**, **ReplacementNativeFork**, **ReplacementRecoveredFull**, **ReplacementRecoveredPartial**, **ReplacementClean**, and **FailedClosed**. “Resumed” must never describe a different thread ID.

**Security requirement.** CLI and telemetry should report source types, sizes, counts, digests, and redaction results—not recovered transcript bodies or secrets. Detailed diagnostics should remain local and access-controlled.

## 16. Test-Seam Inventory

| Required behavior | Test level | Existing seam | Missing coverage / assertion |
| --- | --- | --- | --- |
| Resume succeeds | Unit/protocol | **AgentRuntimeResumeTests.OpenSessionWithResumeIdRunsTheHandshakeEagerly**; **CodexAppServerSessionTests.ResumeSpec...** | Add real 0.142.5+ subprocess compatibility test with negotiated capabilities |
| experimentalApi classification | Protocol/subprocess | **CodexAppServerProtocolTests** frame builders | Current scripted server does not enforce capability; assert exact -32600 mapping and fail-closed policy |
| Negotiated SessionCapabilities | Unit/subprocess/compatibility | Initialize builder and live control matrix | Tri-state Resume/Read/Fork/ExcludeTurns snapshot, evidence provenance, version/schema digest, operation gates, and downgrade on structured rejection |
| Session unavailable | Unit/integration | Scripted **RejectResume** returns “no rollout found” | Structured class rather than text-only exception; policy result |
| Fallback allowed/prohibited | Unit | **DecisionSessionTests.Run_FirstEntry_ResumeFails...** | Split recoverable unavailable from deterministic/auth/config/cancel/unknown; assert no clear/fresh on prohibited |
| Replacement receives content | Unit/integration | FakeAgentRuntime captures prompts/specs | Add recovery package builder and assert exact content/digest/completeness |
| Ordering preserved | Unit/fixture | No transcript normalizer | Mixed user/agent/file/tool fixture with stable sequence |
| Oversized content boundary | Unit | Deterministic token estimator/router options | Full/selective/summary/refusal thresholds and output reserve |
| Missing transcript | Unit/integration | File system and null store fakes | Repository-only partial vs fail-closed policy |
| Partially corrupt rollout | Parser fixture | Rollout locator skips malformed first line only | Valid prefix, malformed middle/tail, duplicate events, unsupported item |
| Native thread/fork | Compatibility/integration | No LoopRelay adapter; CLI/app-server support proven externally | Disposable CODEX_HOME fork, forkedFromId, new ID/path, no source mutation |
| Replacement creation failure | Unit/subprocess | Scripted process can reject/kill | Preserve old pointer and failed recovery attempt |
| Persisted lineage | Persistence | SQLite resume-store tests | New tables/transaction, original/new IDs, scope, digest, completeness |
| RecoveryJournal state machine | Persistence/crash-injection | TransitionRuntime has a separate journal but no recovery attempt | Every allowed/forbidden transition, compare-and-swap conflict, restart lookup, terminal states, and atomic completion/active switch |
| Active identity update | Persistence/integration | Resume store round-trip | Switch only after replacement commit/context acceptance |
| Restart during recovery | Persistence/E2E | Canonical stores and process-restart tests elsewhere | Reconcile Prepared/Creating/Created/Accepted states without second fork |
| Repeated execution idempotency | Unit/E2E | TransitionRuntime records, loop artifact histories | Stable recovery attempt/idempotency key and transition/session joins |
| No duplicate completed work | E2E | Repository products, raw output evidence, change detector | Crash injection at each boundary in section 11 |
| Default / --eval / --traditional | CLI/workflow E2E | **CliArgumentsTests**, **WorkflowChainRunnerTests** | Reach Execute and assert identical policy plus different invocation provenance |
| Logs do not claim resume | Unit/CLI snapshot | RecordingLoopConsole | Production unified output; explicit replacement wording |
| Sensitive content not exposed | Security/unit | No sanitizer seam | Secret fixtures, env/path/tool output redaction, telemetry/CLI assertions |
| Resume-store corruption | Persistence | File/SQLite resume-store tests | Preserve forensic record instead of current clear/delete when recovery feature is enabled |
| Version compatibility | Subprocess/compatibility | LiveCertification tests are empty markers, not live probes | Declared supported CLI matrix and schema/capability contract |

**Proven existing strengths.**

- **CodexAppServerProtocolTests**, lines 87-110, pin the current resume frame.
- **CodexAppServerSessionTests**, lines 509-570, pin resume routing, posture, typed rejection, thread-ID timing, and lazy fresh handshake.
- **AgentRuntimeResumeTests**, lines 39-89, pin eager verification, teardown, registry cleanup, and lazy fresh open.
- **DecisionSessionTests**, lines 906-1110, pin warm resume, stale projection clearing, current fresh fallback, replacement persistence, transfer clear/reopen, failure clear, disposal retention, and kill switch.
- **SqliteDecisionSessionResumeStoreTests**, lines 45-139, pin round trip, legacy import, invalid state, clear, and schema preparation.
- **FileSystemCodexRolloutLocatorTests**, lines 27-78, pin cwd/time lookup and malformed-first-line behavior.
- **UnifiedCliCompositionTests.Production_cli_composition_does_not_construct_legacy_loop_runner**, lines 1153-1171, establishes which execution authority is current.

**Proven missing seam.** No repository interface can read/fork/export a Codex thread, no structured resume failure or negotiated SessionCapabilities model exists, no recovery package model/normalizer exists, and no provider-neutral RecoveryJournal/session-lineage persistence exists. These must be explicit testable abstractions rather than file parsing or procedural recovery embedded in DecisionSession.

## 17. Findings

### Finding RR-01: Resume always sends an unnegotiated experimental field

- **Severity:** Critical
- **Category:** Compatibility
- **Evidence:** **src/LoopRelay.Agents/Services/Codex/CodexAppServerProtocol.cs**, Initialize lines 23-32 and ThreadResume lines 59-67; commit **97a1c572d**; live Codex 0.142.5 probe returned -32600 “thread/resume.excludeTurns requires experimentalApi capability.”
- **Current behavior:** Every persisted-thread resume reaches the same deterministic rejection before thread lookup, then DecisionSession starts fresh.
- **Why it matters:** No current resume can succeed on the audited binary; fallback hides a repository defect and discards continuity.
- **Planning implication:** Treat protocol repair, explicit SessionCapabilities negotiation, and compatibility certification as prerequisite work. Do not classify this error as content fallback.
- **Confidence:** Proven

### Finding RR-02: Current fallback clears the only old identity before replacement creation

- **Severity:** Critical
- **Category:** Persistence
- **Evidence:** **DecisionSession.OpenOrResumeSessionAsync**, lines 214-219, clears then calls fresh open; fresh open is lazy per **AgentRuntime**, lines 32-59, and replacement ID persists only after successful proposal at **DecisionSession**, lines 222-237.
- **Current behavior:** Failure between clear and post-proposal persistence leaves neither old active ID nor replacement ID in repository state.
- **Why it matters:** The exact content source needed for recovery can become undiscoverable from LoopRelay-owned state.
- **Planning implication:** Preserve the old pointer and introduce the provider-neutral RecoveryJournal state machine in section 13.2 plus an atomic RecoveryCompleted/active switch.
- **Confidence:** Proven

### Finding RR-03: Fallback carries repository context, not the failed session's content

- **Severity:** High
- **Category:** Recovery
- **Evidence:** **DecisionSession.BuildProposalPromptAsync**, lines 130-159; current fallback test lines 959-980 asserts operational context only; resume state contains no content/path; no thread read/fork abstraction exists.
- **Current behavior:** A fresh thread receives current operational context and handoff-derived prompt. Prior user/assistant messages, tool context, and exact decision history are not loaded as conversation.
- **Why it matters:** The requested behavior is not implemented, despite the current warning implying a graceful fallback.
- **Planning implication:** Add Codex-owned recovery and an explicit bounded repository-context continuation package with completeness semantics.
- **Confidence:** Proven

### Finding RR-04: Resume failure classification is text-only and overbroad

- **Severity:** High
- **Category:** Correctness
- **Evidence:** **CodexAppServerMessage** stores only ErrorMessage; **AgentRuntime**, lines 42-55, wraps any non-cancellation eager-handshake exception as **AgentSessionResumeException**; DecisionSession catches all instances.
- **Current behavior:** Unknown thread, invalid request, initialize/config/auth failure, stream death, and programming errors can all enter “Starting fresh.”
- **Why it matters:** Automatic fallback can mask deterministic defects, repeat failure on a fresh thread, and erase evidence.
- **Planning implication:** Preserve method, stage, JSON-RPC code/data, process exit/stderr, retryability, and pre-turn proof in a structured result.
- **Confidence:** Proven

### Finding RR-05: “Active session” scope is weaker than its conceptual per-epic contract

- **Severity:** High
- **Category:** Architecture
- **Evidence:** **IDecisionSessionResumeStore** says per repo/per epic; **DecisionSessionResumeState** has no scope identity; SQLite table is singleton; production composition passes null projection service; production does not construct LoopRunner, the only epic-completion clear owner.
- **Current behavior:** One thread ID is implicitly active per repository database and may survive into later workflow/epic lifecycles.
- **Why it matters:** A successful future resume can attach valid but wrong historical context.
- **Planning implication:** Persist and validate stable repository + workflow/epic/stage scope before any resume or fallback.
- **Confidence:** Strongly supported
- **Open question:** Which stable epic/product identity should be canonical in the unified workflow model?

### Finding RR-06: Durable domains cannot correlate session, turn, transition, and artifacts

- **Severity:** High
- **Category:** Persistence
- **Evidence:** resume row has ThreadId only; telemetry has LoopRelay SessionId but no ThreadId/run; transition stores have run but no session/thread/turn; loop_history has sequence but no producer lineage.
- **Current behavior:** Incident reconstruction relies on timestamps/cwd and cannot prove which prompt/output/artifact belongs to the failed thread.
- **Why it matters:** Correct recovery, idempotency, downstream attribution, and auditability require joins.
- **Planning implication:** Define lineage/correlation identifiers and persist them at open, turn, output, artifact, and transition boundaries.
- **Confidence:** Proven

### Finding RR-07: Production suppresses resume/fallback messaging and does not compose session telemetry

- **Severity:** High
- **Category:** Observability
- **Evidence:** **UnifiedPromptExecutor** console is **ConsoleLoopConsole(TextWriter.Null, TextWriter.Null)** at lines 816-820; **ExecuteDecisionSessionAsync** returns no resume metadata; repository search finds no production construction of **GatedAgentRuntime** or **SessionTelemetryComposition**.
- **Current behavior:** A failed resume followed by successful fresh fallback can appear as an ordinary completed transition.
- **Why it matters:** Operators cannot distinguish resumed continuity from lost continuity or detect a systemic compatibility defect.
- **Planning implication:** Surface explicit recovery outcomes through prompt result metadata, CLI output, transition evidence, and a composed telemetry/event sink.
- **Confidence:** Proven

### Finding RR-08: Codex-local content is recoverable but not safely replayable without boundaries

- **Severity:** High
- **Category:** Security
- **Evidence:** observed LoopRelay rollout includes developer/user/assistant messages, encrypted reasoning, 112 tool calls/results, 728,566 tool-output characters, and machine paths; no redaction/normalization code exists.
- **Current behavior:** LoopRelay neither uses nor governs this content.
- **Why it matters:** Naive replay can overflow context, expose secrets, duplicate tool references, and apply stale instructions.
- **Planning implication:** Certify and compare native fork against reconstruction; if planning selects textual recovery, implement a versioned public transcript normalizer, redaction, token budget, omission manifest, and refusal policy.
- **Confidence:** Proven

### Finding RR-09: Native Codex read/fork surfaces are recovery candidates but uncertified

- **Severity:** Medium
- **Category:** Compatibility
- **Evidence:** **codex fork --help** exposes session fork; app-server thread/fork with invalid ID returns normal rollout lookup rather than method-not-found; thread/read returned a valid historical thread and path without source mutation.
- **Current behavior:** LoopRelay implements only start/resume/turn, not read/fork.
- **Why it matters:** A native clone may preserve more provider-owned semantics than reconstruction, but current evidence does not establish its comparative fidelity, availability, or failure safety.
- **Planning implication:** Add a Codex session-content capability adapter and certify native fork/read on disposable fixtures before choosing fallback order.
- **Confidence:** Strongly supported
- **Open question:** Does a valid thread/fork preserve all history/compaction and expose stable forkedFromId across the supported CLI range?

### Finding RR-10: Existing transition evidence helps but does not eliminate unknown outcomes

- **Severity:** High
- **Category:** Correctness
- **Evidence:** **TransitionRuntime** journals Started before execution and raw output afterward; DecisionSession writes resume state before decisions; new run IDs are random; fork/resume attempts are not journaled.
- **Current behavior:** Crash windows can leave completed Codex work without raw output/artifacts, or active identity without decision product.
- **Why it matters:** Automatic replacement or rerun can duplicate proposals, forks, commands, mutations, or workflow advancement.
- **Planning implication:** Journal recovery request stages and define reconciliation at every boundary in section 11; scope automatic fallback to proven pre-turn failures.
- **Confidence:** Proven

### Finding RR-11: Rollout telemetry cannot currently locate a failed resumed thread reliably

- **Severity:** Medium
- **Category:** Observability
- **Evidence:** **FileSystemCodexRolloutLocator** matches only cwd and rollout start at/after current session open; a resumed rollout starts before the new process. Telemetry lacks Codex ThreadId and is not composed.
- **Current behavior:** Even if telemetry were enabled, a resumed old rollout commonly resolves null or an unrelated newer same-cwd rollout.
- **Why it matters:** The system has the exact ThreadId but uses a heuristic that cannot recover its old content.
- **Planning implication:** Resolve Codex content by ThreadId through thread/read/path or exact filename/session metadata, not cwd/time guessing.
- **Confidence:** Proven

### Finding RR-12: Current tests certify serialization and scripted behavior, not live capability negotiation

- **Severity:** Medium
- **Category:** Testing
- **Evidence:** protocol test requires excludeTurns; scripted app-server accepts it without initialize capability; “LiveCertification” tests in **CodexAppServerSessionTests**, lines 308-344, contain only marker assertions and do not launch Codex.
- **Current behavior:** All focused unit tests can pass while every real resume fails.
- **Why it matters:** The regression was introduced with tests and an inaccurate live-verification comment.
- **Planning implication:** Add a hermetic subprocess compatibility suite for initialize/resume/read/fork and declared supported Codex versions.
- **Confidence:** Proven

### Finding RR-13: Session capabilities are hard-coded assumptions rather than a negotiated contract

- **Severity:** High
- **Category:** Compatibility
- **Evidence:** **CodexAppServerProtocol.Initialize** sends fixed clientInfo with no capability model; thread/resume always emits excludeTurns; LoopRelay parses no durable capability snapshot; live controls show the client declaration changes request legality; valid fork semantics remain untested despite method recognition.
- **Current behavior:** Resume/read/fork/excludeTurns eligibility is inferred from implemented code paths, a CLI version, or ad hoc probes rather than captured for the initialized session.
- **Why it matters:** A binary change can make a field or method unsupported while the recovery policy still assumes it is safe, and a restart cannot reproduce the evidence behind an earlier decision.
- **Planning implication:** Add a provider-neutral, tri-state **SessionCapabilities** snapshot derived from initialization plus versioned compatibility evidence; persist its digest and gate every protocol operation. Do not infer support from version alone.
- **Confidence:** Proven for the missing model; strong requirement for the proposed boundary

### Finding RR-14: Recovery has no first-class durable journal

- **Severity:** Critical
- **Category:** Persistence
- **Evidence:** The singleton resume row stores only active thread/accounting state; resume and replacement attempts have no durable identity/status; the current catch clears before creation; TransitionRuntime's separate journal has no session/thread/recovery join; section 11 identifies unknown outcomes at fork, context injection, proposal, and artifact boundaries.
- **Current behavior:** Recovery exists only as synchronous catch logic. A restart reconstructs neither what operation was attempted nor whether a replacement was created or context was accepted.
- **Why it matters:** Recovery is not restart-safe or idempotent, early pointer clearing loses provenance, and blind retries can create duplicate/orphan sessions or repeated work.
- **Planning implication:** Implement the provider-neutral **RecoveryJournal** and **RecoveryAttempt** state machine in section 13.2, including stable idempotency identity, compare-and-swap transitions, reconciliation, capability/source digests, and atomic RecoveryCompleted/active switching.
- **Confidence:** Proven

## 18. Required Planning Inputs

The later **plan.md** must cover these decisions and workstreams without assuming them:

- [ ] Declare the supported Codex CLI/app-server version range and define a provider-neutral, tri-state **SessionCapabilities** negotiation/evidence strategy; do not infer method/field support from version alone.
- [ ] Repair the experimentalApi/excludeTurns contract and add live compatibility certification.
- [ ] Define structured resume failure stages, classes, retryability, and fail-closed defaults.
- [ ] Confirm the fallback policy matrix in section 8, especially that deterministic defects never fall back.
- [ ] Define the stable active-session scope: repository, workflow, epic/product boundary, role, and mode.
- [ ] Choose the decision-session recovery coordinator API, provider-neutral RecoveryJournal boundary, and Codex resume/read/fork capability adapter boundary.
- [ ] Evaluate native fork eligibility and relative ordering only after negotiated support, fidelity, security, observability, and unknown-outcome semantics are certified; do not assume fork-first.
- [ ] Define the canonical recovery envelope, source precedence, ordering, completeness levels, and replacement preamble.
- [ ] Define token budgeting, output reserve, truncation/summary/refusal rules, and model-context source.
- [ ] Define secret detection/redaction, excluded item types, path normalization, and diagnostic exposure policy.
- [ ] Define the first-class **RecoveryJournal** schema and allowed transitions for Pending, ProtocolRepairRequired, ResumeSucceeded, RecoveryPreparing, ReplacementCreating, ReplacementCreated, ContextInjectionPending, RecoveryCompleted, RecoveryFailed, and UnknownOutcome.
- [ ] Define recovery lineage, stable idempotency identity, compare-and-swap rules, active-pointer commit point, original-session retention, and restart reconciliation.
- [ ] Define required SQLite schema/domain ownership and migrations from schema version 1 singleton state.
- [ ] Link workflow transition run, LoopRelay SessionIdentity, provider session/turn (Codex ThreadId/turn), RecoveryAttempt, SessionCapabilities digest, and artifact sequence.
- [ ] Define idempotency/unknown-outcome handling for resume, fork, replacement creation, context acceptance, and first proposal.
- [ ] Reconcile current persistence ordering between resume state, decision artifacts, transition raw output, and product effects.
- [ ] Define production CLI/log/telemetry vocabulary and ensure warnings are not routed to null output.
- [ ] Decide whether to compose existing telemetry or replace it with recovery-specific events; stop using cwd/time for thread lookup.
- [ ] Apply identical Execute policy across default, --eval, --traditional, and bounded execute while recording invocation provenance.
- [ ] Preserve bounded commands' existing no-decision-session behavior.
- [ ] Build unit, persistence, parser, subprocess compatibility, crash-injection, security, mode, and end-to-end coverage listed in section 16.
- [ ] Define rollout format/version fixtures and a policy for archived, missing, corrupt, partial, and externally moved sessions.
- [ ] Define operator controls/kill switch behavior without making “disable resume” silently mean “lose continuity.”

## 19. Open Questions Requiring Runtime Validation

Only external/runtime questions not settled by repository evidence are listed.

### 19.1 Native fork semantics

**Question:** Does app-server **thread/fork** on each supported Codex version create a new durable rollout with complete effective history, stable **forkedFromId**, no source mutation, and zero turns until explicitly started—and is its fidelity and unknown-outcome behavior demonstrably safer than reconstruction for the same fixture?

**Required validation:** Use a disposable CODEX_HOME containing a synthetic harmless session, not the user's real store. Launch:

~~~text
<versioned-codex> --cd <temp-repo> --sandbox read-only --ask-for-approval never app-server --listen stdio://
~~~

Send initialize with **capabilities.experimentalApi=true**, initialized, then:

~~~json
{"jsonrpc":"2.0","id":2,"method":"thread/fork","params":{"threadId":"<fixture-id>","cwd":"<temp-repo>"}}
~~~

Capture response keys, old/new rollout hashes/mtimes, new session_meta, forkedFromId, thread/read counts, effective history/compaction differences versus a reconstruction candidate, response-loss behavior, and behavior after process restart. Run only in the disposable store. The result informs mechanism ordering; method recognition alone must not select fork.

### 19.2 Supported-version compatibility matrix

**Question:** What Codex versions will LoopRelay support; what capabilities are client-declared, server-returned, or compatibility-certified; and how do initialize, excludeTurns, thread/read, and thread/fork schemas differ?

**Required validation:** For every declared version, run the four-frame compatibility fixture:

1. no capability + excludeTurns;
2. capability + excludeTurns;
3. no capability + no excludeTurns;
4. capability + thread/read/thread/fork against disposable valid/invalid IDs.

Also capture initialize response capabilities if present, **codex --version**, **codex app-server --help**, **codex app-server generate-json-schema --experimental --out <temp-dir>**, and schema digests. For every fixture, assert the expected tri-state **SessionCapabilities** snapshot and operation gates. Generated schema must be written only to a disposable directory during implementation work. A version string may select compatibility evidence but is not itself proof of support.

### 19.3 Corrupt and archived rollout behavior

**Question:** Which valid JSONL prefix can Codex thread/read/fork recover from a truncated or corrupted rollout, and can archived_sessions be read by ID?

**Required fixture:** Copy a synthetic rollout into disposable sessions and archived_sessions variants: missing final newline, truncated final record, malformed middle record, missing session_meta, duplicate event, and unsupported item type. Record resume/read/fork outcomes and source mutation.

### 19.4 Fork unknown-outcome reconciliation

**Question:** If transport is cut after Codex accepts thread/fork but before the response reaches LoopRelay, can the created child be deterministically discovered by parent ID and attempt time/digest?

**Required fixture:** A proxy/scripted transport that drops the fork response after server processing, followed by thread/list or filesystem/session-meta reconciliation in disposable CODEX_HOME. Do not retry fork until discovery behavior is proven.

### 19.5 thread/read public projection stability

**Question:** Are returned turn/item types and ordering stable across supported versions, compaction, tool-heavy sessions, and partial sessions?

**Required artifact:** Versioned golden JSON responses for thread/read over text-only, command-heavy, file-edit, MCP, compacted, cancelled, failed, and partial turns. Compare them to rollout records and document intentional omissions.

### 19.6 Actual model context limit

**Question:** What context limit should bound replacement content for the deployed model?

**Required evidence:** Capture model identity and app-server token/context metadata at open/turn for the production configuration, then compare with the 256,000 configuration assumption. Do not infer the window from model name.

## 20. Planning Readiness Verdict

**Ready with explicitly bounded runtime validation**

**Justification.**

- **Proven:** The authoritative source path, state model, process invocation, JSON-RPC frames, error propagation, current fallback, persistence ordering, cleanup behavior, workflow/mode integration, and test seams are traced to concrete files and symbols.
- **Proven:** The experimentalApi error is reproduced against the exact configured Codex 0.142.5 binary, its emitter and trigger are known, two control probes isolate the missing capability/unnegotiated field, and a valid old LoopRelay thread resumes when the capability is advertised.
- **Proven:** Recoverable Codex and repository sources exist, and observed content size/security characteristics rule out naive full injection.
- **Strongly supported:** A decision-session recovery coordinator using a provider-neutral RecoveryJournal, a negotiated SessionCapabilities snapshot, a Codex protocol/content adapter, and bounded recovery-content contracts is the strongest architecture supported by current evidence. The ordering of native fork versus reconstruction remains deliberately unresolved.
- **Bounded validation remains:** A valid native fork was intentionally not created; comparative fork/reconstruction fidelity, capability discovery across versions, multi-version schemas, corrupt/archived rollouts, and fork unknown-outcome reconciliation require disposable runtime fixtures. These questions affect mechanism selection and details, not whether implementation planning can proceed.

The planning agent should not rediscover the architecture or reopen the experimentalApi cause. It should treat the RecoveryJournal and negotiated SessionCapabilities as first-class workstreams, use the decisions in section 18, and explicitly schedule the disposable validations in section 19 before choosing or ordering native fork and reconstruction mechanisms.
