# Commit + push the `.agents/` submodule around each loop iteration

**Date:** 2026-07-01
**Status:** Approved (design)
**Scope:** `CommandCenter.CLI` only for behavior. `.agents/` is now a git submodule
(`.gitmodules` → `CommandCenter.Agents.git`, checked out on `main`). The backend
`RepositoryOrchestrator` is deliberately untouched (it is `internal`-isolated from the CLI loop
and has zero references to `CommitGate`/`LoopRunner`); its parallel gap is recorded as **TD-8**
rather than fixed here.

## Problem

Converting `.agents/` to a submodule broke the CLI loop's persistence and stall detection:

1. **`.agents/` writes commit nowhere.** The parent repo's `git add -A` cannot commit content
   *inside* a submodule — it can only stage a moved gitlink pointer, and only once the submodule
   has its own commit. Nothing in the loop commits *inside* `.agents/`, so every
   `operational_context.md` / `decisions.md` / handoff write now lingers uncommitted in the
   submodule working tree.
2. **Stall detection misfires.** `CommitGate.IsBookkeeping` classifies an iteration as
   "no substantive change" by matching `.agents/decisions/…`, `.agents/handoffs/…`, and
   `.agents/operational_context.md` paths in the parent's `git status --porcelain`. Post-submodule
   the parent only ever shows a single `.agents` gitlink entry, so those prefixes never match and
   the loop believes every iteration made substantive progress — the `MaxNoChangesCount` stall
   gate can never trip.

## Goal

1. **Before invoking codex**, commit + push the `.agents/` submodule (to its own remote) so the
   exact context fed to codex is persisted and shared across repos.
2. Make `CommitGate` **ignore** the `.agents/` submodule entirely: it commits and pushes only the
   target repo's real (non-`.agents`) work and never stages or advances the `.agents` gitlink.
   Stall detection keys purely off real source progress — an iteration that touched only `.agents/`
   is no-progress.

> **Design note (redirect):** an earlier revision had `CommitGate` re-publish the submodule and
> advance the gitlink. That coupled the gate to submodule semantics and (per adversarial review)
> risked (a) reclassifying `.agents/`-internal milestone progress as bookkeeping and (b) desyncing
> the parent gitlink under a `submodule.ignore` config. Per direction, `CommitGate` now simply
> **ignores** `.agents/`; the submodule is owned solely by the pre-codex publisher. Both review
> findings are dissolved by this simpler contract.

## Loop I/O (confirmed)

`LoopRunner.RunAsync` per iteration:

| Step | Call | `.agents/` effect |
|------|------|-------------------|
| 1 | `EnsureOperationalContextAsync()` | writes `operational_context.md` |
| 2 | `decision.RunAsync()` (incl. Transfer) | writes `decisions.md` (+numbered); Transfer writes `operational_delta.md`, archives it to `deltas/`, writes evolved `operational_context.md` |
| 3 | `RotateLiveHandoffAsync()` | archives live `handoff.md` → numbered, deletes live |
| — | **NEW: pre-codex submodule publish** | commit + push `.agents/` (steps 1–3 changes) |
| 4 | `execution.RunAsync()` (**codex**) | writes new `handoff.md`, milestone box-checks, final decisions |
| — | **NEW: post-codex submodule publish** | commit + push codex's `.agents/` writes |
| 5 | `CommitGate` (revised) | commit/push real (non-`.agents`) work, EXCLUDING the `.agents` gitlink → stall eval |

> `CommitGate` never commits `.agents`, so the submodule is versioned solely by the two `LoopRunner`
> publishes. The **post-codex** publish is what makes the epic-completing (and stalling) iteration's state
> durable: `IsEpicCompleteAsync` short-circuits at the *top* of the next iteration, before its pre-codex
> publish would run, so codex's final writes (the completion checkbox, last handoff/decisions) must be
> pushed on the same iteration that produced them.

## Design

### New component — `AgentsSubmodulePublisher` (CLI-internal)

`internal sealed`, mirroring `CommitGate`'s constructor shape
`(IProcessRunner processRunner, Repository repository, ILoopConsole console)`. All git runs with
`workingDirectory = Path.Combine(repository.Path, ".agents")` via the existing
`IProcessRunner.RunAsync(fileName, args, workingDirectory)`.

`Task<bool> PublishAsync(string commitMessage, CancellationToken ct)`:

1. `git status --porcelain` → **if dirty:** require a branch (`git branch --show-current`; blank ⇒
   detached HEAD ⇒ throw an actionable `LoopStepException`), then `git add -A` → `git commit -m
   <commitMessage>` → `git push`; return `true`. Any nonzero exit throws (strict).
2. **If clean:** recover a commit stranded by a prior failed push — a failed `git push` does not roll
   back the commit, and `git status` can no longer see it. `git rev-list --count @{u}..HEAD`; if HEAD
   is ahead of upstream, require a branch and `git push` (return `false`, no new commit). A non-zero
   `rev-list` exit (e.g. no upstream) means "nothing to recover".

