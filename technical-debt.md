# Technical Debt

A register of deliberately deferred changes — work we chose *not* to do now, with
enough context to pick it up later. Each item states what is deferred, why, the
impact of leaving it, and the path to resolve it. Newest section first.

> Convention: when an item is resolved, delete it (git history keeps the record).
> Do not let this file accrue "done" entries.

---

## 2026-07-01 — CLI: execution-first on the first pass (`StartExecution` revived)

Background: the CLI `LoopRunner` was re-sequenced so that on the FIRST pass — when no handoff
exists yet — execution runs FIRST, straight from the self-contained plan via `StartExecution`,
with no decision session and no `decisions.md`. The decision session now runs only once a
handoff exists, always folding it via `GenerateSystemPromptForNextExecutionAgent`. This revives
`StartExecution` in the CLI (`ExecutionStep` renders it when there is no live `decisions.md`) —
so it is no longer CLI-dead (see the correction to TD-6 below) — and, symmetrically, retires the
first-pass decision prompt.

### TD-9 — `GenerateSystemPromptForFirstExecutionAgent` is now CLI-dead but retained

**Deferred (obsolete, not deleted).** With execution-first on the first pass, the CLI decision
session only runs when a handoff is present, so it always renders
`GenerateSystemPromptForNextExecutionAgent.Render(handoff)`. The first-pass branch —
`GenerateSystemPromptForFirstExecutionAgent.Text` — is therefore **never reached from the loop**.

**Why deferred:** removing it means deleting the `handoff is null` branch in
`DecisionSession.BuildProposalPromptAsync`, its unit test
(`DecisionSessionTests.Run_FirstPass_NoHandoff_...`), and the `.prompt` (plus its provenance /
`SourceHash` wiring) — a small but real change the maintainer may prefer to keep as a defensive
path rather than delete now.

**Impact:** one prompt template in `CommandCenter.Core/Prompts` that the active loop never
renders (the CLI has no first-pass decision anymore). Unlike TD-6's `GetNextDecisions`, no
backend path consumes it either — it was CLI-only from the start — so it is dead everywhere.

**Resolution:** delete `GenerateSystemPromptForFirstExecutionAgent.prompt`, the `handoff is
null` branch in `DecisionSession.BuildProposalPromptAsync`, and its unit test once the
first-pass decision path is confirmed permanently unused.

---

## 2026-07-01 — `.agents/` submodule: commit + push before codex (CLI only)

