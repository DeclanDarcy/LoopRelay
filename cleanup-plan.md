# LoopRelay CLI Orphan Cleanup Plan

## Goal

Remove or isolate code that is not meaningfully used by the three CLI entry points:

- `src/LoopRelay.Cli`
- `src/LoopRelay.Plan.Cli`
- `src/LoopRelay.Roadmap.Cli`

The audit treats tests and docs as non-production usage. Code should only stay in the CLI dependency graph when a CLI directly reaches it at runtime or when it is an intentional shared primitive.

## Ground Rules

- Keep cleanup behavior-preserving for all three CLIs.
- Remove project references before deleting code so build errors expose hidden dependencies.
- Move test doubles into test projects instead of keeping them in production assemblies.
- Prefer small shared primitives over a broad `LoopRelay.Orchestration` dependency.
- Run `dotnet build LoopRelay.slnx -c Release --nologo` after each phase.

## Phase 1: Remove The Obvious Unused Reference

`LoopRelay.Roadmap.Cli` references `LoopRelay.Orchestration`, but the roadmap CLI source has no production usage of `LoopRelay.Orchestration` symbols.

Tasks:

- Remove the `LoopRelay.Orchestration` project reference from `src/LoopRelay.Roadmap.Cli/LoopRelay.Roadmap.Cli.csproj`.
- Build the solution.
- Run `dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj -c Release --nologo`.

Expected result:

- Roadmap CLI builds and tests without the orchestration dependency.
- Transitive `Execution`, `DecisionSessions`, `Decisions`, `Continuity`, `Reasoning`, and `Persistence.Sqlite` assemblies no longer ship with the roadmap CLI unless pulled by another reference.

## Phase 2: Extract The Small CLI-Shared Orchestration Surface

`LoopRelay.Cli` and `LoopRelay.Plan.Cli` use only a small subset of `LoopRelay.Orchestration`:

- `OrchestrationArtifactPaths`
- `IDecisionSessionResumeStore`
- `FileDecisionSessionResumeStore`
- `NullDecisionSessionResumeStore`
- `DecisionSessionResumeState`
- `IDecisionSessionRouter`
- `DecisionSessionRouter`
- `DecisionSessionRouterOptions`
- `DecisionTransferPolicy`
- `DecisionRoute`
- `RouterInputs`
- `IDecisionCostModel`
- `EffectiveTokenCostModel`
- `DecisionCostForecast`
- `ISandboxWorkspaceFactory`
- `TempSandboxWorkspaceFactory`

Tasks:

- Create a narrow shared project or move these primitives into `LoopRelay.Core` if the ownership still makes sense.
- Update `LoopRelay.Cli` and `LoopRelay.Plan.Cli` to reference the narrow shared surface.
- Remove their direct `LoopRelay.Orchestration` project references.
- Build and run CLI test projects:
  - `tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj`
  - `tests/LoopRelay.Plan.Cli.Tests/LoopRelay.Plan.Cli.Tests.csproj`

Expected result:

- The two CLIs no longer depend on the legacy orchestration host.
- The only shared code left in their graph is code they actually call.

## Phase 3: Quarantine Legacy Orchestration Host Code

After Phase 2, the following code should no longer be in any CLI dependency graph:

- `RepositoryOrchestrator`
- `RepositoryOrchestratorRegistry`
- `OrchestratorStreamChannel`
- `OrchestratorStreamEvent`
- `AddOrchestration`
- `GitPlanArtifactPublisher`
- `OperationalContextHealthMonitor`
- `OrchestrationFeatureFlags`
- `OrchestratorShutdownHostedService`
- API/stream DTOs such as `PlanWriteRequest`, `PlanReviseRequest`, `PlanStatus`, `PlanRunAcknowledgement`, `DecisionSubmitRequest`, `ConversationProjection`, and `ActiveRunSnapshot`

Tasks:

- Decide whether this host surface is still needed outside the three CLIs.
- If not needed, delete the `LoopRelay.Orchestration` project and its tests.
- If still needed, rename or relocate it so it is clearly backend/legacy host code rather than CLI-shared orchestration.

Expected result:

- CLI dependency graph is not polluted by backend host surfaces.
- Remaining host code has explicit ownership.

## Phase 4: Retire Unused Domain Subsystems From CLI Distribution

The CLIs do not call these registration methods in production:

- `AddExecution`
- `AddDecisionSessions`
- `AddDecisions`
- `AddContinuity`
- `AddReasoning`
- `AddSqlitePersistence`

Tasks:

- After Phase 2, verify none of these projects are transitively referenced by any CLI publish output.
- If the solution is now CLI-only, remove the projects and their tests.
- If they are retained for a non-CLI host, keep them out of CLI project references and document that ownership.

Expected result:

- CLI publishes include only runtime-required assemblies.
- Legacy backend/domain subsystems are either removed or isolated.

## Phase 5: Move Test-Only Production Types

These types appear to be production-placed test doubles or test-only helpers:

- `src/LoopRelay.Execution/Modules/FakeExecutionProvider.cs`
- `src/LoopRelay.Execution/Modules/NoopExecutionProvider.cs`
- `src/LoopRelay.Decisions/Services/InMemoryDecisionRepository.cs`
- `src/LoopRelay.Persistence.Sqlite/MemorySqliteSnapshotCache.cs`
- `src/LoopRelay.Reasoning/Services/ReasoningReferenceFactory.cs`

Tasks:

- Move `FakeExecutionProvider`, `InMemoryDecisionRepository`, and `MemorySqliteSnapshotCache` into the relevant test projects or shared test infrastructure.
- Delete `NoopExecutionProvider` if it remains unreferenced.
- Reassess `ReasoningReferenceFactory`; keep only if a production caller is introduced, otherwise move to tests.

Expected result:

- Production assemblies no longer carry test-only support code.

## Phase 6: Prune Backend-Era Core Models If No Host Remains

These `LoopRelay.Core` types are not meaningfully used by the three CLIs, though some may still support legacy backend/domain tests:

- `ApplicationConfigurationStore`
- `RepositoryService`
- `PlanningService`
- `ArtifactInventory`
- `MilestoneProgress`
- `MilestoneProgressRollup`
- `RepositoryContinuitySummary`
- `RegisterRepositoryRequest`
- `SaveArtifactContentRequest`

Tasks:

- Re-check references after Phases 2 through 4.
- Delete DTOs and services with no production owner.
- Keep only truly shared CLI primitives: repository identity/path, artifact paths/store, prompt catalog, and prompt provenance.

Expected result:

- `LoopRelay.Core` reflects actual CLI-shared code instead of old app/backend contracts.

## Verification Checklist

- `dotnet build LoopRelay.slnx -c Release --nologo`
- `dotnet test tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Plan.Cli.Tests/LoopRelay.Plan.Cli.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj -c Release --nologo`
- Publish each CLI and inspect copied assemblies:
  - `publish-cli.bat`
  - `publish-plan-cli.bat`
  - `publish-roadmap-cli.bat`

## Stop Conditions

Stop and reassess if any of these happen:

- A CLI test begins relying on backend host behavior rather than direct CLI behavior.
- Removing `LoopRelay.Orchestration` from a CLI requires pulling in most of the orchestration project elsewhere.
- A supposedly test-only type is required by a production composition root.
- Publish output still includes large legacy subsystem assemblies after direct references are removed.