Push is **strict**: a failure aborts the iteration. Rationale — a parent pointer that references an
unpushed submodule commit is broken for every other clone, and cross-repo `.agents/` sync is the entire
point. Because strict push aborts mid-run, step 2 is what makes it self-heal: the next publish (this run
or the next) flushes the stranded commit.

### Wiring — `LoopRunner` + `Program.cs`

- `Program.cs`: construct `AgentsSubmodulePublisher(processRunner, repository, console)` after
  `processRunner` is resolved, and pass it to `LoopRunner` (`CommitGate` does **not** take it).
- `LoopRunner.RunAsync` calls the publisher **twice**: once after `RotateLiveHandoffAsync()` and
  **before** `execution.RunAsync()` (`ContextUpdateMessage`), and once **after** `execution.RunAsync()`
  (`ExecutionHandoffMessage`) so codex's writes — including the epic-completing checkbox — are published
  on the iteration that produced them.
  This is the literal "before invoking codex" point.

### Revise — `CommitGate` ignores `.agents/`

Ctor is unchanged (`IProcessRunner`, `Repository`, `ILoopConsole` — no publisher).
`CommitPushAndEvaluateAsync`:

1. `git status --porcelain` on the parent, parsed by the shared `GitPorcelain.ChangedPaths`, then
   **filter out** any `.agents` / `.agents/*` path → the *real* changed paths.
2. If there are real changes: `git add -A -- . :(exclude).agents` (stages everything except the
   submodule gitlink) → `commit` → `push`, and reset `NoChangesCount`.
3. If there are none: increment `NoChangesCount` (a `.agents`-only or empty iteration is no-progress).
4. Stall when `NoChangesCount > MaxNoChangesCount` (unchanged threshold).

No submodule git runs, no gitlink staging, no gitlink-vs-status ambiguity.

### Shared constant + helper

Add `AgentsDirectory = ".agents"` to `OrchestrationArtifactPaths` (alongside
`DecisionsDirectory`/`HandoffsDirectory`/etc.). Extract `GitPorcelain.ChangedPaths(statusOutput)` so
the parent gate and the submodule publisher parse porcelain the same way.

## Backend / technical debt

No backend code change. Append **TD-8** to `technical-debt.md` (mirroring the TD-6 format):
backend `RepositoryOrchestrator` writes `.agents/` via `artifactStore.WriteAsync` but does not
commit the submodule before codex (`agentRuntime.RunOneShotAsync`); it only publishes the plan via
`planArtifactPublisher.PublishAsync`. Deferred because the CLI is the active path and `GitService`
has no submodule/sub-path support; resolution is to add the same commit+push-before-codex there if
the backend path is retained.

## Testing

New `AgentsSubmodulePublisherTests`:

- clean & up-to-date (`status` empty, `rev-list` not ahead) → returns `false`, no `add`/`commit`/`push`.
- clean but ahead of upstream (`rev-list --count` > 0) → pushes the stranded commit, no `add`/`commit`,
  returns `false` (push-failure recovery).
- dirty submodule on a branch → issues `add -A` / `commit` / `push` in order at
  `workingDirectory == {repo}/.agents`, returns `true`.
- detached HEAD (`branch --show-current` blank) with dirty tree → throws `LoopStepException`,
  no `commit`/`push`.
- `push` / `commit` / `status` return nonzero → throws `LoopStepException`.

`LoopRunnerTests` asserts a submodule push occurs both **before** and **after** the codex turn.

Update `CommitGateTests`:

- A parent status of only ` M .agents` (or a `.agents/*` path) counts as no-progress (increments
  `NoChangesCount`, trips after the threshold); a real (non-`.agents`) path resets it.
- With real changes present, the commit stages with the exclude pathspec
  `["add", "-A", "--", ".", ":(exclude).agents"]` — never the gitlink — then `commit` / `push`.

`FakeProcessRunner` (`TestDoubles.cs`) now also captures `workingDirectory`, so the publisher tests can
assert every git call runs inside the `.agents` working directory.

## Verification during implementation

- `dotnet test` for `CommandCenter.CLI.Tests` green; full backend suite unaffected (CLI-isolated).
- Real git: `:(exclude).agents` is accepted by the installed git (verified via `git add -n`), so the
  parent commit never stages the submodule gitlink.
- Manual: in a repo whose `.agents/` is a submodule on a branch, run one loop iteration and confirm the
  pre-codex publish commits+pushes `.agents/` to its own remote, and the parent commit contains only
  real source changes (no `.agents` gitlink bump).

## Out of scope

- Advancing/committing the parent `.agents` gitlink pointer from the loop at all — `CommitGate` ignores
  it; the submodule is versioned only on its own remote via the pre- and post-codex publishes.
- Auto-checkout of a branch on detached HEAD (fail-fast chosen instead).
- Backend `RepositoryOrchestrator` parity (→ TD-8).
- Retention/pruning of submodule history or the `.agents` remote branch strategy.
