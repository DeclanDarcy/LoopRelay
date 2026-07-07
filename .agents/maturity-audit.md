# Codebase Maturity Audit

## 1. Executive Summary

The codebase is a useful but not yet open-source-ready .NET CLI workspace. It has real maturation in the form of separate `src/` and `tests/` trees, shared `LoopRelay.Agents`, `LoopRelay.Core`, and `LoopRelay.Orchestration.Primitives` projects, an atomic `IArtifactStore` implementation, fast unit tests, and explicit roadmap persistence/provenance machinery. A local validation probe passed: `dotnet test LoopRelay.slnx --no-restore --nologo` reported 803 passed and 1 skipped on this Windows machine with .NET SDK 10.0.301.

Open-source readiness is low. The repository has no root README, license, contribution guide, code of conduct, CI workflow, SDK pin, `.editorconfig`, `.gitmodules`, package lock, or cross-platform install story. The build also depends on a sibling checkout via `src/LoopRelay.Core/LoopRelay.Core.csproj`, so the passing local test result is not reproducible from a clean clone.

Refactoring readiness is partial. The shared libraries are a good start, and the tests give a useful safety net. However, app-local infrastructure remains duplicated across console projects, especially console rendering, git/submodule publishing, argument parsing, artifact facades, and agent-spec factories. `LoopRelay.Roadmap.Cli` also contains a 2,000+ line state-machine class inside the console project, which makes the desired merge unsafe until workflow orchestration is moved behind application-level contracts.

Three-app consolidation readiness is not ready. The current separation is still valuable because each app owns a distinct workflow shape and risk profile. A single CLI should be introduced only after shared command, configuration, diagnostics, artifact, git, and trust-policy boundaries are explicit.

The most important architectural risk is that the console projects are still application hosts, workflow orchestrators, and infrastructure owners at the same time. The most important near-term opportunity is to extract the repeated CLI infrastructure and application workflow contracts without changing behavior, guarded by the existing tests.

## 2. Current Topology

| Area | Current State | Evidence | Implication |
| ---- | ------------- | -------- | ----------- |
| Solution | One solution file with six production projects and six test projects. | `LoopRelay.slnx` | The repo is already more organized than a single flat project, but the console apps still own too much workflow behavior. |
| Console apps | Three executable projects: main loop, planning pipeline, and roadmap workflow. | `src/LoopRelay.Cli/LoopRelay.Cli.csproj`, `src/LoopRelay.Plan.Cli/LoopRelay.Plan.Cli.csproj`, `src/LoopRelay.Roadmap.Cli/LoopRelay.Roadmap.Cli.csproj` | Desired consolidation should target command orchestration, not a mechanical file merge. |
| Shared code | Shared agent runtime, artifact store, repository model, and orchestration primitives exist. | `src/LoopRelay.Agents/LoopRelay.Agents.csproj`, `src/LoopRelay.Core/LoopRelay.Core.csproj`, `src/LoopRelay.Orchestration.Primitives/LoopRelay.Orchestration.Primitives.csproj` | Useful seams exist and should be preserved, but some shared candidates are still app-local. |
| Tests | Every production project has a matching xUnit project; tests are fast and broad at component level. | `tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj`, `tests/LoopRelay.Plan.Cli.Tests/LoopRelay.Plan.Cli.Tests.csproj`, `tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj` | There is enough characterization coverage to support staged refactoring, but not enough packaging/process-level validation. |
| Documentation | Rich internal architecture docs exist, but no root onboarding or public project contract exists. | `docs/architecture.md`, `docs/contracts.md`, `LoopRelay.slnx` | Outside users will see detailed internal architecture before they see how to install, run, test, or contribute. |
| Build/release scripts | Release entry points are Windows batch files with local machine output defaults. | `publish-cli.bat`, `publish-plan-cli.bat`, `publish-roadmap-cli.bat` | Packaging is local-operator automation, not an open-source install/release process. |
| Artifact state | Shared `.agents` paths exist for loop/plan, while roadmap has its own wider path catalog and structured JSON persistence. | `src/LoopRelay.Orchestration.Primitives/OrchestrationArtifactPaths.cs`, `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`, `src/LoopRelay.Roadmap.Cli/StructuredPersistence.cs` | Persistence ownership is improving, but consolidation needs one explicit artifact namespace policy. |
| Repository metadata | `.agents` is tracked as a gitlink, but no `.gitmodules` file is present. | `.gitignore`, `src/LoopRelay.Cli/AgentsSubmodulePublisher.cs`, `src/LoopRelay.Plan.Cli/AgentsSubmodulePublisher.cs` | Fresh clones cannot initialize the assumed submodule without additional undocumented setup. |

## 3. Console App Inventory

### LoopRelay.Cli

- Project path: `src/LoopRelay.Cli/LoopRelay.Cli.csproj`
- Entry point: `src/LoopRelay.Cli/Program.cs`
- Primary responsibilities: Runs the execution/decision loop, publishes `.agents`, manages decision-session resume state, records telemetry, handles quota retry behavior, commits/pushes real repository changes, and stops on epic completion, cancellation, failure, or stall.
- Command/argument model: Single positional `REPO_DIR` parsed by `src/LoopRelay.Cli/CliArguments.cs`; no subcommands or help flags.
- State/config usage: Reads/writes `.agents` artifacts through `src/LoopRelay.Cli/LoopArtifacts.cs`; persists local resume/telemetry state under `.LoopRelay` through `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionResumeStore.cs`; uses `CODEX_EXECUTABLE` and `LoopRelay_DECISION_RESUME`.
- Shared code usage: Uses `LoopRelay.Agents`, `LoopRelay.Core`, and `LoopRelay.Orchestration.Primitives`.
- Test coverage: Strong component coverage in `tests/LoopRelay.Cli.Tests`, including runner, decision session, execution step, submodule publishing, commit gate, telemetry, quota parsing, and argument parsing.
- Open-source concerns: No public usage contract, no install story, execution can require broad local authority, and success can block on `Console.ReadKey`.
- Consolidation concerns: Its loop behavior should become an application service behind a `loop run` command, not be merged by moving `Program.cs` logic into a larger switch.

### LoopRelay.Plan.Cli

- Project path: `src/LoopRelay.Plan.Cli/LoopRelay.Plan.Cli.csproj`
- Entry point: `src/LoopRelay.Plan.Cli/Program.cs`
- Primary responsibilities: Runs a planning pipeline: preflight, plan authoring, review, revision, seeded sandbox one-shots, `.agents` publishing, and parent gitlink recording.
- Command/argument model: Single positional `REPO_DIR` parsed by `src/LoopRelay.Plan.Cli/CliArguments.cs`; no subcommands or help flags.
- State/config usage: Reads/writes `.agents` artifacts through `src/LoopRelay.Plan.Cli/PlanArtifacts.cs`; existing planning artifacts are surfaced through preflight violations for operator cleanup.
- Shared code usage: Uses `LoopRelay.Agents`, `LoopRelay.Core`, and `LoopRelay.Orchestration.Primitives`.
- Test coverage: Strong component coverage in `tests/LoopRelay.Plan.Cli.Tests`, including pipeline, preflight, one-shot steps, sandboxing, and submodule publishing.
- Open-source concerns: Publish script is Windows-only.
- Consolidation concerns: Planning should become a subcommand such as `plan run`, but its preflight semantics should remain app-specific until a unified command contract exists.

