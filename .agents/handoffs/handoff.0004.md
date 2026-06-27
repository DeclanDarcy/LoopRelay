# Handoff: Phase 0 Agent Process Supervision Slice

Current milestone state: Phase 0 Runtime Foundation is active. This slice introduced a role-agnostic one-process supervision layer in `CommandCenter.Agents` without changing Execution provider contracts.

New state introduced:

- Added `IAgentProcessSupervisor` under `src/CommandCenter.Agents/Abstractions`.
- Added `AgentProcessSupervisor` under `src/CommandCenter.Agents/Services` to observe one `IAgentProcess` lifecycle, expose completion, invoke the existing compatibility `onExit` callback, cancel the process through disposal, and report terminal state and exit code.
- Added internal `AgentProcessStateMachine` so process supervision lifecycle transitions have one local transition home instead of scattered guard logic.
- Added `AgentProcessSupervisionResult` and extended `AgentProcessState` with `Stopping` and `Disposed`.
- Updated `AgentProcess` so process completion is an intrinsic process fact exposed through `Completion`; the old callback-specific exit observation path is no longer the source of truth.
- Updated `ProcessRunner.StartAsync` to create an `AgentProcessSupervisor` internally while preserving the existing `ProcessStartResult` compatibility surface used by Execution.
- Extended `AgentRuntimeBoundaryTests` to keep supervisor primitives in `CommandCenter.Agents`.
- Added `AgentProcessSupervisorTests` for completion observation, callback invocation, direct completion, cancellation, and failed completion.
- Updated `.agents/milestones/m0-runtime-foundation.md` to mark provider/process lifecycle primitives beyond the initial process handle complete.
- Rotated the previous active handoff to `.agents/handoffs/handoff.0003.md`.

Verification:

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~AgentProcessSupervisorTests` passed: 4 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~AgentRuntimeBoundaryTests` passed: 4 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~CodexExecutionProviderTests` passed: 5 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~GitServiceTests` passed: 7 tests.
- `dotnet build CommandCenter.slnx` passed.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` failed only in `ArchitecturalDecisionGovernanceTests.ReferentialGovernanceClaimsRemainReachable` because active `.agents/decisions/decisions.md` does not cite reachable M0.4 governance evidence. This was not fixed in this slice because decision rotation was reserved for a later user response.

Current limits:

- Supervision is scoped to one process lifecycle only; it does not own session registry behavior, routing, retries, health policy, stream contracts, telemetry aggregation, or repository coordination.
- `ProcessRunner.StartAsync` still returns `ProcessStartResult` for Execution compatibility.
- Stream/event primitives remain open and should be based on supervisor lifecycle facts.
- Full backend certification is blocked by the active decision governance-link failure until decision rotation is authorized.

Next suggested slice:

- Resolve the active decision governance checkpoint first if authorized, then continue Phase 0 with stream/event primitives in `CommandCenter.Agents` that project supervisor lifecycle facts without defining lifecycle semantics.
