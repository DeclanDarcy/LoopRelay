# Testing Performance Improvement Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the test suite's wall clock from ~4–5 minutes to ~1.5–2 minutes and remove ~150–250 s of redundant aggregate work, while preserving every assurance the audit marked as retained.

**Architecture:** Execute the audit's ranked remediations ([test-audit/11-remediation-priorities.md](test-audit/11-remediation-priorities.md)) in four phases: (0) restore a green, parallel-safe baseline; (1) break the two serial monolith classes that *are* the critical path; (2) move immutable per-test setup (fresh-DB creation, canary seeding) to process lifetime; (3) trim redundant assertions and wall-clock dependence. One optional production-scoped phase (4) narrows the Deep verification tier per the repo's own M3 target. Every phase ends with a timed measurement against the recorded baseline.

**Tech Stack:** .NET SDK 10.0.301, xUnit 2.9.3 (VSTest), Microsoft.Data.Sqlite, PowerShell 7.

## Global Constraints

- Anchor evidence: the audit under `test-audit/` at revision `9285dc8c`. **Line numbers, per-family test lists, and call-site counts quoted there must be re-derived before editing — never trusted** (they drift, and agent-recorded counts have a known failure rate in this repo).
- No CI exists and none is added. All validation is local `dotnet test`. No new tracking artifacts, dashboards, or governance files — measurement outputs go to git-ignored `.tmp/test-perf/`.
- Test moves must be behavior-identical: total executed cases stay **1,535**; only outcome changes allowed are the canary repair (Task 1) and Skip reporting (Task 13).
- Timing has ±25–30% run-to-run variance: every before/after comparison uses the **median of 3 runs**, same machine, no other heavy processes.
- Do not change production durability semantics (WAL/pooling — deferred to ORCH-3; likely superseded by the event-sourced storage direction). Do not attempt the runner-vs-composition coverage consolidation (audit rank 9) in this plan — deferred until post-split data shows it still pays.
- Commit style per repo history: `test(<area>): …`, `fix(<area>): …`, `perf(<area>): …`.
- Measurement command used throughout ("**timed run**"):

```powershell
# from repo root; label = e.g. baseline, post-split
$label = "<label>"; New-Item -ItemType Directory -Force ".tmp/test-perf" | Out-Null
dotnet build LoopRelay.slnx -c Debug
1..3 | ForEach-Object {
  $t = Measure-Command { dotnet test LoopRelay.slnx -c Debug --no-build --logger trx --results-directory ".tmp/test-perf/trx-$label-$_" > ".tmp/test-perf/$label-$_.log" 2>&1 }
  "$label run $_ : $([math]::Round($t.TotalSeconds,1)) s" | Tee-Object -Append ".tmp/test-perf/summary.txt"
  Select-String -Path ".tmp/test-perf/$label-$_.log" -Pattern "Duration:" | Tee-Object -Append ".tmp/test-perf/summary.txt"
}
```

---

## Phase 0 — Green, parallel-safe baseline

### Task 1: Repair the red StatusCanary

The suite fails deterministically at HEAD: `StatusCanaryTests.CoverageLedgerIsProductionDerivedAndKeepsUncoveredSetVisible` — expected `catalog-obligation` entries for `EffectCoordinator/effect/EvalRoadmap/...` identities are missing from the derived coverage ledger. Every later before/after comparison needs a green baseline. Finding: [status-canary-red-at-head.md](test-audit/findings/status-canary-red-at-head.md).

**Files:**
- Read: `tests/LoopRelay.Certification.Tests/StatusCanaryTests.cs`
- Modify: whichever side drifted — the ledger/catalog derivation source in `src/LoopRelay.Certification/` **or** the test's expected-obligation list (determined in Step 2)

**Interfaces:**
- Produces: a green full suite — the baseline every other task's validation depends on.

- [ ] **Step 1: Reproduce in isolation**

Run: `dotnet test tests/LoopRelay.Certification.Tests --filter "FullyQualifiedName~CoverageLedgerIsProductionDerivedAndKeepsUncoveredSetVisible"`
Expected: FAIL with `Assert.Contains() Failure: Filter not matched in collection`.

- [ ] **Step 2: Locate the drift commit and decide which side is right**

Read the test to extract the exact expected identity it filters for. Then:

```powershell
# Which commits last touched the two sides?
git log --oneline -10 -- tests/LoopRelay.Certification.Tests/StatusCanaryTests.cs
git log --oneline -10 --all -S "persist-architectural-catalog" -- src/
```