### LoopRelay.Roadmap.Cli

- Project path: `src/LoopRelay.Roadmap.Cli/LoopRelay.Roadmap.Cli.csproj`
- Entry point: `src/LoopRelay.Roadmap.Cli/Program.cs`
- Primary responsibilities: Runs roadmap workflow commands, projection generation/caching, prompt contracts, state persistence, resume/unblock planning, artifact promotion, lifecycle/decision ledgers, execution prompt generation, execution bridge routing, and invariant validation.
- Command/argument model: Hand-rolled `status|run|unblock` command enum in `src/LoopRelay.Roadmap.Cli/CliArguments.cs`; accepts both leading and trailing command forms.
- State/config usage: Uses `.agents/state.json`, `.agents/state.md`, projection manifests, lifecycle ledgers, transition journals, evidence directories, active epic/specs/context/prompt paths, and Project Context files through `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs`.
- Shared code usage: Uses `LoopRelay.Agents` and `LoopRelay.Core`; does not reference `LoopRelay.Orchestration.Primitives`.
- Test coverage: Broad component coverage in `tests/LoopRelay.Roadmap.Cli.Tests`, including state store, state machine flows, resume/unblock planning, projection cache/manifest, lifecycle, parsers, execution preparation, and artifact promotion.
- Open-source concerns: The public workflow is not documented at repo root, the state machine is large and internal, and execution bridge trust posture is hardcoded.
- Consolidation concerns: Roadmap has the closest thing to a subcommand model, but its workflow orchestration must move out of the console assembly before it becomes one command family in a larger CLI.

## 4. Cross-App Duplication and Divergence

| Pattern | App A | App B | App C | Duplication or Divergence | Refactoring Implication |
| ------- | ----- | ----- | ----- | ------------------------- | ----------------------- |
| Console abstraction | `src/LoopRelay.Cli/LoopConsole.cs` | `src/LoopRelay.Plan.Cli/LoopConsole.cs` | `src/LoopRelay.Roadmap.Cli/LoopConsole.cs` | Same interface and implementation copied with minor comment differences. | Extract shared console/diagnostic surface before merge. |
| Turn stream renderer | `src/LoopRelay.Cli/ConsoleTurnRenderer.cs` | `src/LoopRelay.Plan.Cli/ConsoleTurnRenderer.cs` | `src/LoopRelay.Roadmap.Cli/ConsoleTurnRenderer.cs` | Same rendering behavior copied three times. | Make streaming UX one shared behavior and test once. |
| Argument parsing | `src/LoopRelay.Cli/CliArguments.cs` | `src/LoopRelay.Plan.Cli/CliArguments.cs` | `src/LoopRelay.Roadmap.Cli/CliArguments.cs` | First two are near-identical; roadmap adds ad hoc commands. | Introduce a shared parser or real command framework before unified CLI. |
| Agent specs | `src/LoopRelay.Cli/AgentSpecs.cs` | `src/LoopRelay.Plan.Cli/AgentSpecs.cs` | `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs` | Each app encodes sandbox/role/effort policy locally. | Model agent trust/configuration centrally while preserving per-command defaults. |
| Git/submodule publishing | `src/LoopRelay.Cli/AgentsSubmodulePublisher.cs` | `src/LoopRelay.Plan.Cli/AgentsSubmodulePublisher.cs` | None | Two large copies differ mostly in cadence and exception type. | Extract shared publisher with workflow-specific policy/cadence options. |
| Git porcelain parsing | `src/LoopRelay.Cli/GitPorcelain.cs` | `src/LoopRelay.Plan.Cli/GitPorcelain.cs` | None | Verbatim helper copy. | Move to shared infrastructure. |
| Artifact facade | `src/LoopRelay.Cli/LoopArtifacts.cs` | `src/LoopRelay.Plan.Cli/PlanArtifacts.cs` | `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs` | Same repository-relative read/write/list pattern with different path catalogs. | Extract common artifact repository operations while keeping workflow path catalogs explicit. |
| Entry composition | `src/LoopRelay.Cli/Program.cs` | `src/LoopRelay.Plan.Cli/Program.cs` | `src/LoopRelay.Roadmap.Cli/Program.cs` | Each constructs services manually and configures UTF-8/Ctrl+C/exit mapping. | Use a shared CLI host/bootstrap plus command-specific application services. |
| State model | Loop uses live/rotated markdown artifacts and `.LoopRelay` resume JSON. | Plan relies on `.agents` file preflight. | Roadmap uses structured JSON plus markdown renderings and journals. | Different state semantics are intentional. | Do not merge state machines until a shared state taxonomy is explicit. |
| Error model | `LoopStepException` | `PlanStepException` | `RoadmapStepException` | Similar exception-to-exit-code flow, but types are app-local. | Centralize result/exit-code contract while preserving workflow-specific failure details. |

## 5. Open-Source Readiness Findings

### OSR-1: Missing public onboarding, license, contribution, and CI baseline

- Severity: Critical
- Category: Documentation
- Evidence:
  - `LoopRelay.slnx` - shows a buildable solution but no adjacent root README or contributor contract is present.
  - `docs/architecture.md` - documents internal architecture and broader product concepts, not install/use/contribution flow for the CLI repo.
  - `publish-cli.bat` - the clearest operator entry point is a local publish script rather than public installation guidance.
- Why this matters: Outside users cannot determine project purpose, supported commands, prerequisites, license rights, build/test instructions, release process, or contribution rules.
- Required remediation: Add root `README.md`, `LICENSE`, `CONTRIBUTING.md`, test/build instructions, supported platform notes, command examples, environment prerequisites, and CI status. Add issue/PR templates once public contribution is intended.
- Suggested owner boundary: Repository root documentation and release ownership.

### OSR-2: Build is not self-contained or SDK-pinned

- Severity: Critical
- Category: Build
- Evidence:
  - `src/LoopRelay.Core/LoopRelay.Core.csproj` - references `..\..\..\dotnet-libraries\Lib.Prompts\src\Lib.Prompts\Lib.Prompts.csproj` and imports props/targets from the same sibling checkout.
  - `src/LoopRelay.Cli/LoopRelay.Cli.csproj` - targets `net10.0` without a repository `global.json` pin.
  - `src/LoopRelay.Plan.Cli/LoopRelay.Plan.Cli.csproj` - targets `net10.0` without a repository SDK acquisition story.
  - `src/LoopRelay.Roadmap.Cli/LoopRelay.Roadmap.Cli.csproj` - targets `net10.0` without a repository SDK acquisition story.
