# CommandCenter.CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone .NET console app, `CommandCenter.CLI`, that takes a repository directory path and runs the orchestration loop (execution codex session → decision codex session, with handoff/decision rotation) **fully automated** until every checkbox across `.agents/plan.md` and `.agents/milestones/m*.md` is checked, terminating cleanly on Ctrl+C by killing the codex child processes.

**Architecture:** The CLI does **not** reuse `RepositoryOrchestrator` (which is built for the HTTP/UI human-gated model with background tasks and an internal lifetime token). Instead it builds its **own serial loop** that reuses the real low-level building blocks unchanged: the codex runtime (`IAgentRuntime` / `IAgentSession` from `CommandCenter.Agents`), the source-generated prompt catalog (`CommandCenter.Core.Prompts.*`), the artifact path catalog + filesystem store (`OrchestrationArtifactPaths`, `ArtifactPath`, `IArtifactStore`/`FileSystemArtifactStore`), and the pure token-pressure router (`IDecisionSessionRouter`/`DecisionSessionRouter`). Every codex turn is `await`ed inline on one `CancellationToken` wired to `Console.CancelKeyPress`; on cancel the in-flight one-shot session disposes itself (killing codex) and the CLI explicitly closes the warm decision session (killing codex), so Ctrl+C terminates the whole codex process tree.

**Tech Stack:** C# / .NET 10 (`net10.0`), `Microsoft.NET.Sdk` console app, `Microsoft.Extensions.DependencyInjection` for composition, xUnit for tests. Reuses `CommandCenter.Agents`, `CommandCenter.Core`, `CommandCenter.Orchestration`.

---

## Global Constraints

These apply to every task. Exact values copied from the existing codebase:

- **Target framework:** `net10.0`. **`<Nullable>enable</Nullable>`**, **`<ImplicitUsings>enable</ImplicitUsings>`** — set per-csproj (there is NO `Directory.Packages.props` and `Directory.Build.props` does NOT set these centrally).
- **Project SDK:** `Microsoft.NET.Sdk` (NOT `Microsoft.NET.Sdk.Web`). Single explicit `PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0"` (brings `IServiceCollection`, `BuildServiceProvider`, `TryAdd*`). No `Microsoft.Extensions.Hosting` — the CLI runs an explicit loop, not a Generic Host.
- **`<UseExecutionContextAlias>false</UseExecutionContextAlias>`** in the CLI csproj. `Directory.Build.props` injects a `global using ExecutionContext = …` alias gated on this flag; it only resolves for projects that reference `CommandCenter.Execution`. The CLI does **not** reference `CommandCenter.Execution`, so it MUST opt out (exactly as `CommandCenter.Agents` and `CommandCenter.Decisions` do).
- **Codex executable:** resolved by `EnvironmentAgentExecutableResolver` from env var `CODEX_EXECUTABLE`, defaulting to the literal `"codex"` (PATH lookup). The codex binary must be `codex-cli ≈ 0.139` (app-server v2 protocol).
- **All artifact paths are repository-relative and resolved through `ArtifactPath.ResolveRepositoryPath(repository, relativePath)`** (root-confinement guarded). The canonical paths (from `OrchestrationArtifactPaths`, verified against the codebase):
  - Plan: `.agents/plan.md`
  - Milestones dir: `.agents/milestones`, glob `m*.md`
  - Live handoff: `.agents/handoffs/handoff.md` (**plural** `handoffs` dir), rotated → `.agents/handoffs/handoff.{N:0000}.md`
  - Live decisions: `.agents/decisions/decisions.md` (**nested** under `decisions/`), rotated → `.agents/decisions/decisions.{N:0000}.md`
  - Operational context: `.agents/operational_context.md`; operational delta: `.agents/operational_delta.md`
- **Sandbox/effort postures (copied verbatim from `RepositoryOrchestrator.BuildOperationalSpec`/`BuildDecisionSpec`):**
  - Execution turns: `SessionRole.OperationalExecution`, `SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false)`, `EffortProfile(AgentEffortLevel.Medium, Identifier: null)` for **both** Start and Continue.
  - Decision turns: `SessionRole.Decision`, `SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false)`, `EffortProfile(AgentEffortLevel.High, Identifier: "xhigh")`.
  - Transfer's `UpdateOperationalContext` one-shot: operational spec at `EffortProfile(AgentEffortLevel.High, Identifier: "xhigh")`.
  - `RequiresApproval: false` → codex approval policy `never` (no interactive prompt can wedge the automated run).
- **Prompts are source-generated** into `CommandCenter.Core.Prompts.<Name>` by `Lib.Prompts`. NEVER hardcode template text. Use the generated `Render(...)`/`.Text` members so `SourceHash` provenance stays aligned:
  - `StartExecution.Render(string? plan)`
  - `ContinueExecution.Render(string? plan, string? handoff, string? decisions)` — argument order is **(plan, handoff, decisions)**.
  - `StartDecisionSession.Render(string? operationalContext)`
  - `GetNextDecisions.Render(string? handoff)`
  - `StartDecisionSessionFromTransfer.Render(string? operationalContext)`
  - `ProduceOperationalDelta.Text` (static, no holes)
  - `UpdateOperationalContext.Text` (static, no holes)