Decision rule: if a commit deliberately retired/renamed the EvalRoadmap effect obligations (commit message says so), update the test's expectation to a currently-real obligation that still proves "uncovered entries stay visible". If no such commit exists, the derivation dropped real obligations — fix the derivation in `src/LoopRelay.Certification/` so they reappear. Do not weaken the assertion to "any obligation exists"; it must keep pinning a *specific* uncovered identity.

- [ ] **Step 3: Apply the fix and verify the canary still detects its failure class**

Run the isolated test: PASS. Then temporarily perturb (working tree only): remove one obligation from the derivation source, re-run, confirm FAIL, revert the perturbation.

- [ ] **Step 4: Full suite green**

Run: `dotnet test LoopRelay.slnx -c Debug`
Expected: `Failed: 0`, Total 1,535.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "fix(certification): realign coverage-ledger canary with current obligation catalog"
```

### Task 2: Serialize the env-mutating telemetry tests

`SessionTelemetryRecorderTests` mutates process-global env vars (`LOOPRELAY_CERTIFICATION_INVOCATION_ID`/`_ROLE`) inside the parallel Cli.Tests assembly. Task 3's splits raise concurrency, so this lands first. Finding: [env-var-mutation-in-parallel-assembly.md](test-audit/findings/env-var-mutation-in-parallel-assembly.md). Pattern precedent: `tests/LoopRelay.Agents.Tests/Models/ProcessEnvironmentCollection.cs`.

**Files:**
- Create: `tests/LoopRelay.Cli.Tests/Services/CliProcessEnvironmentCollection.cs`
- Modify: `tests/LoopRelay.Cli.Tests/Services/Telemetry/SessionTelemetryRecorderTests.cs` (class attribute only)

**Interfaces:**
- Produces: collection name `"CliProcessEnvironment"` for any future Cli.Tests class that mutates process-global state.

- [ ] **Step 1: Create the collection definition**

```csharp
namespace LoopRelay.Cli.Tests.Services;