- Why this matters: A clean clone cannot build unless the contributor also has the sibling generator repo at the expected relative path and a compatible .NET 10 SDK installed. The local passing test run proves the current machine is configured; it does not prove repository reproducibility.
- Required remediation: Package or vendor the prompt generator dependency, replace the sibling `ProjectReference`/`Import` with a resolvable package path, add `global.json`, document SDK requirements, and add a clean-clone CI build.
- Suggested owner boundary: Build system and dependency management.

### OSR-3: Packaging is Windows-local rather than distributable

- Severity: High
- Category: Packaging
- Evidence:
  - `publish-cli.bat` - defaults output to a local Windows tools directory and references project paths using `LoopRelay.CLI` casing.
  - `publish-plan-cli.bat` - defaults output to a local Windows tools directory and uses a Windows batch script only.
  - `publish-roadmap-cli.bat` - defaults output to a local Windows tools directory and uses a Windows batch script only.
  - `src/LoopRelay.Cli/LoopRelay.Cli.csproj` - has no tool/package metadata such as `PackAsTool`, `ToolCommandName`, package id, version, or license metadata.
- Why this matters: Open-source users expect repeatable installation on supported platforms. The current release path is local Windows automation, not NuGet tool packaging, GitHub release artifacts, or cross-platform scripts.
- Required remediation: Define the supported distribution mode, add package metadata/version stamping, add cross-platform publish scripts or `dotnet tool` packaging, and test published artifacts.
- Suggested owner boundary: Release engineering.

### OSR-4: `.agents` is treated as a submodule but repository metadata is incomplete

- Severity: High
- Category: Contributor UX
- Evidence:
  - `src/LoopRelay.Cli/AgentsSubmodulePublisher.cs` - assumes `.agents` is a git submodule and commits/pushes it directly.
  - `src/LoopRelay.Plan.Cli/AgentsSubmodulePublisher.cs` - repeats the same submodule assumption for the planning pipeline.
  - `.gitignore` - treats `.agents` content as repository-managed local state but does not explain submodule initialization.
- Why this matters: The working tree contains a `.agents` gitlink but no `.gitmodules`; `git submodule status` fails with no mapping. Fresh contributors cannot initialize the expected submodule from repository metadata.
- Required remediation: Add a correct `.gitmodules` entry or remove the gitlink/submodule assumption and replace it with a documented artifact-storage strategy. Add a clean-clone test that validates submodule setup.
- Suggested owner boundary: Repository state/artifact ownership.

### OSR-5: Required external executables and environment variables are undocumented prerequisites

- Severity: Medium
- Category: Portability
- Evidence:
  - `src/LoopRelay.Agents/Services/EnvironmentAgentExecutableResolver.cs` - fails unless `CODEX_EXECUTABLE` is set.
  - `technical-debt.md` - documents that the default Codex batch shim can hang and that a native executable path is the working configuration.
  - `src/LoopRelay.Plan.Cli/PreflightGate.cs` - blocks when existing planning artifacts require operator cleanup before a new planning run.
- Why this matters: A user can build the repo and still fail or hang at runtime because required executables are not installed, discoverable, or validated with actionable startup checks.
- Required remediation: Add prerequisite checks, documented configuration, `--doctor` or preflight diagnostics, and native executable auto-detection.
- Suggested owner boundary: Runtime configuration and diagnostics.

### OSR-6: Internal docs and issue notes are stale or broader than the checked-in CLI repo

- Severity: Medium
- Category: Documentation
- Evidence:
  - `docs/architecture.md` - describes a broader backend/sidecar/Command Center architecture while this solution contains only the CLI-focused .NET projects.
  - `issues/001-roadmap-cli-blocked-rerun-dead-end.md` - states the roadmap CLI has no command verb, but `src/LoopRelay.Roadmap.Cli/CliArguments.cs` now implements `status|run|unblock`.
  - `issues/003-roadmap-cli-markdown-state-parser-corruption.md` - describes raw markdown table split behavior, while `src/LoopRelay.Roadmap.Cli/MarkdownTableParser.cs` now parses escaped pipes and `src/LoopRelay.Roadmap.Cli/StructuredPersistence.cs` introduces canonical JSON persistence.
- Why this matters: Stale internal issue records confuse contributors and can cause them to chase fixed behavior or misunderstand current architecture.
- Required remediation: Mark historical issue documents as resolved/stale, move old product-wide docs under an explicit archive or context directory, and add a current CLI-focused documentation index.
- Suggested owner boundary: Documentation governance.

### OSR-7: Dependency and version governance is ad hoc

- Severity: Medium
- Category: Build
- Evidence:
  - `src/LoopRelay.Agents/LoopRelay.Agents.csproj` - package versions are declared locally.
  - `src/LoopRelay.Cli/LoopRelay.Cli.csproj` - package versions are declared locally.
  - `tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj` - test package versions are declared per test project.
  - `src/LoopRelay.Core/LoopRelay.Core.csproj` - mixes package references with a local generator project/import.
- Why this matters: Without central package management, lock files, or SDK pinning, dependency drift and restore differences can break contributors or release builds.
- Required remediation: Add `Directory.Packages.props`, package lock policy if desired, SDK pinning, dependency update policy, and CI restore/build verification.
- Suggested owner boundary: Build system and dependency management.

## 6. Refactoring Findings

### REF-1: Roadmap workflow orchestration is concentrated in one console-project class

- Severity: Critical
- Category: Structure
- Evidence:
  - `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs` - contains command dispatch, resume/unblock execution, prompt transitions, artifact promotion, execution routing, state persistence, invariant failure persistence, and UX text.
- Current risk: This class is the main blocker to responsibly merging the apps. It is difficult to reason about workflow transitions, test coverage boundaries, and command reuse while the console project owns all orchestration.
- Recommended refactor: Split roadmap into application services such as startup/resume/unblock orchestration, prompt transition executor, state writer, execution result handler, and artifact promotion coordinator. Keep existing planner, registry, validator, and store types as dependencies.
- Why this should happen before/after CLI merge: Before merge. A unified CLI should call a roadmap application service, not import a monolithic console state machine.
- Regression risks: State transitions, persisted evidence paths, journal records, lifecycle updates, and resume/unblock behavior.
- Test coverage needed: Characterization tests around existing state-machine outcomes, transition journals, state JSON/markdown rendering, and failure persistence before moving code.

### REF-2: Console rendering and turn diagnostics are duplicated across apps

- Severity: High
- Category: Duplication
- Evidence:
  - `src/LoopRelay.Cli/LoopConsole.cs` - defines the console abstraction and implementation.
  - `src/LoopRelay.Plan.Cli/LoopConsole.cs` - duplicates the same abstraction and implementation.
  - `src/LoopRelay.Roadmap.Cli/LoopConsole.cs` - duplicates the same abstraction and implementation with fewer comments.
  - `src/LoopRelay.Cli/ConsoleTurnRenderer.cs` - duplicates turn rendering.
  - `src/LoopRelay.Plan.Cli/ConsoleTurnRenderer.cs` - duplicates turn rendering.
  - `src/LoopRelay.Roadmap.Cli/ConsoleTurnRenderer.cs` - duplicates turn rendering.