- **Verify-after-write discipline (the loop's correctness anchors):** every codex turn must check `AgentTurnResult.State == AgentTurnState.Completed` before proceeding; after each execution turn assert `.agents/handoffs/handoff.md` exists; after each decision submit assert `.agents/decisions/decisions.md` exists. Any failed gate stops the loop with an error (never an infinite retry).
- **Cancellation:** ONE `CancellationTokenSource`, cancelled by `Console.CancelKeyPress` (with `e.Cancel = true` so the runtime does not hard-kill the CLI before codex teardown). The token is threaded into every `RunOneShotAsync`/`RunTurnAsync`. Killing codex happens via session disposal (`AgentProcess.DisposeAsync` → `process.Kill(entireProcessTree: true)`), reached automatically for one-shot turns (their `await using` session disposes on cancel) and explicitly for the warm decision session (`IAgentRuntime.CloseSessionAsync`).

## Preconditions / Assumptions (MVP scope)

The CLI implements exactly the loop in the spec and assumes the repository is already **planned**:

- `.agents/plan.md` exists.
- `.agents/milestones/m*.md` exist (authored by the prior planning flow). The CLI does **not** run plan authoring or `ExtractMilestones`. The epic-complete gate **aggregates** checkboxes from **both** `.agents/plan.md` (if present) and every `.agents/milestones/m*.md`; the epic is complete only when ≥1 checkbox exists across them and every one is checked (files with zero checkboxes contribute nothing and never block, so a zero-checkbox repo proceeds to run execution). **Caveat:** if a repo's `.agents/plan.md` still carries its own task checkboxes that are never meant to be checked (i.e. it was *not* rewritten into a milestone-pointer index by `ExtractMilestones`), those unchecked boxes will hold the epic open forever — confirm your repos' `plan.md` checkbox shape before relying on this.
- `.agents/operational_context.md` is required by the decision-session seed. The backend creates it during Execute Plan by copying `plan.md`. The CLI replicates this safety net: at the top of every iteration, if `.agents/operational_context.md` is missing it is created by copying `.agents/plan.md`.
- **Transfer recycling** (the router's `Transfer` branch) is implemented (Task 9) to keep the SessionRouter meaningful, but a deployment may keep the decision session in steady-state `Continue` indefinitely — `Continue` (warm reuse) is the documented steady state.

## Control Flow (the loop the CLI runs — mirrors the spec exactly)

```
RunAsync(ct):
  loop:
    ct.ThrowIfCancellationRequested()
    # ---- LoopStart ----
    if IsEpicComplete(.agents/plan.md + .agents/milestones/m*.md):  # all checkboxes (aggregated) checked, >=1 exists
        return EpicCompleted                                # caller prints "Epic completed. Press any key to exit."
    EnsureOperationalContext()                              # copy plan.md -> operational_context.md if missing
    decisionsExist = exists(.agents/decisions/decisions.md)
    handoffExists  = exists(.agents/handoffs/handoff.md)

    if decisionsExist OR NOT handoffExists:                 # ---- Branch A: execution then decision ----
        if decisionsExist: rotate(decisions.md)            # read + archive decisions.{N:0000}.md + delete live
        if handoffExists:  rotate(handoff.md)              # read + archive handoff.{N:0000}.md + delete live
        ExecutionStep.Run(ct)                              # Start/ContinueExecution one-shot; print Output; verify NEW handoff.md
        DecisionSession.Run(ct)                            # via SessionRouter; print Output; persist + verify NEW decisions.md
    else:                                                  # ---- Branch B: decision only (resume after interrupted execution) ----
        rotate(handoff.md)
        DecisionSession.Run(ct)                            # via SessionRouter; print Output; persist + verify NEW decisions.md
    # goto LoopStart
```

Notes on faithfulness:
- `rotate()` = read live content + write a numbered archive `{base}.{N:0000}.md` (N = max existing 4-digit sequence on disk + 1, abort if target exists) + delete the live file. This is the orchestrator's loop-path rotation (write-numbered + delete-live), deferred to loop-start per the spec (the orchestrator rotates immediately after each write; the artifact effect is identical).
- The execution prompt's `handoff`/`decisions` inputs and the decision prompt's `handoff` input are resolved via **"latest = live file if present else highest numbered archive"** (mirrors `RepositoryOrchestrator.ReadLatestHandoffAsync`), so the post-rotation state still feeds the correct prior content, and `continuing = (latest handoff != null)` decides Start vs Continue execution.

## File Structure

```
src/CommandCenter.CLI/
  CommandCenter.CLI.csproj          # Microsoft.NET.Sdk console app; refs Agents, Core, Orchestration
  Program.cs                        # entrypoint: args -> DI -> CTS+CancelKeyPress -> LoopRunner -> outcome message
  CliArguments.cs                   # parse/validate REPO_DIR -> Repository
  LoopConsole.cs                    # ILoopConsole + ConsoleLoopConsole (print phase/message/delta/info/error)
  MilestoneGate.cs                  # epic-complete check (ports CountCheckboxes verbatim)
  LoopArtifacts.cs                  # rotation, ReadLatest{Handoff,Decisions}, PersistDecisions, EnsureOperationalContext
  AgentSpecs.cs                     # BuildOperationalSpec / BuildDecisionSpec
  ExecutionStep.cs                  # run Start/ContinueExecution one-shot + verify handoff
  DecisionSession.cs               # warm read-only session: router + seed-once + GetNextDecisions + persist + verify + Transfer
  LoopRunner.cs                     # the LoopStart state machine (Branch A/B); IAsyncDisposable owns DecisionSession
  LoopOutcome.cs                    # enum { EpicCompleted, Cancelled, Failed } + LoopStepException

tests/CommandCenter.CLI.Tests/
  CommandCenter.CLI.Tests.csproj    # xUnit; refs CommandCenter.CLI, CommandCenter.Core, CommandCenter.Agents
  TestDoubles.cs                    # FakeAgentRuntime, FakeAgentSession, RecordingLoopConsole
  CliArgumentsTests.cs
  MilestoneGateTests.cs
  LoopArtifactsTests.cs
  AgentSpecsTests.cs
  ExecutionStepTests.cs
  DecisionSessionTests.cs
  LoopRunnerTests.cs
```

Each file has one responsibility; files that change together live together. The testable core (`MilestoneGate`, `LoopArtifacts`, `ExecutionStep`, `DecisionSession`, `LoopRunner`) takes its dependencies as constructor parameters (`IArtifactStore`, `IAgentRuntime`, `IDecisionSessionRouter`, `ILoopConsole`, `Repository`) so tests drive the whole loop with an in-memory store and a fake runtime — no real codex process required.

---

### Task 1: Project scaffold + solution registration

**Files:**
- Create: `src/CommandCenter.CLI/CommandCenter.CLI.csproj`
- Create: `src/CommandCenter.CLI/Program.cs` (temporary stub)
- Modify: `CommandCenter.slnx`

**Interfaces:**
- Produces: a buildable `CommandCenter.CLI` console project referencing `CommandCenter.Agents`, `CommandCenter.Core`, `CommandCenter.Orchestration`.

- [ ] **Step 1: Create the csproj**

Create `src/CommandCenter.CLI/CommandCenter.CLI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>CommandCenter.Cli</RootNamespace>
    <!-- The CLI does not reference CommandCenter.Execution, so it must opt out of the
         ExecutionContext using-alias injected by Directory.Build.props (same as Agents/Decisions). -->
    <UseExecutionContextAlias>false</UseExecutionContextAlias>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CommandCenter.Agents\CommandCenter.Agents.csproj" />
    <ProjectReference Include="..\CommandCenter.Core\CommandCenter.Core.csproj" />
    <ProjectReference Include="..\CommandCenter.Orchestration\CommandCenter.Orchestration.csproj" />
  </ItemGroup>

  <!-- The loop's components are `internal`; the test project drives them directly. -->
  <ItemGroup>
    <InternalsVisibleTo Include="CommandCenter.CLI.Tests" />
  </ItemGroup>

</Project>
```

> Note: the test assembly name is `CommandCenter.CLI.Tests` (the csproj file name), so `InternalsVisibleTo Include="CommandCenter.CLI.Tests"` matches. If you set a custom `<AssemblyName>` on the test project, use that value instead.

- [ ] **Step 2: Create a temporary Program.cs stub**

Create `src/CommandCenter.CLI/Program.cs`:

```csharp
namespace CommandCenter.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        System.Console.WriteLine("CommandCenter.CLI scaffold");
        return 0;
    }
}
```

- [ ] **Step 3: Register the project in the solution**

In `CommandCenter.slnx`, add this line inside the existing `<Folder Name="/src/">` element (the `.slnx` is the XML solution format; no GUIDs required):

```xml
<Project Path="src/CommandCenter.CLI/CommandCenter.CLI.csproj" />
```

- [ ] **Step 4: Build to verify the scaffold compiles**

Run: `dotnet build src/CommandCenter.CLI/CommandCenter.CLI.csproj -c Debug`
Expected: `Build succeeded`. If you get `CS0246: ExecutionContext` or an alias error, confirm `<UseExecutionContextAlias>false</UseExecutionContextAlias>` is present.

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/ CommandCenter.slnx
git commit -m "feat(cli): scaffold CommandCenter.CLI console project"
```

---

### Task 2: CLI argument parsing → Repository

**Files:**
- Create: `src/CommandCenter.CLI/CliArguments.cs`
- Create: `tests/CommandCenter.CLI.Tests/CommandCenter.CLI.Tests.csproj`
- Create: `tests/CommandCenter.CLI.Tests/CliArgumentsTests.cs`
- Modify: `CommandCenter.slnx`

**Interfaces:**
- Produces: `static CliArguments.TryParse(string[] args, out Repository repository, out string error)` returning `bool`. `Repository` is `CommandCenter.Core.Repositories.Repository { Guid Id; string Name; string Path; }`.

- [ ] **Step 1: Create the test project**

Create `tests/CommandCenter.CLI.Tests/CommandCenter.CLI.Tests.csproj` (mirror the existing `tests/CommandCenter.Backend.Tests` package versions — adjust versions to match that csproj if they differ):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <UseExecutionContextAlias>false</UseExecutionContextAlias>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CommandCenter.CLI\CommandCenter.CLI.csproj" />
    <ProjectReference Include="..\..\src\CommandCenter.Core\CommandCenter.Core.csproj" />
    <ProjectReference Include="..\..\src\CommandCenter.Agents\CommandCenter.Agents.csproj" />
  </ItemGroup>

</Project>
```

Add to `CommandCenter.slnx` inside the `<Folder Name="/tests/">` element:

```xml
<Project Path="tests/CommandCenter.CLI.Tests/CommandCenter.CLI.Tests.csproj" />
```

- [ ] **Step 2: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/CliArgumentsTests.cs`:

```csharp
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class CliArgumentsTests
{
    [Fact]
    public void TryParse_WithExistingDirectory_ReturnsRepositoryWithAbsolutePath()
    {
        string dir = Directory.CreateTempSubdirectory("cc-cli-args").FullName;

        bool ok = CliArguments.TryParse(new[] { dir }, out var repository, out string error);

        Assert.True(ok, error);
        Assert.Equal(Path.GetFullPath(dir), repository.Path);
        Assert.Equal(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)), repository.Name);
        Assert.NotEqual(Guid.Empty, repository.Id);
    }

    [Fact]
    public void TryParse_WithNoArgs_Fails()
    {
        bool ok = CliArguments.TryParse(Array.Empty<string>(), out _, out string error);

        Assert.False(ok);
        Assert.Contains("REPO_DIR", error);
    }

    [Fact]
    public void TryParse_WithMissingDirectory_Fails()
    {
        bool ok = CliArguments.TryParse(new[] { "Z:/does/not/exist/cc-cli" }, out _, out string error);

        Assert.False(ok);
        Assert.Contains("does not exist", error);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests -c Debug`
Expected: FAIL — `CliArguments` does not exist (compile error).

- [ ] **Step 4: Implement CliArguments**

Create `src/CommandCenter.CLI/CliArguments.cs`:

```csharp
using CommandCenter.Core.Repositories;

namespace CommandCenter.Cli;

/// <summary>Parses and validates the single REPO_DIR positional argument into a <see cref="Repository"/>.</summary>
internal static class CliArguments
{
    public static bool TryParse(string[] args, out Repository repository, out string error)
    {
        repository = new Repository();

        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            error = "Usage: CommandCenter.CLI <REPO_DIR>  (REPO_DIR is required)";
            return false;
        }

        string path = Path.GetFullPath(args[0]);
        if (!Directory.Exists(path))
        {
            error = $"Repository directory does not exist: {path}";
            return false;
        }

        repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = path,
        };
        error = string.Empty;
        return true;
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests -c Debug`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/CommandCenter.CLI/CliArguments.cs tests/CommandCenter.CLI.Tests/ CommandCenter.slnx
git commit -m "feat(cli): parse and validate REPO_DIR argument into Repository"
```

---

### Task 3: Console abstraction

**Files:**
- Create: `src/CommandCenter.CLI/LoopConsole.cs`
- Create: `tests/CommandCenter.CLI.Tests/TestDoubles.cs` (start the shared test-doubles file here)

**Interfaces:**
- Produces: `interface ILoopConsole { void Phase(string phase); void Message(string content); void Delta(string text); void Info(string text); void Warn(string text); void Error(string text); }` and `ConsoleLoopConsole : ILoopConsole`.
- Produces (test): `RecordingLoopConsole : ILoopConsole` capturing all calls for assertions.

- [ ] **Step 1: Implement the console abstraction**

Create `src/CommandCenter.CLI/LoopConsole.cs`:

```csharp
namespace CommandCenter.Cli;

/// <summary>Sink for everything the loop prints. Abstracted so tests can capture output.</summary>
internal interface ILoopConsole
{
    void Phase(string phase);
    void Message(string content);
    void Delta(string text);
    void Info(string text);
    void Warn(string text);
    void Error(string text);
}

/// <summary>Writes loop progress to the real console. Deltas stream inline; messages/info/warn/error get prefixes.</summary>
internal sealed class ConsoleLoopConsole : ILoopConsole
{
    public void Phase(string phase) => Console.WriteLine($"\n=== {phase} ===");
    public void Message(string content) => Console.WriteLine(content);
    public void Delta(string text) => Console.Write(text);
    public void Info(string text) => Console.WriteLine($"[ok] {text}");
    public void Warn(string text) => Console.WriteLine($"[warn] {text}");
    public void Error(string text) => Console.Error.WriteLine($"[error] {text}");
}
```

- [ ] **Step 2: Add the recording test double**

Create `tests/CommandCenter.CLI.Tests/TestDoubles.cs` (this file is extended in later tasks):

```csharp
using System.Collections.Concurrent;
using CommandCenter.Cli;

namespace CommandCenter.Cli.Tests;

internal sealed class RecordingLoopConsole : ILoopConsole
{
    public ConcurrentQueue<(string Kind, string Text)> Events { get; } = new();
    public void Phase(string phase) => Events.Enqueue(("phase", phase));
    public void Message(string content) => Events.Enqueue(("message", content));
    public void Delta(string text) => Events.Enqueue(("delta", text));
    public void Info(string text) => Events.Enqueue(("info", text));
    public void Warn(string text) => Events.Enqueue(("warn", text));
    public void Error(string text) => Events.Enqueue(("error", text));

    public IReadOnlyList<string> Messages =>
        Events.Where(e => e.Kind == "message").Select(e => e.Text).ToList();
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build tests/CommandCenter.CLI.Tests -c Debug`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/CommandCenter.CLI/LoopConsole.cs tests/CommandCenter.CLI.Tests/TestDoubles.cs
git commit -m "feat(cli): add ILoopConsole abstraction and recording test double"
```

---

### Task 4: Milestone epic-complete gate

**Files:**
- Create: `src/CommandCenter.CLI/MilestoneGate.cs`
- Create: `tests/CommandCenter.CLI.Tests/MilestoneGateTests.cs`

**Interfaces:**
- Consumes: `IArtifactStore` (`CommandCenter.Core.Artifacts`), `Repository`, `OrchestrationArtifactPaths` (`CommandCenter.Orchestration`).
- Produces: `MilestoneGate(IArtifactStore store, Repository repository)` with `Task<bool> IsEpicCompleteAsync()` that **aggregates** checkboxes from `.agents/plan.md` (if present) and every `.agents/milestones/m*.md`. Internal `static (int total, int completed) CountCheckboxes(string content)` ported verbatim from `RepositoryProjectionService`.

- [ ] **Step 1: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/MilestoneGateTests.cs`:

```csharp
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class MilestoneGateTests
{
    private static (MilestoneGate Gate, IArtifactStore Store, Repository Repo) NewGate()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new MilestoneGate(store, repo), store, repo);
    }

    private static string Resolve(Repository repo, string rel) =>
        ArtifactPath.ResolveRepositoryPath(repo, rel);

    [Theory]
    [InlineData("- [x] a\n- [x] b", 2, 2)]
    [InlineData("- [ ] a\n- [x] b", 2, 1)]
    [InlineData("- [X] a", 1, 1)]
    [InlineData("* [x] a\n+ [x] b", 0, 0)]              // non-hyphen bullets ignored
    [InlineData("```\n- [ ] fenced\n```\n- [x] real", 1, 1)] // fenced lines ignored
    [InlineData("- [-] partial\n- [/] partial", 0, 0)]  // unknown marks ignored
    public void CountCheckboxes_MatchesBackendRule(string content, int total, int completed)
    {
        var (t, c) = MilestoneGate.CountCheckboxes(content);
        Assert.Equal(total, t);
        Assert.Equal(completed, c);
    }

    [Fact]
    public async Task IsEpicComplete_AllMilestonesFullyChecked_ReturnsTrue()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a\n- [x] b");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m2.md"), "- [x] c");

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_OneMilestoneIncomplete_ReturnsFalse()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m2.md"), "- [ ] c");

        Assert.False(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_NoMilestoneFiles_ReturnsFalse()
    {
        var (gate, _, _) = NewGate();
        Assert.False(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_MilestoneWithZeroCheckboxes_ReturnsFalse()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "# heading only, no tasks");
        Assert.False(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_UncheckedPlanBox_BlocksEvenWhenMilestonesComplete()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "- [ ] open item in the plan");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.False(await gate.IsEpicCompleteAsync());   // aggregate: plan.md still has an unchecked box
    }

    [Fact]
    public async Task IsEpicComplete_PlanAndMilestonesAllChecked_ReturnsTrue()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "- [x] plan item");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_PointerIndexPlanWithNoCheckboxes_DoesNotBlock()
    {
        var (gate, store, repo) = NewGate();
        // plan.md rewritten into a milestone-pointer index by ExtractMilestones => zero checkboxes.
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "# Plan\n(See ./milestones/m1.md)");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.True(await gate.IsEpicCompleteAsync());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter MilestoneGateTests -c Debug`
Expected: FAIL — `MilestoneGate` does not exist.

- [ ] **Step 3: Implement MilestoneGate**

Create `src/CommandCenter.CLI/MilestoneGate.cs`:

```csharp
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// LoopStart epic-complete gate. Aggregates GitHub task-list checkboxes (parsed with the canonical
/// RepositoryProjectionService.CountCheckboxes rule) across .agents/plan.md (if present) and every
/// .agents/milestones/m*.md. The epic is complete only when at least one checkbox exists across them
/// and every checkbox is checked. Files with zero checkboxes contribute nothing and never block.
/// </summary>
internal sealed class MilestoneGate(IArtifactStore store, Repository repository)
{
    public async Task<bool> IsEpicCompleteAsync()
    {
        int total = 0;
        int completed = 0;

        // .agents/plan.md (if present) contributes its checkboxes alongside the milestones.
        string? plan = await store.ReadAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan));
        if (plan is not null)
        {
            Accumulate(plan, ref total, ref completed);
        }

        string dir = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.MilestonesDirectory);
        IReadOnlyList<string> files = await store.ListAsync(dir, OrchestrationArtifactPaths.MilestoneSearchPattern);
        foreach (string file in files)
        {
            string content = await store.ReadAsync(file) ?? string.Empty;
            Accumulate(content, ref total, ref completed);
        }

        return total > 0 && completed == total;
    }

    private static void Accumulate(string content, ref int total, ref int completed)
    {
        (int t, int c) = CountCheckboxes(content);
        total += t;
        completed += c;
    }

    // Ported verbatim from RepositoryProjectionService.CountCheckboxes (the canonical, authoritative rule).
    internal static (int total, int completed) CountCheckboxes(string content)
    {
        int total = 0;
        int completed = 0;
        bool insideFence = false;

        foreach (ReadOnlySpan<char> rawLine in content.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> line = rawLine.TrimStart();
            if (line.StartsWith("```"))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence || line.Length < 6)
            {
                continue;
            }

            if (line[0] != '-' || line[1] != ' ' || line[2] != '[' || line[4] != ']' || line[5] != ' ')
            {
                continue;
            }

            char mark = line[3];
            if (mark == ' ')
            {
                total++;
            }
            else if (mark is 'x' or 'X')
            {
                total++;
                completed++;
            }
        }

        return (total, completed);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter MilestoneGateTests -c Debug`
Expected: PASS (all theory cases + facts).

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/MilestoneGate.cs tests/CommandCenter.CLI.Tests/MilestoneGateTests.cs
git commit -m "feat(cli): epic-complete gate ported from backend checkbox parser"
```

---

### Task 5: Artifact rotation + latest-read + persist helpers

**Files:**
- Create: `src/CommandCenter.CLI/LoopArtifacts.cs`
- Create: `tests/CommandCenter.CLI.Tests/LoopArtifactsTests.cs`

**Interfaces:**
- Consumes: `IArtifactStore`, `Repository`, `OrchestrationArtifactPaths`, `ArtifactPath`.
- Produces: `LoopArtifacts(IArtifactStore store, Repository repository)` with:
  - `Task<bool> ExistsAsync(string relativePath)`
  - `Task<string?> ReadAsync(string relativePath)`
  - `Task<string?> RotateLiveHandoffAsync()` / `Task<string?> RotateLiveDecisionsAsync()` — read+archive(numbered)+delete-live, returns rotated content or null.
  - `Task<(string? Content, string? RelativePath)> ReadLatestHandoffAsync()` / `ReadLatestDecisionsAsync()` — live else highest numbered.
  - `Task PersistDecisionsAsync(string decisions)` — write numbered archive + canonical `decisions.md`.
  - `Task EnsureOperationalContextAsync()` — copy `plan.md` → `operational_context.md` if missing.
  - `Task<string?> ReadPlanAsync()`, `Task WriteAsync(string rel, string content)`.

- [ ] **Step 1: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/LoopArtifactsTests.cs`:

```csharp
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class LoopArtifactsTests
{
    private static (LoopArtifacts Art, IArtifactStore Store, Repository Repo) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new LoopArtifacts(store, repo), store, repo);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task RotateLiveHandoff_ArchivesNumberedAndDeletesLive()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        string? rotated = await art.RotateLiveHandoffAsync();

        Assert.Equal("H1", rotated);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Equal("H1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task RotateLiveHandoff_WhenAbsent_ReturnsNull()
    {
        var (art, _, _) = New();
        Assert.Null(await art.RotateLiveHandoffAsync());
    }

    [Fact]
    public async Task RotateLiveHandoff_SequenceIsDiskMaxPlusOne()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(1)), "old");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(2)), "old2");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H3");

        await art.RotateLiveHandoffAsync();

        Assert.Equal("H3", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(3))));
    }

    [Fact]
    public async Task ReadLatestHandoff_PrefersLiveThenHighestNumbered()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(1)), "n1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(2)), "n2");

        var numbered = await art.ReadLatestHandoffAsync();
        Assert.Equal("n2", numbered.Content);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "live");
        var live = await art.ReadLatestHandoffAsync();
        Assert.Equal("live", live.Content);
    }

    [Fact]
    public async Task PersistDecisions_WritesNumberedAndCanonical()
    {
        var (art, store, repo) = New();
        await art.PersistDecisionsAsync("D1");

        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
    }

    [Fact]
    public async Task EnsureOperationalContext_CopiesPlanWhenMissing()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        await art.EnsureOperationalContextAsync();

        Assert.Equal("PLAN", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }
}
```

> Verification sub-step before implementing: open `src/CommandCenter.Orchestration/OrchestrationArtifactPaths.cs` and confirm the member names `Plan`, `OperationalContext`, `OperationalDelta`, `Decisions`, `DecisionsDirectory`, `HistoricalDecision(int)`, `HistoricalDecisionSearchPattern`, `HandoffsDirectory`, `LiveHandoff`, `HistoricalHandoff(int)`, `HistoricalHandoffSearchPattern`, `MilestonesDirectory`, `MilestoneSearchPattern`. If `Plan`/`OperationalContext`/`OperationalDelta` are named differently, use the actual member (the path strings are `.agents/plan.md`, `.agents/operational_context.md`, `.agents/operational_delta.md`).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter LoopArtifactsTests -c Debug`
Expected: FAIL — `LoopArtifacts` does not exist.