/// <summary>
/// Tests in this collection mutate process-global state (environment variables),
/// so xUnit's <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/>
/// runs them exclusively, after parallel collections complete.
/// Mirrors the "ProcessEnvironment" collection in LoopRelay.Agents.Tests.
/// </summary>
[Xunit.CollectionDefinition("CliProcessEnvironment", DisableParallelization = true)]
public sealed class CliProcessEnvironmentCollection;
```

- [ ] **Step 2: Attach the test class**

Add above the `SessionTelemetryRecorderTests` class declaration:

```csharp
[Xunit.Collection("CliProcessEnvironment")]
```

- [ ] **Step 3: Verify**

Run: `dotnet test tests/LoopRelay.Cli.Tests --filter "FullyQualifiedName~SessionTelemetryRecorderTests"`
Expected: PASS, same case count as before the change.

- [ ] **Step 4: Commit**

```bash
git add tests/LoopRelay.Cli.Tests/Services/CliProcessEnvironmentCollection.cs tests/LoopRelay.Cli.Tests/Services/Telemetry/SessionTelemetryRecorderTests.cs
git commit -m "test(cli): run env-mutating telemetry recorder tests in an exclusive collection"
```

### Task 3: Record the baseline

- [ ] **Step 1: Timed run with `$label = "baseline"`** (Global Constraints block). Expected: 3 green runs; wall times in the 240–320 s band; `.tmp/test-perf/summary.txt` populated. No commit (git-ignored outputs only).

---

## Phase 1 — Wall clock: break the serial monoliths

### Task 4: Split `LoopRelayCompositionRootTests` into per-family classes

One class = one xUnit collection = strictly serial; this class's sum (241–314 s) *is* the suite's wall clock. Splitting into sibling classes deriving from a shared base preserves every test verbatim while letting collections run in parallel. Finding: [cli-monolith-classes-serialize-suite-critical-path.md](test-audit/findings/cli-monolith-classes-serialize-suite-critical-path.md).

**Files:**
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/CompositionRootTestBase.cs`
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/CompositionRootPlanWorkflowTests.cs`
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/CompositionRootExecuteWorkflowTests.cs`
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/CompositionRootEvalAndTraditionalWorkflowTests.cs`
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/CompositionRootTelemetryAndEvidenceTests.cs`
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/CompositionRootGuardsAndRecoveryTests.cs`
- Delete (at the end): `tests/LoopRelay.Cli.Tests/Services/Cli/LoopRelayCompositionRootTests.cs`

**Interfaces:**
- Produces: `abstract class CompositionRootTestBase : IDisposable` holding **all** shared fields, constructor setup, `Dispose`, and private helpers of the original class, `protected`-scoped. Derived classes contain only `[Fact]`/`[Theory]` methods, moved unchanged.

- [ ] **Step 1: Re-derive the actual test list and grouping**

```powershell
Select-String -Path tests/LoopRelay.Cli.Tests/Services/Cli/LoopRelayCompositionRootTests.cs -Pattern '\[(Fact|Theory)' -Context 0,2
```

Group the (currently ~44) methods by name prefix into the five buckets above: `Plan_*`; `Execute_*`; `EvalRoadmap_*`/`TraditionalRoadmap_*`/`Generate*`; telemetry/spine/ledger/causality (`*_records_*`, `*_evidence_*`, `Cancelled_*`, `Prompt_*`, `Run_command_*`); everything else (retirement guards, recovery markers, storage verdicts). Aim for balanced *duration*, not count — the audit's trx CSV ranks the slow ones: `Plan_workflow_transitions` (~47 s), `Execute_workflow_transitions` (~31 s), `EvalRoadmap_workflow_transitions` (~30 s) must land in three *different* classes. Record the grouping as a comment block at the top of the base class.

- [ ] **Step 2: Extract the base class**

Create `CompositionRootTestBase.cs`: copy the original class's fields, constructor, `Dispose`, and every non-test member; change accessibility `private` → `protected`; class becomes `public abstract class CompositionRootTestBase : IDisposable`. Constructor/Dispose stay per-test (xUnit constructs per test) — semantics unchanged.

- [ ] **Step 3: Create the five derived classes and move the tests verbatim**

Each file:

```csharp
namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class CompositionRootPlanWorkflowTests : CompositionRootTestBase
{
    // [Fact]/[Theory] methods moved here UNCHANGED, plus any helper used only by this family.
}
```

Then delete the original file.

- [ ] **Step 4: Verify count parity and green**

Run: `dotnet test tests/LoopRelay.Cli.Tests -c Debug`
Expected: PASS, **total case count for the assembly identical to before the split (438)**. If the count moved, a test was dropped in the move — diff the method lists.

- [ ] **Step 5: Stability under new parallelism**

Run the assembly 5 consecutive times. Expected: 5× green. A repeatable failure here means hidden cross-test coupling formerly masked by serial execution — fix by moving the affected tests into one class (same collection), not by re-serializing the assembly.

- [ ] **Step 6: Commit**

```bash
git add tests/LoopRelay.Cli.Tests/Services/Cli/
git commit -m "perf(cli-tests): split composition-root monolith into parallel per-family classes"
```

### Task 5: Split `UnifiedCliRunnerTests` (same recipe)

**Files:**
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/UnifiedCliRunnerTestBase.cs`
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/UnifiedCliRunnerStorageLifecycleTests.cs` (storage init/import/export/migrate, schema fail-closed/corrupt/future-version)
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/UnifiedCliRunnerBoundedWorkflowTests.cs` (`RunAsync_bounded_*` — the ~6–16 s cases)
- Create: `tests/LoopRelay.Cli.Tests/Services/Cli/UnifiedCliRunnerSurfaceTests.cs` (cancellation, output-writer, rendering, exit codes — the rest)
- Delete (at the end): `tests/LoopRelay.Cli.Tests/Services/Cli/UnifiedCliRunnerTests.cs`

- [ ] **Step 1: Re-derive the test list** (same Select-String recipe) and bucket into the three files.
- [ ] **Step 2: Extract base, move tests verbatim, delete original** (same recipe as Task 4 Steps 2–3).
- [ ] **Step 3: Verify** — assembly run: PASS, case count still 438; then 5× stability runs green.
- [ ] **Step 4: Commit**

```bash
git add tests/LoopRelay.Cli.Tests/Services/Cli/
git commit -m "perf(cli-tests): split unified runner tests into parallel classes"
```

### Task 6: Drop the whole-assembly serialization in Core.Tests and Agents.Tests

The two `xunit.runner.json` files force single-thread assemblies; the shared state they protect is already guarded by `DisableParallelization` collections which run exclusively. Finding: [assembly-serialization-redundant-with-collections.md](test-audit/findings/assembly-serialization-redundant-with-collections.md).

**Files:**
- Delete: `tests/LoopRelay.Core.Tests/xunit.runner.json`
- Delete: `tests/LoopRelay.Agents.Tests/xunit.runner.json`
- Check-then-modify: both csproj files — remove any `<None>`/`<Content>` item referencing `xunit.runner.json` if present.