- Current risk: Any UX fix or stream-rendering bug fix must be applied three times, and a merged CLI could accidentally preserve divergent output behavior.
- Recommended refactor: Extract a shared console/turn-rendering package or application-level service with one test suite.
- Why this should happen before/after CLI merge: Before merge. It is small, high-confidence, and removes user-visible duplication.
- Regression risks: Streamed output newline handling, stderr/stdout ordering, and silent-output echo behavior.
- Test coverage needed: Keep existing `LoopConsoleTests`/`ConsoleTurnRendererTests` and add shared tests for all app consumers.

### REF-3: Git and `.agents` submodule publishing logic is duplicated and app-local

- Severity: High
- Category: Duplication
- Evidence:
  - `src/LoopRelay.Cli/AgentsSubmodulePublisher.cs` - implements submodule commit/push, parent gitlink recording, retry/recovery, and strict git errors.
  - `src/LoopRelay.Plan.Cli/AgentsSubmodulePublisher.cs` - duplicates most of that logic with different cadence and exception type.
  - `src/LoopRelay.Cli/GitPorcelain.cs` - parses git status.
  - `src/LoopRelay.Plan.Cli/GitPorcelain.cs` - duplicates the parser.
- Current risk: Submodule/git behavior is operationally critical and must stay consistent. Duplicated strict-push, upstream, branch, and gitlink rules make maintenance risky.
- Recommended refactor: Extract shared `GitStatusParser`, `AgentsSubmodulePublisher`, and parent-gitlink policy abstractions. Let workflows provide commit-message/cadence policy and exception/result translation.
- Why this should happen before/after CLI merge: Before merge. A single CLI cannot safely own two slightly different copies of critical git publishing logic.
- Regression risks: Parent repo staging scope, stranded push recovery, detached HEAD errors, parent gitlink timing, and failure messages.
- Test coverage needed: Port both apps' publisher tests to the shared implementation and add behavior cases for cadence differences.

### REF-4: Artifact access and path catalogs are fragmented

- Severity: High
- Category: State
- Evidence:
  - `src/LoopRelay.Cli/LoopArtifacts.cs` - app-local repository-relative facade with rotation and sequence logic.
  - `src/LoopRelay.Plan.Cli/PlanArtifacts.cs` - app-local repository-relative facade with sandbox helpers.
  - `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs` - app-local repository-relative facade with numbered evidence helpers.
  - `src/LoopRelay.Orchestration.Primitives/OrchestrationArtifactPaths.cs` - shared loop/plan path catalog.
  - `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs` - separate roadmap path catalog.
- Current risk: Consolidation will expose overlapping `.agents` responsibilities without one artifact namespace policy. Some paths are shared by name but owned by different workflow meanings.
- Recommended refactor: Extract common artifact access primitives and keep workflow-specific path catalogs explicit. Define an artifact ownership matrix before merging command families.
- Why this should happen before/after CLI merge: Before merge for common access and ownership policy; after merge for deeper state unification if behavior proves compatible.
- Regression risks: Historical rotation sequence, evidence numbering, sandbox copy-back, and path boundary validation.
- Test coverage needed: Characterization tests for sequence generation, repository-boundary validation, sandbox mapping, and path ownership conflicts.

### REF-5: No shared command model exists yet

- Severity: Medium
- Category: Command Model
- Evidence:
  - `src/LoopRelay.Cli/CliArguments.cs` - accepts only `REPO_DIR`.
  - `src/LoopRelay.Plan.Cli/CliArguments.cs` - duplicates the same `REPO_DIR` parser.
  - `src/LoopRelay.Roadmap.Cli/CliArguments.cs` - implements a separate `status|run|unblock` parser.
- Current risk: A merged CLI would need to reconcile positional forms, command order, unsupported commands, help text, and exit-code behavior at the same time as workflow migration.
- Recommended refactor: Introduce a shared command model with explicit commands, options, help, validation, and result mapping. Preserve old executable behavior with compatibility shims if needed.
- Why this should happen before/after CLI merge: Before merge. Command compatibility is the user-facing contract.
- Regression risks: Existing scripts that call the current executables with positional paths.
- Test coverage needed: Golden CLI parser tests for valid/invalid args, help output, and exit codes.

### REF-6: Composition roots construct application behavior directly in `Program.cs`

- Severity: Medium
- Category: Structure
- Evidence:
  - `src/LoopRelay.Cli/Program.cs` - manually wires services and workflow objects.
  - `src/LoopRelay.Plan.Cli/Program.cs` - manually wires services and workflow objects.
  - `src/LoopRelay.Roadmap.Cli/Program.cs` - manually wires a large roadmap object graph.
- Current risk: The entry points are difficult to test as command hosts and difficult to compose under one executable without copying more code.
- Recommended refactor: Move workflow composition into application registration methods. Keep `Program.cs` limited to command parsing, console setup, cancellation, service provider setup, and result-to-exit-code mapping.
- Why this should happen before/after CLI merge: Before merge. It lets the future unified CLI call the same services old executables use.
- Regression risks: Service lifetimes, registry disposal, Ctrl+C behavior, and output encoding.
- Test coverage needed: Composition smoke tests and process-level CLI tests after extraction.

### REF-7: Execution trust posture is hardcoded instead of modeled

- Severity: High
- Category: Coupling
- Evidence:
  - `src/LoopRelay.Cli/AgentSpecs.cs` - operational execution can request `danger-full-access` by string identifier.
  - `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs` - hardcodes roadmap execution bridge to `danger-full-access`, network access, and no approval.
  - `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs` - runs the execution prompt through the hardcoded execution bridge spec.
  - `src/LoopRelay.Roadmap.Cli/ExecutionPromptGenerator.cs` - embeds active epic, milestone spec, and operational context text directly into an execution prompt.
- Current risk: Privilege, network, and approval policy are hidden inside app-local factories. Open-source users cannot see or select the trust posture at the CLI boundary.
- Recommended refactor: Introduce explicit execution options and trust policy records. Default to least privilege where feasible, require explicit elevated mode for broad access, and write chosen posture into execution evidence.
- Why this should happen before/after CLI merge: Before public release and before merge. A unified CLI should not hide different privilege profiles behind command names.
- Regression risks: Existing workflows that require broad local authority may fail under stricter defaults.
- Test coverage needed: Agent spec tests for default/elevated modes, execution evidence tests, and prompt-rendering tests that treat embedded artifacts as data.

### REF-8: `Orchestration.Primitives` is not a clean architectural layer

- Severity: Medium
- Category: Naming
- Evidence:
  - `src/LoopRelay.Orchestration.Primitives/LoopRelay.Orchestration.Primitives.csproj` - references both `LoopRelay.Agents` and `LoopRelay.Core`.
  - `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionResumeStore.cs` - is a concrete filesystem persistence implementation, not only a primitive abstraction.
  - `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionRouter.cs` - is a useful pure service but sits beside infrastructure persistence.
