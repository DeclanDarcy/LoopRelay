# CLI Decision-Session Resume Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When the CLI loop enters the decision step for the first time in a run, resume the previous codex decision session (persisted at `{REPO_DIR}/.commandcenter/decision-session.json`) via the app-server `thread/resume` method, falling back loudly to a fresh primed process on any failure; clear the persisted state on epic completion (both the loop's milestone gate and Plan.CLI's epic rollover).

**Architecture:** The codex app-server thread id (already extracted from `thread/start` but currently discarded in a private field) is surfaced on `IAgentSession`, persisted by `DecisionSession` after every successful proposal turn, and fed back on the next run via a new optional `AgentSessionSpec.ResumeThreadId`. When that field is set, `AgentRuntime.OpenSessionAsync` runs the handshake **eagerly** and `CodexAppServerSession` sends `thread/resume` instead of `thread/start`; failure throws a typed `AgentSessionResumeException` so the caller can fall back to a fresh open before composing its first prompt. The store lives in `CommandCenter.Orchestration` (shared by both CLIs) and is fail-open like the telemetry ledger.

**Tech Stack:** .NET 10 / C# (records, primary constructors), xUnit with hand-rolled fakes (no mocking frameworks), System.Text.Json (compact camelCase), codex app-server JSON-RPC v2.

**Spec:** `docs/superpowers/specs/2026-07-04-cli-decision-session-resume-design.md`

## Global Constraints