- [ ] **Step 1: Verify the load-bearing assumption first** — with the files still present, confirm the collections exist: `WorkspaceDatabaseCountersCollection.cs` and `ProcessEnvironmentCollection.cs` both carry `DisableParallelization = true`. (xUnit ≥2.8 runs such collections exclusively after parallel collections.)
- [ ] **Step 2: Delete both files** (and stale csproj item references, if any). Build: `dotnet build LoopRelay.slnx -c Debug` — clean.
- [ ] **Step 3: Stability** — run each assembly 10 consecutive times:

```powershell
1..10 | ForEach-Object { dotnet test tests/LoopRelay.Core.Tests -c Debug --no-build | Select-String "Passed!|Failed!" }
1..10 | ForEach-Object { dotnet test tests/LoopRelay.Agents.Tests -c Debug --no-build | Select-String "Passed!|Failed!" }
```

Expected: 20× green. If a counter test flakes, the exclusivity assumption failed — restore that assembly's runner.json, note it in the commit message, and continue (this task is opportunistic, not load-bearing).
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "perf(tests): rely on exclusive collections instead of whole-assembly serialization"
```

### Task 7: Measure Phase 1

- [ ] **Step 1: Timed run with `$label = "post-split"`.** Expected: median wall clearly below baseline (target band 90–150 s; the audit's estimate is 90–120 s but was never measured — record whatever is real). If Cli.Tests wall still ≈ one class's sum, the split is unbalanced — move the heaviest test(s) into a separate class and re-measure.

---

## Phase 2 — Aggregate cost: stop rebuilding immutable state

### Task 8: Workspace-DB template helper + adoption in Orchestration store tests

Every fresh temp DB pays ~478 ms of DDL + 182-probe verification; a copy of a once-built template file takes the production stamp fast path (~4 ms). Finding: [fresh-database-schema-creation-per-test.md](test-audit/findings/fresh-database-schema-creation-per-test.md).

**Files:**
- Create: `tests/TestSupport/Services/WorkspaceDatabaseTemplate.cs`
- Create: `tests/LoopRelay.Orchestration.Primitives.Tests/Services/WorkspaceDatabaseTemplateTests.cs`
- Modify: `tests/LoopRelay.Orchestration.Primitives.Tests/LoopRelay.Orchestration.Tests.csproj` (compile-link the new TestSupport file, same pattern as the existing `MemoryArtifactStore.cs` link found in other test csprojs)
- Modify (adoption): the per-test DB-path setup in `tests/LoopRelay.Orchestration.Primitives.Tests/Persistence/CanonicalWorkflowPersistenceStoreTests.cs`, `CanonicalSpineStoreTests.cs`, `CanonicalEffectWorkStoreTests.cs`, `CanonicalAttemptStoreTests.cs`, `CanonicalTransitionPersistenceStoresTests.cs`, and `tests/LoopRelay.Orchestration.Primitives.Tests/Recovery/SqliteRecoveryStoreTests.cs`

**Interfaces:**
- Produces: `static Task<string> WorkspaceDatabaseTemplate.CopyToAsync(string key, string destinationPath, Func<string, Task> buildAtPath)` — builds the template once per process per `key` via `buildAtPath`, then file-copies it to `destinationPath` and returns it. Thread-safe; copies are fully independent files.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Concurrent;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class WorkspaceDatabaseTemplateTests
{
    [Fact]
    public async Task Builds_once_per_key_and_hands_out_independent_copies()
    {
        var builds = 0;
        var dir = Directory.CreateTempSubdirectory("wdt-test").FullName;
        try
        {
            var key = Guid.NewGuid().ToString("N");
            var destinations = Enumerable.Range(0, 8)
                .Select(i => Path.Combine(dir, $"copy-{i}", "looprelay.db")).ToArray();

            var copies = await Task.WhenAll(destinations.Select(d =>
                WorkspaceDatabaseTemplate.CopyToAsync(key, d, async path =>
                {
                    Interlocked.Increment(ref builds);
                    await File.WriteAllTextAsync(path, "template-bytes");
                })));

            Assert.Equal(1, builds);
            Assert.All(copies, c => Assert.Equal("template-bytes", File.ReadAllText(c)));
            File.WriteAllText(copies[0], "mutated");
            Assert.Equal("template-bytes", File.ReadAllText(copies[1]));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/LoopRelay.Orchestration.Primitives.Tests --filter "FullyQualifiedName~WorkspaceDatabaseTemplateTests"`
Expected: FAIL (compile error: `WorkspaceDatabaseTemplate` not defined).