- Current risk: The project name implies low-level primitives, but the project contains service policy and filesystem persistence. Future merge work may put more unrelated shared code here by convenience.
- Recommended refactor: Rename or split by responsibility: application contracts/policies, filesystem infrastructure, and shared path constants.
- Why this should happen before/after CLI merge: Before or during shared-infrastructure extraction. It should not block small duplicate-code extraction, but should be addressed before broad consolidation.
- Regression risks: Namespace churn and project-reference churn.
- Test coverage needed: Build graph tests and existing router/resume store tests.

### REF-9: Prompt catalogs and contracts are string-switch registries in a CLI project

- Severity: Medium
- Category: Coupling
- Evidence:
  - `src/LoopRelay.Roadmap.Cli/RoadmapPromptCatalog.cs` - maps prompt names to generated prompt types through large string switches.
  - `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs` - defines runtime prompt inputs/outputs/decisions inside the CLI project.
  - `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs` - defines runtime-to-projection mapping inside the CLI project.
  - `src/LoopRelay.Core/LoopRelay.Core.csproj` - prompt generation depends on a local generator import.
- Current risk: Prompt contract metadata is application-level behavior, but it is colocated with console code and tied to string names. This increases merge complexity and makes command contracts harder to version.
- Recommended refactor: Move prompt contract/registry/catalog behavior into a roadmap application project and keep generated prompt resources in Core. Consider generated or declarative metadata if the string switch continues to grow.
- Why this should happen before/after CLI merge: Before roadmap is folded into a unified command family.
- Regression risks: Prompt name mapping, source hash/provenance, projection freshness, and allowed-decision routing.
- Test coverage needed: Existing prompt contract and projection tests should move with the application project.

### REF-10: Repository identity and configuration are ephemeral and app-local

- Severity: Medium
- Category: State
- Evidence:
  - `src/LoopRelay.Cli/CliArguments.cs` - assigns a new repository `Guid` per parse.
  - `src/LoopRelay.Plan.Cli/CliArguments.cs` - assigns a new repository `Guid` per parse.
  - `src/LoopRelay.Roadmap.Cli/CliArguments.cs` - assigns a new repository `Guid` per parse.
  - `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionResumeStore.cs` - persists resume state by repository path in `.LoopRelay`, not by a stable repository registry.
- Current risk: A single CLI with multiple subcommands will need predictable repository identity, configuration, and local-state ownership. Per-run IDs are fine for process-local session registries but not for durable user configuration.
- Recommended refactor: Define a repository context object that separates process session identity, filesystem root, display name, and durable local config identity.
- Why this should happen before/after CLI merge: Before introducing global config or cross-command state; can happen during merge if only the unified parser changes.
- Regression risks: Session registry keys, resume state, telemetry paths, and tests that assume random IDs.
- Test coverage needed: Repository context tests and migration tests for existing `.LoopRelay` state.

## 7. CLI Utility and UX Findings

### UX-1: Help and usage are parse-error-only

- Severity: High
- Evidence:
  - `src/LoopRelay.Cli/CliArguments.cs` - returns usage only when required args are missing.
  - `src/LoopRelay.Plan.Cli/CliArguments.cs` - returns usage only when required args are missing.
  - `src/LoopRelay.Roadmap.Cli/CliArguments.cs` - returns usage and command errors but does not model `--help` or `--version`.
- Current user impact: Users cannot discover commands, options, environment variables, examples, or exit codes from the CLI itself.
- Recommended improvement: Add standard `--help`, `--version`, command descriptions, examples, and environment/config diagnostics.
- Open-source relevance: High. CLI self-documentation is part of the public interface.
- Consolidation relevance: High. A unified CLI needs this before subcommands are credible.

### UX-2: User-facing command contract differs between apps

- Severity: High
- Evidence:
  - `src/LoopRelay.Cli/CliArguments.cs` - `LoopRelay.Cli <REPO_DIR>`.
  - `src/LoopRelay.Plan.Cli/CliArguments.cs` - `LoopRelay.Plan.Cli <REPO_DIR>`.
  - `src/LoopRelay.Roadmap.Cli/CliArguments.cs` - `LoopRelay.Roadmap.Cli [status|run|unblock] <REPO_DIR>` with both leading and trailing command forms.
- Current user impact: Users must learn three incompatible executable shapes before the project has one documented command grammar.
- Recommended improvement: Define a future grammar such as `looprelay loop run <repo>`, `looprelay plan run <repo>`, and `looprelay roadmap status|run|unblock <repo>`, with compatibility wrappers for old executables.
- Open-source relevance: High. Predictable command shape reduces support burden.
- Consolidation relevance: Critical. This is a direct merge prerequisite.

### UX-3: Exit codes are implemented but not centralized as a public contract

- Severity: Medium
- Evidence:
  - `src/LoopRelay.Cli/Program.cs` - maps outcomes to `0`, `1`, `2`, `3`, and `130`.
  - `src/LoopRelay.Plan.Cli/Program.cs` - maps outcomes to `0`, `1`, `2`, `4`, and `130`.
  - `src/LoopRelay.Roadmap.Cli/Program.cs` - maps outcomes to `0`, `1`, `2`, `4`, and `130`.
  - `docs/architecture.md` - documents plan CLI exit codes, but not a unified command contract for all apps.
- Current user impact: Automation can infer exit codes from code but not from public docs or a shared result model.
- Recommended improvement: Create a shared exit-code enum/table and document command-specific meanings.
- Open-source relevance: Medium. CI/users need stable process contracts.
- Consolidation relevance: High. The unified CLI should avoid command families reusing codes inconsistently.

### UX-4: Main loop success blocks automation with an interactive prompt

- Severity: Medium
- Evidence:
  - `src/LoopRelay.Cli/Program.cs` - on `LoopOutcome.EpicCompleted`, prints "Press any key to exit" and calls `Console.ReadKey`.
- Current user impact: A successful automated run can hang in non-interactive shells.
- Recommended improvement: Remove the blocking prompt by default or gate it behind an explicit interactive mode.
- Open-source relevance: Medium. Automation-friendly behavior matters for a CLI.
- Consolidation relevance: Medium. Unified CLI commands should all be script-safe by default.

### UX-5: Runtime/configuration diagnostics are not discoverable enough

- Severity: Medium
- Evidence:
  - `src/LoopRelay.Agents/Services/EnvironmentAgentExecutableResolver.cs` - throws if `CODEX_EXECUTABLE` is missing.
  - `src/LoopRelay.Cli/DecisionResumeComposition.cs` - uses `LoopRelay_DECISION_RESUME` but there is no CLI-visible config listing.
  - `src/LoopRelay.Plan.Cli/PreflightGate.cs` - requires operator cleanup for existing planning artifacts before a new planning run.
  - `technical-debt.md` - contains important runtime caveats that are not surfaced in CLI help.
