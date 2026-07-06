# CLI Loop Per-Turn Session Telemetry Log — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Emit one JSONL row per codex turn capturing repo, codex rollout path, session id/type, raw/cached/effective tokens, and pre/post 5h+weekly capacity, into a repo-local rotating log.

**Architecture:** All capture happens at the one chokepoint every gated turn already passes through — `GatedAgentSession`/`GatedAgentRuntime`. The usage gate is widened to *return* the capacity snapshot it already probes (pre-run); a `SessionTelemetryRecorder` adds one post-turn probe, resolves the codex rollout file, computes effective tokens via the router's cost model, and appends a record through a per-day/size-hybrid rotating sink. Everything is fail-open — telemetry never breaks a turn.

**Tech Stack:** C# / .NET 10, xUnit, System.Text.Json. Project: `src/LoopRelay.CLI` (namespace `LoopRelay.Cli`), tests in `tests/LoopRelay.CLI.Tests` (namespace `LoopRelay.Cli.Tests`).

## Global Constraints

- All new production types are `internal` in namespace `LoopRelay.Cli` (match the existing CLI files).
- Tests are `public` classes in namespace `LoopRelay.Cli.Tests`, xUnit `[Fact]`.
- **Fail-open:** no telemetry path (probe, locator, sink, serialization) may throw out of a turn. On failure, `ILoopConsole.Warn(...)` and continue.
- Capacity is a **remaining percent (0–100)**, from `CodexUsageStatus` (`CodexUsage.cs`). Never a token budget.
- Effective tokens = `IDecisionCostModel.Measure(usage)` — reuse `EffectiveTokenCostModel` (`(prompt−cached) + cached×0.10 + output`). Do not re-derive the formula.
- Rotation size cap constant: **5 MiB = 5,242,880 bytes**.
- Log directory: `<repository.Path>/.LoopRelay/telemetry/`, git-ignored. Files: `sessions.<yyyy-MM-dd>.<NNNN>.jsonl`.
- SDK-style csproj globs `.cs` files — new files need no csproj edits.
- Build: `dotnet build LoopRelay.sln -c Debug`. Test one project: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug`.

## File Structure

**Create (production, `src/LoopRelay.CLI/`):**
- `SessionTelemetryRecord.cs` — the 14-field record + `SessionTelemetryJson.Options`.
- `Clock.cs` — `IClock` + `SystemClock`.
- `SessionTelemetrySink.cs` — `ISessionTelemetrySink`, `RotatingJsonlTelemetrySink`.
- `CodexRolloutLocator.cs` — `ICodexRolloutLocator`, `FileSystemCodexRolloutLocator`.
- `SessionTelemetryRecorder.cs` — `ISessionTelemetryRecorder`, `SessionTelemetryRecorder`, `NullSessionTelemetryRecorder`.
- `SessionTelemetryComposition.cs` — factory building the recorder for `Program.cs`.

**Modify (production):**
- `UsageGate.cs` — `IUsageGate.WaitForCapacityAsync` returns `Task<CodexUsageStatus?>`; re-probe after a wait.
- `GatedAgentRuntime.cs` — thread recorder/clock/repoName; emit a record per turn and one-shot; cache the log path per session.
- `Program.cs` — build the recorder via the factory; pass to `GatedAgentRuntime`.
- `.gitignore` (repo root) — add `.LoopRelay/`.

**Modify (tests, `tests/LoopRelay.CLI.Tests/`):**
- `TestDoubles.cs` — add `FakeClock`, `FakeSessionTelemetrySink`, `FakeCodexRolloutLocator`, `RecordingSessionTelemetryRecorder`.
- `GatedAgentRuntimeTests.cs` — update `RecordingGate` return type + `New()` ctor; add emission tests.
- New: `SessionTelemetryRecordTests.cs`, `RotatingJsonlTelemetrySinkTests.cs`, `FileSystemCodexRolloutLocatorTests.cs`, `SessionTelemetryRecorderTests.cs`, `SessionTelemetryCompositionTests.cs`.
- `UsageGateTests.cs` — add the re-probe-after-wait test.

---

### Task 1: `SessionTelemetryRecord` + JSON options

**Files:**
- Create: `src/LoopRelay.CLI/SessionTelemetryRecord.cs`
- Test: `tests/LoopRelay.CLI.Tests/SessionTelemetryRecordTests.cs`

**Interfaces:**
- Produces: `SessionTelemetryRecord(DateTimeOffset Timestamp, string RepoName, string? CodexLogPath, string SessionId, string SessionType, int TurnIndex, int PromptTokens, int OutputTokens, int CachedTokens, double EffectiveTokens, int? PreFiveHourPercent, int? PostFiveHourPercent, int? PreWeeklyPercent, int? PostWeeklyPercent)` and `SessionTelemetryJson.Options` (`JsonSerializerOptions`, camelCase, compact).

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.Text.Json;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class SessionTelemetryRecordTests
{
    private static SessionTelemetryRecord Sample(string? path = "/logs/rollout.jsonl",
        int? pre5h = 55, int? post5h = 54) =>
        new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            "myrepo", path, "sid-1", "Decision", 2,
            100, 20, 30, 97.0, pre5h, post5h, 80, 79);

    [Fact]
    public void SerializesToSingleCamelCaseJsonLine()
    {
        string json = JsonSerializer.Serialize(Sample(), SessionTelemetryJson.Options);

        Assert.DoesNotContain("\n", json);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        Assert.Equal("myrepo", r.GetProperty("repoName").GetString());
        Assert.Equal("/logs/rollout.jsonl", r.GetProperty("codexLogPath").GetString());
        Assert.Equal("Decision", r.GetProperty("sessionType").GetString());
        Assert.Equal(2, r.GetProperty("turnIndex").GetInt32());
        Assert.Equal(100, r.GetProperty("promptTokens").GetInt32());
        Assert.Equal(30, r.GetProperty("cachedTokens").GetInt32());
        Assert.Equal(97.0, r.GetProperty("effectiveTokens").GetDouble());
        Assert.Equal(55, r.GetProperty("preFiveHourPercent").GetInt32());
        Assert.Equal(80, r.GetProperty("preWeeklyPercent").GetInt32());
    }

    [Fact]
    public void EmitsNullsForAbsentCapacityAndPath()
    {
        string json = JsonSerializer.Serialize(Sample(path: null, pre5h: null, post5h: null),
            SessionTelemetryJson.Options);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        Assert.Equal(JsonValueKind.Null, r.GetProperty("codexLogPath").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("preFiveHourPercent").ValueKind);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter SessionTelemetryRecordTests`
Expected: FAIL — `SessionTelemetryRecord` / `SessionTelemetryJson` do not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Text.Json;

namespace LoopRelay.Cli;