- `thread/resume` verified against installed codex-cli **0.142.5**: params `{threadId (required), cwd, sandbox, approvalPolicy, excludeTurns}`; response shape identical to `thread/start` (`result.thread.id`); errors arrive as JSON-RPC error responses.
- The state file is only ever written **after a successful decision turn** (its existence implies the thread is primed — there is deliberately NO `seeded` field).
- All store IO is **fail-open**: read/write/clear failures must never break a turn or the loop; they surface only through a warning callback.
- `.commandcenter/` must be self-ignoring: creating the directory writes `.commandcenter/.gitignore` containing `*` (never overwrite an existing one). This is load-bearing — `CommitGate`/`WorkingTreeChangeDetector` exclude only `.agents`.
- Kill switch: `COMMANDCENTER_DECISION_RESUME=0|false` skips the resume attempt only; persist/clear behavior is unchanged.
- Only the **first** decision-session open of a CLI process attempts resume; post-Transfer and post-failure reopens always start fresh.
- Restored router accounting is applied **only at successful resume-open** (never before the router's route evaluation, which runs before the open).
- CLI projects' types are `internal` with `InternalsVisibleTo` for their test projects; follow existing comment density and naming.
- Do not touch anything under `.agents/` (frozen artifacts submodule).
- Test commands run from the repo root `C:\kernritsu\CommandCenter`. If a build fails with file-lock errors (a Debug loop running elsewhere), retry with `-c Release`.

---

### Task 1: Resume state record + store (Orchestration) + file-store tests

**Files:**
- Create: `src/CommandCenter.Orchestration/Models/DecisionSessionResumeState.cs`
- Create: `src/CommandCenter.Orchestration/Abstractions/IDecisionSessionResumeStore.cs`
- Create: `src/CommandCenter.Orchestration/Services/DecisionSessionResumeStore.cs`
- Test: `tests/CommandCenter.CLI.Tests/FileDecisionSessionResumeStoreTests.cs`

**Interfaces:**
- Consumes: `CommandCenter.Core.Repositories.Repository` (record with `Id`/`Name`/`Path`).
- Produces (later tasks depend on these exact names):
  - `DecisionSessionResumeState(string ThreadId, int OccupancyTokens, double ReuseCost, int ReuseCycles, double LastCycleCost, double PrevCycleCost, double TransferCost, int TransferCount, int? PreviousOperationalContextSize, int OperationalContextGrowthStreak)` with `int SchemaVersion` (init, default `CurrentSchemaVersion = 1`) and `DateTimeOffset SavedAtUtc` (init).
  - `IDecisionSessionResumeStore` with `Task<DecisionSessionResumeState?> ReadAsync(CancellationToken)`, `Task WriteAsync(DecisionSessionResumeState, CancellationToken)`, `Task ClearAsync(CancellationToken)`.
  - `FileDecisionSessionResumeStore(Repository repository, Action<string>? onWarning = null)` and `NullDecisionSessionResumeStore`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CommandCenter.CLI.Tests/FileDecisionSessionResumeStoreTests.cs`:

```csharp
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

/// <summary>Real-filesystem tests (temp dir per test, like RotatingJsonlTelemetrySinkTests).</summary>
public sealed class FileDecisionSessionResumeStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-resume-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> warnings = new();

    private FileDecisionSessionResumeStore NewStore() =>
        new(new Repository { Id = Guid.NewGuid(), Name = "r", Path = root }, warnings.Add);

    private static DecisionSessionResumeState State(string threadId = "thread-1") =>
        new(threadId, 100, 5d, 2, 3d, 2d, 300_000d, 1, 500, 1);

    private string FilePath => Path.Combine(root, ".commandcenter", "decision-session.json");

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsEveryField_AndStampsSavedAtUtc()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        DecisionSessionResumeState? read = await store.ReadAsync();

        Assert.NotNull(read);
        Assert.Equal("thread-1", read!.ThreadId);
        Assert.Equal(100, read.OccupancyTokens);
        Assert.Equal(5d, read.ReuseCost);
        Assert.Equal(2, read.ReuseCycles);
        Assert.Equal(3d, read.LastCycleCost);
        Assert.Equal(2d, read.PrevCycleCost);
        Assert.Equal(300_000d, read.TransferCost);
        Assert.Equal(1, read.TransferCount);
        Assert.Equal(500, read.PreviousOperationalContextSize);
        Assert.Equal(1, read.OperationalContextGrowthStreak);
        Assert.Equal(DecisionSessionResumeState.CurrentSchemaVersion, read.SchemaVersion);
        Assert.NotEqual(default, read.SavedAtUtc);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Read_WhenNoFile_ReturnsNullWithoutWarning()
    {
        Assert.Null(await NewStore().ReadAsync());
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Read_CorruptJson_WarnsDeletesAndReturnsNull()
    {
        FileDecisionSessionResumeStore store = NewStore();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{not json");

        Assert.Null(await store.ReadAsync());

        Assert.False(File.Exists(FilePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Read_WrongSchemaVersion_WarnsDeletesAndReturnsNull()
    {
        FileDecisionSessionResumeStore store = NewStore();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """{"schemaVersion":999,"threadId":"thread-1"}""");

        Assert.Null(await store.ReadAsync());

        Assert.False(File.Exists(FilePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Read_EmptyThreadId_WarnsDeletesAndReturnsNull()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State(threadId: ""));

        Assert.Null(await store.ReadAsync());

        Assert.False(File.Exists(FilePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Clear_IsIdempotent()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        await store.ClearAsync();
        await store.ClearAsync(); // deleting nothing is a no-op, never a warning

        Assert.False(File.Exists(FilePath));
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Write_CreatesTheSelfIgnoringGitignore_AndNeverOverwritesAnExistingOne()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        string gitignore = Path.Combine(root, ".commandcenter", ".gitignore");
        Assert.Equal("*\n", await File.ReadAllTextAsync(gitignore));

        await File.WriteAllTextAsync(gitignore, "custom");
        await store.WriteAsync(State("thread-2"));
        Assert.Equal("custom", await File.ReadAllTextAsync(gitignore));
    }

    [Fact]
    public async Task JsonOnDisk_IsCompactCamelCase()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        string json = await File.ReadAllTextAsync(FilePath);
        Assert.Contains("\"threadId\":\"thread-1\"", json);
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.DoesNotContain("\n", json.TrimEnd());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~FileDecisionSessionResumeStore"`
Expected: FAIL to compile — `DecisionSessionResumeState`/`FileDecisionSessionResumeStore` do not exist.

- [ ] **Step 3: Implement the state record**

Create `src/CommandCenter.Orchestration/Models/DecisionSessionResumeState.cs`:

```csharp
namespace CommandCenter.Orchestration.Models;

/// <summary>
/// Snapshot of the CLI DecisionSession's resumable state: the codex app-server thread id plus the
/// per-process router accounting and context-health counters, captured after every successful proposal
/// turn. The state is ONLY written after a successful turn, so its existence implies the thread was
/// primed with the operational context (no seeded flag is needed).
/// </summary>
public sealed record DecisionSessionResumeState(
    string ThreadId,
    int OccupancyTokens,
    double ReuseCost,
    int ReuseCycles,
    double LastCycleCost,
    double PrevCycleCost,
    double TransferCost,
    int TransferCount,
    int? PreviousOperationalContextSize,
    int OperationalContextGrowthStreak)
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset SavedAtUtc { get; init; }
}
```

- [ ] **Step 4: Implement the interface**

Create `src/CommandCenter.Orchestration/Abstractions/IDecisionSessionResumeStore.cs`:

```csharp
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Abstractions;

/// <summary>Persistence seam for the decision session's cross-run resume state (per repo, per epic).</summary>
public interface IDecisionSessionResumeStore
{
    /// <summary>Null when absent or unusable (an unusable file is deleted).</summary>
    Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default);

    /// <summary>Idempotent delete.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Implement the file store + null store**

Create `src/CommandCenter.Orchestration/Services/DecisionSessionResumeStore.cs`:

```csharp
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Services;

/// <summary>
/// Persists the CLI loop's decision-session resume state at <c>{repo}/.commandcenter/decision-session.json</c>.
/// Fail-open in the telemetry sense: no read/write/clear failure may ever break a turn or the loop — failures
/// surface only through <paramref name="onWarning"/> (each CLI passes its console's Warn; ILoopConsole is
/// internal and duplicated per CLI, so this shared type takes a callback instead). Creating the directory also
/// drops a self-ignoring <c>.commandcenter/.gitignore</c> (<c>*</c>): CommitGate and WorkingTreeChangeDetector
/// exclude only <c>.agents</c>, so an un-ignored state file would read as a real working-tree change
/// (corrupting the no-changes/stall gates) and be committed into the target repo.
/// </summary>
public sealed class FileDecisionSessionResumeStore(Repository repository, Action<string>? onWarning = null)
    : IDecisionSessionResumeStore
{
    public const string DirectoryName = ".commandcenter";
    public const string FileName = "decision-session.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string DirectoryPath => Path.Combine(repository.Path, DirectoryName);

    private string FilePath => Path.Combine(DirectoryPath, FileName);

    public async Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(FilePath, cancellationToken);
            DecisionSessionResumeState? state = JsonSerializer.Deserialize<DecisionSessionResumeState>(json, Json);
            if (state is null
                || state.SchemaVersion != DecisionSessionResumeState.CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(state.ThreadId))
            {
                onWarning?.Invoke(
                    $"Ignoring unusable decision-session resume state at {FilePath} (schema/content mismatch) — deleting it.");
                File.Delete(FilePath);
                return null;
            }

            return state;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not read decision-session resume state at {FilePath}: {ex.Message} — deleting it.");
            TryDelete();
            return null;
        }
    }

    public async Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            EnsureSelfIgnore();
            string json = JsonSerializer.Serialize(state with { SavedAtUtc = DateTimeOffset.UtcNow }, Json);
            await File.WriteAllTextAsync(FilePath, json, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onWarning?.Invoke($"Could not persist decision-session resume state at {FilePath}: {ex.Message}");
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        TryDelete();
        return Task.CompletedTask;
    }

    private void TryDelete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception ex)
        {
            onWarning?.Invoke($"Could not delete decision-session resume state at {FilePath}: {ex.Message}");
        }
    }

    // `*` inside .commandcenter/.gitignore ignores the whole directory (including the .gitignore itself),
    // making it self-ignoring regardless of the target repo's root .gitignore. Never overwrite an existing
    // file — an operator may have customized it.
    private void EnsureSelfIgnore()
    {
        string gitignore = Path.Combine(DirectoryPath, ".gitignore");
        if (!File.Exists(gitignore))
        {
            File.WriteAllText(gitignore, "*\n");
        }
    }
}

/// <summary>No-op store: nothing is persisted and nothing is ever found (default for tests/compositions
/// that do not opt into resume).</summary>
public sealed class NullDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DecisionSessionResumeState?>(null);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~FileDecisionSessionResumeStore"`
Expected: PASS (8 tests).

- [ ] **Step 7: Commit**

```bash
git add src/CommandCenter.Orchestration/Models/DecisionSessionResumeState.cs src/CommandCenter.Orchestration/Abstractions/IDecisionSessionResumeStore.cs src/CommandCenter.Orchestration/Services/DecisionSessionResumeStore.cs tests/CommandCenter.CLI.Tests/FileDecisionSessionResumeStoreTests.cs
git commit -m "feat(orchestration): decision-session resume state store under .commandcenter"
```

---

### Task 2: `thread/resume` protocol frame

**Files:**
- Modify: `src/CommandCenter.Agents/Services/CodexAppServerProtocol.cs` (insert after `ThreadStart`, line 52)
- Test: `tests/CommandCenter.Backend.Tests/CodexAppServerProtocolTests.cs`

**Interfaces:**
- Produces: `CodexAppServerProtocol.ThreadResume(long id, string threadId, string? cwd, string? sandbox, string? approvalPolicy)` — a `thread/resume` request frame that always carries `excludeTurns: true`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/CommandCenter.Backend.Tests/CodexAppServerProtocolTests.cs` (inside `CodexAppServerProtocolTests`):

```csharp
[Fact]
public void ThreadResumeFrameMapsThreadIdCwdSandboxApprovalAndExcludeTurns()
{
    JsonElement root = Root(CodexAppServerProtocol.ThreadResume(4, "thread-old", "/repo", "read-only", "never"));

    Assert.Equal("thread/resume", root.GetProperty("method").GetString());
    JsonElement p = root.GetProperty("params");
    Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
    Assert.Equal("/repo", p.GetProperty("cwd").GetString());
    Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
    Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
    // History replay is never needed (the CLI streams no prior turns) and can be huge — always excluded.
    Assert.True(p.GetProperty("excludeTurns").GetBoolean());
}

[Fact]
public void ThreadResumeOmitsNullOptionals()
{
    JsonElement p = Root(CodexAppServerProtocol.ThreadResume(4, "thread-old", cwd: null, sandbox: null, approvalPolicy: null))
        .GetProperty("params");

    Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
    Assert.False(p.TryGetProperty("cwd", out _));
    Assert.False(p.TryGetProperty("sandbox", out _));
    Assert.False(p.TryGetProperty("approvalPolicy", out _));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/CommandCenter.Backend.Tests --filter "FullyQualifiedName~CodexAppServerProtocolTests"`
Expected: FAIL to compile — `ThreadResume` does not exist.

- [ ] **Step 3: Implement the frame builder**

In `src/CommandCenter.Agents/Services/CodexAppServerProtocol.cs`, insert after the `ThreadStart` method:

```csharp
/// <summary>
/// Resumes a previously persisted thread by id (codex loads it from its own rollout on disk). The response
/// carries the same <c>thread.id</c> shape as <c>thread/start</c>. <c>excludeTurns</c> is always true —
/// replayed history is never consumed and can be arbitrarily large. Verified against codex-cli 0.142.5.
/// </summary>
public static string ThreadResume(long id, string threadId, string? cwd, string? sandbox, string? approvalPolicy) =>
    Request(id, "thread/resume", Compact(new Dictionary<string, object?>
    {
        ["threadId"] = threadId,
        ["cwd"] = cwd,
        ["sandbox"] = sandbox,
        ["approvalPolicy"] = approvalPolicy,
        ["excludeTurns"] = true
    }));
```

Also update the class doc comment's method list (line 7-8) to mention `thread/resume`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/CommandCenter.Backend.Tests --filter "FullyQualifiedName~CodexAppServerProtocolTests"`
Expected: PASS (all, including the 2 new).

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.Agents/Services/CodexAppServerProtocol.cs tests/CommandCenter.Backend.Tests/CodexAppServerProtocolTests.cs
git commit -m "feat(agents): thread/resume app-server frame builder"
```

---

### Task 3: Resume surface area — spec field, typed exception, `IAgentSession.ThreadId` on every implementer

**Files:**
- Modify: `src/CommandCenter.Agents/Models/AgentSessionSpec.cs`
- Create: `src/CommandCenter.Agents/Models/AgentSessionResumeException.cs`
- Modify: `src/CommandCenter.Agents/Abstractions/IAgentSession.cs`
- Modify: `src/CommandCenter.Agents/Services/CodexAppServerSession.cs` (property only in this task)
- Modify: `src/CommandCenter.Agents/Services/AgentSession.cs`
- Modify: `src/CommandCenter.CLI/GatedAgentRuntime.cs` (`GatedAgentSession` passthrough)
- Modify: `tests/CommandCenter.CLI.Tests/TestDoubles.cs` (`FakeAgentRuntime` + `FakeAgentSession`)
- Modify: `tests/CommandCenter.CLI.Tests/GatedAgentRuntimeTests.cs` (`RecordingSession` + passthrough test)
- Modify: `tests/CommandCenter.Plan.CLI.Tests/TestDoubles.cs` (`FakeAgentSession`)
- Modify: `tests/CommandCenter.Backend.Tests/Orchestration/OrchestrationTestDoubles.cs` (`FakeAgentSession`)
- Modify: `tests/CommandCenter.Backend.Tests/AgentSessionRegistryTests.cs` (private `FakeAgentSession`)

**Interfaces:**
- Produces:
  - `AgentSessionSpec.ResumeThreadId` (`string?`, new last optional ctor param `resumeThreadId = null`).
  - `AgentSessionResumeException : Exception` in `CommandCenter.Agents.Models`.
  - `IAgentSession.ThreadId` (`string?`): codex thread id, null until the app-server handshake completes; null forever on one-shot/legacy sessions.
  - CLI `FakeAgentRuntime` gains `List<AgentSessionSpec> OpenedSpecs` and `bool FailResume`; CLI `FakeAgentSession.ThreadId` = `spec.ResumeThreadId ?? $"thread-{N}"` where N is the 1-based open count.

- [ ] **Step 1: Add `ResumeThreadId` to the spec**

In `src/CommandCenter.Agents/Models/AgentSessionSpec.cs`, add a final ctor parameter and property:

```csharp
public AgentSessionSpec(
    SessionIdentity sessionId,
    string repositoryId,
    SessionRole role,
    SandboxProfile sandbox,
    EffortProfile effort,
    string? workingDirectory = null,
    IReadOnlyDictionary<string, string>? startupOptions = null,
    string? resumeThreadId = null)
{
    SessionId = sessionId;
    RepositoryId = repositoryId;
    Role = role;
    Sandbox = sandbox;
    Effort = effort;
    WorkingDirectory = workingDirectory;
    StartupOptions = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(startupOptions ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    ResumeThreadId = resumeThreadId;
}
```

and after `StartupOptions`:

```csharp
/// <summary>Codex app-server thread id to resume instead of starting a fresh thread (persistent sessions
/// only; ignored by one-shots). When set, the handshake runs eagerly at open — see AgentRuntime.</summary>
public string? ResumeThreadId { get; }
```

- [ ] **Step 2: Add the typed exception**

Create `src/CommandCenter.Agents/Models/AgentSessionResumeException.cs`:

```csharp
namespace CommandCenter.Agents.Models;

/// <summary>
/// A codex session resume attempt failed (rollout deleted, unknown thread id, protocol drift, or the
/// process died during the eager handshake). RECOVERABLE by contract: the caller falls back to opening a
/// fresh session — which is why this is typed rather than a bare InvalidOperationException.
/// </summary>
public sealed class AgentSessionResumeException : Exception
{
    public AgentSessionResumeException(string message)
        : base(message)
    {
    }

    public AgentSessionResumeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 3: Add `ThreadId` to `IAgentSession` and every implementer**

In `src/CommandCenter.Agents/Abstractions/IAgentSession.cs`, after `TotalUsage`:

```csharp
/// <summary>The codex app-server thread id — null until the handshake completes, and always null for
/// one-shot/legacy sessions. The durable key a later process can resume the conversation with.</summary>
string? ThreadId { get; }
```

Implementers (add one member each):

1. `src/CommandCenter.Agents/Services/CodexAppServerSession.cs`, after `TotalUsage` (line 72):
```csharp
public string? ThreadId => threadId;
```
2. `src/CommandCenter.Agents/Services/AgentSession.cs`, after `TotalUsage` (line 57):
```csharp
public string? ThreadId => null; // one-shot/legacy path — no app-server thread exists
```
3. `src/CommandCenter.CLI/GatedAgentRuntime.cs`, in `GatedAgentSession` after `TotalUsage` (line 70):
```csharp
public string? ThreadId => inner.ThreadId;
```
4. `tests/CommandCenter.CLI.Tests/TestDoubles.cs` — replace `FakeAgentRuntime.OpenSessionAsync` and extend `FakeAgentSession`:

```csharp
public Queue<ScriptedTurn> OneShotTurns { get; } = new();
public Queue<ScriptedTurn> SessionTurns { get; } = new();
public int OpenSessions { get; private set; }
public int ClosedSessions { get; private set; }
public List<(AgentSessionSpec Spec, string Prompt)> OneShotCalls { get; } = new();
public List<AgentSessionSpec> OpenedSpecs { get; } = new();

/// <summary>When true, any open that ASKS to resume throws the typed resume failure (scripting the
/// rollout-gone / unknown-thread case); non-resume opens still succeed.</summary>
public bool FailResume { get; set; }
```

```csharp
public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken ct = default)
{
    OpenedSpecs.Add(spec);
    if (spec.ResumeThreadId is not null && FailResume)
    {
        throw new AgentSessionResumeException("scripted resume failure");
    }

    OpenSessions++;
    return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec));
}
```

and in `FakeAgentSession` (mirrors the real contract: a resumed session keeps the requested thread id, a
fresh one mints its own):

```csharp
internal sealed class FakeAgentSession(FakeAgentRuntime runtime, AgentSessionSpec spec) : IAgentSession
{
    private readonly string threadId = spec.ResumeThreadId ?? $"thread-{runtime.OpenSessions}";

    public SessionIdentity SessionId => spec.SessionId;
    public string RepositoryId => spec.RepositoryId;
    public SessionRole Role => spec.Role;
    public AgentSessionMode Mode => AgentSessionMode.Persistent;
    public AgentProcessState State => AgentProcessState.Running;
    public int CompletedTurns => 0;
    public AgentTokenUsage TotalUsage => new(0, 0);
    public string? ThreadId => threadId;

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default) =>
        Task.FromResult(runtime.RunSessionTurn(spec, prompt));

    public Task CancelAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

5. `tests/CommandCenter.CLI.Tests/GatedAgentRuntimeTests.cs`, in `RecordingSession` add:
```csharp
public string? ThreadId => "thread-inner";
```
6. `tests/CommandCenter.Plan.CLI.Tests/TestDoubles.cs`, in `FakeAgentSession` add:
```csharp
public string? ThreadId => null;
```
7. `tests/CommandCenter.Backend.Tests/Orchestration/OrchestrationTestDoubles.cs`, in `FakeAgentSession` add:
```csharp
public string? ThreadId => null;
```
8. `tests/CommandCenter.Backend.Tests/AgentSessionRegistryTests.cs`, in the private `FakeAgentSession` add:
```csharp
public string? ThreadId => null;
```

(If the compiler flags any further `IAgentSession` implementer, give it `public string? ThreadId => null;` — the full list above was found with `grep ": IAgentSession"`.)

- [ ] **Step 4: Write the passthrough test**

Add to `tests/CommandCenter.CLI.Tests/GatedAgentRuntimeTests.cs` (reuse the file's existing `RecordingSession` and `RecordingGate`; construct the spec with `AgentSpecs.Decision` or inline the same way neighboring tests build specs):

```csharp
[Fact]
public void GatedSessionExposesTheInnerThreadId()
{
    var log = new List<string>();
    var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
    var gated = new GatedAgentSession(
        new RecordingSession(log, AgentSpecs.Decision(repo)),
        new RecordingGate(log),
        new NullSessionTelemetryRecorder(),
        "r", "/repo", DateTimeOffset.UtcNow);

    // Persist-after-turn reads the thread id THROUGH the gate — a missing passthrough would silently
    // disable resume in production (the gated wrapper is what DecisionSession actually holds).
    Assert.Equal("thread-inner", gated.ThreadId);
}
```

(Adjust the `RecordingSession`/`RecordingGate` constructor arguments to match their actual signatures in that file if they differ; add `using CommandCenter.Core.Repositories;` if missing.)

- [ ] **Step 5: Build the whole solution and run the touched suites**

Run: `dotnet build CommandCenter.slnx`
Expected: Build succeeded, 0 errors.

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~GatedAgentRuntimeTests"`
Expected: PASS (including `GatedSessionExposesTheInnerThreadId`).

- [ ] **Step 6: Commit**

```bash
git add -A -- src tests
git commit -m "feat(agents): AgentSessionSpec.ResumeThreadId, AgentSessionResumeException, IAgentSession.ThreadId"
```

---

### Task 4: Resume handshake — `CodexAppServerSession.EnsureReadyAsync`, `thread/resume` branch, eager open in `AgentRuntime`

**Files:**
- Modify: `src/CommandCenter.Agents/Services/CodexAppServerSession.cs`
- Modify: `src/CommandCenter.Agents/Services/AgentRuntime.cs`
- Create: `tests/CommandCenter.Backend.Tests/ScriptedAppServerProcess.cs` (extracted from `CodexAppServerSessionTests` + extended)
- Modify: `tests/CommandCenter.Backend.Tests/CodexAppServerSessionTests.cs`
- Create: `tests/CommandCenter.Backend.Tests/AgentRuntimeResumeTests.cs`

**Interfaces:**
- Consumes: `AgentSessionSpec.ResumeThreadId`, `AgentSessionResumeException`, `CodexAppServerProtocol.ThreadResume` (Tasks 2-3).
- Produces:
  - `CodexAppServerSession.EnsureReadyAsync(CancellationToken)` — runs the handshake eagerly (idempotent; later turns skip it).
  - Contract: `AgentRuntime.OpenSessionAsync` with `spec.ResumeThreadId != null` either returns a session whose `ThreadId == spec.ResumeThreadId`, or disposes the process, deregisters, and throws `AgentSessionResumeException`. Non-resume opens keep today's lazy handshake.
  - Test double `ScriptedAppServerProcess` (now `internal`, shared): reacts to `thread/resume` (echoes the requested id, or an error when `RejectResume` is true).

- [ ] **Step 1: Extract `ScriptedAppServerProcess` into its own file and extend it**

Move the nested `private sealed class ScriptedAppServerProcess` (bottom of `tests/CommandCenter.Backend.Tests/CodexAppServerSessionTests.cs`) to a new file `tests/CommandCenter.Backend.Tests/ScriptedAppServerProcess.cs`, verbatim except:
- class declaration becomes `internal sealed class ScriptedAppServerProcess : IAgentProcess` (namespace `CommandCenter.Backend.Tests`; carry over the usings it needs: `System.Runtime.CompilerServices`, `System.Text.Json`, `System.Threading.Channels`, `CommandCenter.Agents.Abstractions`, `CommandCenter.Agents.Models`),
- add two members next to `EmitApprovalRequest`:

```csharp
/// <summary>When true, a thread/resume request is answered with a JSON-RPC error (rollout gone).</summary>
public bool RejectResume { get; init; }

/// <summary>The threadId carried by the last thread/resume request (null if none arrived).</summary>
public string? LastResumeThreadId { get; private set; }
```

- add a `case "thread/resume":` to the `switch (method)` in `React`, directly after the `case "thread/start":` block:

```csharp
case "thread/resume":
    string? requested = null;
    try
    {
        using (JsonDocument resumeDoc = JsonDocument.Parse(line))
        {
            requested = resumeDoc.RootElement.GetProperty("params").GetProperty("threadId").GetString();
        }
    }
    catch (Exception)
    {
        // Malformed frame — leave requested null; the assertion in the test will surface it.
    }

    LastResumeThreadId = requested;
    if (RejectResume)
    {
        EmitError(id, $"no rollout found for thread {requested}");
    }
    else
    {
        EmitResponse(id, new { thread = new { id = requested } });
    }

    break;
```

- add the error emitter next to `EmitResponse`:

```csharp
private void EmitError(long id, string message) =>
    Emit(new Dictionary<string, object?> { ["id"] = id, ["error"] = new { message } });
```

Then delete the nested copy from `CodexAppServerSessionTests.cs` (the tests keep compiling against the extracted internal class — same namespace).

- [ ] **Step 2: Write the failing session-level tests**

Add to `tests/CommandCenter.Backend.Tests/CodexAppServerSessionTests.cs`:

```csharp
private static AgentSessionSpec ResumeSpec(string threadId) => new(
    SessionIdentity.New(),
    "repo-1",
    SessionRole.Decision,
    new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
    new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
    workingDirectory: "/repo",
    resumeThreadId: threadId);

[Fact]
public async Task ResumeSpecSendsThreadResumeInsteadOfThreadStartAndAddressesTurnsAtTheResumedThread()
{
    var process = new ScriptedAppServerProcess();
    await using var session = new CodexAppServerSession(ResumeSpec("thread-old"), process, new DeterministicAgentTokenEstimator());

    await session.EnsureReadyAsync();
    AgentTurnResult result = await session.RunTurnAsync("hello");

    Assert.Equal(AgentTurnState.Completed, result.State);
    Assert.Equal(["initialize", "initialized", "thread/resume", "turn/start"], process.Methods);
    Assert.Equal("thread-old", session.ThreadId);
    Assert.Equal("thread-old", ParamsOf(process, "turn/start").GetProperty("threadId").GetString());
}

[Fact]
public async Task ResumeFrameCarriesTheSessionPostureAndExcludeTurns()
{
    var process = new ScriptedAppServerProcess();
    await using var session = new CodexAppServerSession(ResumeSpec("thread-old"), process, new DeterministicAgentTokenEstimator());

    await session.EnsureReadyAsync();

    JsonElement p = ParamsOf(process, "thread/resume");
    Assert.Equal("thread-old", p.GetProperty("threadId").GetString());
    Assert.Equal("/repo", p.GetProperty("cwd").GetString());
    Assert.Equal("read-only", p.GetProperty("sandbox").GetString());
    Assert.Equal("never", p.GetProperty("approvalPolicy").GetString());
    Assert.True(p.GetProperty("excludeTurns").GetBoolean());
}

[Fact]
public async Task RejectedResumeThrowsTheTypedExceptionFromEnsureReady()
{
    var process = new ScriptedAppServerProcess { RejectResume = true };
    await using var session = new CodexAppServerSession(ResumeSpec("thread-old"), process, new DeterministicAgentTokenEstimator());

    AgentSessionResumeException ex =
        await Assert.ThrowsAsync<AgentSessionResumeException>(() => session.EnsureReadyAsync());
    Assert.Contains("no rollout found", ex.Message);
}

[Fact]
public async Task ThreadIdIsNullBeforeTheHandshakeAndSetAfterIt()
{
    var process = new ScriptedAppServerProcess();
    await using CodexAppServerSession session = NewSession(process);

    Assert.Null(session.ThreadId);
    await session.RunTurnAsync("hello");
    Assert.Equal("thread-xyz", session.ThreadId);
}

[Fact]
public async Task NonResumeEnsureReadyRunsTheNormalHandshakeExactlyOnce()
{
    var process = new ScriptedAppServerProcess();
    await using CodexAppServerSession session = NewSession(process);

    await session.EnsureReadyAsync();
    await session.RunTurnAsync("hello");

    Assert.Equal(["initialize", "initialized", "thread/start", "turn/start"], process.Methods);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/CommandCenter.Backend.Tests --filter "FullyQualifiedName~CodexAppServerSessionTests"`
Expected: FAIL to compile — `EnsureReadyAsync` does not exist.

- [ ] **Step 4: Implement `EnsureReadyAsync` and the resume branch**

In `src/CommandCenter.Agents/Services/CodexAppServerSession.cs`:

Add after `RunTurnAsync` (before `CancelAsync`):

```csharp
/// <summary>
/// Runs the handshake eagerly. Used by the resume path: the caller must know whether the resume succeeded
/// BEFORE composing its first prompt (priming is decided at prompt-build time). Normal sessions keep the
/// lazy first-turn handshake; calling this on one is a harmless no-op after the first time.
/// </summary>
public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(disposed, this);

    using CancellationTokenSource linked =
        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCts.Token);

    await turnGate.WaitAsync(linked.Token);
    try
    {
        await EnsureHandshakeAsync(linked.Token);
    }
    finally
    {
        turnGate.Release();
    }
}
```

Replace the thread-start section of `EnsureHandshakeAsync` (from `long threadStartId = NextId();` through `initialized = true;`) with:

```csharp
long threadRequestId = NextId();
bool resuming = spec.ResumeThreadId is { Length: > 0 };
string threadFrame = resuming
    ? CodexAppServerProtocol.ThreadResume(
        threadRequestId, spec.ResumeThreadId!, spec.WorkingDirectory, Sandbox(), ApprovalPolicy())
    : CodexAppServerProtocol.ThreadStart(threadRequestId, spec.WorkingDirectory, Sandbox(), ApprovalPolicy());
CodexAppServerMessage threadResponse = await SendRequestAsync(threadRequestId, threadFrame, cancellationToken);
if (resuming && threadResponse.ErrorMessage is { } resumeError)
{
    // A rejected resume (rollout deleted, unknown thread, protocol drift) is RECOVERABLE: the typed
    // exception lets the runtime tear this process down and the caller fall back to a fresh thread.
    throw new AgentSessionResumeException($"Codex thread/resume failed: {resumeError}");
}

ThrowIfError(threadResponse, "thread/start");

string? extractedThreadId = ExtractThreadId(threadResponse.Result);
if (extractedThreadId is null)
{
    if (resuming)
    {
        throw new AgentSessionResumeException("Codex thread/resume response did not contain a thread id.");
    }

    throw new InvalidOperationException("Codex thread/start response did not contain a thread id.");
}

threadId = extractedThreadId;
initialized = true;
```

Update the class doc comment's handshake sentence (line 21-22) to: "The first turn lazily runs the `initialize` → `initialized` → `thread/start` (or `thread/resume` when the spec carries a ResumeThreadId; the resume path runs it eagerly via EnsureReadyAsync) handshake; later turns reuse the thread."

- [ ] **Step 5: Make `AgentRuntime.OpenSessionAsync` eager for resume specs**

In `src/CommandCenter.Agents/Services/AgentRuntime.cs`, in `OpenSessionAsync`, insert between the registry guard and `return session;`:

```csharp
if (spec.ResumeThreadId is not null)
{
    // Resume verification is EAGER: the caller decides how to prime its first prompt based on whether
    // the resume succeeded, so the outcome must be known before any turn runs. On failure the process is
    // torn down through the single-sited CloseSessionAsync (deregister + dispose) and the typed exception
    // surfaces so the caller can fall back to a fresh, non-resuming open. A normal open stays lazy.
    try
    {
        await session.EnsureReadyAsync(cancellationToken);
    }
    catch (AgentSessionResumeException)
    {
        await CloseSessionAsync(session);
        throw;
    }
    catch (OperationCanceledException)
    {
        await CloseSessionAsync(session);
        throw;
    }
    catch (Exception ex)
    {
        await CloseSessionAsync(session);
        throw new AgentSessionResumeException($"Codex session resume failed: {ex.Message}", ex);
    }
}
```

- [ ] **Step 6: Write the failing runtime-level tests**

Create `tests/CommandCenter.Backend.Tests/AgentRuntimeResumeTests.cs`:

```csharp
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Backend.Tests;

public sealed class AgentRuntimeResumeTests
{
    private sealed class StubLauncher(IAgentProcess process) : IAgentProcessLauncher
    {
        public Task<IAgentProcess> LaunchAsync(
            AgentSessionSpec spec, AgentSessionMode mode, CancellationToken cancellationToken = default) =>
            Task.FromResult(process);
    }

    // OpenSessionAsync never inspects turn boundaries (that is the one-shot path), so this must never be hit.
    private sealed class UnusedBoundaryDetector : IAgentTurnBoundaryDetector
    {
        public AgentLineInspection Inspect(string line) => throw new NotSupportedException();
    }

    private static AgentSessionSpec ResumeSpec() => new(
        SessionIdentity.New(),
        "repo-1",
        SessionRole.Decision,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
        workingDirectory: "/repo",
        resumeThreadId: "thread-old");

    private static AgentRuntime NewRuntime(ScriptedAppServerProcess process, AgentSessionRegistry registry) =>
        new(new StubLauncher(process), new UnusedBoundaryDetector(), new DeterministicAgentTokenEstimator(), registry);

    [Fact]
    public async Task OpenSessionWithResumeIdRunsTheHandshakeEagerly()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        IAgentSession session = await runtime.OpenSessionAsync(ResumeSpec());

        // The handshake already ran at open time — before any turn.
        Assert.Equal(["initialize", "initialized", "thread/resume"], process.Methods);
        Assert.Equal("thread-old", session.ThreadId);
        Assert.Equal(1, registry.Count);

        await runtime.CloseSessionAsync(session);
    }

    [Fact]
    public async Task FailedResumeDisposesTheProcessDeregistersAndThrowsTheTypedException()
    {
        var process = new ScriptedAppServerProcess { RejectResume = true };
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);

        await Assert.ThrowsAsync<AgentSessionResumeException>(() => runtime.OpenSessionAsync(ResumeSpec()));

        Assert.True(process.HasExited);   // the codex process was torn down, not leaked
        Assert.Equal(0, registry.Count);  // and no stale registry entry survives
    }

    [Fact]
    public async Task OpenSessionWithoutResumeIdStaysLazy()
    {
        var process = new ScriptedAppServerProcess();
        var registry = new AgentSessionRegistry();
        AgentRuntime runtime = NewRuntime(process, registry);
        var spec = new AgentSessionSpec(
            SessionIdentity.New(),
            "repo-1",
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
            workingDirectory: "/repo");

        IAgentSession session = await runtime.OpenSessionAsync(spec);

        Assert.Empty(process.Methods);   // no frame sent — the handshake waits for the first turn
        Assert.Null(session.ThreadId);

        await runtime.CloseSessionAsync(session);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/CommandCenter.Backend.Tests --filter "FullyQualifiedName~CodexAppServerSessionTests|FullyQualifiedName~AgentRuntimeResumeTests|FullyQualifiedName~CodexAppServerProtocolTests"`
Expected: PASS — all existing session tests (unchanged behavior) plus the 8 new ones (5 session-level, 3 runtime-level).

- [ ] **Step 8: Commit**

```bash
git add src/CommandCenter.Agents/Services/CodexAppServerSession.cs src/CommandCenter.Agents/Services/AgentRuntime.cs tests/CommandCenter.Backend.Tests/ScriptedAppServerProcess.cs tests/CommandCenter.Backend.Tests/CodexAppServerSessionTests.cs tests/CommandCenter.Backend.Tests/AgentRuntimeResumeTests.cs
git commit -m "feat(agents): eager thread/resume handshake with typed fallback exception"
```

---

### Task 5: `AgentSpecs.Decision` resume overload

**Files:**
- Modify: `src/CommandCenter.CLI/AgentSpecs.cs`
- Test: `tests/CommandCenter.CLI.Tests/AgentSpecsTests.cs`

**Interfaces:**
- Produces: `AgentSpecs.Decision(Repository repository, string? resumeThreadId = null)` — identical posture to today, plus the resume id.

- [ ] **Step 1: Write the failing tests**

Add to `tests/CommandCenter.CLI.Tests/AgentSpecsTests.cs` (match the file's existing repo-construction style):

```csharp
[Fact]
public void Decision_WithResumeThreadId_CarriesItOnTheSpec()
{
    var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    AgentSessionSpec spec = AgentSpecs.Decision(repo, "thread-old");

    Assert.Equal("thread-old", spec.ResumeThreadId);
    // The resume overload must not perturb the decision posture.
    Assert.Equal("read-only", spec.Sandbox.Identifier);
    Assert.Equal("xhigh", spec.Effort.Identifier);
    Assert.Equal("/repo", spec.WorkingDirectory);
}

[Fact]
public void Decision_Default_HasNoResumeThreadId()
{
    var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    Assert.Null(AgentSpecs.Decision(repo).ResumeThreadId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~AgentSpecsTests"`
Expected: FAIL to compile — no two-argument `Decision` overload.

- [ ] **Step 3: Implement**

In `src/CommandCenter.CLI/AgentSpecs.cs`, change `Decision` to:

```csharp
public static AgentSessionSpec Decision(Repository repository, string? resumeThreadId = null) =>
    new(
        SessionIdentity.New(),
        repository.Id.ToString("N"),
        SessionRole.Decision,
        new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.High, "xhigh"),
        repository.Path,
        resumeThreadId: resumeThreadId);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~AgentSpecsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CommandCenter.CLI/AgentSpecs.cs tests/CommandCenter.CLI.Tests/AgentSpecsTests.cs
git commit -m "feat(cli): AgentSpecs.Decision carries an optional resume thread id"
```

---

### Task 6: `DecisionSession` integration — resume on first open, persist after turns, clear on dead-thread closes

**Files:**
- Modify: `src/CommandCenter.CLI/DecisionSession.cs`
- Modify: `tests/CommandCenter.CLI.Tests/TestDoubles.cs` (add `FakeDecisionSessionResumeStore`)
- Test: `tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs`

**Interfaces:**
- Consumes: `IDecisionSessionResumeStore`/`DecisionSessionResumeState`/`NullDecisionSessionResumeStore` (Task 1), `AgentSpecs.Decision(repo, resumeThreadId)` (Task 5), `AgentSessionResumeException` + `IAgentSession.ThreadId` (Tasks 3-4), CLI `FakeAgentRuntime.OpenedSpecs`/`FailResume` (Task 3).
- Produces: `DecisionSession` ctor gains two trailing optional params: `IDecisionSessionResumeStore? resumeStore = null` (null → `NullDecisionSessionResumeStore`) and `bool resumeEnabled = true`. Behavior contract used by Tasks 7-8: persist after every successful proposal; clear on transfer/failure closes; keep on dispose.

- [ ] **Step 1: Add the fake store to TestDoubles**

Add to `tests/CommandCenter.CLI.Tests/TestDoubles.cs`:

```csharp
internal sealed class FakeDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public DecisionSessionResumeState? State { get; set; }
    public List<DecisionSessionResumeState> Written { get; } = new();
    public int ClearCalls { get; private set; }

    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(State);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        Written.Add(state);
        State = state;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCalls++;
        State = null;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs` a second factory (the existing 5-tuple `New` stays untouched so the 14 existing tests keep compiling) and the new tests:

```csharp
private static (DecisionSession Session, FakeAgentRuntime Rt, MemoryArtifactStore Store, Repository Repo,
    RecordingLoopConsole Con, FakeDecisionSessionResumeStore Resume)
    NewWithResume(
        DecisionSessionRouterOptions? routerOptions = null,
        DecisionSessionResumeState? state = null,
        bool resumeEnabled = true)
{
    var store = new MemoryArtifactStore();
    var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
    var art = new LoopArtifacts(store, repo);
    var con = new RecordingLoopConsole();
    var rt = new FakeAgentRuntime(store);
    var router = new DecisionSessionRouter(routerOptions ?? new DecisionSessionRouterOptions());
    var sandbox = new FakeSandboxWorkspaceFactory { Root = repo.Path };
    var resume = new FakeDecisionSessionResumeStore { State = state };
    var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox,
        resumeStore: resume, resumeEnabled: resumeEnabled);
    return (session, rt, store, repo, con, resume);
}

private static DecisionSessionResumeState ResumeState(string threadId = "thread-old") =>
    new(threadId, 100, 5d, 2, 3d, 2d, 300_000d, 1, 500, 1);

[Fact]
public async Task Run_FirstEntry_WithPersistedState_ResumesWarm_NoContextResend_AndRestoresAccounting()
{
    var (session, rt, store, repo, con, resume) = NewWithResume(state: ResumeState());
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
    {
        // A successfully resumed thread already holds the operational context — the proposal is the
        // warm handoff-only delta, exactly as if the process had never restarted.
        Assert.DoesNotContain("OPCTX", prompt);
        Assert.Contains("HANDOFF", prompt);
        return Turns.Completed("D-RESUMED");
    }));

    await session.RunAsync(CancellationToken.None);

    Assert.Equal("thread-old", rt.OpenedSpecs.Single().ResumeThreadId);
    Assert.Contains(con.Events, e => e.Kind == "info" && e.Text.Contains("Resumed decision session"));
    // The restored accounting flowed through the post-turn persist: reuseCycles 2 -> 3, reuseCost intact,
    // transfer calibration intact.
    DecisionSessionResumeState written = Assert.Single(resume.Written);
    Assert.Equal("thread-old", written.ThreadId);
    Assert.Equal(3, written.ReuseCycles);
    Assert.Equal(5d, written.ReuseCost);
    Assert.Equal(300_000d, written.TransferCost);
    Assert.Equal(1, written.TransferCount);
}

[Fact]
public async Task Run_FirstEntry_ResumeFails_WarnsClearsAndFallsBackToAFreshPrimedProcess()
{
    var (session, rt, store, repo, con, resume) = NewWithResume(state: ResumeState());
    rt.FailResume = true;
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "HANDOFF");
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
    {
        Assert.Contains("OPCTX", prompt);   // fresh process: primed inline, byte-identical to today
        return Turns.Completed("D-FRESH");
    }));

    await session.RunAsync(CancellationToken.None);

    Assert.Equal(2, rt.OpenedSpecs.Count);
    Assert.Equal("thread-old", rt.OpenedSpecs[0].ResumeThreadId);
    Assert.Null(rt.OpenedSpecs[1].ResumeThreadId);
    Assert.Contains(con.Events, e => e.Kind == "warn" && e.Text.Contains("Could not resume"));
    Assert.Equal(1, resume.ClearCalls);
    // The fresh thread re-persisted after its successful turn — the next run resumes THAT thread.
    Assert.Equal("thread-1", Assert.Single(resume.Written).ThreadId);
}

[Fact]
public async Task Run_NoPersistedState_OpensFresh_AndPersistsAfterTheSuccessfulProposal()
{
    var (session, rt, store, repo, _, resume) = NewWithResume();
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));

    await session.RunAsync(CancellationToken.None);

    Assert.Null(rt.OpenedSpecs.Single().ResumeThreadId);
    DecisionSessionResumeState written = Assert.Single(resume.Written);
    Assert.Equal("thread-1", written.ThreadId);
    Assert.Equal(1, written.ReuseCycles);
    Assert.Equal(20, written.OccupancyTokens);
}

[Fact]
public async Task Run_TransferRecycle_ClearsTheState_ReopensWithoutResume_ThenPersistsTheNewThread()
{
    var (session, rt, store, repo, _, resume) = NewWithResume(
        new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

    // Round 1: propose (occupancy 20 -> round 2 crosses the guard and Transfers).
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
    await session.RunAsync(CancellationToken.None);

    // Round 2: Transfer (delta + update + optimize + propose on the recycled process).
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));
    rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
    {
        s.WriteAsync(Resolve(repo, SandboxContext), "OPCTX-1").Wait();
        return Turns.Completed("updated");
    }));
    rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("optimized")));
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));
    await session.RunAsync(CancellationToken.None);

    Assert.Equal(1, resume.ClearCalls);                    // the transfer close deleted the dead thread's state
    Assert.Null(rt.OpenedSpecs[1].ResumeThreadId);         // the recycle opened FRESH — resume is first-open-only
    Assert.Equal(2, resume.Written.Count);
    Assert.Equal("thread-2", resume.Written[^1].ThreadId); // the post-transfer thread re-persisted
}

[Fact]
public async Task Run_FailedProposal_ClearsThePersistedState()
{
    var (session, rt, store, repo, _, resume) = NewWithResume();
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Failed()));

    await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));

    Assert.Equal(1, resume.ClearCalls);
    Assert.Empty(resume.Written);
}

[Fact]
public async Task Dispose_KeepsThePersistedState_ItIsTheNextRunsResumePayload()
{
    var (session, rt, store, repo, _, resume) = NewWithResume();
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D1")));

    await session.RunAsync(CancellationToken.None);
    await session.DisposeAsync();

    Assert.Equal(0, resume.ClearCalls);
    Assert.NotNull(resume.State);
    Assert.Equal(1, rt.ClosedSessions);   // the process still dies with the run — only the STATE survives
}

[Fact]
public async Task Run_WhenResumeDisabled_OpensFresh_ButStillPersists()
{
    var (session, rt, store, repo, _, resume) = NewWithResume(state: ResumeState(), resumeEnabled: false);
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");
    rt.SessionTurns.Enqueue(new ScriptedTurn((_, prompt, _) =>
    {
        Assert.Contains("OPCTX", prompt);   // no resume attempt -> fresh priming
        return Turns.Completed("D1");
    }));

    await session.RunAsync(CancellationToken.None);

    Assert.Null(rt.OpenedSpecs.Single().ResumeThreadId);   // the kill switch skips ONLY the resume attempt
    Assert.NotEmpty(resume.Written);                        // persist/clear behavior is unchanged
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~DecisionSessionTests"`
Expected: FAIL to compile — `DecisionSession` has no `resumeStore`/`resumeEnabled` parameters.

- [ ] **Step 4: Implement in `DecisionSession`**

In `src/CommandCenter.CLI/DecisionSession.cs`:

(a) Append two optional primary-ctor parameters (after `sandboxFactory`):

```csharp
    IDecisionCostModel? costModel = null,
    ISandboxWorkspaceFactory? sandboxFactory = null,
    IDecisionSessionResumeStore? resumeStore = null,
    bool resumeEnabled = true) : IAsyncDisposable
```

(b) Add the defaulted field next to the existing `costModel`/`sandboxFactory` fields, and the attempt flag next to `seeded`:

```csharp
private readonly IDecisionSessionResumeStore resumeStore = resumeStore ?? new NullDecisionSessionResumeStore();
```

```csharp
private IAgentSession? session;
private bool seeded;
private bool resumeAttempted;
```

(c) In `RunAsync`, replace `session ??= await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);` with:

```csharp
session ??= await OpenOrResumeSessionAsync(cancellationToken);
```

(d) In `RunAsync`, after `RecordProposalCost(proposed.Usage);` (keeping `seeded = true;` above it), add:

```csharp
await PersistResumeStateAsync(cancellationToken);
```

(e) Add the two new methods (place them after `BuildProposalPromptAsync`):

```csharp
/// <summary>
/// The FIRST open of this CLI process attempts to resume the persisted decision session (if any); every
/// later open — the post-Transfer recycle, the reopen after a failed turn — starts fresh, because the
/// persisted state describes a thread this process has already moved past. Restored accounting is applied
/// only HERE, at a successful resume: the router's route evaluation runs before the open, so the first
/// route of a run always sees pre-restore (zeroed) inputs and the existing !seeded downgrade guards it.
/// </summary>
private async Task<IAgentSession> OpenOrResumeSessionAsync(CancellationToken cancellationToken)
{
    bool firstOpen = !resumeAttempted;
    resumeAttempted = true;

    DecisionSessionResumeState? state = firstOpen && resumeEnabled
        ? await resumeStore.ReadAsync(cancellationToken)
        : null;
    if (state is null)
    {
        return await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
    }

    try
    {
        IAgentSession resumed = await runtime.OpenSessionAsync(
            AgentSpecs.Decision(repository, state.ThreadId), cancellationToken);

        // The resumed thread already holds the operational context (its first proposal primed it), and
        // the router accounting it accrued — restore both so priming and transfer economics continue
        // where the previous run left off.
        seeded = true;
        occupancyTokens = state.OccupancyTokens;
        reuseCost = state.ReuseCost;
        reuseCycles = state.ReuseCycles;
        lastCycleCost = state.LastCycleCost;
        prevCycleCost = state.PrevCycleCost;
        transferCost = state.TransferCost;
        transferCount = state.TransferCount;
        previousOperationalContextSize = state.PreviousOperationalContextSize;
        operationalContextGrowthStreak = state.OperationalContextGrowthStreak;
        console.Info($"Resumed decision session (thread {state.ThreadId}).");
        return resumed;
    }
    catch (AgentSessionResumeException ex)
    {
        console.Warn($"Could not resume decision session (thread {state.ThreadId}): {ex.Message} Starting fresh.");
        await resumeStore.ClearAsync(cancellationToken);
        return await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
    }
}

/// <summary>
/// The state is only ever written after a SUCCESSFUL proposal turn, so its existence implies the thread is
/// primed (no seeded field in the schema). One small file write per decision step; the store is fail-open.
/// </summary>
private async Task PersistResumeStateAsync(CancellationToken cancellationToken)
{
    if (session?.ThreadId is not { Length: > 0 } threadId)
    {
        return; // no codex thread id (legacy/one-shot shapes) — nothing a later run could resume
    }

    await resumeStore.WriteAsync(new DecisionSessionResumeState(
        threadId, occupancyTokens, reuseCost, reuseCycles, lastCycleCost, prevCycleCost,
        transferCost, transferCount, previousOperationalContextSize, operationalContextGrowthStreak),
        cancellationToken);
}
```

(f) Change `CloseAsync`/`DisposeAsync` (bottom of the class) to:

```csharp
// clearResumeState: a Transfer recycle or a failed turn ends the thread's useful life — the persisted
// resume state must die with it (the recycled process re-persists after its first successful turn).
// Disposal (loop exit) KEEPS the state: it is precisely the next run's resume payload, and no turn can
// mutate the thread between the last persist and disposal.
private async Task CloseAsync(bool clearResumeState = true)
{
    if (session is not null)
    {
        await runtime.CloseSessionAsync(session);
        session = null;
        seeded = false;
        // Per-process accounting resets for the fresh process; transferCost/transferCount persist.
        occupancyTokens = 0;
        reuseCost = 0d;
        reuseCycles = 0;
        lastCycleCost = 0d;
        prevCycleCost = 0d;

        if (clearResumeState)
        {
            await resumeStore.ClearAsync(CancellationToken.None);
        }
    }
}

public async ValueTask DisposeAsync() => await CloseAsync(clearResumeState: false);
```

The three existing `await CloseAsync();` call sites (failed proposal in `RunAsync`, failed delta turn in `TransferAsync`, and the recycle close in `TransferAsync`) stay untouched — the default `clearResumeState: true` is exactly their contract.

(g) Update the class doc comment: append a sentence — "Across CLI runs, the warm process is resumable: the codex thread id + router accounting persist to {repo}/.commandcenter/decision-session.json after every successful proposal (see OpenOrResumeSessionAsync)."

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~DecisionSessionTests"`
Expected: PASS — all 14 existing tests (unchanged) plus the 7 new ones.

- [ ] **Step 6: Commit**

```bash
git add src/CommandCenter.CLI/DecisionSession.cs tests/CommandCenter.CLI.Tests/TestDoubles.cs tests/CommandCenter.CLI.Tests/DecisionSessionTests.cs
git commit -m "feat(cli): DecisionSession resumes the persisted codex thread on first entry"
```

---

### Task 7: Loop CLI — clear on epic completion, kill switch, composition wiring

**Files:**
- Modify: `src/CommandCenter.CLI/LoopRunner.cs`
- Create: `src/CommandCenter.CLI/DecisionResumeComposition.cs`
- Modify: `src/CommandCenter.CLI/Program.cs`
- Test: `tests/CommandCenter.CLI.Tests/LoopRunnerTests.cs`
- Test: `tests/CommandCenter.CLI.Tests/DecisionResumeCompositionTests.cs` (new)

**Interfaces:**
- Consumes: `IDecisionSessionResumeStore` (Task 1), `FakeDecisionSessionResumeStore` (Task 6), `DecisionSession` resume params (Task 6).
- Produces: `LoopRunner` ctor gains `IDecisionSessionResumeStore resumeStore` (inserted before the final `ILoopConsole console` param); `DecisionResumeComposition.IsEnabled()`.

- [ ] **Step 1: Write the failing LoopRunner test**

In `tests/CommandCenter.CLI.Tests/LoopRunnerTests.cs`, extend the harness: add `FakeDecisionSessionResumeStore Resume` to the `Harness` record, create `var resume = new FakeDecisionSessionResumeStore();` in `New()`, and pass it to the runner: `new LoopRunner(gate, art, exec, dec, submodulePublisher, commitGate, resume, con)`. Add `using CommandCenter.Orchestration.Models;` to the file. Then add:

```csharp
[Fact]
public async Task Run_WhenEpicComplete_ClearsThePersistedDecisionSessionState()
{
    var h = New();
    await h.Store.WriteAsync(Resolve(h.Repo, ".agents/milestones/m1.md"), "- [x] done");
    h.Resume.State = new DecisionSessionResumeState("thread-old", 0, 0d, 0, 0d, 0d, 250_000d, 0, null, 0);

    LoopOutcome outcome = await h.Runner.RunAsync(CancellationToken.None);

    Assert.Equal(LoopOutcome.EpicCompleted, outcome);
    Assert.Equal(1, h.Resume.ClearCalls);   // idempotent: re-runs against a completed epic re-delete a no-op
    Assert.Null(h.Resume.State);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/CommandCenter.CLI.Tests --filter "FullyQualifiedName~LoopRunnerTests"`
Expected: FAIL to compile — `LoopRunner` has no `resumeStore` parameter.

- [ ] **Step 3: Implement the LoopRunner clear**

In `src/CommandCenter.CLI/LoopRunner.cs`:
- add `using CommandCenter.Orchestration.Abstractions;`
- extend the primary ctor (insert before `ILoopConsole console`):

```csharp
internal sealed class LoopRunner(
    MilestoneGate gate,
    LoopArtifacts artifacts,
    ExecutionStep execution,
    DecisionSession decision,
    AgentsSubmodulePublisher submodulePublisher,
    CommitGate commitGate,
    IDecisionSessionResumeStore resumeStore,
    ILoopConsole console) : IAsyncDisposable
```

- change the epic-complete gate block to:

```csharp
if (await gate.IsEpicCompleteAsync())
{
    // A finished epic obsoletes the persisted decision-session resume state — the next epic must start
    // from a fresh decision process primed with its own operational context. Idempotent by design: this
    // fires again on every re-run against a completed epic, and deleting nothing is a no-op. (Plan.CLI's
    // epic rollover clears it too, covering the epic-rolled-over-without-this-gate-observing case.)
    await resumeStore.ClearAsync(cancellationToken);
    return LoopOutcome.EpicCompleted;
}
```

- [ ] **Step 4: Write the kill-switch composition + test**

Create `src/CommandCenter.CLI/DecisionResumeComposition.cs`:

```csharp
using System;

namespace CommandCenter.Cli;

/// <summary>
/// The decision-session resume kill switch. Enabled by default; set
/// <c>COMMANDCENTER_DECISION_RESUME=0</c> (or <c>false</c>) to skip the resume-on-open attempt — persist
/// and clear behavior is unchanged either way. Mirrors COMMANDCENTER_SESSION_LOG. Insurance against
/// thread/resume behavioral surprises: the app-server protocol is still marked experimental upstream.
/// </summary>
internal static class DecisionResumeComposition
{
    public static bool IsEnabled()
    {
        string? flag = Environment.GetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME");
        return !(string.Equals(flag, "0", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase));
    }
}
```

Create `tests/CommandCenter.CLI.Tests/DecisionResumeCompositionTests.cs`:

```csharp
using Xunit;

namespace CommandCenter.Cli.Tests;

public sealed class DecisionResumeCompositionTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    public void IsEnabled_HonorsTheKillSwitch(string? value, bool expected)
    {
        string? original = Environment.GetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME");
        try
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME", value);
            Assert.Equal(expected, DecisionResumeComposition.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME", original);
        }
    }
}
```

- [ ] **Step 5: Wire the composition root**

In `src/CommandCenter.CLI/Program.cs`:
- add `using CommandCenter.Orchestration.Services;` (if not already pulled in transitively — the file currently imports `CommandCenter.Orchestration.Services`; verify and skip if present),
- after `var changeDetector = ...` (line 63), add:

```csharp
// Cross-run decision-session resume state (spec: docs/superpowers/specs/2026-07-04-cli-decision-session-resume-design.md).
var resumeStore = new FileDecisionSessionResumeStore(repository, console.Warn);
```

- change the `DecisionSession` construction (line 65) to:

```csharp
var decision = new DecisionSession(gatedRuntime, router, artifacts, console, repository,
    resumeStore: resumeStore, resumeEnabled: DecisionResumeComposition.IsEnabled());
```

- change the `LoopRunner` construction (line 68) to:

```csharp
var loop = new LoopRunner(gate, artifacts, execution, decision, submodulePublisher, commitGate, resumeStore, console);
```

- [ ] **Step 6: Run the CLI suite**

Run: `dotnet test tests/CommandCenter.CLI.Tests`
Expected: PASS (every existing test plus the new ones — currently ~210+).

- [ ] **Step 7: Commit**

```bash
git add src/CommandCenter.CLI/LoopRunner.cs src/CommandCenter.CLI/DecisionResumeComposition.cs src/CommandCenter.CLI/Program.cs tests/CommandCenter.CLI.Tests/LoopRunnerTests.cs tests/CommandCenter.CLI.Tests/DecisionResumeCompositionTests.cs
git commit -m "feat(cli): clear decision-session resume state on epic completion; wire resume + kill switch"
```

---

### Task 8: Plan.CLI — clear on epic rollover

**Files:**
- Modify: `src/CommandCenter.Plan.CLI/PlanPipeline.cs`
- Modify: `src/CommandCenter.Plan.CLI/Program.cs`
- Modify: `tests/CommandCenter.Plan.CLI.Tests/TestDoubles.cs` (add `FakeDecisionSessionResumeStore` — same code as the CLI copy)
- Test: `tests/CommandCenter.Plan.CLI.Tests/PlanPipelineTests.cs`

**Interfaces:**
- Consumes: `IDecisionSessionResumeStore`/`FileDecisionSessionResumeStore` (Task 1).
- Produces: `PlanPipeline` ctor gains `IDecisionSessionResumeStore resumeStore` (inserted before the final `ILoopConsole console` param).

- [ ] **Step 1: Add the fake store to Plan.CLI TestDoubles**

Add to `tests/CommandCenter.Plan.CLI.Tests/TestDoubles.cs` (needs `using CommandCenter.Orchestration.Abstractions;` and `using CommandCenter.Orchestration.Models;` if not present):

```csharp
internal sealed class FakeDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public DecisionSessionResumeState? State { get; set; }
    public List<DecisionSessionResumeState> Written { get; } = new();
    public int ClearCalls { get; private set; }

    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(State);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        Written.Add(state);
        State = state;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCalls++;
        State = null;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

In `tests/CommandCenter.Plan.CLI.Tests/PlanPipelineTests.cs`: extend `Harness` with `FakeDecisionSessionResumeStore Resume`, create it in `New()` and pass to the pipeline ctor before `console`. Add `using CommandCenter.Orchestration.Models;`. Then add:

```csharp
[Fact]
public async Task RunAsync_WhenTheRolloverArchives_ClearsThePersistedDecisionSessionState()
{
    Harness h = New();
    // A COMPLETE previous workspace (presence-based criterion): plan + details + operational context +
    // a non-empty milestones directory. The scripted new-epic invocation deletes plan.md so the
    // rollover's post-gate passes; the run then stops at Preflight (no specs/epic.md) — which is fine,
    // the clear must already have happened.
    await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan), "PLAN");
    await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Details), "DETAILS");
    await h.Store.WriteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX");
    await h.Store.WriteAsync(Resolve(h.Repo, MilestonePath("m1.md")), "- [ ] t");
    h.Resume.State = new DecisionSessionResumeState("thread-old", 0, 0d, 0, 0d, 0d, 250_000d, 0, null, 0);
    h.Processes.Handler = (_, args) =>
    {
        if (args.Count == 0 || args.Contains("new-epic"))
        {
            h.Store.DeleteAsync(Resolve(h.Repo, OrchestrationArtifactPaths.Plan)).Wait();
        }

        return FakeProcessRunner.Ok();
    };

    PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

    Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
    Assert.Equal(1, h.Resume.ClearCalls);
    Assert.Null(h.Resume.State);
}

[Fact]
public async Task RunAsync_WhenNoRolloverHappens_LeavesThePersistedDecisionSessionStateAlone()
{
    Harness h = New();
    // An incomplete workspace: the rollover is skipped and preflight blocks. The resume state survives.
    h.Resume.State = new DecisionSessionResumeState("thread-old", 0, 0d, 0, 0d, 0d, 250_000d, 0, null, 0);

    PlanOutcome outcome = await h.Pipeline.RunAsync(CancellationToken.None);

    Assert.Equal(PlanOutcome.PreflightBlocked, outcome);
    Assert.Equal(0, h.Resume.ClearCalls);
    Assert.NotNull(h.Resume.State);
}
```

(If `EpicRolloverStepTests` scripts the new-epic invocation differently — e.g. matching on the file name rather than args — mirror that file's proven matching pattern in the Handler above.)

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/CommandCenter.Plan.CLI.Tests --filter "FullyQualifiedName~PlanPipelineTests"`
Expected: FAIL to compile — `PlanPipeline` has no `resumeStore` parameter.

- [ ] **Step 4: Implement**

In `src/CommandCenter.Plan.CLI/PlanPipeline.cs`:
- add `using CommandCenter.Orchestration.Abstractions;`
- extend the primary ctor (insert before `ILoopConsole console`):

```csharp
internal sealed class PlanPipeline(
    EpicRolloverStep rollover,
    PreflightGate preflight,
    PlanSession planSession,
    ReviewStep review,
    SandboxedPromptStep oneShot,
    AgentsSubmodulePublisher publisher,
    PlanArtifacts artifacts,
    IDecisionSessionResumeStore resumeStore,
    ILoopConsole console) : IAsyncDisposable
```

- at the top of the rollover-true branch (immediately inside `if (await rollover.TryArchiveAsync(cancellationToken))`, before the publish), add:

```csharp
// The epic boundary invalidates the loop CLI's persisted decision-session resume state — the next
// epic must start from a fresh decision process. Cleared here AS WELL AS in the loop's epic-complete
// gate: a rollover can happen without the loop ever re-running against the completed epic. Idempotent.
await resumeStore.ClearAsync(cancellationToken);
```

In `src/CommandCenter.Plan.CLI/Program.cs` (already imports `CommandCenter.Orchestration.Services`), after the `rollover` construction (line 51), add and rewire:

```csharp
// Shared with CommandCenter.CLI: the loop's decision-session resume state, cleared at the epic boundary.
var resumeStore = new FileDecisionSessionResumeStore(repository, console.Warn);
var pipeline = new PlanPipeline(rollover, preflight, planSession, review, oneShot, publisher, artifacts, resumeStore, console);
```

- [ ] **Step 5: Run the Plan.CLI suite**

Run: `dotnet test tests/CommandCenter.Plan.CLI.Tests`
Expected: PASS (all existing plus the 2 new).

- [ ] **Step 6: Commit**

```bash
git add src/CommandCenter.Plan.CLI/PlanPipeline.cs src/CommandCenter.Plan.CLI/Program.cs tests/CommandCenter.Plan.CLI.Tests/TestDoubles.cs tests/CommandCenter.Plan.CLI.Tests/PlanPipelineTests.cs
git commit -m "feat(plan-cli): clear decision-session resume state at the epic rollover boundary"
```

---

### Task 9: Docs, full-suite verification, operator smoke notes

**Files:**
- Modify: `technical-debt.md`
- Modify: `docs/superpowers/specs/2026-07-01-cli-loop-session-telemetry-log-design.md`

- [ ] **Step 1: Record the backend gap as technical debt**

Append to `technical-debt.md` (use the next free TD number after the file's current highest — the text below assumes TD-13; renumber if taken):

```markdown
## TD-13: Backend decision session does not resume across restarts

The CLI loop persists its decision session's codex thread id + router accounting to
`{repo}/.commandcenter/decision-session.json` and resumes it via app-server `thread/resume`
(spec: docs/superpowers/specs/2026-07-04-cli-decision-session-resume-design.md). The legacy backend's
`RepositoryOrchestrator` decision session (`IAgentSession? decisionSession`) has no equivalent — a backend
restart always re-primes a fresh process. Won't-fix-in-place per the backend-rewrite policy; carry the
CLI's store/spec-field/handshake design into the rewrite instead.
```

- [ ] **Step 2: Supersede the manual gitignore instruction in the telemetry design doc**

In `docs/superpowers/specs/2026-07-01-cli-loop-session-telemetry-log-design.md`, directly after the "Add `.commandcenter/` to the repo's .gitignore (create if absent)" instruction (around lines 125-127), add:

```markdown
> **Superseded (2026-07-04):** `.commandcenter/` is now self-ignoring — `FileDecisionSessionResumeStore`
> writes a `.commandcenter/.gitignore` containing `*` when it first creates the directory, so the manual
> root-.gitignore step is no longer load-bearing once the loop has run a decision step in the repo.
```

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test CommandCenter.slnx`
Expected: PASS across all projects, 0 failures (1 pre-existing skip: the live codex certification fact). If unrelated pre-existing failures appear, report them but do not chase them in this plan.

- [ ] **Step 4: Commit**

```bash
git add technical-debt.md docs/superpowers/specs/2026-07-01-cli-loop-session-telemetry-log-design.md
git commit -m "docs: TD entry for backend resume gap; supersede manual .commandcenter gitignore step"
```

- [ ] **Step 5: Operator smoke (manual, post-merge — do NOT attempt in-session)**

Documented for the operator; requires a codex login and a target repo:

1. Stop any running loops, then publish both CLIs (`publish-cli.bat`, `publish-plan-cli.bat`) — a running loop locks `C:\tools\command-center` DLLs and publish half-fails.
2. Run the loop against a mid-epic repo; let at least one decision turn complete; verify `{repo}/.commandcenter/decision-session.json` exists and `.commandcenter/.gitignore` contains `*`.
3. Kill the CLI (Ctrl+C), rerun: expect `Resumed decision session (thread …)` and a proposal turn **without** the operational-context preamble (watch the decision phase output size).
4. Delete the matching rollout under `~/.codex/sessions` and rerun: expect the `Could not resume … Starting fresh.` warning and normal fresh behavior.
5. Complete an epic (or tick all milestone boxes on a scratch repo): expect the loop to exit `Epic completed.` and the state file to be gone.
6. `COMMANDCENTER_DECISION_RESUME=0` rerun: expect no resume attempt, state file still written.

---

## Self-review checklist (run after all tasks)

- Spec coverage: storage schema + store (Task 1), protocol frame (Task 2), spec field/exception/ThreadId surface (Task 3), eager resume handshake + fallback contract (Task 4), decision-spec overload (Task 5), first-open resume + persist + clear-on-close semantics + kill-switch behavior (Task 6), loop-gate clearing + kill-switch composition + wiring (Task 7), rollover clearing (Task 8), debt/docs/verification (Task 9).
- Every new public/internal name used across tasks matches: `DecisionSessionResumeState`, `IDecisionSessionResumeStore`, `FileDecisionSessionResumeStore`, `NullDecisionSessionResumeStore`, `AgentSessionResumeException`, `ResumeThreadId`, `ThreadId`, `EnsureReadyAsync`, `ThreadResume`, `DecisionResumeComposition`, `FakeDecisionSessionResumeStore`.
- The telemetry locator degradation for resumed sessions (possibly-null `codexLogPath`) is an accepted spec non-goal — no code change.