- [ ] **Step 3: Implement LoopArtifacts**

Create `src/CommandCenter.CLI/LoopArtifacts.cs`:

```csharp
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// All .agents/* disk effects for the loop: rotation (read+archive numbered+delete live), restart-safe
/// latest reads (live or highest numbered), decision persistence (numbered + canonical), and the
/// operational_context safety copy. Rotation is move-semantics (matches RepositoryOrchestrator's loop path).
/// </summary>
internal sealed class LoopArtifacts(IArtifactStore store, Repository repository)
{
    public Task<bool> ExistsAsync(string relativePath) =>
        store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) =>
        store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) =>
        store.WriteAsync(Resolve(relativePath), content);

    public Task<string?> ReadPlanAsync() => ReadAsync(OrchestrationArtifactPaths.Plan);

    public Task<string?> RotateLiveHandoffAsync() => RotateAsync(
        OrchestrationArtifactPaths.LiveHandoff,
        OrchestrationArtifactPaths.HandoffsDirectory,
        OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
        "handoff",
        OrchestrationArtifactPaths.HistoricalHandoff);

    public Task<string?> RotateLiveDecisionsAsync() => RotateAsync(
        OrchestrationArtifactPaths.Decisions,
        OrchestrationArtifactPaths.DecisionsDirectory,
        OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
        "decisions",
        OrchestrationArtifactPaths.HistoricalDecision);

    public Task<(string? Content, string? RelativePath)> ReadLatestHandoffAsync() => ReadLatestAsync(
        OrchestrationArtifactPaths.LiveHandoff,
        OrchestrationArtifactPaths.HandoffsDirectory,
        OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
        "handoff",
        OrchestrationArtifactPaths.HistoricalHandoff);

    public Task<(string? Content, string? RelativePath)> ReadLatestDecisionsAsync() => ReadLatestAsync(
        OrchestrationArtifactPaths.Decisions,
        OrchestrationArtifactPaths.DecisionsDirectory,
        OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
        "decisions",
        OrchestrationArtifactPaths.HistoricalDecision);

    public async Task PersistDecisionsAsync(string decisions)
    {
        int sequence = await NextSequenceAsync(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions");
        await store.WriteAsync(Resolve(OrchestrationArtifactPaths.HistoricalDecision(sequence)), decisions);
        await store.WriteAsync(Resolve(OrchestrationArtifactPaths.Decisions), decisions);
    }

    public async Task EnsureOperationalContextAsync()
    {
        if (await ExistsAsync(OrchestrationArtifactPaths.OperationalContext))
        {
            return;
        }

        string? plan = await ReadPlanAsync();
        if (plan is not null)
        {
            await WriteAsync(OrchestrationArtifactPaths.OperationalContext, plan);
        }
    }

    private string Resolve(string relativePath) =>
        ArtifactPath.ResolveRepositoryPath(repository, relativePath);

    private async Task<string?> RotateAsync(
        string liveRelative, string directoryRelative, string searchPattern, string baseName, Func<int, string> historical)
    {
        string? content = await store.ReadAsync(Resolve(liveRelative));
        if (content is null)
        {
            return null;
        }

        int sequence = await NextSequenceAsync(directoryRelative, searchPattern, baseName);
        string target = Resolve(historical(sequence));
        if (await store.ExistsAsync(target))
        {
            throw new IOException($"Historical artifact already exists: {historical(sequence)}");
        }

        await store.WriteAsync(target, content);
        await store.DeleteAsync(Resolve(liveRelative));
        return content;
    }

    private async Task<(string? Content, string? RelativePath)> ReadLatestAsync(
        string liveRelative, string directoryRelative, string searchPattern, string baseName, Func<int, string> historical)
    {
        string? live = await store.ReadAsync(Resolve(liveRelative));
        if (live is not null)
        {
            return (live, liveRelative);
        }

        int highest = await HighestSequenceAsync(directoryRelative, searchPattern, baseName);
        if (highest == 0)
        {
            return (null, null);
        }

        string rel = historical(highest);
        return (await store.ReadAsync(Resolve(rel)), rel);
    }

    private async Task<int> NextSequenceAsync(string directoryRelative, string searchPattern, string baseName) =>
        await HighestSequenceAsync(directoryRelative, searchPattern, baseName) + 1;

    private async Task<int> HighestSequenceAsync(string directoryRelative, string searchPattern, string baseName)
    {
        IReadOnlyList<string> files = await store.ListAsync(Resolve(directoryRelative), searchPattern);
        int max = 0;
        foreach (string file in files)
        {
            string[] parts = Path.GetFileName(file).Split('.');
            if (parts.Length < 3 || !string.Equals(parts[0], baseName, StringComparison.Ordinal))
            {
                continue;
            }

            string segment = parts[^2];
            if (segment.Length == 4 && int.TryParse(segment, out int parsed) && parsed > 0)
            {
                max = Math.Max(max, parsed);
            }
        }

        return max;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter LoopArtifactsTests -c Debug`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/LoopArtifacts.cs tests/CommandCenter.CLI.Tests/LoopArtifactsTests.cs
