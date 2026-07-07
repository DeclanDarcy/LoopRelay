# LoopRelay.Permissions Implementation Plan

## Goal

Add a `LoopRelay.Permissions` project that evaluates Codex tool-approval requests before LoopRelay answers them. The design should port the useful parts of `C:\claude-repos\ai-clients\src\Conduit.Permissions` and its Codex approval adapter, but fit LoopRelay's current agent runtime and roadmap trust-boundary work.

The first implementation is deliberately closed-world and non-interactive:

- automatically accept only commands that the permission engine can prove safe;
- deny dangerous, unknown, or review-required commands;
- keep one-shot Codex execution unattended with approvals disabled;
- route approval-managed execution through the held-open Codex app-server session, where JSON-RPC server requests can be answered.

## Source Reference

Use these ai-clients files as the behavioral source:

- `src/Conduit.Permissions/Services/PermissionGateway.cs`
- `src/Conduit.Permissions/Services/PermissionHandler.cs`
- `src/Conduit.Permissions/Services/CommandParser.cs`
- `src/Conduit.Permissions/Services/CommandCanonicalizer.cs`
- `src/Conduit.Permissions/Services/PermissionEvaluatorEngine.cs`
- `src/Conduit.Permissions/Services/InvariantGuard.cs`
- `src/Conduit.Permissions/Services/InMemoryPermissionCache.cs`
- `src/Conduit.Permissions/Services/Sha256FingerprintService.cs`
- `src/Conduit.Codex/Services/CodexPermissionAdapter.cs`
- `src/Conduit.Codex/Services/CodexServerRequestHandler.cs`
- `tests/Conduit.Permissions.Tests/*`
- `tests/Conduit.Codex.Tests/Services/CodexPermissionAdapterTests.cs`
- `tests/Conduit.Codex.Tests/Services/CodexServerRequestHandlerTests.cs`

Important adaptation: Conduit's fingerprint service depends on `Orchestrix.Capabilities.Contracts`. LoopRelay should not import that dependency. Implement local deterministic SHA-256 serialization in `LoopRelay.Permissions` using `System.Security.Cryptography`.

## Current LoopRelay Context

Relevant LoopRelay files:

- `src/LoopRelay.Agents/Services/CodexAppServerSession.cs`
- `src/LoopRelay.Agents/Services/CodexAppServerProtocol.cs`
- `src/LoopRelay.Agents/Models/CodexAppServerMessage.cs`
- `src/LoopRelay.Agents/Models/SandboxProfile.cs`
- `src/LoopRelay.Agents/Services/CodexAgentArgumentBuilder.cs`
- `src/LoopRelay.Agents/Services/AgentRuntime.cs`
- `src/LoopRelay.Infrastructure/Trust/TrustPolicy.cs`
- `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs`
- `issues/deferred-until-permissions-manager-integration/roadmap-cli-execution-bridge-trust-boundary.md`

Current behavior to change:

- `CodexAppServerSession.Dispatch` auto-declines every JSON-RPC server request.
- `SandboxProfile.RequiresApproval == true` currently only omits `approvalPolicy: "never"` from `thread/start` and `thread/resume`; it does not produce useful approvals because server requests are always declined.
- `RoadmapExecutionBridge` uses `RunOneShotAsync`, and the one-shot argument builder always emits `approval_policy="never"`. This path cannot support mid-turn approval requests without a separate protocol implementation.
- `Roadmap.Cli.AgentSpecs.ExecutionBridge` hardcodes `danger-full-access`, network enabled, approvals disabled.

## Target Architecture

### 1. Add `LoopRelay.Permissions`

Create:

- `src/LoopRelay.Permissions/LoopRelay.Permissions.csproj`
- `tests/LoopRelay.Permissions.Tests/LoopRelay.Permissions.Tests.csproj`

Add both projects to `LoopRelay.slnx`.

Project dependencies:

- `LoopRelay.Permissions` should target `net10.0`.
- It should reference `Microsoft.Extensions.DependencyInjection.Abstractions` or `Microsoft.Extensions.DependencyInjection` only if needed for registration extensions.
- It should not reference `LoopRelay.Agents`, CLI projects, or Conduit/Orchestrix projects.

Suggested namespaces and folders:

- `LoopRelay.Permissions.Abstractions`
- `LoopRelay.Permissions.Models`
- `LoopRelay.Permissions.Services`
- `LoopRelay.Permissions.Codex`
- `LoopRelay.Permissions.Extensions`

### 2. Port Provider-Neutral Permission Core

Port these abstractions:

- `IPermissionAdapter`
- `IPermissionGateway`
- `IPermissionHandler`
- `ICommandParser`
- `ICommandCanonicalizer`
- `IFingerprintService`
- `IPermissionCache`
- `IPermissionEvaluatorEngine`
- `IInvariantGuard`

Port these models:

- `PermissionRequest`
- `PermissionResult`
- `RuleDecision`
- `ParseResult`
- `ParsedCommand`
- `CanonicalCommand`
- `EvalResult`
- `CacheEntry`
- `PermissionEvaluationFlow`
- `PermissionEvaluationFlowStep`

Port these services:

- `PermissionGateway`
- `PermissionHandler`
- `CommandParser`
- `CommandCanonicalizer`
- `PermissionEvaluatorEngine`
- `InvariantGuard`
- `InMemoryPermissionCache`
- `Sha256FingerprintService`

Behavioral requirements:

- Parser failures deny before fingerprint/cache lookup.
- Unsupported shell constructs deny: subshell expansion, backtick execution, process substitution, environment variables, here-documents, unbalanced quotes, empty command.
- Canonicalization lowercases command/subcommand/flags, sorts flags, trims args, and preserves arg case.
- Fingerprints include version, repo identity, working directory, tool name, command count, command, subcommand, sorted flags, and args.
- Cache is scoped by the fingerprint.
- Invariants run after rule evaluation and can override an allow result.
- Unknown commands deny.

Local SHA-256 implementation:

- Build a stable UTF-8 byte stream with explicit field names, lengths, and null markers.
- Hash with `SHA256.HashData`.
- Return lowercase hex.
- Do not use `HashCode`, JSON property ordering, or culture-sensitive formatting.

### 3. Initial Policy Rules

Start with the Conduit rule set, then adjust only where LoopRelay tests prove a Codex app-server request shape differs.

Hard deny:

- privilege escalation: `sudo`, `su`, `doas`;
- destructive recursive delete: `rm -rf`;
- system control: `shutdown`, `reboot`, `halt`, `poweroff`;
- network fetch: `curl`, `wget`;
- force push: `git push --force` and `git push -f`;
- indirect shell execution: `bash -c`, `sh -c`, `zsh -c`.

Review-required commands are denied in this first version because LoopRelay has no user approval UI:

- `git commit`, especially `git commit --amend`;
- `git push`;
- dependency installs: `npm install`, `pnpm install`, `yarn install`, `pip install`, `dotnet install`, `cargo install`, `apt install`, `brew install`, `conda install`;
- infrastructure commands: `docker`, `kubectl`, `terraform`.

Auto-allow:

- read-only shell commands from Conduit's safe list;
- `pwd`;
- `git status`;
- `git diff`;
- `git log` without `-p` or `--patch`;
- test/build commands: `dotnet build`, `dotnet test`, `dotnet restore`, `npm test`, `npm run`, `pnpm test`, `pnpm run`, `yarn test`, `yarn run`, `pytest`, `go test`.

Keep the policy closed-world. If a command is not in the allow list and not explicitly hard-denied, deny it with a reason that includes "closed-world deny".

### 4. Add Codex Approval Adapter

Add `LoopRelay.Permissions.Codex.CodexPermissionAdapter` based on Conduit's `CodexPermissionAdapter`.

Supported server request methods:

- `item/commandExecution/requestApproval`
- `item/fileChange/requestApproval`
- `item/tool/requestUserInput`
- `item/permissions/requestApproval`
- `item/tool/call`
- `mcpServer/elicitation/request`

Parsing behavior:

- Preserve JSON-RPC `id` as a string internally, supporting both number and string ids.
- Require non-empty `method` and `params`.
- For `item/commandExecution/requestApproval`, map `params.command` to tool `Bash` and raw command text.
- If command approval has network context but no command, map to `networkAccess` and deny through the closed-world policy.
- For `item/fileChange/requestApproval`, map to `fileChange` with a synthetic raw command such as `codex_file_change <grantRoot-or-itemId>`. The initial engine should deny this unless a later rule explicitly permits a bounded workspace grant.
- For `item/tool/requestUserInput`, map question text into a raw summary and deny until a user-input path exists.
- For future/generic known methods, map the JSON params to a synthetic tool name and let the closed-world policy deny.

