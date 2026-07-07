# Plan: Replace Sandbox-Copy One-Shots with App-Server Perfect Permissions

## Goal

Replace the seeded temp-workspace plus `codex exec` one-shot pattern with fresh Codex app-server turns that enforce the same constraint through operation-scoped permissions.

The current sandbox-copy mechanism constrains an agent by controlling which files exist in its temporary working directory and which outputs are copied back. The replacement must constrain the agent more directly: for each orchestrated operation, the permission layer allows only the reads, writes, and tool calls that operation is declared to need. Everything else is denied, even if the global permission policy would normally allow it.

## Non-Goals

- Do not add an interactive approval UI.
- Do not broaden the global `LoopRelay.Permissions` allow list to make these operations work.
- Do not rely on `workspace-write` against the real repository as the primary safety boundary.
- Do not reuse one operation's app-server thread for another operation.
- Do not remove deterministic filesystem gates; they remain the correctness oracle after each turn.

## Current State

The sandbox-copy pattern appears in two places:

| Caller | Current operation | Current sandbox inputs | Current copy-back |
| --- | --- | --- | --- |
| `LoopRelay.Plan.Cli.SandboxedPromptStep` | `CollectDetails` | `.agents/specs/*.md`, `.agents/plan.md` | `.agents/details.md` |
| `LoopRelay.Plan.Cli.SandboxedPromptStep` | `ExtractMilestones` | `.agents/plan.md` | rewritten `.agents/plan.md`, `.agents/milestones/m*.md` |
| `LoopRelay.Plan.Cli.SandboxedPromptStep` | `ExtractDetails` | `.agents/details.md`, `.agents/milestones/m*.md` | `.agents/details.md`, `.agents/milestones/m*.md` |
| `LoopRelay.Cli.DecisionSession` | `UpdateOperationalContext` | `.agents/operational_context.md`, `.agents/operational_delta.md` | rewritten `.agents/operational_context.md` |
| `LoopRelay.Cli.DecisionSession` | `OptimizeOperationalDocuments` | optional `.agents/plan.md`, optional `.agents/details.md`, `.agents/operational_context.md` | same documents if present |

The current pattern has useful properties that must survive:

- The agent cannot inspect unrelated repository files during the scoped operation.
- Failed turns and failed post-turn gates do not mutate repository inputs.
- Copy-back only promotes declared outputs.
- `ExtractMilestones` has a changed-content guard on `plan.md`.
- Milestone extraction has a false-closure guard requiring at least one strict checkbox.
- Temp workspaces are disposed on every path.

The current pattern also has liabilities this migration removes:

- It uses filesystem copying as a permission surrogate.
- It requires path translation because `.agents/` is stripped inside the sandbox.
- It uses `codex exec` one-shots, which cannot answer app-server approval requests.
- Its safety boundary is outside `LoopRelay.Permissions`, so policy evidence is implicit.

## Target Model

Each scoped artifact operation runs as a fresh single-turn app-server session:

```text
preflight artifact snapshot
  -> open fresh app-server session
  -> read-only sandbox, no network, approvals on-request
  -> operation-scoped permission profile installed for this session
  -> run one turn
  -> close session in finally
  -> deterministic filesystem gates
  -> restore snapshot on failure, keep changes on success
```

Use Codex `read-only` plus `on-request` for these operation sessions. The app-server permission gateway is the only path that can approve file writes or tool calls. If Codex can mutate a file in this posture without an approval request, the migration is blocked until that protocol behavior is understood and pinned by a test.

## Operation Permission Contract

Introduce an operation descriptor that is the permission-level replacement for `SandboxedStepPlan`.

Suggested shape:

```csharp
internal sealed record ArtifactOperationPlan(
    string Label,
    string Prompt,
    IReadOnlyList<string> AllowedReads,
    IReadOnlyList<(string Directory, string Pattern)> AllowedReadGlobs,
    IReadOnlyList<string> AllowedWrites,
    IReadOnlyList<(string Directory, string Pattern)> AllowedWriteGlobs,
    IReadOnlyList<string> RequiredOutputs,
    (string Directory, string Pattern)? RequiredOutputGlob,
    string? ChangedGuard,
    bool RequireChecklistInGlob);
```

Rules:

- Paths are repository-relative and keep their real `.agents/...` names.
- Every path is normalized under the repository root before evaluation.
- `..`, rooted paths outside the repository, symlink/reparse-point escapes, and case-spoofing on Windows are denied.
- Reads are denied unless they match `AllowedReads` or `AllowedReadGlobs`.
- Writes are denied unless they match `AllowedWrites` or `AllowedWriteGlobs`.
- Deletes are denied for these operations. The current copy-back model never deletes repository files.
- Network, MCP elicitation, user input, dependency installs, git mutation, process control, and shell indirection are denied.
- Command execution is denied by default for operation-scoped sessions. Add a command only if an operation demonstrably needs it, and bind any file arguments to the same read/write profile.
- The global safe command/tool allow list does not apply inside an operation-scoped session unless the operation explicitly opts into that exact action.

## Operation Matrix

| Operation | Allowed reads | Allowed writes | Post-turn gates |
| --- | --- | --- | --- |
| `collect-details` | `.agents/plan.md`, `.agents/specs/*.md` | `.agents/details.md` | `details.md` exists |
| `extract-milestones` | `.agents/plan.md` | `.agents/plan.md`, `.agents/milestones/m*.md` | milestone glob non-empty, at least one strict checkbox, `plan.md` changed |
| `extract-details` | `.agents/details.md`, `.agents/milestones/m*.md` | `.agents/details.md`, `.agents/milestones/m*.md` | `details.md` exists |
| `operational-context-evolution` | `.agents/operational_context.md`, `.agents/operational_delta.md` | `.agents/operational_context.md` | context exists and changed |
| `operational-documents-optimization` | optional `.agents/plan.md`, optional `.agents/details.md`, `.agents/operational_context.md` | same documents that existed pre-turn | context exists; no change required |

For `operational-documents-optimization`, the allowed write set is computed from the pre-turn snapshot. If `details.md` or `plan.md` did not exist before the turn, the operation may not create it.

## Prompt Path Changes

The current prompts name flat sandbox files such as `plan.md`, `details.md`, `milestones/m*.md`, and `operational_context.md`.

Update them to refer to real repository artifact paths:

- `.agents/plan.md`
- `.agents/details.md`
- `.agents/specs/*.md`
- `.agents/milestones/m*.md`
- `.agents/operational_context.md`
- `.agents/operational_delta.md`

This removes the `.agents/` prefix-stripping convention from the planning and transfer flows.

## Permission Gateway Changes

Add an operation-scoped permission path next to the existing closed-world command policy.

Implementation options:

- Add an optional `PermissionScope` or `OperationPermissionProfile` to `AgentSessionSpec`.
- Pass that scope into `CodexAppServerSession`.
- Change `IPermissionGateway.Evaluate(...)` to accept a context object containing repo identity, working directory, and optional operation scope.
- Route scoped sessions to an `OperationPermissionHandler`; route unscoped sessions to the existing `PermissionHandler`.

The operation handler should evaluate in this order:

1. Parse the Codex app-server request.
2. Apply absolute hard denies first.
3. Normalize requested paths against the repository root.
4. Check the request against the operation profile.
5. Return `accept` only for exact matches.
6. Return `decline` for everything else.

The existing `CodexPermissionAdapter` currently treats several app-server methods generically. Extend it to parse enough structure for operation decisions:

- `item/commandExecution/requestApproval`: command, shell, cwd, reason, network context.
- `item/fileChange/requestApproval`: operation kind, target path, grant root, item id.
- `item/tool/call`: tool name and path-bearing arguments for read, list, grep, edit, write, and patch-like tools.
- `item/tool/requestUserInput`: always deny for operation sessions.
- `mcpServer/elicitation/request`: always deny for operation sessions.

Protocol certification is required before implementation. If the installed Codex app-server only offers a broad `grantRoot` approval that cannot be narrowed to the declared write paths or globs, do not accept that grant. Keep the old sandbox-copy path for that operation until Codex exposes a precise enough request shape.

## Repository Mutation Transaction

Direct app-server writes happen in the real repository, so the migration needs an explicit rollback mechanism to preserve the current "failed step copies nothing back" invariant.

Before each scoped turn:

- Snapshot every existing file that may be written.
- Snapshot every existing file matching allowed write globs.
- Record which allowed output files did not exist.
- Record directory listings for allowed write directories.

After the turn:

- If the turn failed, restore snapshots and remove newly-created files under allowed write globs.
- If any deterministic gate fails, restore snapshots and remove newly-created files under allowed write globs.
- If cancellation happens, restore before surfacing cancellation when possible.
- If restore fails, report the restore failure explicitly and leave the session closed.

On success, keep the repository changes and publish exactly as today.

This transaction is not a sandbox and is not a permission boundary. It is a failure-atomicity mechanism.

## Runner Changes

Replace `SandboxedPromptStep` with an app-server runner, for example `PermissionedArtifactOperationStep`.

Behavior:

- Validate all required read inputs before opening Codex.
- Capture the mutation transaction snapshot.
- Open a fresh app-server session with `read-only`, no network, approvals required, and the operation permission profile.
- Run exactly one turn.
- Close the session in `finally`.
- Run the same deterministic gates currently enforced by `SandboxedPromptStep` and `DecisionSession`.
- Restore on failed turn, failed gate, or cancellation.

Update `AgentSpecs`:

- Add `Plan.Cli.AgentSpecs.ScopedArtifactOperation(repository, operationProfile)`.
- Add `Cli.AgentSpecs.ScopedArtifactOperation(repository, effort, operationProfile)`.
- Keep planning authoring and decision sessions unchanged.

Remove from these call paths:

- `ISandboxWorkspaceFactory`
- `TempSandboxWorkspaceFactory`
- `AgentSpecs.SandboxedOneShot`
- `runtime.RunOneShotAsync(...)` for scoped artifact transformations
- `.agents/` prefix stripping and flat sandbox seed names

Do not delete `ISandboxWorkspaceFactory` until all references are removed and downstream/back-end mirrors are checked.

## Testing Plan

Add permission tests:

- operation profile path normalization denies repository escapes;
- read allow exact path;
- read allow glob;
- write allow exact path;
- write allow glob;
- delete denied;
- global safe commands denied in operation scope unless explicitly allowed;
- network, MCP, user input, git mutation, installs, and shell indirection denied;
- hard-deny commands override an operation allow.

Add Codex adapter tests:

- parse command approval request into structured command data;
- parse file-change approval with path-level target;
- reject broad grant roots that exceed the operation profile;
- parse tool-call request path arguments;
- preserve numeric and string JSON-RPC ids;
- return `accept` only for an operation match and `decline` otherwise.

Add agent runtime tests:

- scoped app-server session routes server requests through the operation profile;
- approval request accepted when it matches the operation;
- approval request declined when it targets any other file;
- gateway exception declines and does not hang the turn;
- session closes on success, failure, cancellation, and gate failure.

Add planning CLI tests:

- `CollectDetails` can only read plan/specs and write details;
- `ExtractMilestones` can only rewrite plan and write milestone files;
- `ExtractDetails` can only read/write details and milestone files;
- failed turn restores all candidate writes;
- failed post-turn gate restores all candidate writes;
- prompts use `.agents/...` paths;
- no temp sandbox is created.

Add decision session tests:

- operational-context evolution can only read context/delta and write context;
- unchanged evolved context fails and restores;
- optimization can only touch pre-existing optimization documents;
- failed optimization restores all candidate writes;
- transfer still archives `operational_delta.md` only after successful update and optimization.

Add live certification tests as skipped/runbook tests:

- a read-only app-server session requests approval before file edits;
- file-change approvals expose enough target-path information for exact operation decisions;
- declined approvals fail or continue without hanging;
- accepted approvals allow only the requested write.

## Rollout

1. Add protocol certification tests and operation-scope models.
2. Implement operation-scoped permission evaluation behind a feature flag or CLI setting.
3. Migrate `LoopRelay.Plan.Cli` operations first because their artifact matrix is small.
4. Migrate `LoopRelay.Cli.DecisionSession` transfer operations next.
5. Run both targeted and full test suites.
6. Flip the default only after live app-server certification passes.
7. Remove sandbox-copy code and tests only after the app-server path is the default and the rollback path is no longer needed.

Suggested verification:

```powershell
dotnet test tests\LoopRelay.Permissions.Tests\LoopRelay.Permissions.Tests.csproj
dotnet test tests\LoopRelay.Agents.Tests\LoopRelay.Agents.Tests.csproj
dotnet test tests\LoopRelay.Plan.Cli.Tests\LoopRelay.Plan.Cli.Tests.csproj
dotnet test tests\LoopRelay.Cli.Tests\LoopRelay.Cli.Tests.csproj
dotnet test LoopRelay.slnx
```

## Acceptance Criteria

- No scoped artifact operation uses `codex exec` one-shot execution.
- No scoped artifact operation creates a temp workspace to constrain the agent.
- Scoped operations run through app-server sessions with `read-only`, no network, and approvals on-request.
- The permission gateway accepts only the current operation's declared reads/writes/tool calls.
- The global safe allow list cannot grant extra authority inside operation-scoped sessions.
- Failed turns, failed gates, and cancellation preserve repository inputs through transaction restore.
- Prompt paths refer to real `.agents/...` artifacts.
- Existing deterministic gates still enforce required outputs, changed guards, and checklist guards.
- Live app-server protocol behavior is pinned before the sandbox-copy fallback is removed.