git commit -m "feat(cli): artifact rotation, latest-read, decision persistence helpers"
```

---

### Task 6: Agent session specs

**Files:**
- Create: `src/CommandCenter.CLI/AgentSpecs.cs`
- Create: `tests/CommandCenter.CLI.Tests/AgentSpecsTests.cs`

**Interfaces:**
- Consumes: `AgentSessionSpec, SessionIdentity, SessionRole, SandboxProfile, EffortProfile, AgentEffortLevel` (`CommandCenter.Agents.Models`), `Repository`.
- Produces: `static AgentSessionSpec AgentSpecs.Operational(Repository repo, AgentEffortLevel level, string? identifier)` and `static AgentSessionSpec AgentSpecs.Decision(Repository repo)`.

- [ ] **Step 1: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/AgentSpecsTests.cs`:

```csharp
using CommandCenter.Cli;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class AgentSpecsTests
{
    private static readonly Repository Repo = new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    [Fact]
    public void Operational_IsWorkspaceWriteNoApprovalAtGivenEffort()
    {
        AgentSessionSpec spec = AgentSpecs.Operational(Repo, AgentEffortLevel.Medium, identifier: null);

        Assert.Equal(SessionRole.OperationalExecution, spec.Role);
        Assert.Equal("workspace-write", spec.Sandbox.Identifier);
        Assert.True(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.Medium, spec.Effort.Level);
        Assert.Null(spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }

    [Fact]
    public void Decision_IsReadOnlyHighXhigh()
    {
        AgentSessionSpec spec = AgentSpecs.Decision(Repo);

        Assert.Equal(SessionRole.Decision, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Equal(AgentEffortLevel.High, spec.Effort.Level);
        Assert.Equal("xhigh", spec.Effort.Identifier);
        Assert.Equal(Repo.Path, spec.WorkingDirectory);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter AgentSpecsTests -c Debug`