- [ ] **Step 3: Implement the helper**

```csharp
using System.Collections.Concurrent;

namespace LoopRelay.Core.Artifacts; // matches TestSupport's existing namespace convention — verify against MemoryArtifactStore.cs and adjust to match

/// <summary>
/// Process-lifetime cache of immutable, fully-initialized workspace database files.
/// The template is built once per key by the supplied initializer (which should run the
/// production schema-creation path); each caller receives an independent file copy, so
/// per-test isolation is preserved while the ~0.5 s creation+verification cost is paid once.
/// Invalidation: none needed — templates die with the process and are rebuilt from
/// production DDL next run, so schema changes propagate automatically.
/// </summary>
public static class WorkspaceDatabaseTemplate
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Templates = new();

    public static async Task<string> CopyToAsync(string key, string destinationPath, Func<string, Task> buildAtPath)
    {
        var template = Templates.GetOrAdd(key, k => new Lazy<Task<string>>(() => BuildAsync(k, buildAtPath)));
        var source = await template.Value.ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(source, destinationPath, overwrite: false);
        return destinationPath;
    }

    private static async Task<string> BuildAsync(string key, Func<string, Task> buildAtPath)
    {
        var dir = Path.Combine(Path.GetTempPath(), "LoopRelay.TestTemplates", $"{Environment.ProcessId}-{key}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "template.db");
        await buildAtPath(path).ConfigureAwait(false);
        return path;
    }
}
```

Compile-link it in the Orchestration test csproj exactly like the existing `MemoryArtifactStore.cs` link (copy that `<Compile Include="..\TestSupport\Services\...">` item and adjust the filename).

- [ ] **Step 4: Run the test to verify it passes**

Same filter as Step 2. Expected: PASS.

- [ ] **Step 5: Adopt in one store test class, prove the speedup, then roll out**

