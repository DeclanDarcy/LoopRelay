# LoopRelay Prune Candidates

Audit date: 2026-07-06

Scope: code and prompt assets not meaningfully used by any of the three CLI entry points:

- `src/LoopRelay.Cli`
- `src/LoopRelay.Plan.Cli`
- `src/LoopRelay.Roadmap.Cli`

Tests and docs were treated as non-production usage. A candidate stays in this list when it has no CLI runtime path, is only exercised by tests, or is statically referenced behind a branch that the CLI state machine no longer reaches.

Verification performed:

- `dotnet build LoopRelay.slnx -c Release --nologo`
- Result: passed with 0 warnings and 0 errors

## High Confidence Deletes

### Dead Core Prompt Assets

These prompt files have no production references from the three CLIs.

- `src/LoopRelay.Core/Prompts/GetNextDecisions.prompt`
- `src/LoopRelay.Core/Prompts/StartDecisionSession.prompt`
- `src/LoopRelay.Core/Prompts/StartDecisionSessionFromTransfer.prompt`
- `src/LoopRelay.Core/Prompts/Projections/ProjectionForAdversarialPlanReview.prompt`
- `src/LoopRelay.Core/Prompts/Projections/ProjectionForDecisionSession.prompt`

Evidence:

- `GetNextDecisions` has no source references outside docs/debt notes.
- `StartDecisionSession` and `StartDecisionSessionFromTransfer` are only mentioned by comments/tests that say the separate seed turn is gone.
- `ProjectionForAdversarialPlanReview` and `ProjectionForDecisionSession` are not registered in `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`.
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptCatalog.cs` has no metadata/render cases for those two projection prompts.

Suggested action:

- Delete the five prompt files.
- Run `dotnet build LoopRelay.slnx -c Release --nologo`.
- If generated prompt artifacts are cached by the prompt generator, clear/rebuild generated output as needed.

### Test-Only CLI Artifact Helper

Candidate:

- `src/LoopRelay.Cli/LoopArtifacts.cs`
  - `RotateLiveDecisionsAsync`

Evidence:

- No production source calls this method.
- Current loop retirement path uses `RetireLiveDecisionsAsync` after execution consumes `decisions.md`.
- The method is covered only by `tests/LoopRelay.Cli.Tests/LoopArtifactsTests.cs`.

Suggested action:

- Delete `RotateLiveDecisionsAsync`.
- Delete or rewrite the test that exists only for this method.

### Unused Roadmap Artifact Path Predicate

Candidate:

- `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`
  - `IsMilestoneSpecPath`

Evidence:

- No production or test source calls it.

Suggested action:

- Delete the method.

## High Confidence API Surface Prune

### Unused Artifact Store Members

Candidates:

- `src/LoopRelay.Core/Artifacts/IArtifactStore.cs`
  - `ReadAs<T>`
  - `ListDirectoriesAsync`
- `src/LoopRelay.Core/Artifacts/FileSystemArtifactStore.cs`
  - `ReadAs<T>`
  - deserialization-cache fields/helpers used only by `ReadAs<T>`
  - `ListDirectoriesAsync`
- `tests/TestSupport/MemoryArtifactStore.cs`
  - matching `ReadAs<T>` and `ListDirectoriesAsync` implementations

Evidence:

- No CLI production source calls `ReadAs<T>` or `ListDirectoriesAsync`.
- Current CLI code reads strings through `ReadAsync` and lists files through `ListAsync`.
- `FileSystemArtifactStore` carries substantial deserialization-cache code solely to support the unused `ReadAs<T>` member.

Suggested action:

- Remove the interface members.
- Remove `FileSystemArtifactStore.ReadAs<T>` and its `deserializedCache` support types.
- Remove `ListDirectoriesAsync` from concrete stores and test decorators.
- Run all Core and CLI tests.

## Medium Confidence Deletes

### Dead First-Pass Decision Prompt Branch

Candidates:

- `src/LoopRelay.Core/Prompts/GenerateSystemPromptForFirstExecutionAgent.prompt`
- `src/LoopRelay.Cli/DecisionSession.cs`
  - `BuildProposalPromptAsync` branch for `handoff is null`
- tests that assert first-pass decision proposal behavior

Evidence:

- `src/LoopRelay.Cli/LoopRunner.cs` skips `decision.RunAsync` when no handoff exists.
- `src/LoopRelay.Cli/DecisionSession.cs` only reaches `GenerateSystemPromptForFirstExecutionAgent.Text` when `handoff is null`.
- Therefore the normal CLI loop never invokes the first-pass decision branch.

Why medium confidence:

- The branch is still statically valid and may have been retained intentionally as defensive behavior for direct `DecisionSession` tests or unusual future composition.

Suggested action:

- Decide whether direct `DecisionSession.RunAsync` without a handoff is a supported component-level behavior.
- If not supported, delete the prompt and simplify `BuildProposalPromptAsync` to require a handoff.
- Update tests to assert the loop-level first pass runs `StartExecution` directly instead.

### Agent Session Registry Backend-Style API

Candidates:

- `src/LoopRelay.Agents/Services/AgentSessionRegistry.cs`
  - `Count`
  - `TryGet`
  - `ForRepository`
  - `DisposeRepositoryAsync`

Evidence:

- The three CLIs reach the registry through `AgentRuntime` and final disposal.
- Production code needs `TryAdd`, `RemoveAsync`, and `DisposeAsync`.
- The listed members are only used by registry-focused tests.

Why medium confidence:

- `Count` is useful for leak tests even if it is not production behavior.
- This is a public class in a shared assembly, so removing members is an API break for any out-of-solution consumer.

Suggested action:

- If `LoopRelay.Agents` is CLI-internal, delete the unused members and update tests to assert behavior through `AgentRuntime`.
- If public API stability matters, leave them or mark them as non-CLI support surface.

## Review Before Deleting

### Split Child Promotion Validator

Candidate:

- `src/LoopRelay.Roadmap.Cli/InvariantValidator.cs`
  - `ValidateSplitChildPromotionAsync`

Evidence:

- No production source calls the method.
- Only `tests/LoopRelay.Roadmap.Cli.Tests/InvariantValidatorTests.cs` references it directly.
- The current split flow validates the generated split bundle, writes the selected child, writes a split-family record, and then promotes the selected child through `PromoteActiveEpicAsync`.

Why review first:

- The method may represent a missing intended resume/unblock validation, not just dead code.
- It checks that a child path belongs to a split family before promotion. If that invariant is still desired after interruptions, it may need to be wired into the state machine rather than deleted.

Suggested action:

- Decide whether split-child promotion can occur on resume/unblock from persisted state.
- If yes, wire this validation into that path.
- If no, delete the method and its direct tests.

## Not Candidates

These looked suspicious in simple reference scans but are production-reachable:

- `src/LoopRelay.Agents/Extensions/ServiceCollectionExtensions.cs`
  - Reached by all three `Program.cs` files through `services.AddAgents()`.
- `CodexAgentProcessLauncher`, `EnvironmentAgentExecutableResolver`, `ProcessRunner`, `DeterministicAgentTokenEstimator`, `CodexEventTurnBoundaryDetector`
  - Reached through `AddAgents()` registration.
- `AgentSession`
  - Still used by the active `RunOneShotAsync` path for sandboxed one-shot prompts.
- telemetry recorder/sink/probe types under `src/LoopRelay.Cli`
  - Production-reachable by default through `SessionTelemetryComposition.CreateRecorder`; disabled only when `LoopRelay_SESSION_LOG=0` or `false`.
- `LoopRelay.Orchestration.Primitives`
  - Still used by `LoopRelay.Cli` and `LoopRelay.Plan.Cli` for artifact paths, decision-session resume, routing, cost model, and sandbox workspace creation.

## Suggested Prune Order

1. Delete the high-confidence dead prompt assets.
2. Remove the tiny unused helper methods: `RotateLiveDecisionsAsync` and `IsMilestoneSpecPath`.
3. Prune unused `IArtifactStore` members and the associated `FileSystemArtifactStore` deserialization cache.
4. Decide whether to delete or keep the first-pass decision prompt branch.
5. Decide whether `AgentSessionRegistry` is public API or CLI-internal, then prune accordingly.
6. Decide whether `ValidateSplitChildPromotionAsync` should be wired into roadmap resume/unblock or deleted.

Run after each phase:

- `dotnet build LoopRelay.slnx -c Release --nologo`
- `dotnet test tests/LoopRelay.Core.Tests/LoopRelay.Core.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Agents.Tests/LoopRelay.Agents.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Plan.Cli.Tests/LoopRelay.Plan.Cli.Tests.csproj -c Release --nologo`
- `dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj -c Release --nologo`
