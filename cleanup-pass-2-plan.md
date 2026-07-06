# LoopRelay CLI Orphan Cleanup Plan - Pass 2

## Goal

Remove or relocate code that is not meaningfully used by any of the three CLI
entry points after the first cleanup pass:

- `src/LoopRelay.Cli`
- `src/LoopRelay.Plan.Cli`
- `src/LoopRelay.Roadmap.Cli`

This pass focuses on smaller orphan clusters inside the current CLI-shaped
solution. Tests and docs do not count as production usage. A type should stay in
`src` only when a CLI can reach it at runtime, it is an intentional public
primitive, or it is required by the build/generator pipeline.

## Ground Rules

- Keep all three CLI behaviors unchanged.
- Prefer deletion over moving when code has no production owner.
- Move test-only helpers into test projects or shared test support.
- Update docs when they claim an invariant that the CLI code no longer
  implements.
- Remove API members before deleting implementation clusters so compile errors
  expose hidden dependencies.
- After each phase, run a focused build/test before continuing.

## Phase 1: Remove Stale Core Contracts

The following Core types are not referenced by production code or tests:

- `src/LoopRelay.Core/Repositories/Repository.cs`
  - `RepositoryAvailability`
- `src/LoopRelay.Core/Prompts/PromptProvenance.cs`
  - `PromptProvenance`
  - `PromptSessionRole`

Tasks:

- Delete `RepositoryAvailability`.
- Delete `PromptProvenance.cs` if no non-CLI consumer exists in this solution.
- Update docs that still describe `PromptProvenance` as active CLI behavior:
  - `docs/prompt-architecture.md`
  - `docs/orchestration-loop-governance.md`
  - `docs/architecture.md`
  - `docs/architectural-mechanisms.md`
  - `docs/contracts.md`
- Update docs that still name `RepositoryAvailability` as a live contract:
  - `docs/contract-endpoint-catalog.md`
- Build `LoopRelay.Core`.

Expected result:

- Core no longer advertises unused prompt provenance or repository availability
  contracts.
- Documentation distinguishes current CLI provenance from retired backend-era
  provenance.

## Phase 2: Remove The Unused Process Start API

The CLIs use only:

- `IProcessRunner.RunAsync`
- `IProcessRunner.StartInteractiveAsync`

No production code calls the callback-style `StartAsync` branch.

Tasks:

- Remove `IProcessRunner.StartAsync`.
- Remove `ProcessRunner.StartAsync`.
- Delete `src/LoopRelay.Agents/Models/ProcessStartResult.cs`.
- Update fake process runners in tests to drop their `StartAsync`
  implementations.
- Build `LoopRelay.Agents` and all CLI test projects.

Expected result:

- The Agents process runner exposes only the two execution modes the CLIs
  actually use.
- Test doubles stop carrying an unused method.

## Phase 3: Delete The Process Supervisor/Event Cluster

After Phase 2, the following cluster should have no production reachability:

- `src/LoopRelay.Agents/Abstractions/IAgentProcessSupervisor.cs`
- `src/LoopRelay.Agents/Services/AgentProcessSupervisor.cs`
- `src/LoopRelay.Agents/Services/AgentProcessEventStream.cs`
- `src/LoopRelay.Agents/Services/AgentProcessStateMachine.cs`
- `src/LoopRelay.Agents/Models/AgentProcessEvent.cs`
- `src/LoopRelay.Agents/Models/AgentProcessEventKind.cs`
- `src/LoopRelay.Agents/Models/AgentProcessSupervisionResult.cs`

Tasks:

- Delete the cluster.
- Delete or rewrite `tests/LoopRelay.Agents.Tests/AgentProcessSupervisorTests.cs`.
- Confirm no CLI runtime path lost process cleanup behavior:
  - `AgentProcess` still owns completion observation.
  - `ProcessRunner.StartInteractiveAsync` still drains stderr.
  - `AgentRuntime.CloseSessionAsync` still disposes sessions.
- Run `tests/LoopRelay.Agents.Tests`.

Expected result:

- Agents keeps the process primitives used by Codex sessions.
- The unused supervision/event abstraction layer is gone.

## Phase 4: Relocate Test-Only Production Helpers

These production types are not used by the CLIs at runtime:

- `src/LoopRelay.Core/Artifacts/MemoryArtifactStore.cs`
- `src/LoopRelay.Agents/Services/SentinelTurnBoundaryDetector.cs`

Tasks:

- Move `MemoryArtifactStore` into shared test support, or duplicate small local
  test stores in the test projects that need them.
- Move `SentinelTurnBoundaryDetector` into `tests/LoopRelay.Agents.Tests` if
  `AgentSessionTests` still need sentinel-style line completion.