Start with `CanonicalWorkflowPersistenceStoreTests` (30 cases, 14–21 s). Re-derive its current setup (the audit says: unique temp path per test; store's first op runs full ensure). Replace the fresh-path setup with:

```csharp
// in the test class constructor / setup helper, replacing the bare fresh path:
DatabasePath = WorkspaceDatabaseTemplate.CopyToAsync(
        key: "canonical-workspace-v-current",
        destinationPath: Path.Combine(_tempDir, ".agents", "looprelay.db"),
        buildAtPath: async path =>
        {
            // Build via the SAME production path the store itself uses, so the template
            // carries the canonical schema + verification stamp. Re-derive the store's
            // cheapest schema-initializing call and use it here; a no-op read after
            // construction is typically sufficient to trigger EnsureSchemaAsync.
        })
    .GetAwaiter().GetResult();
```

Rules for rollout across the six listed files: (a) **skip any test asserting empty/new-DB or creation/migration behavior** — they must keep building from scratch; (b) one class at a time; (c) after each class, run that class filtered — PASS with identical case count, and note the class's duration change.
Expected magnitude: multi-second drop per adopted class (e.g. `CanonicalWorkflowPersistenceStoreTests` 14→<8 s).
Explicitly **not** adopted in this plan: Core.Tests schema suite (it *tests* creation — must keep paying full price) and Cli.Tests composition tests (their DB is created inside production run-initialization; adapting that is a separate decision — record as follow-up if Phase 2 measurement shows it still dominates).

- [ ] **Step 6: Full suite green + commit**

```bash
git add -A
git commit -m "perf(tests): copy a once-built workspace-db template instead of re-creating schema per test"
```

### Task 9: Bulk-seed the keyed-read perf canaries

The one-row-read canaries seed thousands of history rows through per-op store writes (~58 ms each → 10–29 s of setup). The assertion is about the *read*; seeding provenance is irrelevant. Finding: [perf-canary-seeding-through-production-stores.md](test-audit/findings/perf-canary-seeding-through-production-stores.md).

**Files:**
- Modify: `tests/LoopRelay.Orchestration.Primitives.Tests/Persistence/CanonicalTransitionPersistenceStoresTests.cs` — the seeding loops inside `ReadTransitionRunAsync_reads_exactly_one_row_no_matter_how_large_the_history_table_is` and `PersistStateAsync_updates_the_correct_existing_run_when_history_has_many_rows`

- [ ] **Step 1: Re-derive the current seeding loop and the target table's row shape.** Write **one** row through the production store first (this row is the read target and the shape anchor), then bulk-insert the remaining N−1 rows in a single connection/transaction:

```csharp
// after one store-written anchor row:
await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
    new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ConnectionString);
await connection.OpenAsync();
await using var transaction = connection.BeginTransaction();
var insert = connection.CreateCommand();
insert.Transaction = transaction;
// Re-derive the exact column list by reading the store's own INSERT for this table;
// mirror it here with parameters, varying only the key columns per row.
insert.CommandText = "INSERT INTO <table> (<columns>) VALUES (<parameters>)";
for (var i = 1; i < n; i++) { /* set parameters from the anchor row's values with unique keys */ await insert.ExecuteNonQueryAsync(); }
await transaction.CommitAsync();
```

Add one shape assertion: read the anchor row and one bulk row and assert identical column count/nullability-relevant values, so schema drift breaks the fixture loudly.

- [ ] **Step 2: Verify the canary still bites** — run the class: PASS and visibly faster (was 26–47 s; expect low single-digit seconds). Then temporarily broaden the production keyed read (working-tree perturbation), confirm the canary FAILS, revert.
- [ ] **Step 3: Commit**

```bash
git add tests/LoopRelay.Orchestration.Primitives.Tests/Persistence/CanonicalTransitionPersistenceStoresTests.cs
git commit -m "perf(tests): bulk-seed keyed-read canaries in one transaction, keep a store-written shape anchor"
```

### Task 10: Measure Phase 2

- [ ] **Step 1: Timed run with `$label = "post-lifecycle"`.** Expected: aggregate (sum of per-assembly durations in the logs) down materially vs post-split; suite wall at or below the post-split median. Record medians in `.tmp/test-perf/summary.txt`.

---

## Phase 3 — Redundancy and determinism trims

### Task 11: Consolidate the schema-fingerprint triple assertion

Finding: [schema-fingerprint-and-ensure-overlap.md](test-audit/findings/schema-fingerprint-and-ensure-overlap.md).

**Files:**
- Modify: `tests/LoopRelay.Core.Tests/Services/LoopRelayWorkspaceDatabaseRecoveryScopeColumnTests.cs`, `.../LoopRelayWorkspaceDatabaseLoopHistoryConvergenceIndexTests.cs` (keep feature-delta assertions; drop their canonical-fingerprint re-assertions)
- Keep untouched: `.../LoopRelayWorkspaceDatabaseSchemaV9Tests.cs` — becomes the single fingerprint owner

- [ ] **Step 1: Re-derive the three fingerprint assertion sites** (`Select-String -Pattern "fingerprint" -Path tests/LoopRelay.Core.Tests/Services/*.cs`). Confirm SchemaV9's assertion covers the same constant.
- [ ] **Step 2: In the two feature files**, replace fingerprint assertions with the feature-specific delta each file exists for (column present with expected affinity; index exists and is unique — assert via the file's existing introspection helpers). Case counts may legitimately stay identical (assertions change, not tests); if a test *only* asserted the fingerprint, delete it and say so in the commit message.
- [ ] **Step 3: Perturbation check** — temporarily alter one table's DDL in the schema source (working tree only): run Core.Tests, confirm **exactly one** test now fails on the fingerprint (SchemaV9's), revert.
- [ ] **Step 4: Commit** — `git commit -am "test(core): single canonical owner for the schema shape fingerprint"`

### Task 12: Remove compile-enforced and triple-asserted surface checks

Finding: [compile-enforced-and-triple-asserted-surface-contracts.md](test-audit/findings/compile-enforced-and-triple-asserted-surface-contracts.md).

**Files:**
- Modify: `tests/LoopRelay.Cli.Tests/Services/Cli/CliSurfaceDependencyTests.cs` — delete the assembly-reference-purity reflection test only (the published-CLI matrix and parser/renderer tests stay)
- Modify: the composition-root guards class from Task 4 (`CompositionRootGuardsAndRecoveryTests.cs`) — retirement-duplicate assertions

- [ ] **Step 1: Re-derive.** Read the reference-purity test; confirm it asserts only what `src/LoopRelay.Cli.Surface/LoopRelay.Cli.Surface.csproj` already enforces by its single project reference. If it asserts anything beyond the reference graph (e.g., specific framework-contract allowlists that a transitive package could violate), keep that part and delete only the graph mirror.
- [ ] **Step 2: Compare the composition-root retirement assertions against `ConvergenceArchitectureVerifierTests`** (which delegates to the production verifier). Delete only those whose assertion is a strict subset of the verifier's checks; keep any that assert composition-specific behavior (e.g., a typed error when a retired command is requested).
- [ ] **Step 3: Perturbation check** — temporarily reintroduce a fake retired-composition registration (working tree only); confirm the verifier test alone fails; revert.
- [ ] **Step 4: Full Cli.Tests + Certification.Tests green; commit** — `git commit -am "test(cli): drop compile-enforced reference mirror and retirement duplicates; verifier is the single authority"`

### Task 13: Make the four wall-clock-dependent tests deterministic, and bound untimed process waits

Finding: [wall-clock-dependent-assertions.md](test-audit/findings/wall-clock-dependent-assertions.md). Each site is independent; re-derive the exact current code before editing — the transformations below are patterns, not verbatim diffs.

**Files:**
- Modify: `tests/LoopRelay.Core.Tests/Models/Identity/CausalUlidTests.cs` (the `Task.Delay(50)` ordering test)
- Modify: `tests/LoopRelay.Cli.Tests/Services/Usage/CodexUsageProbeTests.cs` (the `sw.Elapsed < 5 s` assertion)
- Modify: `tests/LoopRelay.Certification.Tests/CertificationFailureDiagnosisTests.cs` (the 100 ms timed CTS)
- Modify: `tests/LoopRelay.Cli.Tests/Services/Agents/InputWaitProgressAgentRuntimeTests.cs` (the `Task.Delay(25)` interleaves)
- Modify: `tests/LoopRelay.Orchestration.Primitives.Tests/Resolution/GitObservationTests.cs`, `tests/LoopRelay.Permissions.Tests/Services/OperationPermissionHandlerTests.cs`, `tests/LoopRelay.Infrastructure.Tests/Services/RepositoryArtifactStoreTests.cs` (untimed `WaitForExit()` on real processes)

- [ ] **Step 1: ULID test** — the sibling caching/detector tests show the repo's injected-clock pattern (`monotonicNow`). If `CausalUlid` accepts a timestamp source, generate the two ULIDs with explicitly different injected milliseconds and assert ordering; delete the delay. If it does not accept one, replace the fixed delay with a loop that regenerates until the wall-clock millisecond actually changes (bounded at 1 s) — removes the fixed cost and the race without a production change.
- [ ] **Step 2: Probe test** — keep the behavioral assertion (the read returned/timed out) and demote the stopwatch to a generous hang guard only (e.g. assert < 60 s with a comment naming it a deadlock guard), or drop the elapsed assertion if the timeout outcome is already asserted directly.
- [ ] **Step 3: Diagnosis test** — replace the 100 ms timer with an untimed `CancellationTokenSource` cancelled deterministically after the waiting agent has observably started (a `TaskCompletionSource` the fake signals on entry — the class's `WaitingAgent` fake is scriptable; re-derive its shape).
- [ ] **Step 4: InputWait tests** — replace the 25 ms delays with `TaskCompletionSource` handshakes between the scripted runtime's chunk and completion callbacks so the interleaving is forced, not raced.
- [ ] **Step 5: Untimed waits** — change bare `WaitForExit()` to a bounded form (`WaitForExit(120_000)` with a failure assert, or `WaitForExitAsync` + `.WaitAsync(TimeSpan.FromMinutes(2))`) so a broken git cannot hang the run.
- [ ] **Step 6: Flake-resistance verification** — run each modified test 50× filtered; expect 50× PASS. For each, apply a one-line behavior-breaking perturbation to the code under test (working tree only), confirm the test fails, revert.
- [ ] **Step 7: Commit** — `git commit -am "test: replace wall-clock dependence with deterministic signals; bound process waits"`

### Task 14: Report gated no-op tests as Skipped

Finding: [env-gated-noop-tests-report-passed.md](test-audit/findings/env-gated-noop-tests-report-passed.md). xUnit 2.9.3 has no built-in dynamic skip; use the `Xunit.SkippableFact` package (smallest established mechanism).

**Files:**
- Modify: `Directory.Packages.props` (add `<PackageVersion Include="Xunit.SkippableFact" Version="<resolved>" />` — resolve the current stable with `dotnet package search Xunit.SkippableFact --take 1` and pin that)
- Modify: `tests/LoopRelay.Agents.Compatibility.Tests/LoopRelay.Agents.Compatibility.Tests.csproj`, `tests/LoopRelay.Orchestration.Primitives.Tests/LoopRelay.Orchestration.Tests.csproj` (add versionless `<PackageReference Include="Xunit.SkippableFact" />`)
- Modify: `tests/LoopRelay.Agents.Compatibility.Tests/CodexAppServerCertificationTests.cs`, `tests/LoopRelay.Orchestration.Primitives.Tests/Measurement/WorkspaceMagnitudeHarness.cs`

- [ ] **Step 1: Convert each gated early-return** from the pattern `if (binary is null) return;` under `[Fact]` to:

```csharp
[SkippableFact]
public async Task Certifies_against_live_codex_app_server()
{
    Skip.If(Environment.GetEnvironmentVariable("LOOPRELAY_CODEX_CERT_BINARY") is null,
        "Set LOOPRELAY_CODEX_CERT_BINARY to run live codex certification.");
    // unchanged body
}
```

Apply to every env-gated early return in the two files (re-derive the full set by `Select-String -Pattern "GetEnvironmentVariable" -Path <both files>`).
- [ ] **Step 2: Verify both directions** — run without env vars: cases report **Skipped** with the reason; set a dummy `LOOPRELAY_MEASUREMENT_OUTPUT` to a temp dir and run the harness filtered once to confirm the live path still executes. Full suite: totals now show the skips (`1,535 = passed + skipped`); nothing fails.
- [ ] **Step 3: Commit** — `git commit -am "test: gated live/measurement tests report Skipped instead of silently passing"`

### Task 15: Final measurement and wrap-up

- [ ] **Step 1: Timed run with `$label = "final"`.** Compare medians across baseline / post-split / post-lifecycle / final in `.tmp/test-perf/summary.txt`.
- [ ] **Step 2: Sanity checks** — full suite green; case accounting explained (1,535 total; any count change traces to Task 11 deletions or Task 14 skips, named in commit messages); `git log --oneline` shows one commit per task.

---

## Phase 4 (optional, production-scoped) — Narrow the Deep verification default

Only start after Phase 2's measurement, and skip entirely if the event-sourced storage migration is scheduled — it may moot this. Finding: [deep-verification-default-on-routine-observations.md](test-audit/findings/deep-verification-default-on-routine-observations.md); target set by the repo's own M3 analysis.

### Task 16: Route routine observations through the stamped/light tier

**Files:**
- Read first: `src/LoopRelay.Orchestration.Primitives/` storage contracts (tier enum — audit noted Deep is the zero/default value) and `WorkspaceStorageInspector`; the routine observation call path in `RepositoryObserver`
- Modify: the routine observation call site(s) to request the light tier explicitly; **do not renumber the enum** (zero-value changes are serialization-hazardous)
- Modify/extend: `tests/LoopRelay.Orchestration.Primitives.Tests/Storage/WorkspaceStorageVerificationTierTests.cs`

- [ ] **Step 1: Re-derive every consumer of the default tier** (`Select-String` for the enum type name across `src/`). Classify each call site: routine observation vs trust boundary (import, recovery, first contact, explicit repair). If any site's classification is unclear, stop and record the question instead of guessing.
- [ ] **Step 2: Write the failing test** — extend the tier tests with an assertion that the routine observation path requests the stamped/light tier (arrange a recording inspector or assert on the tier value the observer passes; re-derive the seam from the existing tier tests' arrangement).
- [ ] **Step 3: Implement** — pass the light tier explicitly at the routine call site(s); leave boundary sites on Deep.
- [ ] **Step 4: Verify fail-closed is intact** — run the corrupt/foreign-schema tests in `UnifiedCliRunnerStorageLifecycleTests` (from Task 5) and the import-gateway tests: all green (they exercise boundary paths, which still run Deep).
- [ ] **Step 5: Measure** — timed run `$label = "post-tier"`; expect a further drop in the composition-root family classes. Commit: `git commit -am "perf(orchestration): routine observations use stamped verification; deep tier reserved for trust boundaries"`

---

## Explicitly deferred (decided, not forgotten)

- **WAL/pooling (~58 ms/store-op floor):** production durability decision owned by ORCH-3; run the prior audit's M1 WAL comparison only if the event-sourced direction stalls. [Finding](test-audit/findings/store-operation-durability-cost-no-wal-no-pooling.md).
- **`CreateForTests` caching:** measure-first — profile one composition-root test (`dotnet-trace`, gap #4 in [12-measurement-gaps.md](test-audit/12-measurement-gaps.md)) before designing anything.
- **Runner-vs-composition coverage consolidation (audit rank 9):** high-effort, judgment-heavy; revisit only if post-Phase-2 numbers show those classes still dominating. [Finding](test-audit/findings/bounded-workflow-double-coverage-composition-vs-runner.md).
- **Test counters on the production DB class:** retained deliberately; revisit at the storage migration. [Finding](test-audit/findings/test-counters-on-production-database.md).