/// <summary>
/// One row of the per-turn session telemetry log. Capacity fields are a remaining PERCENT (0–100) or null
/// when the codex usage probe could not be read. Raw tokens = <see cref="PromptTokens"/> + <see cref="OutputTokens"/>;
/// <see cref="EffectiveTokens"/> is the router's cache-adjusted cost. Many rows share one <see cref="CodexLogPath"/>
/// (one rollout file per codex process serves many turns).
/// </summary>
internal sealed record SessionTelemetryRecord(
    DateTimeOffset Timestamp,
    string RepoName,
    string? CodexLogPath,
    string SessionId,
    string SessionType,
    int TurnIndex,
    int PromptTokens,
    int OutputTokens,
    int CachedTokens,
    double EffectiveTokens,
    int? PreFiveHourPercent,
    int? PostFiveHourPercent,
    int? PreWeeklyPercent,
    int? PostWeeklyPercent);

/// <summary>Canonical serialization for the telemetry log: compact, camelCase, one object per line.</summary>
internal static class SessionTelemetryJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter SessionTelemetryRecordTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/SessionTelemetryRecord.cs tests/LoopRelay.CLI.Tests/SessionTelemetryRecordTests.cs
git commit -m "feat(cli): SessionTelemetryRecord + canonical JSON options"
```

---

### Task 2: `IClock` / `SystemClock`

**Files:**
- Create: `src/LoopRelay.CLI/Clock.cs`
- Modify: `tests/LoopRelay.CLI.Tests/TestDoubles.cs` (add `FakeClock`)
- Test: covered by `RotatingJsonlTelemetrySinkTests` (Task 3); add a smoke test here.

**Interfaces:**
- Produces: `IClock { DateTimeOffset UtcNow { get; } }`, `SystemClock : IClock`. Test double `FakeClock : IClock { DateTimeOffset UtcNow { get; set; } }`.

- [ ] **Step 1: Write the failing test** — append to `tests/LoopRelay.CLI.Tests/TestDoubles.cs` (inside the `namespace LoopRelay.Cli.Tests;`):

```csharp
/// <summary>A clock a test can set to any instant (drives day-rotation + record timestamps).</summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
}
```

And create `tests/LoopRelay.CLI.Tests/ClockTests.cs`:

```csharp
using System;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class ClockTests
{
    [Fact]
    public void SystemClock_ReturnsAUtcInstantNearNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        DateTimeOffset value = new SystemClock().UtcNow;
        Assert.True(value >= before.AddSeconds(-5) && value <= DateTimeOffset.UtcNow.AddSeconds(5));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter ClockTests`
Expected: FAIL — `IClock` / `SystemClock` do not exist.

- [ ] **Step 3: Write minimal implementation** — `src/LoopRelay.CLI/Clock.cs`:

```csharp
using System;

namespace LoopRelay.Cli;

/// <summary>Wall clock, abstracted so telemetry timestamps and day-rotation are deterministic under test.</summary>
internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter ClockTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/Clock.cs tests/LoopRelay.CLI.Tests/Clock*.cs tests/LoopRelay.CLI.Tests/TestDoubles.cs
git commit -m "feat(cli): IClock/SystemClock + FakeClock test double"
```

---

### Task 3: `RotatingJsonlTelemetrySink` (per-day/size hybrid, keep-all)

**Files:**
- Create: `src/LoopRelay.CLI/SessionTelemetrySink.cs`
- Test: `tests/LoopRelay.CLI.Tests/RotatingJsonlTelemetrySinkTests.cs`

**Interfaces:**
- Consumes: `SessionTelemetryRecord`, `SessionTelemetryJson.Options` (Task 1), `IClock` (Task 2).
- Produces: `ISessionTelemetrySink { void Append(SessionTelemetryRecord record); }`, `RotatingJsonlTelemetrySink(string directory, IClock clock, long maxBytes = 5_242_880)`. Disabled telemetry is handled by `NullSessionTelemetryRecorder`.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class RotatingJsonlTelemetrySinkTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "cc-tel-" + Guid.NewGuid().ToString("N"));

    private static SessionTelemetryRecord Rec(string repo) =>
        new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), repo, null, "sid", "Decision", 1,
            10, 5, 0, 15.0, 90, 89, 88, 88);

    public void Dispose() { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }

    [Fact]
    public void Append_WritesOneJsonLineToTodaysZeroSequenceFile_CreatingTheDirectory()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);

        sink.Append(Rec("a"));

        string file = Path.Combine(dir, "sessions.2026-07-01.0000.jsonl");
        Assert.True(File.Exists(file));
        string[] lines = File.ReadAllLines(file);
        Assert.Single(lines);
        using JsonDocument doc = JsonDocument.Parse(lines[0]);
        Assert.Equal("a", doc.RootElement.GetProperty("repoName").GetString());
    }

    [Fact]
    public void Append_WhenActiveFileExceedsSizeCap_RollsToNextSequence_KeepingTheOld()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock, maxBytes: 1); // any record exceeds 1 byte

        sink.Append(Rec("first"));
        sink.Append(Rec("second"));

        string f0 = Path.Combine(dir, "sessions.2026-07-01.0000.jsonl");
        string f1 = Path.Combine(dir, "sessions.2026-07-01.0001.jsonl");
        Assert.True(File.Exists(f0));
        Assert.True(File.Exists(f1)); // rolled
        Assert.Contains("first", File.ReadAllText(f0));
        Assert.Contains("second", File.ReadAllText(f1));
    }

    [Fact]
    public void Append_OnANewDay_StartsAFreshZeroSequenceFile()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);
        sink.Append(Rec("day1"));

        clock.UtcNow = new DateTimeOffset(2026, 7, 2, 1, 0, 0, TimeSpan.Zero);
        sink.Append(Rec("day2"));

        Assert.True(File.Exists(Path.Combine(dir, "sessions.2026-07-01.0000.jsonl")));
        Assert.True(File.Exists(Path.Combine(dir, "sessions.2026-07-02.0000.jsonl")));
    }

}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter RotatingJsonlTelemetrySinkTests`
Expected: FAIL — sink types do not exist.

- [ ] **Step 3: Write minimal implementation** — `src/LoopRelay.CLI/SessionTelemetrySink.cs`:

```csharp
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace LoopRelay.Cli;

/// <summary>Appends telemetry rows. Implementations must never throw out of <see cref="Append"/> in a way
/// that could reach a turn — the recorder wraps calls, but keep sink work simple and self-contained.</summary>
internal interface ISessionTelemetrySink
{
    void Append(SessionTelemetryRecord record);
}

/// <summary>
/// Per-day / size-hybrid rotating JSONL sink. A new file begins each UTC calendar day; within a day the
/// active file rolls to the next 4-digit sequence once it crosses <c>maxBytes</c>. Files are NEVER deleted
/// (a separate visualizer manages pruning). One compact JSON object per line.
/// </summary>
internal sealed class RotatingJsonlTelemetrySink : ISessionTelemetrySink
{
    private const long DefaultMaxBytes = 5_242_880; // 5 MiB

    private readonly string directory;
    private readonly IClock clock;
    private readonly long maxBytes;
    private readonly object gate = new();

    public RotatingJsonlTelemetrySink(string directory, IClock clock, long maxBytes = DefaultMaxBytes)
    {
        this.directory = directory;
        this.clock = clock;
        this.maxBytes = maxBytes;
    }

    public void Append(SessionTelemetryRecord record)
    {
        string line = JsonSerializer.Serialize(record, SessionTelemetryJson.Options);
        lock (gate)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(ResolveActiveFile(), line + "\n");
        }
    }

    private string ResolveActiveFile()
    {
        string date = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (int sequence = 0; ; sequence++)
        {
            string candidate = Path.Combine(directory, $"sessions.{date}.{sequence:D4}.jsonl");
            var info = new FileInfo(candidate);
            if (!info.Exists || info.Length < maxBytes)
            {
                return candidate;
            }
        }
    }
}

// Disabled telemetry is handled at the recorder layer by NullSessionTelemetryRecorder.
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter RotatingJsonlTelemetrySinkTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/SessionTelemetrySink.cs tests/LoopRelay.CLI.Tests/RotatingJsonlTelemetrySinkTests.cs
git commit -m "feat(cli): per-day/size rotating JSONL telemetry sink"
```

---

### Task 4: `FileSystemCodexRolloutLocator`

**Files:**
- Create: `src/LoopRelay.CLI/CodexRolloutLocator.cs`
- Test: `tests/LoopRelay.CLI.Tests/FileSystemCodexRolloutLocatorTests.cs`

**Interfaces:**
- Produces: `ICodexRolloutLocator { string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc); }`, `FileSystemCodexRolloutLocator(string sessionsRoot)`, static `FileSystemCodexRolloutLocator.ResolveDefaultSessionsRoot()`.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.IO;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class FileSystemCodexRolloutLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-codex-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    // Writes a rollout file whose first line is a codex session_meta record.
    private string WriteRollout(string name, string cwd, DateTimeOffset started)
    {
        string day = Path.Combine(root, "2026", "07", "01");
        Directory.CreateDirectory(day);
        string file = Path.Combine(day, name);
        string meta =
            "{\"timestamp\":\"" + started.UtcDateTime.ToString("o") + "\",\"type\":\"session_meta\"," +
            "\"payload\":{\"session_id\":\"" + Guid.NewGuid().ToString() + "\",\"cwd\":" +
            System.Text.Json.JsonSerializer.Serialize(cwd) + ",\"timestamp\":\"" + started.UtcDateTime.ToString("o") + "\"}}";
        File.WriteAllText(file, meta + "\n{\"type\":\"event\"}\n");
        return file;
    }

    [Fact]
    public void Resolve_ReturnsRolloutWhoseCwdMatchesAndStartedAfterOpen()
    {
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-a.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        string? found = new FileSystemCodexRolloutLocator(root)
            .Resolve(cwd, new DateTimeOffset(2026, 7, 1, 9, 59, 0, TimeSpan.Zero));

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_IgnoresRolloutsForADifferentCwd()
    {
        WriteRollout("rollout-a.jsonl", Path.Combine(root, "other"), new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        string? found = new FileSystemCodexRolloutLocator(root)
            .Resolve(Path.Combine(root, "work"), new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Null(found);
    }

    [Fact]
    public void Resolve_WhenMultipleMatch_ReturnsTheNewest()
    {
        string cwd = Path.Combine(root, "work");
        WriteRollout("rollout-old.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));
        string newer = WriteRollout("rollout-new.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero));

        string? found = new FileSystemCodexRolloutLocator(root)
            .Resolve(cwd, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(newer, found);
    }

    [Fact]
    public void Resolve_WhenRootMissing_ReturnsNull()
    {
        string? found = new FileSystemCodexRolloutLocator(Path.Combine(root, "nope"))
            .Resolve(root, DateTimeOffset.MinValue);
        Assert.Null(found);
    }

    [Fact]
    public void Resolve_SkipsFilesWithMalformedFirstLine()
    {
        string day = Path.Combine(root, "2026", "07", "01");
        Directory.CreateDirectory(day);
        File.WriteAllText(Path.Combine(day, "rollout-bad.jsonl"), "not json\n");

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(root, DateTimeOffset.MinValue);
        Assert.Null(found);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter FileSystemCodexRolloutLocatorTests`
Expected: FAIL — locator types do not exist.

- [ ] **Step 3: Write minimal implementation** — `src/LoopRelay.CLI/CodexRolloutLocator.cs`:

```csharp
using System;
using System.IO;
using System.Text.Json;

namespace LoopRelay.Cli;

/// <summary>Resolves the on-disk codex rollout JSONL for a session's process, or null when it cannot.</summary>
internal interface ICodexRolloutLocator
{
    /// <param name="workingDirectory">The session's cwd (matched against the rollout's session_meta.cwd).</param>
    /// <param name="openedAtUtc">When the session opened; the rollout must have started at/after this.</param>
    string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc);
}

/// <summary>
/// Finds codex's own rollout log under <c>~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl</c>. Codex never tells
/// LoopRelay its session id, so we match on the rollout's first-line <c>session_meta</c> (cwd == the session's
/// working directory) and pick the newest whose start timestamp is at/after the session opened. Fails to null —
/// the log row is still valuable without the path.
/// </summary>
internal sealed class FileSystemCodexRolloutLocator : ICodexRolloutLocator
{
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(1);

    private readonly string sessionsRoot;

    public FileSystemCodexRolloutLocator(string sessionsRoot) => this.sessionsRoot = sessionsRoot;

    /// <summary><c>%CODEX_HOME%/sessions</c>, else <c>%USERPROFILE%/.codex/sessions</c>.</summary>
    public static string ResolveDefaultSessionsRoot()
    {
        string? home = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        return Path.Combine(home, "sessions");
    }

    public string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)
    {
        try
        {
            if (!Directory.Exists(sessionsRoot))
            {
                return null;
            }

            string target = Normalize(workingDirectory);
            string? best = null;
            DateTimeOffset bestStart = DateTimeOffset.MinValue;

            foreach (string file in Directory.EnumerateFiles(sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                if (!TryReadMeta(file, out string cwd, out DateTimeOffset started))
                {
                    continue;
                }

                if (!string.Equals(Normalize(cwd), target, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (started < openedAtUtc - StartTolerance)
                {
                    continue;
                }

                if (best is null || started > bestStart)
                {
                    best = file;
                    bestStart = started;
                }
            }

            return best;
        }
        catch
        {
            return null; // never break a turn over a telemetry lookup
        }
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }

    private static bool TryReadMeta(string file, out string cwd, out DateTimeOffset started)
    {
        cwd = string.Empty;
        started = default;
        try
        {
            using var reader = new StreamReader(file);
            string? first = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(first))
            {
                return false;
            }

            using JsonDocument doc = JsonDocument.Parse(first);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out JsonElement payload)
                || payload.ValueKind != JsonValueKind.Object
                || !payload.TryGetProperty("cwd", out JsonElement cwdEl)
                || cwdEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            cwd = cwdEl.GetString() ?? string.Empty;
            if (cwd.Length == 0)
            {
                return false;
            }

            started = ReadTimestamp(root, payload) ?? File.GetCreationTimeUtc(file);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement root, JsonElement payload)
    {
        foreach (JsonElement holder in new[] { root, payload })
        {
            if (holder.TryGetProperty("timestamp", out JsonElement ts)
                && ts.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(ts.GetString(), out DateTimeOffset parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter FileSystemCodexRolloutLocatorTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/CodexRolloutLocator.cs tests/LoopRelay.CLI.Tests/FileSystemCodexRolloutLocatorTests.cs
git commit -m "feat(cli): locate codex rollout jsonl by cwd + open time"
```

---

### Task 5: Widen `IUsageGate` to return the capacity snapshot (re-probe after a wait)

**Files:**
- Modify: `src/LoopRelay.CLI/UsageGate.cs`
- Modify: `src/LoopRelay.CLI/GatedAgentRuntime.cs` (callers must compile against the new return type)
- Modify: `tests/LoopRelay.CLI.Tests/GatedAgentRuntimeTests.cs` (local `RecordingGate` return type)
- Test: `tests/LoopRelay.CLI.Tests/UsageGateTests.cs` (add one test)

**Interfaces:**
- Changes: `IUsageGate.WaitForCapacityAsync(CancellationToken) : Task<CodexUsageStatus?>` (was `Task`). Returns the capacity the turn will start with (re-probed after any reset-wait); null when unreadable.

- [ ] **Step 1: Write the failing test** — append to `UsageGateTests.cs`:

```csharp
    [Fact]
    public async Task WaitForCapacity_WhenItHadToWait_ReturnsAFreshlyReprobedSnapshot()
    {
        var t = New();
        // First probe: 5h exhausted (forces a wait). Second probe (after the reset delay): full again.
        t.Probe.Results.Enqueue(new CodexUsageStatus(0, TimeSpan.FromMinutes(30), 60, TimeSpan.FromHours(2)));
        t.Probe.Results.Enqueue(new CodexUsageStatus(100, TimeSpan.FromHours(5), 100, TimeSpan.FromHours(9)));

        CodexUsageStatus? returned = await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Equal(2, t.Probe.Calls);                         // re-probed after the wait
        Assert.Equal(100, returned!.FiveHourRemainingPercent);  // returned the post-reset snapshot
    }

    [Fact]
    public async Task WaitForCapacity_WhenNoWait_ReturnsTheProbedSnapshotWithoutReprobing()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(50, TimeSpan.FromHours(1), 60, TimeSpan.FromHours(2));

        CodexUsageStatus? returned = await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Equal(1, t.Probe.Calls);
        Assert.Equal(50, returned!.FiveHourRemainingPercent);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter UsageGateTests`
Expected: FAIL to compile — `WaitForCapacityAsync` returns `Task`, not `Task<CodexUsageStatus?>`.

- [ ] **Step 3a: Change the interface + implementation** in `UsageGate.cs`.

Replace the interface:

```csharp
/// <summary>Blocks until Codex has capacity to run a turn (or fails open), returning the capacity snapshot the
/// turn will start with (null when unreadable). See <see cref="UsageGate"/>.</summary>
internal interface IUsageGate
{
    Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken);
}
```

Replace the `WaitForCapacityAsync` method body:

```csharp
    public async Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CodexUsageStatus? status = await probe.QueryAsync(cancellationToken);
        if (status is null)
        {
            console.Warn("Codex usage could not be read — proceeding without the usage gate.");
            return null;
        }

        TimeSpan wait = TimeSpan.Zero;

        if (status.FiveHourRemainingPercent <= ExhaustionWatermarkPercent)
        {
            console.Warn($"Codex 5h limit spent ({status.FiveHourRemainingPercent}% left, <= {ExhaustionWatermarkPercent}% watermark) — resets in {Format(status.FiveHourTimeUntilReset)}.");
            wait = Longer(wait, status.FiveHourTimeUntilReset);
        }

        if (status.WeeklyRemainingPercent <= ExhaustionWatermarkPercent)
        {
            console.Warn($"Codex weekly limit spent ({status.WeeklyRemainingPercent}% left, <= {ExhaustionWatermarkPercent}% watermark) — resets in {Format(status.WeeklyTimeUntilReset)}.");
            wait = Longer(wait, status.WeeklyTimeUntilReset);
        }

        if (wait > TimeSpan.Zero)
        {
            console.Warn($"Waiting {Format(wait)} for Codex usage to reset before continuing.");
            await delay.DelayAsync(wait, cancellationToken);

            // After the reset the pre-wait reading is stale; re-probe so the returned snapshot reflects the
            // capacity the turn actually starts with. Keep the pre-wait value if the re-probe is unreadable.
            status = await probe.QueryAsync(cancellationToken) ?? status;
        }

        return status;
    }
```

- [ ] **Step 3b: Update callers** in `GatedAgentRuntime.cs` so it compiles (behavior unchanged for now — capture and ignore). In `RunOneShotAsync` change `await usageGate.WaitForCapacityAsync(cancellationToken);` to:

```csharp
        _ = await usageGate.WaitForCapacityAsync(cancellationToken);
```

and in `GatedAgentSession.RunTurnAsync` change `await usageGate.WaitForCapacityAsync(cancellationToken);` to:

```csharp
        _ = await usageGate.WaitForCapacityAsync(cancellationToken);
```

(These are rewritten fully in Task 7; this keeps Task 5 green in isolation.)

- [ ] **Step 3c: Update the local fake** in `GatedAgentRuntimeTests.cs` — replace the `RecordingGate` class:

```csharp
    private sealed class RecordingGate(List<string> log) : IUsageGate
    {
        public int Calls { get; private set; }
        public Exception? Throw { get; set; }
        public CodexUsageStatus? Status { get; set; }

        public Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken)
        {
            Calls++;
            log.Add("gate");
            return Throw is not null ? throw Throw : Task.FromResult(Status);
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter "UsageGateTests|GatedAgentRuntimeTests"`
Expected: PASS — all existing UsageGate/GatedAgentRuntime tests plus the 2 new ones. (The existing `WaitForCapacity_QueriesTheProbeExactlyOnce` stays green: it uses a no-wait scenario.)

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/UsageGate.cs src/LoopRelay.CLI/GatedAgentRuntime.cs tests/LoopRelay.CLI.Tests/UsageGateTests.cs tests/LoopRelay.CLI.Tests/GatedAgentRuntimeTests.cs
git commit -m "feat(cli): usage gate returns turn-start capacity snapshot"
```

---

### Task 6: `SessionTelemetryRecorder` (post-probe + resolve + effective + append, fail-open)

**Files:**
- Create: `src/LoopRelay.CLI/SessionTelemetryRecorder.cs`
- Modify: `tests/LoopRelay.CLI.Tests/TestDoubles.cs` (add `FakeSessionTelemetrySink`, `FakeCodexRolloutLocator`)
- Test: `tests/LoopRelay.CLI.Tests/SessionTelemetryRecorderTests.cs`

**Interfaces:**
- Consumes: `ICodexUsageProbe` (post-probe), `ICodexRolloutLocator`, `ISessionTelemetrySink`, `IDecisionCostModel` (`Measure`), `IClock`, `ILoopConsole`; `AgentTurnResult`/`AgentTokenUsage`/`SessionIdentity`/`SessionRole` from `LoopRelay.Agents.Models`; `CodexUsageStatus`.
- Produces: `ISessionTelemetryRecorder { Task<string?> RecordTurnAsync(string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role, DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result, CodexUsageStatus? preStatus, CancellationToken cancellationToken); }`, `SessionTelemetryRecorder(...)`, `NullSessionTelemetryRecorder`.

- [ ] **Step 1: Add test doubles** — append to `TestDoubles.cs`:

```csharp
internal sealed class FakeSessionTelemetrySink : ISessionTelemetrySink
{
    public List<SessionTelemetryRecord> Records { get; } = new();
    public bool Throw { get; set; }

    public void Append(SessionTelemetryRecord record)
    {
        if (Throw) throw new IOException("disk full");
        Records.Add(record);
    }
}

internal sealed class FakeCodexRolloutLocator : ICodexRolloutLocator
{
    public string? Path { get; set; }
    public int Calls { get; private set; }

    public string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)
    {
        Calls++;
        return Path;
    }
}
```

- [ ] **Step 2: Write the failing test** — `SessionTelemetryRecorderTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using LoopRelay.Agents.Models;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class SessionTelemetryRecorderTests
{
    private sealed record Kit(
        SessionTelemetryRecorder Recorder, FakeCodexUsageProbe Probe, FakeCodexRolloutLocator Locator,
        FakeSessionTelemetrySink Sink, StubCostModel Cost, RecordingLoopConsole Con);

    private static Kit New()
    {
        var probe = new FakeCodexUsageProbe();
        var locator = new FakeCodexRolloutLocator();
        var sink = new FakeSessionTelemetrySink();
        var cost = new StubCostModel { MeasureValue = 42.0 };
        var con = new RecordingLoopConsole();
        var clock = new FakeClock();
        var recorder = new SessionTelemetryRecorder(probe, locator, sink, cost, clock, con);
        return new Kit(recorder, probe, locator, sink, cost, con);
    }

    private static AgentTurnResult Turn(int index = 3) =>
        new(index, AgentTurnState.Completed, "out", new AgentTokenUsage(100, 20, 30));

    [Fact]
    public async Task RecordTurn_BuildsAFullRecordFromGatePreProbePostAndTurn()
    {
        var k = New();
        var pre = new CodexUsageStatus(60, TimeSpan.FromHours(1), 80, TimeSpan.FromHours(5));
        k.Probe.Default = new CodexUsageStatus(58, TimeSpan.FromHours(1), 79, TimeSpan.FromHours(5)); // post
        k.Locator.Path = "/logs/rollout.jsonl";

        string? path = await k.Recorder.RecordTurnAsync(
            "myrepo", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, cachedLogPath: null, Turn(index: 3), pre, CancellationToken.None);

        Assert.Equal("/logs/rollout.jsonl", path);
        SessionTelemetryRecord r = Assert.Single(k.Sink.Records);
        Assert.Equal("myrepo", r.RepoName);
        Assert.Equal("/logs/rollout.jsonl", r.CodexLogPath);
        Assert.Equal("Decision", r.SessionType);
        Assert.Equal(3, r.TurnIndex);
        Assert.Equal(100, r.PromptTokens);
        Assert.Equal(20, r.OutputTokens);
        Assert.Equal(30, r.CachedTokens);
        Assert.Equal(42.0, r.EffectiveTokens);
        Assert.Equal(60, r.PreFiveHourPercent);
        Assert.Equal(58, r.PostFiveHourPercent);
        Assert.Equal(80, r.PreWeeklyPercent);
        Assert.Equal(79, r.PostWeeklyPercent);
    }

    [Fact]
    public async Task RecordTurn_WhenPathAlreadyCached_ReusesItWithoutCallingTheLocator()
    {
        var k = New();
        k.Probe.Default = new CodexUsageStatus(50, TimeSpan.Zero, 50, TimeSpan.Zero);

        string? path = await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.OperationalExecution,
            DateTimeOffset.UnixEpoch, cachedLogPath: "/cached.jsonl", Turn(), null, CancellationToken.None);

        Assert.Equal("/cached.jsonl", path);
        Assert.Equal(0, k.Locator.Calls);
        Assert.Equal("/cached.jsonl", Assert.Single(k.Sink.Records).CodexLogPath);
    }

    [Fact]
    public async Task RecordTurn_WhenPreAndPostUnavailable_WritesNullCapacities()
    {
        var k = New();
        k.Probe.Default = null; // post-probe unreadable

        await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, null, Turn(), preStatus: null, CancellationToken.None);

        SessionTelemetryRecord r = Assert.Single(k.Sink.Records);
        Assert.Null(r.PreFiveHourPercent);
        Assert.Null(r.PostFiveHourPercent);
        Assert.Null(r.PreWeeklyPercent);
        Assert.Null(r.PostWeeklyPercent);
    }

    [Fact]
    public async Task RecordTurn_WhenSinkThrows_WarnsAndDoesNotThrow()
    {
        var k = New();
        k.Sink.Throw = true;
        k.Probe.Default = new CodexUsageStatus(50, TimeSpan.Zero, 50, TimeSpan.Zero);

        string? path = await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, "/cached.jsonl", Turn(), null, CancellationToken.None);

        Assert.Equal("/cached.jsonl", path); // still returns the path
        Assert.Contains(k.Con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task NullRecorder_ReturnsCachedPathAndRecordsNothing()
    {
        var sink = new FakeSessionTelemetrySink();
        string? path = await new NullSessionTelemetryRecorder().RecordTurnAsync(
            "r", "/w", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, "/cached", Turn(), null, CancellationToken.None);

        Assert.Equal("/cached", path);
        Assert.Empty(sink.Records);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter SessionTelemetryRecorderTests`
Expected: FAIL — recorder types do not exist.

- [ ] **Step 4: Write minimal implementation** — `src/LoopRelay.CLI/SessionTelemetryRecorder.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using LoopRelay.Agents.Models;
using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli;

/// <summary>Records one telemetry row per codex turn. Returns the resolved codex rollout path (cached across a
/// session's turns), or null. MUST NOT throw — telemetry never breaks a turn.</summary>
internal interface ISessionTelemetryRecorder
{
    Task<string?> RecordTurnAsync(
        string repoName,
        string workingDirectory,
        SessionIdentity sessionId,
        SessionRole role,
        DateTimeOffset openedAtUtc,
        string? cachedLogPath,
        AgentTurnResult result,
        CodexUsageStatus? preStatus,
        CancellationToken cancellationToken);
}

/// <summary>
/// Adds one post-turn capacity probe, resolves the codex rollout file once per session, computes effective
/// tokens with the router's cost model, and appends a <see cref="SessionTelemetryRecord"/>. Every step is
/// best-effort: a failure warns and is swallowed so the turn result always flows through.
/// </summary>
internal sealed class SessionTelemetryRecorder(
    ICodexUsageProbe probe,
    ICodexRolloutLocator locator,
    ISessionTelemetrySink sink,
    IDecisionCostModel costModel,
    IClock clock,
    ILoopConsole console) : ISessionTelemetryRecorder
{
    public async Task<string?> RecordTurnAsync(
        string repoName,
        string workingDirectory,
        SessionIdentity sessionId,
        SessionRole role,
        DateTimeOffset openedAtUtc,
        string? cachedLogPath,
        AgentTurnResult result,
        CodexUsageStatus? preStatus,
        CancellationToken cancellationToken)
    {
        string? path = cachedLogPath;
        try
        {
            CodexUsageStatus? post = await ProbePostAsync(cancellationToken);
            path ??= locator.Resolve(workingDirectory, openedAtUtc);

            AgentTokenUsage usage = result.Usage;
            var record = new SessionTelemetryRecord(
                clock.UtcNow,
                repoName,
                path,
                sessionId.Value.ToString(),
                role.ToString(),
                result.TurnIndex,
                usage.PromptTokens,
                usage.OutputTokens,
                usage.CachedInputTokens,
                costModel.Measure(usage),
                preStatus?.FiveHourRemainingPercent,
                post?.FiveHourRemainingPercent,
                preStatus?.WeeklyRemainingPercent,
                post?.WeeklyRemainingPercent);

            sink.Append(record);
        }
        catch (Exception ex)
        {
            console.Warn($"Session telemetry not recorded: {ex.Message}");
        }

        return path;
    }

    // The post probe is best-effort: the token row is worth keeping even when capacity is unknown.
    private async Task<CodexUsageStatus?> ProbePostAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await probe.QueryAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>No-op recorder used when session telemetry is disabled (also skips the post-probe cost).</summary>
internal sealed class NullSessionTelemetryRecorder : ISessionTelemetryRecorder
{
    public Task<string?> RecordTurnAsync(
        string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role,
        DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result,
        CodexUsageStatus? preStatus, CancellationToken cancellationToken) =>
        Task.FromResult(cachedLogPath);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter SessionTelemetryRecorderTests`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/LoopRelay.CLI/SessionTelemetryRecorder.cs tests/LoopRelay.CLI.Tests/SessionTelemetryRecorderTests.cs tests/LoopRelay.CLI.Tests/TestDoubles.cs
git commit -m "feat(cli): SessionTelemetryRecorder builds+appends per-turn rows (fail-open)"
```

---

### Task 7: Wire the recorder into `GatedAgentRuntime` / `GatedAgentSession`

**Files:**
- Modify: `src/LoopRelay.CLI/GatedAgentRuntime.cs`
- Modify: `tests/LoopRelay.CLI.Tests/GatedAgentRuntimeTests.cs`

**Interfaces:**
- Changes: `GatedAgentRuntime(IAgentRuntime inner, IUsageGate usageGate, ISessionTelemetryRecorder recorder, IClock clock, string repoName)`. Each turn/one-shot: capture the gate's pre-status, run the turn, then `recorder.RecordTurnAsync(...)`; cache the rollout path per session.

- [ ] **Step 1: Write the failing test** — update `GatedAgentRuntimeTests.cs`.

Update the `Fixture` record and `New()` to supply the new ctor args and expose a `RecordingSessionTelemetryRecorder`:

```csharp
    private sealed record Fixture(
        GatedAgentRuntime Runtime, RecordingRuntime Inner, RecordingGate Gate,
        RecordingSessionTelemetryRecorder Recorder, List<string> Log);

    private static Fixture New()
    {
        var log = new List<string>();
        var inner = new RecordingRuntime(log);
        var gate = new RecordingGate(log);
        var recorder = new RecordingSessionTelemetryRecorder();
        var runtime = new GatedAgentRuntime(inner, gate, recorder, new FakeClock(), "myrepo");
        return new Fixture(runtime, inner, gate, recorder, log);
    }
```

Add a local recording recorder at the bottom of the class (near the other fakes):

```csharp
    private sealed class RecordingSessionTelemetryRecorder : ISessionTelemetryRecorder
    {
        public List<(SessionRole Role, int TurnIndex, string? CachedLogPath, LoopRelay.Cli.CodexUsageStatus? Pre)> Calls { get; } = new();
        public string? PathToReturn { get; set; } = "/log";

        public Task<string?> RecordTurnAsync(
            string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role,
            DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result,
            CodexUsageStatus? preStatus, CancellationToken cancellationToken)
        {
            Calls.Add((role, result.TurnIndex, cachedLogPath, preStatus));
            return Task.FromResult(PathToReturn);
        }
    }
```

Add new `[Fact]`s:

```csharp
    [Fact]
    public async Task RunTurn_EmitsOneRecordPerTurnWithTheGatesPreStatus()
    {
        var f = New();
        f.Gate.Status = new CodexUsageStatus(70, TimeSpan.FromHours(1), 90, TimeSpan.FromHours(5));
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = new AgentTurnResult(1, AgentTurnState.Completed, "o", new AgentTokenUsage(1, 1));

        await session.RunTurnAsync("p1");

        var call = Assert.Single(f.Recorder.Calls);
        Assert.Equal(SessionRole.Decision, call.Role);
        Assert.Equal(1, call.TurnIndex);
        Assert.Null(call.CachedLogPath);                 // first turn: nothing cached yet
        Assert.Equal(70, call.Pre!.FiveHourRemainingPercent);
    }

    [Fact]
    public async Task RunTurn_SecondTurnReusesTheCachedLogPathFromTheFirst()
    {
        var f = New();
        f.Recorder.PathToReturn = "/rollout.jsonl";
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        await session.RunTurnAsync("p1");
        await session.RunTurnAsync("p2");

        Assert.Equal(2, f.Recorder.Calls.Count);
        Assert.Null(f.Recorder.Calls[0].CachedLogPath);
        Assert.Equal("/rollout.jsonl", f.Recorder.Calls[1].CachedLogPath);
    }

    [Fact]
    public async Task RunOneShot_EmitsOneRecord()
    {
        var f = New();

        await f.Runtime.RunOneShotAsync(Spec(), "prompt");

        Assert.Single(f.Recorder.Calls);
    }

    [Fact]
    public async Task RunTurn_WhenGateThrows_EmitsNoRecord()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Gate.Throw = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.RunTurnAsync("p"));

        Assert.Empty(f.Recorder.Calls);
    }
```

(Existing tests keep working; `AgentTurnState`/`AgentTokenUsage` are already imported via `LoopRelay.Agents.Models`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter GatedAgentRuntimeTests`
Expected: FAIL to compile — `GatedAgentRuntime` ctor takes 2 args, not 5.

- [ ] **Step 3: Rewrite `GatedAgentRuntime.cs`**:

```csharp
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

/// <summary>
/// Wraps an <see cref="IAgentRuntime"/> so the Codex usage gate runs before EVERY codex turn/one-shot, and one
/// telemetry row is emitted after each. A single iteration invokes codex many times and the warm decision
/// session is reused across iterations, so quota can drain to zero mid-iteration; gating at each turn is the
/// finest boundary we control. Opening a session spawns the app-server but spends no quota, so it is NOT gated
/// or recorded — only turns and one-shots are.
/// </summary>
internal sealed class GatedAgentRuntime(
    IAgentRuntime inner,
    IUsageGate usageGate,
    ISessionTelemetryRecorder recorder,
    IClock clock,
    string repoName) : IAgentRuntime
{
    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        IAgentSession session = await inner.OpenSessionAsync(spec, cancellationToken);
        return new GatedAgentSession(session, usageGate, recorder, repoName, spec.WorkingDirectory, clock.UtcNow);
    }

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset openedAt = clock.UtcNow;
        CodexUsageStatus? pre = await usageGate.WaitForCapacityAsync(cancellationToken);
        AgentTurnResult result = await inner.RunOneShotAsync(spec, prompt, onChunk, cancellationToken);
        await recorder.RecordTurnAsync(
            repoName, spec.WorkingDirectory, spec.SessionId, spec.Role, openedAt,
            cachedLogPath: null, result, pre, cancellationToken);
        return result;
    }

    public ValueTask CloseSessionAsync(IAgentSession session) =>
        inner.CloseSessionAsync(session is GatedAgentSession gated ? gated.Inner : session);
}

/// <summary>
/// A session wrapper that runs the usage gate before each turn, delegates to <see cref="Inner"/>, then records
/// one telemetry row. The codex rollout log path is resolved once (lazily, after the first turn) and cached for
/// the session's remaining turns.
/// </summary>
internal sealed class GatedAgentSession(
    IAgentSession inner,
    IUsageGate usageGate,
    ISessionTelemetryRecorder recorder,
    string repoName,
    string workingDirectory,
    DateTimeOffset openedAtUtc) : IAgentSession
{
    private string? cachedLogPath;

    internal IAgentSession Inner => inner;

    public SessionIdentity SessionId => inner.SessionId;
    public string RepositoryId => inner.RepositoryId;
    public SessionRole Role => inner.Role;
    public AgentSessionMode Mode => inner.Mode;
    public AgentProcessState State => inner.State;
    public int CompletedTurns => inner.CompletedTurns;
    public AgentTokenUsage TotalUsage => inner.TotalUsage;

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        CodexUsageStatus? pre = await usageGate.WaitForCapacityAsync(cancellationToken);
        AgentTurnResult result = await inner.RunTurnAsync(prompt, onChunk, cancellationToken);
        cachedLogPath = await recorder.RecordTurnAsync(
            repoName, workingDirectory, inner.SessionId, inner.Role, openedAtUtc,
            cachedLogPath, result, pre, cancellationToken);
        return result;
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) => inner.CancelAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter GatedAgentRuntimeTests`
Expected: PASS — existing tests + 4 new emission tests.

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/GatedAgentRuntime.cs tests/LoopRelay.CLI.Tests/GatedAgentRuntimeTests.cs
git commit -m "feat(cli): emit a telemetry row per codex turn at the gate seam"
```

---

### Task 8: Compose in `Program.cs` + `.gitignore`

**Files:**
- Create: `src/LoopRelay.CLI/SessionTelemetryComposition.cs`
- Modify: `src/LoopRelay.CLI/Program.cs`
- Modify: `.gitignore` (repo root)
- Test: `tests/LoopRelay.CLI.Tests/SessionTelemetryCompositionTests.cs`

**Interfaces:**
- Consumes: everything above; `Repository` (`LoopRelay.Core.Repositories`), `ICodexUsageProbe`, `IDecisionCostModel`.
- Produces: `SessionTelemetryComposition.CreateRecorder(Repository repository, bool enabled, ICodexUsageProbe probe, IDecisionCostModel costModel, IClock clock, ILoopConsole console) : ISessionTelemetryRecorder` and `SessionTelemetryComposition.RepoName(Repository)` and `SessionTelemetryComposition.IsEnabled()`.

- [ ] **Step 1: Write the failing test** — `SessionTelemetryCompositionTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LoopRelay.Agents.Models;
using LoopRelay.Cli;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Services;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class SessionTelemetryCompositionTests : IDisposable
{
    private readonly string repoPath = Path.Combine(Path.GetTempPath(), "cc-repo-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(repoPath)) Directory.Delete(repoPath, recursive: true); }

    private Repository Repo(string name = "AxiomRepo") =>
        new() { Id = Guid.NewGuid(), Name = name, Path = repoPath };

    private static AgentTurnResult Turn() =>
        new(1, AgentTurnState.Completed, "o", new AgentTokenUsage(10, 2, 0));

    [Fact]
    public async Task CreateRecorder_WhenEnabled_WritesUnderRepoLoopRelayTelemetry()
    {
        ISessionTelemetryRecorder recorder = SessionTelemetryComposition.CreateRecorder(
            Repo(), enabled: true, new FakeCodexUsageProbe(), new EffectiveTokenCostModel(),
            new FakeClock(), new RecordingLoopConsole());

        await recorder.RecordTurnAsync("AxiomRepo", repoPath, new SessionIdentity(Guid.NewGuid()),
            SessionRole.Decision, DateTimeOffset.UnixEpoch, null, Turn(), null, CancellationToken.None);

        string dir = Path.Combine(repoPath, ".LoopRelay", "telemetry");
        Assert.True(Directory.Exists(dir));
        Assert.NotEmpty(Directory.EnumerateFiles(dir, "sessions.*.jsonl"));
    }

    [Fact]
    public async Task CreateRecorder_WhenDisabled_IsANullRecorderThatWritesNothing()
    {
        ISessionTelemetryRecorder recorder = SessionTelemetryComposition.CreateRecorder(
            Repo(), enabled: false, new FakeCodexUsageProbe(), new EffectiveTokenCostModel(),
            new FakeClock(), new RecordingLoopConsole());

        await recorder.RecordTurnAsync("AxiomRepo", repoPath, new SessionIdentity(Guid.NewGuid()),
            SessionRole.Decision, DateTimeOffset.UnixEpoch, null, Turn(), null, CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(repoPath, ".LoopRelay")));
        Assert.IsType<NullSessionTelemetryRecorder>(recorder);
    }

    [Fact]
    public void RepoName_FallsBackToTheFolderNameWhenRepositoryNameIsBlank()
    {
        Assert.Equal("AxiomRepo", SessionTelemetryComposition.RepoName(Repo("AxiomRepo")));
        string folder = Path.GetFileName(repoPath);
        Assert.Equal(folder, SessionTelemetryComposition.RepoName(new Repository { Id = Guid.NewGuid(), Name = "", Path = repoPath }));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter SessionTelemetryCompositionTests`
Expected: FAIL — `SessionTelemetryComposition` does not exist.

- [ ] **Step 3: Write the factory** — `src/LoopRelay.CLI/SessionTelemetryComposition.cs`:

```csharp
using System;
using System.IO;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli;

/// <summary>
/// Builds the per-turn telemetry recorder for the CLI loop. Enabled by default; set
/// <c>LoopRelay_SESSION_LOG=0</c> (or <c>false</c>) to disable (swaps in the no-op recorder, which also
/// skips the extra post-turn probe). The log lives under <c>&lt;repo&gt;/.LoopRelay/telemetry/</c>.
/// </summary>
internal static class SessionTelemetryComposition
{
    public static bool IsEnabled()
    {
        string? flag = Environment.GetEnvironmentVariable("LoopRelay_SESSION_LOG");
        return !(string.Equals(flag, "0", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase));
    }

    public static string RepoName(Repository repository) =>
        !string.IsNullOrWhiteSpace(repository.Name)
            ? repository.Name
            : Path.GetFileName(repository.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static ISessionTelemetryRecorder CreateRecorder(
        Repository repository,
        bool enabled,
        ICodexUsageProbe probe,
        IDecisionCostModel costModel,
        IClock clock,
        ILoopConsole console)
    {
        if (!enabled)
        {
            return new NullSessionTelemetryRecorder();
        }

        string directory = Path.Combine(repository.Path, ".LoopRelay", "telemetry");
        var sink = new RotatingJsonlTelemetrySink(directory, clock);
        var locator = new FileSystemCodexRolloutLocator(FileSystemCodexRolloutLocator.ResolveDefaultSessionsRoot());
        return new SessionTelemetryRecorder(probe, locator, sink, costModel, clock, console);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug --filter SessionTelemetryCompositionTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Wire `Program.cs`.** After the `usageGate` line (currently `var usageGate = new UsageGate(...)`) and before `var gatedRuntime = ...`, insert:

```csharp
var telemetryClock = new SystemClock();
var telemetryRecorder = SessionTelemetryComposition.CreateRecorder(
    repository, SessionTelemetryComposition.IsEnabled(),
    usageProbe, new EffectiveTokenCostModel(), telemetryClock, console);
```

Change the `gatedRuntime` construction to:

```csharp
var gatedRuntime = new GatedAgentRuntime(
    runtime, usageGate, telemetryRecorder, telemetryClock, SessionTelemetryComposition.RepoName(repository));
```

`EffectiveTokenCostModel` is in `LoopRelay.Orchestration.Services`, already imported at the top of `Program.cs` (line 9). No new using needed.

- [ ] **Step 6: Add to `.gitignore`.** Append to the repo-root `.gitignore`:

```gitignore

# CLI per-turn session telemetry log (local only; a visualizer manages pruning)
.LoopRelay/
```

- [ ] **Step 7: Build the whole solution + run the full CLI test suite**

Run: `dotnet build LoopRelay.sln -c Debug`
Expected: Build succeeded, 0 errors.

Run: `dotnet test tests/LoopRelay.CLI.Tests/LoopRelay.CLI.Tests.csproj -c Debug`
Expected: PASS — all CLI tests (existing + new) green.

- [ ] **Step 8: Commit**

```bash
git add src/LoopRelay.CLI/SessionTelemetryComposition.cs src/LoopRelay.CLI/Program.cs tests/LoopRelay.CLI.Tests/SessionTelemetryCompositionTests.cs .gitignore
git commit -m "feat(cli): compose session telemetry recorder in Program + gitignore log"
```

---

## Self-Review

**Spec coverage:**
- 11 requested fields + timestamp + turnIndex + split raw tokens → Task 1 record; all 14 present. ✓
- Per-turn grain, both persistent turns and one-shots → Task 7 (`RunTurnAsync` + `RunOneShotAsync`). ✓
- Pre capacity from the gate (re-probe after wait) → Task 5. ✓
- Post capacity via one added probe → Task 6 (`ProbePostAsync`). ✓
- Effective tokens via `EffectiveTokenCostModel.Measure` → Task 6 + Task 8 (`new EffectiveTokenCostModel()`). ✓
- Codex rollout path (cwd + open-time, lazy + cached per session) → Task 4 + Task 7 caching. ✓
- Location repo-local `.LoopRelay/telemetry/`, git-ignored → Task 8. ✓
- Per-day/size-hybrid rotation, keep all → Task 3. ✓
- Fail-open everywhere → Tasks 4/6 try-catch; Task 7 relies on recorder never throwing. ✓
- Enable/disable default-on → Task 8 (`LoopRelay_SESSION_LOG`). ✓

**Placeholder scan:** none — every step carries full code.

**Type consistency:** `ISessionTelemetryRecorder.RecordTurnAsync` signature is identical across the interface (Task 6), `NullSessionTelemetryRecorder` (Task 6), the wrapper call sites (Task 7), and the test recorder (Task 7). `IUsageGate.WaitForCapacityAsync : Task<CodexUsageStatus?>` is consistent across UsageGate, both callers, and both fakes. `RotatingJsonlTelemetrySink(directory, clock, maxBytes)` matches its test usage. `SessionTelemetryRecord`'s 14 positional fields match the recorder's construction order and the serialization test. ✓

**Note for the implementer:** run each task's `--filter` first, then the full `dotnet test` at Task 8 to catch any interaction. If `dotnet build` reports an unreferenced `IUsageGate` implementation elsewhere, grep `WaitForCapacityAsync` and update its return type the same way (only `UsageGate` and the test `RecordingGate` are known today).