Expected: FAIL — `AgentSpecs` does not exist.

- [ ] **Step 3: Implement AgentSpecs**

Create `src/CommandCenter.CLI/AgentSpecs.cs`:

```csharp
using CommandCenter.Agents.Models;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Cli;

/// <summary>
/// The two codex session postures copied verbatim from RepositoryOrchestrator.BuildOperationalSpec /
/// BuildDecisionSpec. RepositoryId namespaces the session registry; WorkingDirectory is the repo dir.
/// </summary>
internal static class AgentSpecs
{
    public static AgentSessionSpec Operational(Repository repository, AgentEffortLevel level, string? identifier) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.OperationalExecution,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(level, identifier),
            repository.Path);

    public static AgentSessionSpec Decision(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);
}
```

> Verification sub-step: confirm `SessionIdentity.New()` exists and the `AgentSessionSpec` positional ctor order is `(SessionIdentity, string repositoryId, SessionRole, SandboxProfile, EffortProfile, string? workingDirectory, IReadOnlyDictionary<string,string>? startupOptions)` in `src/CommandCenter.Agents/Models/AgentSessionSpec.cs`. Confirm `EffortProfile(AgentEffortLevel Level, string? Identifier = null)` parameter names.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter AgentSpecsTests -c Debug`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/AgentSpecs.cs tests/CommandCenter.CLI.Tests/AgentSpecsTests.cs
git commit -m "feat(cli): operational and decision codex session specs"
```

---

### Task 7: Execution step + shared agent test doubles

**Files:**
- Create: `src/CommandCenter.CLI/LoopOutcome.cs`
- Create: `src/CommandCenter.CLI/ExecutionStep.cs`
- Modify: `tests/CommandCenter.CLI.Tests/TestDoubles.cs` (add `FakeAgentRuntime`, `FakeAgentSession`)
- Create: `tests/CommandCenter.CLI.Tests/ExecutionStepTests.cs`

**Interfaces:**
- Consumes: `IAgentRuntime` (`CommandCenter.Agents.Abstractions`), `LoopArtifacts`, `ILoopConsole`, `Repository`, the prompt catalog (`StartExecution`, `ContinueExecution`).
- Produces:
  - `enum LoopOutcome { EpicCompleted, Cancelled, Failed }` and `sealed class LoopStepException(string message) : Exception(message)`.
  - `ExecutionStep(IAgentRuntime runtime, LoopArtifacts artifacts, ILoopConsole console, Repository repository)` with `Task RunAsync(CancellationToken ct)`. Resolves latest handoff/decisions, renders Start/Continue, runs a one-shot operational turn, prints `Output`, verifies a new `handoff.md`, throws `LoopStepException` on any failed gate.
- Produces (test): `FakeAgentRuntime` implementing `IAgentRuntime` with a scripted turn queue; each scripted turn gets `(spec, prompt)` and may mutate an `IArtifactStore` (to simulate the agent writing `handoff.md`) then returns an `AgentTurnResult`.

- [ ] **Step 1: Implement LoopOutcome (no test needed — type-only)**

Create `src/CommandCenter.CLI/LoopOutcome.cs`:

```csharp
namespace CommandCenter.Cli;

internal enum LoopOutcome
{
    EpicCompleted,
    Cancelled,
    Failed,
}

/// <summary>A verify/agent gate failed; aborts the loop (never retried).</summary>
internal sealed class LoopStepException(string message) : Exception(message);
```

- [ ] **Step 2: Add the agent test doubles**

Append to `tests/CommandCenter.CLI.Tests/TestDoubles.cs`:

```csharp
// --- appended ---
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;

namespace CommandCenter.Cli.Tests;

/// <summary>A scripted codex turn: inspect (spec, prompt), optionally mutate the store, return a result.</summary>
internal sealed record ScriptedTurn(Func<AgentSessionSpec, string, IArtifactStore, AgentTurnResult> Handler);

internal static class Turns
{
    public static AgentTurnResult Completed(string output) =>
        new(0, AgentTurnState.Completed, output, new AgentTokenUsage(0, 0));

    public static AgentTurnResult Failed(string output = "boom") =>
        new(0, AgentTurnState.Failed, output, new AgentTokenUsage(0, 0));
}

internal sealed class FakeAgentRuntime(IArtifactStore store) : IAgentRuntime
{
    public Queue<ScriptedTurn> OneShotTurns { get; } = new();
    public Queue<ScriptedTurn> SessionTurns { get; } = new();
    public int OpenSessions { get; private set; }
    public int ClosedSessions { get; private set; }
    public List<(AgentSessionSpec Spec, string Prompt)> OneShotCalls { get; } = new();

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec, string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default)
    {
        OneShotCalls.Add((spec, prompt));
        ScriptedTurn turn = OneShotTurns.Dequeue();
        return Task.FromResult(turn.Handler(spec, prompt, store));
    }

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken ct = default)
    {
        OpenSessions++;
        return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec, store));
    }

    public ValueTask CloseSessionAsync(IAgentSession session)
    {
        ClosedSessions++;
        return ValueTask.CompletedTask;
    }

    internal AgentTurnResult RunSessionTurn(AgentSessionSpec spec, string prompt) =>
        SessionTurns.Dequeue().Handler(spec, prompt, store);
}

internal sealed class FakeAgentSession(FakeAgentRuntime runtime, AgentSessionSpec spec, IArtifactStore store) : IAgentSession
{
    public SessionIdentity SessionId => spec.SessionId;
    public string RepositoryId => spec.RepositoryId;
    public SessionRole Role => spec.Role;
    public AgentSessionMode Mode => AgentSessionMode.Persistent;
    public AgentProcessState State => AgentProcessState.Running;
    public int CompletedTurns => 0;
    public AgentTokenUsage TotalUsage => new(0, 0);

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default) =>
        Task.FromResult(runtime.RunSessionTurn(spec, prompt));

    public Task CancelAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

> Verification sub-step: confirm `AgentTokenUsage` ctor shape (`AgentTokenUsage(int PromptTokens, int OutputTokens)`) and the `IAgentSession` member set (`SessionId, RepositoryId, Role, Mode, State, CompletedTurns, TotalUsage, RunTurnAsync, CancelAsync, DisposeAsync`) against `src/CommandCenter.Agents/Abstractions/IAgentSession.cs` and `Models/AgentTokenUsage.cs`; adjust the fake to match.

- [ ] **Step 3: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/ExecutionStepTests.cs`:

```csharp
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class ExecutionStepTests
{
    private static (ExecutionStep Step, FakeAgentRuntime Rt, MemoryArtifactStore Store, LoopArtifacts Art, Repository Repo, RecordingLoopConsole Con) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        return (new ExecutionStep(rt, art, con, repo), rt, store, art, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task Run_FirstIteration_UsesStartExecution_WritesHandoff_Verifies()
    {
        var (step, rt, store, _, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, s) =>
        {
            Assert.Contains("PLAN", prompt);               // StartExecution.Render(plan) includes the plan
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            return Turns.Completed("execution done");
        }));

        await step.RunAsync(CancellationToken.None);

        Assert.True(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Contains("execution done", con.Messages);
    }

    [Fact]
    public async Task Run_WhenAgentDoesNotWriteHandoff_Throws()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("did nothing")));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Run_WhenTurnNotCompleted_Throws()
    {
        var (step, rt, store, _, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => step.RunAsync(CancellationToken.None));
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter ExecutionStepTests -c Debug`
Expected: FAIL — `ExecutionStep` does not exist.

- [ ] **Step 5: Implement ExecutionStep**

Create `src/CommandCenter.CLI/ExecutionStep.cs`:

```csharp
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// One execution codex turn. Mirrors RepositoryOrchestrator.RunExecutionAsync/RunContinuationAsync:
/// render Start/ContinueExecution, run a one-shot workspace-write Medium turn, print the assistant
/// message, then verify the agent wrote a new .agents/handoffs/handoff.md.
/// </summary>
internal sealed class ExecutionStep(
    IAgentRuntime runtime, LoopArtifacts artifacts, ILoopConsole console, Repository repository)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string? plan = await artifacts.ReadPlanAsync();
        (string? handoff, _) = await artifacts.ReadLatestHandoffAsync();
        (string? decisions, _) = await artifacts.ReadLatestDecisionsAsync();

        bool continuing = handoff is not null;
        string phase = continuing ? "ContinueExecution" : "StartExecution";
        string prompt = continuing
            ? ContinueExecution.Render(plan, handoff, decisions)
            : StartExecution.Render(plan);

        console.Phase($"Execution: {phase}");
        AgentTurnResult result = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.Medium, identifier: null),
            prompt,
            StreamToConsole,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new LoopStepException($"Execution turn ended in state {result.State}.");
        }

        console.Message(result.Output);

        if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff))
        {
            throw new LoopStepException(
                "Execution completed but .agents/handoffs/handoff.md was not written.");
        }

        console.Info("New handoff.md verified.");
    }

    private Task StreamToConsole(AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput)
        {
            console.Delta(chunk.Content);
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter ExecutionStepTests -c Debug`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/CommandCenter.CLI/LoopOutcome.cs src/CommandCenter.CLI/ExecutionStep.cs tests/CommandCenter.CLI.Tests/
git commit -m "feat(cli): execution step with handoff verification + agent test doubles"
```

---

### Task 8: Decision session (router-driven Continue path)

**Files:**
- Create: `src/CommandCenter.CLI/DecisionSession.cs`
- Create: `tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs`

**Interfaces:**
- Consumes: `IAgentRuntime`, `IDecisionSessionRouter` (`CommandCenter.Orchestration.Abstractions`), `RouterInputs` (`CommandCenter.Orchestration.Models`), `DecisionRoute`, `LoopArtifacts`, `ILoopConsole`, `Repository`, prompts (`StartDecisionSession`, `GetNextDecisions`).
- Produces: `sealed class DecisionSession(...) : IAsyncDisposable` with `Task RunAsync(CancellationToken ct)`. Owns ONE warm read-only `IAgentSession` reused across iterations; consults the router each round; seeds once with `StartDecisionSession`; proposes with `GetNextDecisions(latestHandoff)`; prints `Output`; persists decisions (numbered + canonical); verifies `decisions.md`; accumulates per-process token pressure for the router; resets on close. (Transfer branch added in Task 9.)

- [ ] **Step 1: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs`:

```csharp
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class DecisionSessionTests
{
    private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con)
        New(int transferThreshold = 200_000)
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(transferThreshold));
        return (new DecisionSession(rt, router, art, con, repo), rt, store, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task Run_SeedsOnce_Proposes_PersistsAndVerifiesDecisions()
    {
        var (session, rt, store, repo, con) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // seed
        {
            Assert.Contains("OPCTX", prompt);
            return Turns.Completed("seeded");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>   // propose
        {
            Assert.Contains("HANDOFF", prompt);
            return Turns.Completed("DECISIONS-TEXT");
        }));

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("DECISIONS-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Contains("DECISIONS-TEXT", con.Messages);
        Assert.Equal(1, rt.OpenSessions);
    }

    [Fact]
    public async Task Run_SecondRound_ReusesWarmSession_NoReseed()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));  // no second seed

        await session.RunAsync(CancellationToken.None);
        await session.RunAsync(CancellationToken.None);

        Assert.Equal(1, rt.OpenSessions);     // warm reuse: only one process opened
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
    }

    [Fact]
    public async Task Run_WhenProposeNotCompleted_ClosesSessionAndThrows()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));
        Assert.Equal(1, rt.ClosedSessions);
    }

    [Fact]
    public async Task Dispose_ClosesWarmSession()
    {
        var (session, rt, store, repo, _) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));

        await session.RunAsync(CancellationToken.None);
        await session.DisposeAsync();

        Assert.Equal(1, rt.ClosedSessions);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter DecisionSessionTests -c Debug`
Expected: FAIL — `DecisionSession` does not exist.

- [ ] **Step 3: Implement DecisionSession (Continue path only; Transfer stub throws)**

Create `src/CommandCenter.CLI/DecisionSession.cs`:

```csharp
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Cli;

/// <summary>
/// The decision-making codex session, routed by the SessionRouter. Mirrors RepositoryOrchestrator's
/// RunDecisionAsync + the auto-submit half of BeginSubmitDecisionsAsync (a fully-automated CLI submits
/// the agent's proposal verbatim). Owns ONE warm read-only process reused across iterations; seeds once
/// with StartDecisionSession(operationalContext); proposes with GetNextDecisions(latestHandoff); persists
/// the proposal to decisions.{N:0000}.md AND canonical decisions.md; verifies decisions.md exists.
/// </summary>
internal sealed class DecisionSession(
    IAgentRuntime runtime,
    IDecisionSessionRouter router,
    LoopArtifacts artifacts,
    ILoopConsole console,
    Repository repository) : IAsyncDisposable
{
    private IAgentSession? session;
    private bool seeded;
    private int decisionTokens;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DecisionRoute route = router.Evaluate(new RouterInputs(decisionTokens, 0));
        // Eligibility downgrade: a Transfer needs a primed warm process to extract a delta from.
        if (route == DecisionRoute.Transfer && !seeded)
        {
            route = DecisionRoute.Continue;
        }

        console.Phase($"Decision (route={route})");

        if (route == DecisionRoute.Transfer)
        {
            await TransferAsync(cancellationToken);
        }

        await EnsureSeededAsync(cancellationToken);

        (string? handoff, _) = await artifacts.ReadLatestHandoffAsync();
        if (handoff is null)
        {
            throw new LoopStepException("No handoff available for the decision session.");
        }

        AgentTurnResult proposed = await session!.RunTurnAsync(
            GetNextDecisions.Render(handoff), StreamToConsole, cancellationToken);

        if (proposed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Decision turn ended in state {proposed.State}.");
        }

        decisionTokens += proposed.Usage.PromptTokens + proposed.Usage.OutputTokens;
        console.Message(proposed.Output);

        // Auto-submit: the CLI is fully automated, so the agent's proposal is persisted verbatim.
        await artifacts.PersistDecisionsAsync(proposed.Output);

        if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
        {
            throw new LoopStepException(".agents/decisions/decisions.md was not written.");
        }

        console.Info("New decisions.md verified.");
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        session ??= await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
        if (seeded)
        {
            return;
        }

        await artifacts.EnsureOperationalContextAsync();
        string operationalContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;

        AgentTurnResult seed = await session.RunTurnAsync(
            StartDecisionSession.Render(operationalContext), onChunk: null, cancellationToken);

        if (seed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Decision seed turn ended in state {seed.State}.");
        }

        seeded = true;
    }

    // Implemented in Task 9. Throwing keeps the Continue-only build honest until then.
    private Task TransferAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("Transfer recycle is implemented in Task 9.");

    private async Task CloseAsync()
    {
        if (session is not null)
        {
            await runtime.CloseSessionAsync(session);
            session = null;
            seeded = false;
            decisionTokens = 0;
        }
    }

    private Task StreamToConsole(AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput)
        {
            console.Delta(chunk.Content);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter DecisionSessionTests -c Debug`
Expected: PASS (4 tests). The default router threshold (200k) keeps every round on `Continue`, so `TransferAsync` is never hit.

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/DecisionSession.cs tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs
git commit -m "feat(cli): router-driven decision session (Continue path) with auto-submit"
```

---

### Task 9: Decision session Transfer recycle path

**Files:**
- Modify: `src/CommandCenter.CLI/DecisionSession.cs`
- Modify: `tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs`

**Interfaces:**
- Consumes: prompts `ProduceOperationalDelta.Text`, `UpdateOperationalContext.Text`, `StartDecisionSessionFromTransfer.Render(operationalContext)`; `OrchestrationArtifactPaths.OperationalDelta`/`.OperationalContext`.
- Produces: a real `TransferAsync` that recycles the decision process when the router elects `Transfer` (decision-session token pressure ≥ threshold). Mirrors `RepositoryOrchestrator.PrepareTransferAsync`.

- [ ] **Step 1: Write the failing test**

Append to `tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs`:

```csharp
    [Fact]
    public async Task Run_WhenTokensExceedThreshold_RecyclesViaTransfer()
    {
        // Threshold 1 forces Transfer on the SECOND round (round 1 seeds & accrues tokens).
        var (session, rt, store, repo, con) = New(transferThreshold: 1);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: seed + propose (propose accrues tokens so round 2 routes Transfer).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer => produce-delta (warm) + close + update-context (one-shot) + reseed-from-transfer + propose.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) => Turns.Completed("DELTA-TEXT")));      // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                            // UpdateOperationalContext
        {
            s.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated context");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>                                       // reseed from transfer
        {
            Assert.Contains("OPCTX-1", prompt);
            return Turns.Completed("reseeded");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));                   // propose

        await session.RunAsync(CancellationToken.None);

        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal(2, rt.OpenSessions);   // original + recycled
        Assert.Equal(1, rt.ClosedSessions); // original closed during recycle
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter DecisionSessionTests -c Debug`
Expected: FAIL — `Run_WhenTokensExceedThreshold_RecyclesViaTransfer` throws `NotImplementedException`.

- [ ] **Step 3: Implement TransferAsync**

In `src/CommandCenter.CLI/DecisionSession.cs`, replace the stub `TransferAsync` with:

```csharp
    /// <summary>
    /// Transfer recycle, mirroring RepositoryOrchestrator.PrepareTransferAsync: extract an operational delta
    /// from the warm process, close it, rewrite operational_context.md via a one-shot operational turn, then
    /// open a FRESH decision process and seed it from the rewritten context.
    /// </summary>
    private async Task TransferAsync(CancellationToken cancellationToken)
    {
        console.Phase("Decision: Transfer/ProduceOperationalDelta");
        AgentTurnResult delta = await session!.RunTurnAsync(
            ProduceOperationalDelta.Text, StreamToConsole, cancellationToken);
        if (delta.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Operational-delta turn ended in state {delta.State}.");
        }

        await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, delta.Output);

        // Close the old process (resets seeded + token pressure).
        await CloseAsync();

        console.Phase("Decision: Transfer/UpdateOperationalContext");
        AgentTurnResult update = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.High, identifier: "xhigh"),
            UpdateOperationalContext.Text,
            StreamToConsole,
            cancellationToken);
        if (update.State != AgentTurnState.Completed)
        {
            throw new LoopStepException($"Update-operational-context turn ended in state {update.State}.");
        }

        // Open a fresh decision process and seed from the rewritten context.
        session = await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
        string newContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;

        console.Phase("Decision: Transfer/StartDecisionSessionFromTransfer");
        AgentTurnResult reseed = await session.RunTurnAsync(
            StartDecisionSessionFromTransfer.Render(newContext), onChunk: null, cancellationToken);
        if (reseed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Transfer reseed turn ended in state {reseed.State}.");
        }

        seeded = true;
    }
