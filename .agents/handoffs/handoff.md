# Handoff

## Slice Summary

- Continued Epic 2 M2B.3: restart and orphan recovery for persisted active execution sessions.
- Added `ExecutionSessionService.RecoverAsync`.
- Added `ExecutionSessionRecoveryHostedService` and registered it at backend startup.
- Startup recovery now loads persisted sessions and mutates only sessions whose repository execution state is `Executing`.
- Because Codex reattach is unsupported in this phase, unrecoverable active sessions are marked `Failed`.
- The repository execution state for those sessions is also marked `Failed`.
- The stable failure reason is `Active provider process could not be reattached after backend restart.`
- Recovery preserves existing provider executable path, provider process id, provider start time, prompt metadata, repository snapshot, and previous handoff snapshot.
- Non-executing sessions restore without recovery mutation.
- Updated M2 milestone checklist to reflect the supported recovery behavior and remove the obsolete reattachable-provider certification item for this phase.

## Files Changed

- `.agents/milestones/m2-session-integration.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0006.md`
- `src/CommandCenter.Backend/Execution/ExecutionSessionRecoveryHostedService.cs`
- `src/CommandCenter.Backend/Execution/ExecutionSessionService.cs`
- `src/CommandCenter.Backend/Execution/IExecutionSessionService.cs`
- `src/CommandCenter.Backend/Program.cs`
- `tests/CommandCenter.Backend.Tests/ArtifactRotationServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionSessionServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/RepositoryProjectionServiceTests.cs`

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed: 81 tests.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## High-Leverage Decisions

- Recovery is explicit and startup-owned: `ExecutionSessionRecoveryHostedService` invokes `RecoverAsync` when the backend starts instead of making normal query paths mutate state.
- Codex reattach remains unsupported for M2B.3, so persisted active sessions fail deterministically instead of remaining indefinitely active.
- The failure reason is a stable exact string so UI, tests, and later certification can key off one contract.
- Recovery only targets repository state `Executing`; failed, cancelled, awaiting-review, and other non-executing session records remain unchanged.

## Recommended Next Slice

- Move to the M2 UI closeout slice:
  - Display provider name and process-start failure messages in the execution workspace.
  - Surface orphaned-session failure details after backend restart.
  - Keep stdout/stderr streaming, SSE, completion detection, and handoff validation deferred to M3 and M4.