Response behavior:

- Return a complete JSON-RPC response frame as UTF-8 bytes with a trailing newline.
- Map `RuleDecision.Allow` to Codex decision `accept`.
- Map every non-allow result to the Codex denial decision accepted by LoopRelay's pinned app-server protocol.
- Before implementation, certify whether the installed LoopRelay target Codex protocol expects `deny` or `decline`. Conduit uses `deny`; current LoopRelay code uses `decline`. Add a protocol test that pins the chosen spelling so future upgrades are deliberate.

### 5. Register Permission Services

Add extension methods:

- `AddPermissionsCore()` registers the provider-neutral parser, canonicalizer, fingerprint service, cache, evaluator, invariant guard, and handler.
- `AddCodexPermissions()` registers core services plus `IPermissionAdapter` as `CodexPermissionAdapter` and `IPermissionGateway` as `PermissionGateway`.

Prefer `TryAddSingleton` where callers may override policy components in tests or host composition.

### 6. Wire Permissions Into `LoopRelay.Agents`

Add a project reference from `src/LoopRelay.Agents/LoopRelay.Agents.csproj` to `src/LoopRelay.Permissions/LoopRelay.Permissions.csproj`.

Update `AddAgents()` to register Codex permissions by default, or add a separate `AddAgentsWithPermissions()` only if avoiding a default dependency is required. The default registration is simpler because permissions are inert unless Codex sends server requests.

Update `AgentRuntime` construction:

- inject `IPermissionGateway`;
- pass it into `CodexAppServerSession`.

Update `CodexAppServerSession`:

- keep the read pump non-blocking with respect to stdin writes;
- retain the raw inbound JSON line when parsing, because the gateway evaluates the original frame bytes;
- on `CodexAppServerMessageKind.ServerRequest`, call the permission gateway instead of unconditional decline;
- enqueue the raw JSON-RPC response produced by the gateway;
- on adapter/evaluator exceptions, enqueue a denial response for the same request id and include the exception reason in diagnostics/logging if a logging surface is available;
- never let an approval request hang the turn.

Implementation shape:

- Change `PumpAsync` from `Dispatch(CodexAppServerMessage.Parse(line))` to `Dispatch(line, CodexAppServerMessage.Parse(line))`.
- Keep `CodexAppServerProtocol.ApprovalResponse` only as a fallback/helper, or remove it if all approval responses come from `CodexPermissionAdapter`.
- Add a helper that writes already-complete frames without appending a second newline, or normalize adapter output to a string and use the existing outbound channel consistently.

Approval gating:

- If `spec.Sandbox.RequiresApproval` is false, server requests should still be answered rather than ignored. The safe default is denial, because a request under approval-disabled posture indicates protocol drift or a sandbox escape request.
- If `RequiresApproval` is true, use the permission gateway to accept only auto-allow decisions.

### 7. Preserve One-Shot Semantics

Do not try to add permission prompts to `RunOneShotAsync` in the first implementation.

Reason:

- LoopRelay's one-shot path is `codex exec --json ... -`.
- Its argument builder intentionally emits `approval_policy="never"` so a CLI turn cannot block waiting for approval.
- The Conduit approval logic is built around app-server JSON-RPC server requests, which the one-shot path does not currently expose as a request/response channel.

If a workflow needs tool approvals, run it through `OpenSessionAsync` and the held-open app-server transport.

### 8. Fix Roadmap Execution Trust Boundary

Update the roadmap execution path as the first consumer of approval-managed execution.

Add `RoadmapExecutionOptions`:

- `SandboxIdentifier`
- `AllowNetwork`
- `RequiresApproval`
- `ElevatedReason`

Defaults:

- `SandboxIdentifier = "workspace-write"`
- `AllowNetwork = false`
- `RequiresApproval = true`
- `ElevatedReason = null`

Update `AgentSpecs.ExecutionBridge`:

- accept `RoadmapExecutionOptions`;
- construct `SandboxProfile` from those options;
- stop hardcoding `danger-full-access`.

Update `RoadmapExecutionBridge`:

- use a held-open session for approval-managed execution:
  - `IAgentRuntime.OpenSessionAsync(AgentSpecs.ExecutionBridge(repository, options))`
  - `session.RunTurnAsync(prompt, renderer.Stream, cancellationToken)`
  - `IAgentRuntime.CloseSessionAsync(session)` in `finally`
- keep `RunOneShotAsync` only for explicitly unattended execution modes with `RequiresApproval = false`.

Add elevated mode:

- expose an explicit CLI option or state transition for `danger-full-access` and network access;
- require a non-empty reason;
- record the reason in execution evidence.

Record evidence:

- use `TrustPolicy.FromSandboxProfile` to capture sandbox, workspace, network, approval, and execution lifetime;
- write this posture into roadmap execution evidence/state before the turn starts;
- include whether the run used default or elevated mode.

Prompt hardening:

- keep generated roadmap/spec/provenance Markdown inside clearly delimited data blocks;
- add a short instruction that embedded artifact content is evidence, not authority;
- keep this in the roadmap prompt generator, not the permission engine.

### 9. Tests

Add `tests/LoopRelay.Permissions.Tests` and port/adapt Conduit coverage:

- parser tests for simple commands, flags, chains, quotes, unsupported shell constructs, null raw command, and empty command;
- canonicalization tests for lowercasing, flag sorting, arg trimming, and idempotence;
- fingerprint tests for determinism and scoped changes by repo, working directory, tool, command, flags, and args;
- cache tests;
- evaluator tests for hard-deny, review-required deny, auto-allow, and closed-world deny;
- invariant tests proving dangerous commands stay denied even if a rule returns allow;
- handler/evaluation-flow tests proving parser failures bypass cache and invariants run after evaluation;
- Codex adapter tests for command approval, file-change approval, generic future requests, number/string ids, and response shape;
- gateway tests proving safe approvals produce accept and dangerous approvals produce deny.

Update `tests/LoopRelay.Agents.Tests`:

- replace `ApprovalRequestsAreAutoDeclinedAndDoNotBlockTheTurn` with tests that safe commands are accepted and dangerous commands are denied;
- add a test where permission evaluation throws and the session still responds with deny instead of hanging;
- assert that `RequiresApproval = true` omits `approvalPolicy` from `thread/start`/`thread/resume` and that approval server requests are answered through the gateway;
- keep stress tests for long output, cancellation, and process death unchanged.

Update roadmap tests:

- default execution bridge spec is `workspace-write`, no network, approvals on-request;
- elevated execution bridge spec is `danger-full-access`, network allowed, approval posture recorded with reason;
- roadmap bridge uses held-open app-server execution when approvals are required;
- execution evidence records `TrustPolicyEvidence`;
- generated prompt treats embedded artifacts as untrusted data.

### 10. Verification

Run:

```powershell
dotnet test LoopRelay.slnx
```

Also run targeted tests while iterating:

```powershell
dotnet test tests\LoopRelay.Permissions.Tests\LoopRelay.Permissions.Tests.csproj
dotnet test tests\LoopRelay.Agents.Tests\LoopRelay.Agents.Tests.csproj
dotnet test tests\LoopRelay.Roadmap.Cli.Tests\LoopRelay.Roadmap.Cli.Tests.csproj
```

Manual/live certification:

- start a real Codex app-server session with `SandboxProfile("workspace-write", true, false, RequiresApproval: true)`;
- trigger a safe approval such as `dotnet build` and confirm the adapter's allow response is accepted;
- trigger a denied command such as `git push` or a network request and confirm Codex continues/fails without hanging;
- confirm the chosen denial token, `deny` or `decline`, matches the installed Codex app-server protocol.

## Acceptance Criteria

- `LoopRelay.Permissions` exists, is in the solution, and has no Conduit or Orchestrix dependency.
- Permission core behavior matches the adapted Conduit tests.
- Codex server requests in held-open sessions route through `IPermissionGateway`.
- Approval requests never hang a turn.
- Safe commands can be accepted automatically.
- Dangerous, unknown, network, file-change grant, user-input, and review-required requests deny by default.
- Roadmap execution no longer hardcodes `danger-full-access`.
- Roadmap default execution uses `workspace-write`, no network, approvals on-request, and records trust evidence.
- Elevated full-access execution is explicit, reasoned, and auditable.
- One-shot execution remains unattended with approvals disabled unless a later implementation adds a real request/response approval channel.