```

Also add `using CommandCenter.Agents.Models;` is already present (for `AgentEffortLevel`). Confirm `AgentEffortLevel` import resolves.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter DecisionSessionTests -c Debug`
Expected: PASS (5 tests, including the Transfer case).

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/DecisionSession.cs tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs
git commit -m "feat(cli): decision-session Transfer recycle mirroring PrepareTransferAsync"
```

---

### Task 10: Loop runner (LoopStart state machine)

**Files:**
- Create: `src/CommandCenter.CLI/LoopRunner.cs`
- Create: `tests/CommandCenter.CLI.Tests/LoopRunnerTests.cs`

**Interfaces:**
- Consumes: `MilestoneGate`, `LoopArtifacts`, `ExecutionStep`, `DecisionSession`, `ILoopConsole`.
- Produces: `sealed class LoopRunner(MilestoneGate gate, LoopArtifacts artifacts, ExecutionStep execution, DecisionSession decision, ILoopConsole console) : IAsyncDisposable` with `Task<LoopOutcome> RunAsync(CancellationToken ct)`. Implements the exact control flow: epic-complete → `EpicCompleted`; else ensure operational context, branch on `decisionsExist || !handoffExists`, rotate, run execution (Branch A only) + decision. Catches `OperationCanceledException` → `Cancelled`; `LoopStepException` → prints error → `Failed`. `DisposeAsync` disposes the owned `DecisionSession`.

- [ ] **Step 1: Write the failing test**

Create `tests/CommandCenter.CLI.Tests/LoopRunnerTests.cs`:

```csharp
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class LoopRunnerTests
{
    private sealed record Harness(
        LoopRunner Runner, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo, RecordingLoopConsole Con);

    private static Harness New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions());
        var gate = new MilestoneGate(store, repo);
        var exec = new ExecutionStep(rt, art, con, repo);
        var dec = new DecisionSession(rt, router, art, con, repo);
        return new Harness(new LoopRunner(gate, art, exec, dec, con), rt, store, repo, con);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task Run_WhenEpicAlreadyComplete_ReturnsEpicCompletedImmediately()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] done");

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal(0, h.Rt.OneShotCalls.Count);   // no codex run at all
    }

    [Fact]
    public async Task Run_FirstIterationBranchA_RunsExecutionThenDecision_ThenCompletes()
    {
        var h = New();
        // milestone incomplete at first, becomes complete after the execution agent "checks the box".
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] task");

        // Execution one-shot: writes handoff.md AND checks the milestone box (epic completes next LoopStart).
        h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF-1").Wait();
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] task").Wait();
            return Turns.Completed("executed");
        }));
        // Decision session: seed then propose.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DECISIONS-1")));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal("DECISIONS-1", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Decisions)));
        // After one Branch-A iteration: live handoff present (written by execution, rotated next loop only).
        Assert.True(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));
    }

    [Fact]
    public async Task Run_BranchB_DecisionsAbsentHandoffPresent_RunsDecisionOnly()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff), "H-RESUME");

        // Decision-only: seed + propose. After it, mark milestone done so the loop stops.
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        h.Rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] t").Wait();
            return Turns.Completed("DEC-B");
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.EpicCompleted, outcome);
        Assert.Equal(0, h.Rt.OneShotCalls.Count);   // Branch B never runs execution
        // handoff rotated away (archived + live deleted) in Branch B.
        Assert.False(await h.Store.ExistsAsync(Resolve(h.Repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Equal("H-RESUME", await h.Store.ReadAsync(Resolve(h.Repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task Run_WhenStepFails_ReturnsFailed()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("no handoff written")));

        LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

        Assert.Equal(LoopOutcome.Failed, outcome);
    }

    [Fact]
    public async Task Run_WhenCancelled_ReturnsCancelled()
    {
        var h = New();
        await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [ ] t");
        using var cts = new CancellationTokenSource();
        h.Rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }));

        LoopOutcome outcome = await h.Runner.RunAsync(cts.Token);

        Assert.Equal(LoopOutcome.Cancelled, outcome);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter LoopRunnerTests -c Debug`
Expected: FAIL — `LoopRunner` does not exist.

- [ ] **Step 3: Implement LoopRunner**

Create `src/CommandCenter.CLI/LoopRunner.cs`:

```csharp
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// The LoopStart state machine. Runs serially until the epic completes, a step fails, or cancellation
/// is requested. Owns the warm DecisionSession and disposes it on exit.
/// </summary>
internal sealed class LoopRunner(
    MilestoneGate gate,
    LoopArtifacts artifacts,
    ExecutionStep execution,
    DecisionSession decision,
    ILoopConsole console) : IAsyncDisposable
{
    public async Task<LoopOutcome> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ---- LoopStart ----
                if (await gate.IsEpicCompleteAsync())
                {
                    return LoopOutcome.EpicCompleted;
                }

                await artifacts.EnsureOperationalContextAsync();

                bool decisionsExist = await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions);
                bool handoffExists = await artifacts.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff);

                if (decisionsExist || !handoffExists)
                {
                    // ---- Branch A: execution then decision ----
                    if (decisionsExist)
                    {
                        await artifacts.RotateLiveDecisionsAsync();
                    }

                    if (handoffExists)
                    {
                        await artifacts.RotateLiveHandoffAsync();
                    }

                    await execution.RunAsync(cancellationToken);
                    await decision.RunAsync(cancellationToken);
                }
                else
                {
                    // ---- Branch B: decision only (resume after an interrupted execution) ----
                    await artifacts.RotateLiveHandoffAsync();
                    await decision.RunAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            console.Warn("Cancellation requested — stopping the loop.");
            return LoopOutcome.Cancelled;
        }
        catch (LoopStepException ex)
        {
            console.Error(ex.Message);
            return LoopOutcome.Failed;
        }
    }

    public async ValueTask DisposeAsync() => await decision.DisposeAsync();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter LoopRunnerTests -c Debug`
Expected: PASS (5 tests).

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test tests/CommandCenter.CLI.Tests -c Debug`
Expected: PASS (all CLI tests across tasks 2–10).

- [ ] **Step 6: Commit**

```bash
git add src/CommandCenter.CLI/LoopRunner.cs tests/CommandCenter.CLI.Tests/LoopRunnerTests.cs
git commit -m "feat(cli): LoopStart state machine with Branch A/B, cancellation, and failure handling"
```

---

### Task 11: Program composition — DI, Ctrl+C wiring, run, teardown

**Files:**
- Modify: `src/CommandCenter.CLI/Program.cs` (replace the stub)

**Interfaces:**
- Consumes: `CliArguments`, `AddAgents()` (`CommandCenter.Agents.Extensions`), `IArtifactStore`/`FileSystemArtifactStore`, `IDecisionSessionRouter`/`DecisionSessionRouter`, `IAgentRuntime`, `AgentSessionRegistry`, all loop types.
- Produces: `Program.Main` — async entrypoint returning an exit code (0 = epic complete, 1 = failed, 130 = cancelled).

- [ ] **Step 1: Implement Program.cs**

Replace `src/CommandCenter.CLI/Program.cs`:

```csharp
using CommandCenter.Agents.Extensions;
using CommandCenter.Agents.Services;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;

if (!CliArguments.TryParse(args, out Repository repository, out string error))
{
    Console.Error.WriteLine(error);
    return 2;
}

// --- Composition: only the building blocks the serial loop needs (no Generic Host, no orchestrator/registry). ---
var services = new ServiceCollection();
services.AddAgents();                                                  // IAgentRuntime + codex runtime
services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
services.AddSingleton<DecisionSessionRouterOptions>();
services.AddSingleton<IDecisionSessionRouter, DecisionSessionRouter>();
await using ServiceProvider provider = services.BuildServiceProvider();

var console = new ConsoleLoopConsole();
var store = provider.GetRequiredService<IArtifactStore>();
var runtime = provider.GetRequiredService<IAgentRuntime>();
var router = provider.GetRequiredService<IDecisionSessionRouter>();

var artifacts = new LoopArtifacts(store, repository);
var gate = new MilestoneGate(store, repository);
var execution = new ExecutionStep(runtime, artifacts, console, repository);
var decision = new DecisionSession(runtime, router, artifacts, console, repository);
await using var loop = new LoopRunner(gate, artifacts, execution, decision, console);

// --- Ctrl+C: cancel the loop AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    console.Warn("Ctrl+C received — terminating codex sessions...");
    cts.Cancel();
};

console.Info($"CommandCenter.CLI starting for {repository.Path}");

LoopOutcome outcome;
try
{
    outcome = await loop.RunAsync(cts.Token);
}
finally
{
    // Explicitly close the warm decision session (kills its codex process tree); also dispose the
    // AgentSessionRegistry via the provider as a belt-and-suspenders teardown for any straggler.
    await loop.DisposeAsync();
    if (provider.GetService<AgentSessionRegistry>() is { } registry)
    {
        await registry.DisposeAsync();
    }
}

switch (outcome)
{
    case LoopOutcome.EpicCompleted:
        Console.WriteLine("Epic completed. Press any key to exit.");
        Console.ReadKey(intercept: true);
        return 0;
    case LoopOutcome.Cancelled:
        console.Warn("Run cancelled. Codex sessions terminated.");
        return 130;
    default:
        console.Error("Run failed. See the error above.");
        return 1;
}
```

> Verification sub-step: confirm `AgentSessionRegistry` is registered by `AddAgents()` as a concrete singleton (it is — `TryAddSingleton<AgentSessionRegistry>()`) so `provider.GetService<AgentSessionRegistry>()` resolves. Confirm `DecisionSessionRouterOptions` has a parameterless-friendly registration (it has a default ctor `DecisionSessionRouterOptions(int ModelContextWindowTokens = 256_000, double TransferOccupancyFraction = 0.80)`; `AddSingleton<DecisionSessionRouterOptions>()` activates it with the default). If activation fails, register `new DecisionSessionRouterOptions()` instead.

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build CommandCenter.slnx -c Debug`
Expected: `Build succeeded` (CLI + tests + all referenced projects compile).

- [ ] **Step 3: Smoke-run the executable's argument handling (no codex)**

Run: `dotnet run --project src/CommandCenter.CLI -c Debug -- ./nonexistent-dir-xyz`
Expected: prints `Repository directory does not exist: …` and exits non-zero (2). This exercises composition + arg validation without launching codex.

- [ ] **Step 4: Commit**

```bash
git add src/CommandCenter.CLI/Program.cs
git commit -m "feat(cli): wire DI, Ctrl+C cancellation, loop run, and codex teardown in Program"
```

---

### Task 12: Full build + test verification + manual end-to-end check

**Files:** none (verification only)

- [ ] **Step 1: Run the full CLI test suite in Release**

Per repo convention (tests run reliably against the running Debug app when invoked with `-c Release`):
Run: `dotnet test tests/CommandCenter.CLI.Tests -c Release`
Expected: PASS, 0 failures across `CliArgumentsTests`, `MilestoneGateTests`, `LoopArtifactsTests`, `AgentSpecsTests`, `ExecutionStepTests`, `DecisionSessionTests`, `LoopRunnerTests`.

- [ ] **Step 2: Confirm the existing backend suite is unaffected**

Run: `dotnet build CommandCenter.slnx -c Release`
Expected: `Build succeeded`. (The CLI is additive — new project + new test project only; no existing file changed except `CommandCenter.slnx`.)

- [ ] **Step 3: Manual end-to-end check against a real planned repo (requires a working `codex` binary)**

Prereqs: a scratch repo `REPO` that already has `.agents/plan.md` and `.agents/milestones/m*.md` (with unchecked boxes), and `codex` on PATH (or `CODEX_EXECUTABLE` set). Then:

Run: `dotnet run --project src/CommandCenter.CLI -c Release -- <REPO>`
Expected behavior to observe:
- Branch A on the first iteration: `=== Execution: StartExecution ===`, streamed deltas, the assistant message, then `[ok] New handoff.md verified.`
- `=== Decision (route=Continue) ===`, the proposed decisions message, then `[ok] New decisions.md verified.`
- The loop repeats; `.agents/handoffs/handoff.{0001,0002,…}.md` and `.agents/decisions/decisions.{0001,0002,…}.md` accumulate; live `handoff.md`/`decisions.md` are present between iterations.
- Pressing **Ctrl+C** prints `Ctrl+C received — terminating codex sessions...`, the in-flight codex process is killed (verify no orphan `codex` process remains in Task Manager / `Get-Process codex`), and the app exits with code 130.
- When every milestone's boxes are checked, it prints exactly `Epic completed. Press any key to exit.` and waits for a key.

- [ ] **Step 4: Commit any fixups discovered during the manual check**

```bash
git add -A
git commit -m "test(cli): verify end-to-end loop, rotation, and Ctrl+C codex teardown"
```

---

## Self-Review (run before handing off)

1. **Spec coverage**
   - "parse `.agents/plan.md` and `.agents/milestones/m*.md`; if all checkboxes (aggregated) checked, stop + print message" → Task 4 (`MilestoneGate` aggregates plan.md + milestones) + Task 10 (`LoopRunner` returns `EpicCompleted`) + Task 11 (prints `"Epic completed. Press any key to exit."`). ✓
   - "if decisions.md exists OR handoff.md missing: rotate decisions (if present), rotate handoff (if present), run execution + print + verify handoff, then via SessionRouter run decision + print + verify decisions, goto LoopStart" → Task 10 Branch A + Task 7 (execution) + Task 8/9 (decision via router). ✓
   - "else (no decisions.md AND handoff.md exists): rotate handoff, via SessionRouter run decision + print + verify decisions, goto LoopStart" → Task 10 Branch B. ✓
   - "mirror the backend" → prompt catalog, sandbox/effort specs, rotation, ReadLatest, router all reused/ported verbatim (Tasks 4–9). ✓
   - "100% automated" → decision auto-submit (Task 8) replaces the human review gate. ✓
   - "CancellationTokenSource integrated with console cancel key press, propagated to codex to terminate them" → Task 11 (`Console.CancelKeyPress` → `cts.Cancel()`; one-shot turns dispose-on-cancel; warm session `CloseSessionAsync`; registry dispose) reaching `AgentProcess.Kill(entireProcessTree: true)`. ✓

2. **Placeholder scan** — no `TBD`/`add error handling`/`similar to Task N`; every code step contains complete code and the verify gates are explicit. ✓

3. **Type consistency** — `LoopArtifacts`, `MilestoneGate`, `AgentSpecs`, `ExecutionStep`, `DecisionSession`, `LoopRunner` signatures match across consumer/producer blocks; `OrchestrationArtifactPaths` members are cited consistently; prompt `Render` arities match Global Constraints. The two "Verification sub-steps" (OrchestrationArtifactPaths member names; `AgentSessionSpec`/`AgentTokenUsage`/`IAgentSession` shapes) flag the only signatures that must be confirmed against source at implementation time.

## Risks & Notes

- **`MemoryArtifactStore.ListAsync` glob semantics:** the rotation/latest tests assume `ListAsync(dir, "handoff.*.md")` matches numbered files but not the single-dot live file, matching `FileSystemArtifactStore`. If `MemoryArtifactStore`'s pattern matching differs, adjust the test store or use a temp-dir `FileSystemArtifactStore` in `LoopArtifactsTests`.
- **Transfer is exercised only under token pressure** (≥ 200k decision-session tokens by default). The default deployment stays on `Continue` indefinitely; Transfer (Task 9) exists for long-horizon runs and to keep the SessionRouter meaningful per the spec.
- **`ExtractMilestones` / plan authoring are out of scope** — the CLI assumes a pre-planned repo (see Preconditions). If a future iteration must bootstrap milestones, add an `ExtractMilestones.Text` one-shot (operational, High/`xhigh`) before the first execution, mirroring `RepositoryOrchestrator.RunExecutionAsync`.
- **No git commit/push** — the backend optionally commits plan+milestones after the first Execute Plan (`AutomaticCommitPushAfterExecuteEnabled`). The CLI omits all git side effects; add a publisher step if per-iteration commits are wanted.

## Execution Handoff

**Plan complete and saved to `plan.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration. REQUIRED SUB-SKILL: superpowers:subagent-driven-development.

**2. Inline Execution** — execute tasks in this session with checkpoints. REQUIRED SUB-SKILL: superpowers:executing-plans.