Background: `.agents/` was converted to a git submodule in every repo. The CLI loop
(`AgentsSubmodulePublisher`) now commits and pushes the submodule to its own remote at two
points per iteration — once BEFORE invoking codex (`LoopRunner`, persisting the
operational_context / decisions / rotated handoff codex will consume) and again at
end-of-iteration (`CommitGate`, capturing codex's new handoff) — then the revamped
`CommitGate` advances the parent gitlink and pushes it, with stall detection re-based onto
parent changes outside the `.agents` gitlink. Scope was CLI-only; the backend
`RepositoryOrchestrator` was deliberately left unchanged (TD-8).

### TD-8 — Backend `RepositoryOrchestrator` does not commit+push the `.agents/` submodule before codex

**Deferred.** The CLI's `AgentsSubmodulePublisher` commits and pushes the `.agents/`
submodule before every codex turn and at end-of-iteration. The backend
`RepositoryOrchestrator` writes `.agents/` artifacts via `artifactStore.WriteAsync`
(operational_context, decisions, handoffs, specs) and invokes codex via
`agentRuntime.RunOneShotAsync`, but never commits the submodule — it only publishes the plan
via `planArtifactPublisher.PublishAsync`, which commits the target repo through `GitService`
(and `GitService`/`IGitService` have no submodule/sub-path support).

**Why deferred:** the CLI loop is the active execution path; the backend
`RepositoryOrchestrator` is the legacy in-process orchestration mode (see TD-1). Bringing
parity requires either extending `IGitService` with a submodule-aware working-directory
overload or introducing a shared submodule publisher the backend can call before each
`RunOneShotAsync`.

**Impact:** post-submodule-conversion, backend execution runs write `.agents/` artifacts to
disk but never persist them to the submodule's remote, and the parent repo cannot capture
them through its plan commit (a dirty submodule is not committable from the parent). So the
backend path's `.agents/` history and cross-repo sync are lost until a CLI loop later commits
them. Asymmetry with the CLI.

**Resolution:** when the backend execution paths are retained or migrated (decision: TD-1),
apply the same commit+push-before-codex to the `RepositoryOrchestrator` transfer/execution
sites, sharing the CLI's publisher logic or a submodule-aware `IGitService` overload.

---

## 2026-06-30 — decisions.md is the execution agent's system prompt (CLI decision-first)

Background: `CommandCenter.CLI` was re-sequenced so the **decision session runs before
execution** and `decisions.md` is now *the execution agent's system prompt*, not a set of
directions. The decision proposal turn no longer renders `GetNextDecisions`; it renders
`GenerateSystemPromptForFirstExecutionAgent` on the first pass (no handoff yet) and
`GenerateSystemPromptForNextExecutionAgent.Render(handoff)` afterwards, still persisting the
output to `decisions.md`. `ExecutionStep` always renders `ContinueExecution` (the
`StartExecution` first-milestone branch is gone) and the shared `ContinueExecution.prompt`
had its `{handoff}` hole **removed** (its `Render` is now `(plan, decisions)`), because the
handoff now reaches the next agent through the generated system prompt, not the execution
prompt. Scope was CLI-only; the backend/legacy paths were adapted just enough to stay green.

### TD-6 — `GetNextDecisions` prompt is now CLI-dead but retained

**Deferred (obsolete, not deleted).** After the decision-first change the CLI no longer renders
`GetNextDecisions.prompt` — it proposes via `GenerateSystemPromptFor{First,Next}ExecutionAgent`
instead. It is **kept** solely because the legacy backend decision path still consumes it:
`CommandCenter.Orchestration` → `RepositoryOrchestrator` renders `GetNextDecisions.Render(handoff)`.

(`StartExecution` was also CLI-dead under decision-first, but the 2026-07-01 execution-first
change **revived it in the CLI** — `ExecutionStep` renders `StartExecution.Render(plan)` on the
first pass — so it is no longer dead anywhere and has been dropped from this item. The backend
`RepositoryOrchestrator` / `ExecutionPromptBuilder` continue to render it too.)

**Why deferred:** deleting it would break the backend decision path, and the maintainer chose to
leave the backend flow unchanged (see TD-1). Marking it obsolete here rather than deleting keeps
the record without forcing the larger backend migration.

**Impact:** one prompt template in `CommandCenter.Core/Prompts` that the *active* (CLI) loop
never renders. Provenance/tests still reference its `SourceHash`, so it cannot be removed
piecemeal.

**Resolution:** delete `GetNextDecisions.prompt` (and its `RepositoryOrchestrator` consumer +
provenance) if/when the backend execution paths are retired (this is the same decision as TD-1's
option (b)).

### TD-7 — Shared `ContinueExecution.prompt` lost `{handoff}`; docs and backend prompt-text drift

**Deferred.** Removing `{handoff}` from the shared `ContinueExecution.prompt` changed its
generated `Render` signature to `(plan, decisions)` for **all** consumers, so:

- The backend `RepositoryOrchestrator` continuation turn no longer injects the prior handoff
  text into the prompt (it still *reads* the handoff — the null-check gate and the provenance
  `InputArtifactIdentities` list are unchanged — it just isn't rendered). Same class of silent
  backend behavior change as TD-1; a backend test assertion for the handoff substring was
  de-asserted (`ExecutionPromptBuilderTests`), not fixed.
- Documentation still describes the old 3-arg form: `plan.md` (the `Render(plan, handoff,
  decisions)` signature line + the m6 flow step), `docs/final-acceptance.md` (FA-8), and
  `docs/prompt-architecture.md`. These were left unchanged (CLI-only scope) and now misstate
  the generated signature.

**Impact:** backend continuation prompts omit the handoff body; governance docs overstate the
`ContinueExecution` inputs.

**Resolution:** when the backend is migrated (TD-1/TD-6) update the docs to the `(plan,
decisions)` signature, or correct the docs sooner as a standalone doc fix.

---

## 2026-06-30 — Two-turn execution session

Background: `CommandCenter.CLI`'s `ExecutionStep` was changed so an execution
slice runs as **two user-input turns over one held-open operational app-server
session** — turn 1 (`StartExecution`/`ContinueExecution`) does the work and is no
longer asked for a handoff; turn 2 (`GenerateHandoff`) writes
`.agents/handoffs/handoff.md` from the in-session context of turn 1. The handoff
instruction was removed from `StartExecution.prompt`/`ContinueExecution.prompt`
and now lives standalone in `GenerateHandoff.prompt`. These three prompts are
shared from `CommandCenter.Core`, so the change ripples beyond the CLI.

### TD-1 — Legacy backend execution paths still assume single-turn handoff

**Deferred.** Two backend execution paths render the same shared prompts but were
intentionally left on the old single-turn model:

- `CommandCenter.Orchestration` → `RepositoryOrchestrator` renders
  `StartExecution.Render(plan)` / `ContinueExecution.Render(plan, handoff, decisions)`
  as a **single one-shot codex turn** and never calls `GenerateHandoff`.
- `CommandCenter.Execution` → `ExecutionPromptBuilder` → `ExecutionSessionService`
  builds the same prompts for the legacy HTTP execution-session path.

**Why deferred:** the CLI serial loop is the active orchestration path; these are
the older backend paths the CLI superseded. Migrating them is a separate,
larger change (the orchestrator's execution is one-shot, not a held-open
session, so it would need the same `OpenSession → 2 turns → CloseSession`
restructuring plus its own provenance/test updates).

**Impact:** because the handoff instruction was removed from the shared
`StartExecution`/`ContinueExecution` prompts, *if either backend path is run
against real codex it will no longer produce a `handoff.md`* — the agent is never
told to write one and `GenerateHandoff` is never sent. The orchestrator does not
verify handoff existence, so nothing fails loudly; the decision step would simply
read a stale/missing handoff. Backend tests were kept green by de-asserting the
deleted substring (see `ExecutionPromptBuilderTests`, `ExecutionSessionServiceTests`,
`ArchitectureLayeringTests`/`PromptAuthorityTests`), **not** by fixing behavior.

**Resolution:** either (a) migrate `RepositoryOrchestrator` (and the legacy
`ExecutionSessionService` path) to the two-turn flow — execution turn, then a
second `GenerateHandoff` turn before returning — or (b) formally retire these
paths if the CLI is the sole supported loop, and delete the dead prompt
consumers. Decision belongs to the maintainer.

### TD-2 — Execution session is opened/closed per iteration, not held warm

**Deferred.** `ExecutionStep` opens a fresh operational app-server session each
loop iteration and closes it in a `finally` after the two turns. `DecisionSession`,
by contrast, holds one warm process across iterations (with transfer/reset logic
for token pressure).

**Why deferred:** the two turns that must share context (work → handoff) both live
*within* one iteration; cross-iteration continuity already flows through
`handoff.md`/`decisions.md` on disk. Holding the execution session warm across
iterations would be more token-efficient (continue-execution as a delta) but
pulls in unbounded-context management and a transfer/reset path, mirroring the
decision session's machinery.

**Impact:** one extra app-server process spawn per iteration. Negligible today;
revisit only if execution turn-startup cost becomes material.

**Resolution:** if warranted, give execution the same warm-process + transfer
treatment as `DecisionSession`, bounded by a token threshold.

---

## Codex launch / runtime (carried from prior session)

### TD-3 — Default codex executable resolves to a batch shim that hangs

**Deferred.** `EnvironmentAgentExecutableResolver` defaults to `"codex.cmd"`. On
Windows the `codex.cmd` npm shim runs under `cmd.exe`, which does **not** forward
the stdin EOF that `codex exec -` needs to begin — so the one-shot exec path
hangs with no output and no session log. The working configuration is to set
`CODEX_EXECUTABLE` to the native binary, e.g.
`C:\Program Files\nodejs\node_modules\@openai\codex\node_modules\@openai\codex-win32-x64\vendor\x86_64-pc-windows-msvc\bin\codex.exe`.

**Why deferred:** the env-var override is a working escape hatch, and a startup log
line (`Codex executable: ...` in `Program.cs`) makes the active binary visible at
a glance.

**Impact:** a fresh environment that doesn't set `CODEX_EXECUTABLE` will hang on
the first one-shot exec turn (still used by `DecisionSession.TransferAsync`). The
two-turn execution change moves the *main* execution path onto the app-server
transport, but the one-shot path is not gone.

**Resolution:** make the resolver auto-detect the native `codex.exe` (resolve the
shim to its underlying binary, or probe the known npm vendor path) and fall back
to the shim only if the native binary is absent. Then drop the manual env var.

### TD-4 — One-shot args emit `approval_policy="never"` unconditionally

**Deferred.** In `CodexAgentArgumentBuilder` the one-shot/exec branch always emits
`-c approval_policy="never"`; the prior `if (!spec.Sandbox.RequiresApproval)` guard
is commented out, and `--sandbox` is intentionally omitted.

**Why deferred:** every current CLI spec uses `RequiresApproval: false`, so the
behavior is identical to the guarded version today.

**Impact:** latent — a future spec with `RequiresApproval: true` would still run
fully unattended on the one-shot path (no approval gate), silently diverging from
the persistent path, which honors `RequiresApproval` via `ApprovalPolicy()`.

**Resolution:** restore the guard (or make the approval posture an explicit,
spec-driven argument shared by both paths) if approval-required specs ever exist.

### TD-5 — Inert debug scaffolding left in `ProcessRunner`

**Deferred (cleanup).** `ProcessRunner.cs` carries commented-out debug lines
(`// Console.WriteLine(...)`, `// throw new Exception("TEST")`) from launch-hang
diagnosis.

**Impact:** none functional; noise.

**Resolution:** delete before the next commit that touches the file.

---

## Operational / pending (not code debt, but don't lose track)

### OP-1 — Redeploy to `C:\tools\command-center` blocked by running CLI

The published CLI process holds the deployed `*.dll` files locked, so
`publish-cli.bat` fails with `MSB3027`/`MSB3021` while it is running. The
redeploy carrying the stderr-drain fix, the startup `Codex executable:` log, and
the `--json` re-add is pending: close the running CLI, then re-run
`publish-cli.bat`.

### OP-2 — Uncommitted codex-launch fixes on `next`

The stderr-drain fix (`AgentProcess.StartErrorDrain` + `ProcessRunner` wiring,
`ProcessRunnerStderrDrainTests`), the `Program.cs` startup log, the
`CodexAgentArgumentBuilder` `--json` re-add, the `EnvironmentAgentExecutableResolver`
default change, and the two-turn execution change are uncommitted, alongside
earlier unpushed commits on `next`. Commit/push is held pending explicit
instruction.