- Current user impact: Users learn about missing/misconfigured tools only at runtime, sometimes after a long workflow has started.
- Recommended improvement: Add `doctor`/preflight diagnostics and help output that lists all environment variables and external executables.
- Open-source relevance: High. Self-service diagnostics reduce issue churn.
- Consolidation relevance: High. A single CLI should share config discovery across command families.

### UX-6: Execution privilege is invisible to CLI users

- Severity: Medium
- Evidence:
  - `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs` - roadmap execution bridge hardcodes `danger-full-access`.
  - `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs` - runs the execution bridge without a user-visible trust-mode choice.
  - `src/LoopRelay.Roadmap.Cli/ExecutionPromptGenerator.cs` - produces the prompt that is sent to the privileged execution bridge.
- Current user impact: Users cannot choose or audit the privilege/network/approval posture of an execution run from the command line.
- Recommended improvement: Add explicit trust-mode options and record selected posture in execution evidence.
- Open-source relevance: High. Trust posture is a core safety and adoption concern.
- Consolidation relevance: High. Different subcommands need explicit privilege boundaries.

## 8. Testing and Validation Gaps

| Gap | Evidence | Risk | Characterization Test Needed | Refactor Enabled |
| --- | -------- | ---- | ---------------------------- | ---------------- |
| Process-level CLI tests | `tests/LoopRelay.Cli.Tests/CliArgumentsTests.cs`, `tests/LoopRelay.Plan.Cli.Tests/CliArgumentsTests.cs`, and `tests/LoopRelay.Roadmap.Cli.Tests/CliArgumentsTests.cs` test parsers, not launched executables. | A merged CLI could compile but expose broken help, exit codes, encoding, or command routing. | Launch published/testhost commands with `--help`, bad args, missing repo, and each known command. | Shared command model and unified CLI shell. |
| Clean-clone build | `src/LoopRelay.Core/LoopRelay.Core.csproj` depends on a sibling prompt generator. | Contributors cannot reproduce local green tests. | CI job from a clean checkout with no sibling repo and no pre-restored artifacts. | Open-source build confidence. |
| Packaging/install validation | `publish-cli.bat`, `publish-plan-cli.bat`, `publish-roadmap-cli.bat` are not covered by tests. | Release artifacts may be missing, Windows-only, or wrongly named. | Publish artifact smoke tests on Windows and at least one non-Windows runner if cross-platform support is intended. | NuGet tool or release artifact packaging. |
| Shared console rendering | Console code is copied in three projects. | Extraction can subtly change streaming UX. | Shared golden output tests for streamed deltas, tool calls, stderr, and silent echoes. | REF-2 extraction. |
| Shared git/submodule publisher | Two publisher copies have separate tests. | Extraction can break parent gitlink timing or strict push recovery. | Port both existing publisher suites to a shared implementation with cadence policy cases. | REF-3 extraction. |
| Artifact namespace compatibility | Separate path catalogs and facades exist. | Unified CLI can overwrite or misclassify `.agents` state. | Path ownership matrix tests and round-trip tests for rotations, evidence numbering, and roadmap structured state. | REF-4 extraction and safe merge. |
| Roadmap state-machine split | `RoadmapStateMachine` has many tests but one large implementation class. | Moving methods can lose journal/state side effects. | Transition-level characterization tests that assert state JSON, markdown rendering, journal event, lifecycle, and evidence paths for each major branch. | REF-1 application extraction. |
| Trust policy | Agent spec tests exist, but trust posture is still hardcoded. | A safer default could silently break workflows or a merge could hide dangerous defaults. | Default/elevated execution mode tests and evidence-rendering tests. | REF-7 policy modeling. |
| Stale docs guard | Issue/docs can contradict code. | Contributors act on obsolete instructions. | Documentation lint or status metadata for issue documents that claim "Verified against current codebase." | Open-source documentation cleanup. |

## 9. Consolidation Readiness Assessment

## Consolidation Verdict

Verdict: Not Ready

Rationale:

The three console apps should not be merged immediately. They share infrastructure concepts, but their command models, state lifecycles, privilege postures, and persistence semantics are still app-local. A single executable today would mostly wrap duplicated code and preserve accidental boundaries. The correct path is to extract shared infrastructure and application services first, then introduce one CLI command surface over those services.

### Must Happen Before Merge

1. Extract shared console/turn rendering, diagnostics, exit-code mapping, and command parsing conventions.
2. Extract git/submodule publishing and git status parsing with workflow-specific cadence policies.
3. Move `RoadmapStateMachine` orchestration behind roadmap application services.
4. Define a command grammar, help contract, and compatibility policy for old executable names.
5. Define artifact ownership for shared `.agents` paths versus roadmap-specific state paths.
6. Model execution trust posture explicitly instead of hardcoding `danger-full-access`.
7. Make the build self-contained enough for clean-clone CI.

### Can Happen During Merge

1. Add the unified `LoopRelay.Cli` command host with subcommands that delegate to existing application services.
2. Keep old executable projects as thin wrappers temporarily, or replace them with compatibility aliases/scripts.
3. Centralize service registration and repository context construction.
4. Centralize environment/config discovery and a `doctor` command.

### Should Happen After Merge

1. Deeper state unification, if evidence shows loop/plan/roadmap states can share more than artifact primitives.
2. NuGet/global-tool packaging and release automation, once command names stabilize.
3. Richer CLI UX such as shell completions, structured output, and machine-readable diagnostics.
4. Documentation expansion around workflows and examples.

### Should Remain Separate or Optional

1. Workflow state machines should remain separate application services even under one CLI.
2. Roadmap execution should keep an explicit optional elevated mode rather than inheriting loop execution defaults.
3. Planning cleanup and archival should remain explicit until packaged with the project.
4. Roadmap prompt/projection contract registries should remain isolated from generic loop/plan execution until a real shared contract emerges.

## 10. Proposed Target Shape

A conservative target shape:

```text
/src
  /LoopRelay.Cli
    Unified command host, help/version/doctor, command routing, exit-code mapping.
  /LoopRelay.Application
    Shared command results, repository context, diagnostics, artifact service contracts,
    git publishing policies, and workflow-neutral orchestration helpers.
  /LoopRelay.Loop.Application
    Current loop runner, decision session, execution step, telemetry, stall/commit gates.
  /LoopRelay.Plan.Application
    Current plan pipeline, preflight, one-shot planning steps.
  /LoopRelay.Roadmap.Application
    Roadmap state machine split into services, prompt/projection contracts, resume/unblock,
    artifact promotion, execution preparation, lifecycle/journal persistence.
  /LoopRelay.Agents
    Codex/agent runtime abstractions and process/session implementations.
  /LoopRelay.Core
    Prompt resources, repository-neutral artifact contracts, generated prompt surfaces.
  /LoopRelay.Infrastructure
    File system artifact store, process runner, git runner, package-specific filesystem adapters.
/tests
  /Unit
  /Integration
  /Cli
```

CLI layer responsibilities:

- Parse commands/options, print help/version, run `doctor`, construct repository context, wire cancellation, map application results to exit codes, and render user-facing diagnostics.