- Update namespaces/usings in tests.
- Delete XML documentation references that imply the sentinel detector is a
  current production transport.

Expected result:

- Production assemblies no longer ship test fixtures.
- Tests keep their simple in-memory and sentinel helpers.

## Phase 5: Remove The Unused Telemetry Sink

Telemetry disablement currently uses `NullSessionTelemetryRecorder`, not
`NullSessionTelemetrySink`.

Tasks:

- Delete `NullSessionTelemetrySink` from
  `src/LoopRelay.Cli/SessionTelemetrySink.cs`.
- Delete or replace the unit test that only asserts the sink no-ops.
- Update any stale telemetry design docs that list `NullSessionTelemetrySink` as
  production behavior.

Expected result:

- The telemetry surface has one no-op abstraction: `NullSessionTelemetryRecorder`.

## Phase 6: Re-run The Orphan Sweep

Tasks:

- Search production references for each deleted or moved symbol.
- Re-run the declaration/reference sweep used for this audit.
- Review any newly exposed single-use abstractions, especially in:
  - `src/LoopRelay.Agents`
  - `src/LoopRelay.Core`
  - `src/LoopRelay.Cli`
- Add a short note to this file if any candidate is intentionally retained.

Expected result:

- Remaining low-reference types are either file-local helpers, serializer DTOs,
  or explicitly retained production seams.

Execution note (2026-07-06):

- Retained `CodexAgentProcessLauncher`, `EnvironmentAgentExecutableResolver`,
  and `ServiceCollectionExtensions` because the three CLIs reach them through
  `services.AddAgents()`.
- Retained `NullDecisionSessionResumeStore` and `TaskDelayScheduler` as small
  production seams used by CLI composition.
- Retained `RoadmapExecutionBridge` and `RoadmapExecutionEvidenceArtifact` as
  roadmap CLI execution transport/evidence surfaces.
- Historical plan docs that mention `MemoryArtifactStore` were left as
  historical notes; production no longer ships the type.

## Stale Documentation Worklist

Tackle documentation in the same cleanup pass so docs do not continue to
describe retired contracts as current CLI behavior.

Prompt provenance docs to revise or delete:

- `docs/prompt-architecture.md`
- `docs/orchestration-loop-governance.md`
- `docs/architecture.md`
- `docs/final-acceptance.md`
- `docs/contracts.md`
- `docs/architectural-mechanisms.md`

Required update:

- Remove claims that every CLI turn records `PromptProvenance` /
  `PromptSessionRole`.
- Replace them with the current mechanisms the CLIs actually use:
  - Roadmap projection and derived-artifact provenance.
  - Roadmap transition input snapshots and journals.
  - Main CLI session telemetry.

Repository availability docs to revise or delete:

- `docs/contract-endpoint-catalog.md`

Required update:

- Remove or mark backend-retired rows that describe `RepositoryAvailability`.
- Do not leave `RepositoryAvailability` documented as a live CLI contract unless
  a production owner is reintroduced.

Telemetry docs to revise:

- `docs/superpowers/specs/2026-07-01-cli-loop-session-telemetry-log-design.md`
- `docs/superpowers/plans/2026-07-01-cli-loop-session-telemetry-log.md`

Required update:

- Remove `NullSessionTelemetrySink` from the active design.
- State that disabled telemetry uses `NullSessionTelemetryRecorder`.

Historical/test-plan docs to review after moving test helpers:

- `docs/refactor-lazy-sqlite.md`
- `docs/superpowers/plans/2026-07-04-cli-decision-session-resume.md`
- `docs/superpowers/plans/2026-07-01-archive-operational-delta.md`

Required update:

- If these docs remain active implementation guidance, update references to
  `MemoryArtifactStore` after it moves to test support.
- If they are historical plans only, mark them historical or leave them alone
  deliberately; do not rewrite history without adding value.

## Verification Checklist

- `dotnet build LoopRelay.slnx -c Release --nologo`
- `dotnet test tests/LoopRelay.Core.Tests/LoopRelay.Core.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Agents.Tests/LoopRelay.Agents.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Plan.Cli.Tests/LoopRelay.Plan.Cli.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj -c Release --nologo`
- Publish each CLI and inspect copied assemblies:
  - `publish-cli.bat`
  - `publish-plan-cli.bat`
  - `publish-roadmap-cli.bat`

## Stop Conditions

Stop and reassess if any of these happen:

- Removing `PromptProvenance` breaks generated prompt code or an external
  contract that still has an owner.
- Removing `StartAsync` exposes an untested CLI runtime path that still needs
  callback-style process execution.
- Moving `MemoryArtifactStore` forces broad test infrastructure churn unrelated
  to orphan cleanup.
- Deleting the process supervisor cluster changes child-process teardown,
  stderr draining, or session disposal behavior.