Application service responsibilities:

- Own workflow sequencing, command results, state-transition decisions, artifact ownership rules, trust policies, and validation gates. Application services should be testable without launching a process.

Infrastructure responsibilities:

- File system reads/writes, process execution, Codex process/session mechanics, git/submodule operations, SDK/package probing, and OS-specific behavior.

Shared concepts to extract:

- Repository context, artifact access primitives, console/turn rendering, result/exit-code model, git status parsing, submodule publishing, trust policy records, and environment/config discovery.

App-specific concepts to keep isolated:

- Loop decision/execution flow, plan authoring/review/extraction pipeline, roadmap projection/prompt contracts, roadmap lifecycle/journal/selection/epic-specific state, and roadmap completion certification.

## 11. Recommended Refactoring Sequence

### Stage 1: Freeze Behavior With Characterization Tests

- Goal: Lock down current observable behavior before moving code.
- Why now: The code has many edge-case tests already, but no single public CLI contract.
- Prerequisites: Existing green test suite.
- Files likely affected: `tests/LoopRelay.Cli.Tests/*`, `tests/LoopRelay.Plan.Cli.Tests/*`, `tests/LoopRelay.Roadmap.Cli.Tests/*`
- Work included: Add process-level command tests for args/help/exit codes, artifact path ownership tests, and git/submodule publisher extraction tests.
- Work explicitly excluded: No architecture movement yet.
- Validation required: `dotnet test LoopRelay.slnx --no-restore --nologo`
- Risk: Low.

### Stage 2: Extract Shared CLI Infrastructure

- Goal: Remove duplicated console rendering, turn rendering, exit-code/result basics, and argument validation helpers.
- Why now: This is narrow, heavily testable, and reduces merge friction quickly.
- Prerequisites: Stage 1 tests for output and parser behavior.
- Files likely affected: `src/LoopRelay.Cli/LoopConsole.cs`, `src/LoopRelay.Plan.Cli/LoopConsole.cs`, `src/LoopRelay.Roadmap.Cli/LoopConsole.cs`, `src/LoopRelay.Cli/ConsoleTurnRenderer.cs`, `src/LoopRelay.Plan.Cli/ConsoleTurnRenderer.cs`, `src/LoopRelay.Roadmap.Cli/ConsoleTurnRenderer.cs`, `src/LoopRelay.Cli/CliArguments.cs`, `src/LoopRelay.Plan.Cli/CliArguments.cs`, `src/LoopRelay.Roadmap.Cli/CliArguments.cs`
- Work included: Create shared console/renderer services and parser result model.
- Work explicitly excluded: No subcommand merge.
- Validation required: Existing console/parser tests plus process-level smoke tests.
- Risk: Low to medium because output behavior is user-visible.

### Stage 3: Extract Git and Artifact Infrastructure

- Goal: Centralize git status parsing, `.agents` publishing, parent gitlink recording, and repository-relative artifact primitives.
- Why now: Git/artifact logic is operationally critical and duplicated.
- Prerequisites: Publisher characterization tests from Stage 1.
- Files likely affected: `src/LoopRelay.Cli/AgentsSubmodulePublisher.cs`, `src/LoopRelay.Plan.Cli/AgentsSubmodulePublisher.cs`, `src/LoopRelay.Cli/GitPorcelain.cs`, `src/LoopRelay.Plan.Cli/GitPorcelain.cs`, `src/LoopRelay.Cli/LoopArtifacts.cs`, `src/LoopRelay.Plan.Cli/PlanArtifacts.cs`, `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs`
- Work included: Shared git runner/publisher with workflow-specific policies; common artifact base service.
- Work explicitly excluded: No state schema migration.
- Validation required: Publisher tests, path boundary tests, rotation/evidence tests.
- Risk: Medium to high because failures can affect user repositories.

### Stage 4: Move Workflow Logic Into Application Projects

- Goal: Make console apps thin wrappers over application services.
- Why now: This is the main prerequisite to a unified CLI.
- Prerequisites: Shared infrastructure from Stages 2 and 3.
- Files likely affected: `src/LoopRelay.Cli/LoopRunner.cs`, `src/LoopRelay.Plan.Cli/PlanPipeline.cs`, `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`, all three `Program.cs` files.
- Work included: Create loop, plan, and roadmap application services; move service registration out of `Program.cs`.
- Work explicitly excluded: No behavior changes and no command merge yet.
- Validation required: Full test suite plus state/journal/evidence characterization tests.
- Risk: High, especially roadmap.

### Stage 5: Model Configuration, Repository Context, and Trust Policy

- Goal: Make runtime prerequisites and privilege choices explicit.
- Why now: Public users need predictable diagnostics before a merged CLI hides complexity.
- Prerequisites: Application service boundaries.
- Files likely affected: `src/LoopRelay.Agents/Services/EnvironmentAgentExecutableResolver.cs`, `src/LoopRelay.Cli/AgentSpecs.cs`, `src/LoopRelay.Plan.Cli/AgentSpecs.cs`, `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs`, `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs`, `src/LoopRelay.Roadmap.Cli/ExecutionPromptGenerator.cs`, `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionResumeStore.cs`
- Work included: Repository context object, config diagnostics, `doctor`, execution trust options, documented environment variables.
- Work explicitly excluded: No global config UI unless needed.
- Validation required: Config/doctor tests, agent spec trust-mode tests, execution evidence tests.
- Risk: Medium.

### Stage 6: Introduce Unified CLI With Subcommands

- Goal: Add one command host that delegates to application services.
- Why now: Boundaries are now explicit enough to avoid a shallow merge.
- Prerequisites: Stages 2 through 5.
- Files likely affected: `src/LoopRelay.Cli/Program.cs`, project files, old wrapper entry points if retained.
- Work included: `looprelay loop run`, `looprelay plan run`, `looprelay roadmap status|run|unblock`, shared help/version/doctor, compatibility wrappers.
- Work explicitly excluded: No deep roadmap/loop state unification.
- Validation required: Process-level CLI tests and compatibility tests for old executables/scripts.
- Risk: Medium.

### Stage 7: Open-Source Release Hardening

- Goal: Make the project credible for external users and contributors.
- Why now: Packaging and docs should describe the stable command surface, not pre-merge internals.
- Prerequisites: Unified command surface or explicit decision to keep separate binaries.
- Files likely affected: `README.md`, `LICENSE`, `CONTRIBUTING.md`, `.github/workflows/*`, `global.json`, `.editorconfig`, project files, publish scripts.
- Work included: Clean-clone CI, package metadata, install instructions, license, contribution docs, CI badges, release artifacts, submodule metadata, dependency governance.
- Work explicitly excluded: Feature additions.
- Validation required: Clean-clone build/test, package install smoke test, docs quickstart smoke test.
- Risk: Medium.

## 12. Filepath Extraction Index

## Filepath Extraction Index

| Relative Filepath | Finding IDs | Rationale |
|---|---|---|
| `.gitignore` | OSR-4 | Shows `.agents`-related local artifact assumptions without submodule setup metadata. |
| `LoopRelay.slnx` | OSR-1 | Establishes the root solution while public onboarding files are absent. |
| `docs/architecture.md` | OSR-1, OSR-6, UX-3 | Internal/broader architecture docs and partial exit-code documentation. |
| `issues/001-roadmap-cli-blocked-rerun-dead-end.md` | OSR-6 | Stale roadmap issue note that contradicts current command support. |
| `issues/003-roadmap-cli-markdown-state-parser-corruption.md` | OSR-6 | Stale issue note that does not reflect current parser/structured persistence behavior. |
| `publish-cli.bat` | OSR-1, OSR-3 | Local Windows publish entry point and non-public install story. |
| `publish-plan-cli.bat` | OSR-3 | Windows-only plan publish script with local default path. |
| `publish-roadmap-cli.bat` | OSR-3 | Windows-only roadmap publish script with local default path. |
| `src/LoopRelay.Agents/LoopRelay.Agents.csproj` | OSR-7 | Locally managed package version evidence. |
| `src/LoopRelay.Agents/Services/EnvironmentAgentExecutableResolver.cs` | OSR-5, UX-5 | Runtime dependency on `CODEX_EXECUTABLE`. |
| `src/LoopRelay.Cli/AgentSpecs.cs` | REF-7 | App-local trust/role/effort factory. |
| `src/LoopRelay.Cli/AgentsSubmodulePublisher.cs` | OSR-4, REF-3 | Assumes `.agents` submodule and duplicates git publishing logic. |
| `src/LoopRelay.Cli/CliArguments.cs` | REF-5, REF-10, UX-1, UX-2 | Positional parser and ephemeral repository identity. |
| `src/LoopRelay.Cli/ConsoleTurnRenderer.cs` | REF-2 | Duplicated stream renderer. |
| `src/LoopRelay.Cli/DecisionResumeComposition.cs` | UX-5 | Hidden environment flag. |
| `src/LoopRelay.Cli/GitPorcelain.cs` | REF-3 | Duplicated git status parser. |
| `src/LoopRelay.Cli/LoopArtifacts.cs` | REF-4 | App-local artifact facade. |
| `src/LoopRelay.Cli/LoopConsole.cs` | REF-2 | Duplicated console abstraction. |
| `src/LoopRelay.Cli/LoopRelay.Cli.csproj` | OSR-2, OSR-3, OSR-7 | Net10 executable project without package metadata or central dependency governance. |
| `src/LoopRelay.Cli/Program.cs` | REF-6, UX-3, UX-4 | Manual composition, exit mapping, and interactive success prompt. |
| `src/LoopRelay.Core/LoopRelay.Core.csproj` | OSR-2, OSR-7, REF-9 | Local sibling prompt generator dependency and package/version governance issue. |
| `src/LoopRelay.Orchestration.Primitives/LoopRelay.Orchestration.Primitives.csproj` | REF-8 | Layer naming/dependency boundary evidence. |
| `src/LoopRelay.Orchestration.Primitives/OrchestrationArtifactPaths.cs` | REF-4 | Shared loop/plan path catalog. |
| `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionResumeStore.cs` | REF-8, REF-10 | Concrete filesystem persistence inside primitives project and local repository-state ownership. |
| `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionRouter.cs` | REF-8 | Useful pure policy service colocated with infrastructure persistence. |
| `src/LoopRelay.Plan.Cli/AgentSpecs.cs` | REF-7 | App-local trust/role/effort factory. |
| `src/LoopRelay.Plan.Cli/AgentsSubmodulePublisher.cs` | OSR-4, REF-3 | Duplicated submodule publisher. |
| `src/LoopRelay.Plan.Cli/CliArguments.cs` | REF-5, REF-10, UX-1, UX-2 | Positional parser and ephemeral repository identity. |
| `src/LoopRelay.Plan.Cli/ConsoleTurnRenderer.cs` | REF-2 | Duplicated stream renderer. |
| `src/LoopRelay.Plan.Cli/PreflightGate.cs` | OSR-5, UX-5 | Explicit cleanup gate for existing planning artifacts. |
| `src/LoopRelay.Plan.Cli/GitPorcelain.cs` | REF-3 | Duplicated git status parser. |
| `src/LoopRelay.Plan.Cli/LoopConsole.cs` | REF-2 | Duplicated console abstraction. |
| `src/LoopRelay.Plan.Cli/LoopRelay.Plan.Cli.csproj` | OSR-2, OSR-7 | Net10 executable project without central dependency governance. |
| `src/LoopRelay.Plan.Cli/PlanArtifacts.cs` | REF-4 | App-local artifact facade. |
| `src/LoopRelay.Plan.Cli/Program.cs` | REF-6, UX-3 | Manual composition and exit mapping. |
| `src/LoopRelay.Roadmap.Cli/AgentSpecs.cs` | REF-7, UX-6 | Hardcoded execution bridge trust posture. |
| `src/LoopRelay.Roadmap.Cli/CliArguments.cs` | OSR-6, REF-5, REF-10, UX-1, UX-2 | Current roadmap command parser and ephemeral repository identity. |
| `src/LoopRelay.Roadmap.Cli/ConsoleTurnRenderer.cs` | REF-2 | Duplicated stream renderer. |
| `src/LoopRelay.Roadmap.Cli/ExecutionPromptGenerator.cs` | REF-7, UX-6 | Generates privileged execution prompt from artifact content. |
| `src/LoopRelay.Roadmap.Cli/LoopConsole.cs` | REF-2 | Duplicated console abstraction. |
| `src/LoopRelay.Roadmap.Cli/LoopRelay.Roadmap.Cli.csproj` | OSR-2, OSR-7 | Net10 executable project without central dependency governance. |
| `src/LoopRelay.Roadmap.Cli/MarkdownTableParser.cs` | OSR-6 | Current parser behavior differs from stale issue note. |
| `src/LoopRelay.Roadmap.Cli/Program.cs` | REF-6, UX-3 | Manual composition and exit mapping. |
| `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs` | REF-9 | CLI-local prompt/projection registry. |
| `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs` | REF-9 | CLI-local prompt contract registry. |
| `src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs` | REF-4 | Separate roadmap path catalog. |
| `src/LoopRelay.Roadmap.Cli/RoadmapArtifacts.cs` | REF-4 | App-local artifact facade. |
| `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs` | REF-7, UX-6 | Runs hardcoded privileged execution bridge. |
| `src/LoopRelay.Roadmap.Cli/RoadmapPromptCatalog.cs` | REF-9 | String-switch prompt catalog inside CLI project. |
| `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs` | REF-1 | Monolithic roadmap orchestration class. |
| `src/LoopRelay.Roadmap.Cli/StructuredPersistence.cs` | OSR-6 | Current canonical structured persistence behavior. |
| `technical-debt.md` | OSR-5, UX-5 | Documents runtime executable caveats not surfaced as public CLI help. |
| `tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj` | OSR-7 | Repeated local test package versions. |
