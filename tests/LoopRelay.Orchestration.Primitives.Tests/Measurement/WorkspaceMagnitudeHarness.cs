using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace LoopRelay.Orchestration.Tests.Measurement;

/// <summary>
/// The §12 measurement harness: generates workspace fixtures at a requested number of
/// <c>canonical_transition_evidence</c> rows by replaying the writes an attempt really performs,
/// then measures M1, M2, M3, M4 and M6 against them.
/// <para>
/// Fixtures are generated, never bulk-inserted. Every row this harness creates is written by the
/// same production store method the runtime calls — <see cref="CanonicalTransitionEvidenceStore"/>,
/// <see cref="CanonicalTransitionBoundaryJournal"/>, <see cref="CanonicalTransitionRunStore"/>,
/// <see cref="CanonicalEffectWorkStore"/> and <see cref="EffectWorker"/> — so each write pays the
/// real per-open cost (a fresh unpooled connection plus <c>EnsureSchemaAsync</c>) and every
/// <c>document_json</c> is produced by the store's own serializer with its own options. A bulk
/// INSERT would produce a database no code path could have created and would invalidate M2's read
/// costs, which is exactly what M2 forbids.
/// </para>
/// <para>
/// Opt-in: the single fact below returns immediately unless <c>LOOPRELAY_MEASUREMENT_OUTPUT</c>
/// names a directory to write results into. It is a data-collection run, not an assertion, so it
/// must not lengthen the ordinary suite. Set <c>LOOPRELAY_MEASUREMENT_N</c> to a comma-separated
/// list to override the default 100 / 1000 / 10000 scales.
/// </para>
/// </summary>
public sealed class WorkspaceMagnitudeHarness
{
    private const string OutputVariable = "LOOPRELAY_MEASUREMENT_OUTPUT";
    private const string ScaleVariable = "LOOPRELAY_MEASUREMENT_N";

    /// <summary>Evidence rows one replayed attempt writes: 3 boundaries, 1 raw output, 2 events, 1 settlement.</summary>
    private const int EvidenceRowsPerAttempt = 7;

    private const int TurnsPerAttempt = 3;
    private const int EffectsPerAttempt = 2;

    /// <summary>M4's "~10 evidence candidates" for the dedicated settlement measurement.</summary>
    private const int M4EffectCandidates = 10;

    /// <summary>
    /// Bytes of provider text per captured raw output. Calibrated from the only real agent-authored
    /// corpus available in this working copy — the 33 markdown files under <c>.agents/</c>, whose
    /// mean size is 15,719 bytes — because no aged workspace database exists to sample from. The
    /// results file records this parameter and reports <c>document_json</c> broken down by event
    /// name so the M6 dominance answer can be rescaled rather than taken on trust.
    /// </summary>
    private const int RawOutputBytes = 15_719;

    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };

    [Fact]
    public async Task Record_measurement_plan_numbers()
    {
        string? output = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        Directory.CreateDirectory(output);
        var report = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["generatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["machine"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["processorCount"] = Environment.ProcessorCount,
                ["osVersion"] = Environment.OSVersion.VersionString,
                ["runtime"] = Environment.Version.ToString(),
                ["is64Bit"] = Environment.Is64BitProcess,
            },
            ["parameters"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["evidenceRowsPerAttempt"] = EvidenceRowsPerAttempt,
                ["turnsPerAttempt"] = TurnsPerAttempt,
                ["effectsPerAttempt"] = EffectsPerAttempt,
                ["rawOutputBytes"] = RawOutputBytes,
                ["rawOutputCalibration"] = "mean size of the 33 .agents/**/*.md files in this working copy",
            },
            ["emptySchemaFloor"] = await MeasureEmptySchemaFloorAsync(),
            ["hostWorkspace"] = MeasureHostWorkspace(),
        };

        // Written after every scale, not once at the end: generation cost grows with N, so a large
        // scale can outrun the run budget, and losing the completed smaller scales with it would
        // throw away measurements that are already in hand.
        string resultsPath = Path.Combine(output, "measurements.json");
        var scales = new List<Dictionary<string, object>>();
        report["scales"] = scales;
        await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(report, ReportJson));
        foreach (int n in Scales())
        {
            scales.Add(await MeasureScaleAsync(n));
            await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(report, ReportJson));
        }
    }

    private static IReadOnlyList<int> Scales()
    {
        string? raw = Environment.GetEnvironmentVariable(ScaleVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [100, 1_000, 10_000];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.Parse(item, CultureInfo.InvariantCulture))
            .ToArray();
    }

    // ---------------------------------------------------------------- M1 inputs: the per-open floor

    /// <summary>
    /// The cost every store operation pays before it issues its own statement. Both the schema-ensure
    /// statement count and the resulting empty database size are measured, not modelled: a SQLite
    /// authorizer counts what <c>EnsureSchemaAsync</c> really compiles, on a connection this harness
    /// owns and opens exactly the way <c>CanonicalWorkflowPersistenceStore.OpenAsync</c> opens its own.
    /// The second open re-measures on a fresh connection to the same file, which is what shows
    /// whether the ensure work is memoized per process or repaid per connection.
    /// </summary>
    private static async Task<Dictionary<string, object>> MeasureEmptySchemaFloorAsync()
    {
        Repository repository = CreateRepository("looprelay-schema-floor-");
        try
        {
            string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            var first = new AuthorizerCounter();
            var firstWatch = Stopwatch.StartNew();
            await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
            {
                await connection.OpenAsync();
                first.Watch(connection);
                await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
            }
            firstWatch.Stop();

            var second = new AuthorizerCounter();
            var secondWatch = Stopwatch.StartNew();
            await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
            {
                await connection.OpenAsync();
                second.Watch(connection);
                await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
            }
            secondWatch.Stop();

            // A third open, timed only, after the process is warm: this is the number that multiplies by
            // every store operation in an attempt.
            var warm = new List<double>();
            for (int index = 0; index < 20; index++)
            {
                var watch = Stopwatch.StartNew();
                await using SqliteConnection connection =
                    LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
                await connection.OpenAsync();
                await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
                watch.Stop();
                warm.Add(watch.Elapsed.TotalMilliseconds);
            }

            Dictionary<string, object> counters = ReadDatabaseCounters();
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["databaseBytes"] = new FileInfo(databasePath).Length,
                ["pageCount"] = await ScalarLongAsync(databasePath, "SELECT * FROM pragma_page_count();"),
                ["pageSizeBytes"] = await ScalarLongAsync(databasePath, "SELECT * FROM pragma_page_size();"),
                ["tableCount"] = await ScalarLongAsync(
                    databasePath,
                    "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';"),
                ["indexCount"] = await ScalarLongAsync(
                    databasePath, "SELECT count(*) FROM sqlite_master WHERE type = 'index';"),
                ["firstOpen"] = first.Snapshot(firstWatch.Elapsed.TotalMilliseconds),
                ["secondOpen"] = second.Snapshot(secondWatch.Elapsed.TotalMilliseconds),
                ["warmOpenAndEnsureMs"] = Distribution(warm),
                ["coreCountersAfterOpens"] = counters,
            };
        }
        finally
        {
            DeleteFixture(repository.Path);
        }
    }

    /// <summary>
    /// The three ensure/repair counters <c>LoopRelayWorkspaceDatabase</c> maintains. They are
    /// <c>internal</c> to <c>LoopRelay.Core</c>, whose internals are visible only to
    /// <c>LoopRelay.Core.Tests</c>, so the harness reads them reflectively rather than asking for a
    /// production change it is not allowed to make.
    /// </summary>
    private static Dictionary<string, object> ReadDatabaseCounters()
    {
        var counters = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (string name in new[] { "ShapeRequirementProbes", "RepairTransactionsOpened", "FullVerificationRuns" })
        {
            System.Reflection.FieldInfo? field = typeof(LoopRelayWorkspaceDatabase).GetField(
                name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            counters[name] = field?.GetValue(null) ?? "not-readable";
        }

        return counters;
    }

    /// <summary>
    /// M6's filesystem half, taken from a real dogfooded working copy rather than from the generated
    /// fixture. A scripted ledger replay writes rows, not working-tree artifacts, so it cannot
    /// manufacture an evidence tree or a telemetry log; inventing one would put a number in the audit
    /// that nothing produced. Point <c>LOOPRELAY_MEASUREMENT_HOST_REPO</c> at a checkout to collect it.
    /// </summary>
    private static Dictionary<string, object> MeasureHostWorkspace()
    {
        string? host = Environment.GetEnvironmentVariable("LOOPRELAY_MEASUREMENT_HOST_REPO");
        if (string.IsNullOrWhiteSpace(host) || !Directory.Exists(host))
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["measured"] = false,
                ["reason"] = "LOOPRELAY_MEASUREMENT_HOST_REPO was not set to an existing directory.",
            };
        }

        (long evidenceFiles, long evidenceBytes) = TreeSize(Path.Combine(host, ".agents"));
        (long markdownFiles, long markdownBytes) = TreeSize(Path.Combine(host, ".agents"), "*.md");
        (long telemetryFiles, long telemetryBytes) = TreeSize(
            Path.Combine(host, ".LoopRelay", "telemetry"), "*.jsonl");
        string databasePath = Path.Combine(
            host, LoopRelayWorkspaceDatabase.RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["measured"] = true,
            ["path"] = host,
            ["evidenceTreeFiles"] = evidenceFiles,
            ["evidenceTreeBytes"] = evidenceBytes,
            ["evidenceTreeMarkdownFiles"] = markdownFiles,
            ["evidenceTreeMarkdownBytes"] = markdownBytes,
            ["evidenceTreeMeanMarkdownBytes"] = markdownFiles == 0 ? 0 : markdownBytes / markdownFiles,
            ["telemetryJsonlFiles"] = telemetryFiles,
            ["telemetryJsonlBytes"] = telemetryBytes,
            ["databaseExists"] = File.Exists(databasePath),
            ["databaseBytes"] = File.Exists(databasePath) ? new FileInfo(databasePath).Length : 0L,
        };
    }

    // ------------------------------------------------------------------------ per-scale measurement

    private static async Task<Dictionary<string, object>> MeasureScaleAsync(int targetEvidenceRows)
    {
        Repository repository = CreateRepository(
            $"looprelay-magnitude-{targetEvidenceRows}-", asGitRepository: true);
        try
        {
            var store = new CanonicalWorkflowPersistenceStore(repository);
            var evidenceStore = new CanonicalTransitionEvidenceStore(store);
            var boundaryJournal = new CanonicalTransitionBoundaryJournal(store);
            var runStore = new CanonicalTransitionRunStore(store);
            var effectStore = new CanonicalEffectWorkStore(repository);
            string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);

            var root = new RunRecord(
                CausalUlid.NewId("run"),
                WorkspaceIdentity.New().Value,
                "magnitude-chain",
                "unbounded",
                "running",
                DateTimeOffset.UtcNow,
                null,
                null,
                "Generated workspace magnitude fixture.");
            await store.UpsertRunAsync(root);
            var instance = new WorkflowInstanceRecord(
                CausalUlid.NewId("wfi"),
                root.RunId,
                WorkflowIdentity.Execute,
                "1",
                "running",
                DateTimeOffset.UtcNow,
                null,
                null);
            await store.UpsertWorkflowInstanceAsync(instance);

            int attempts = (targetEvidenceRows + EvidenceRowsPerAttempt - 1) / EvidenceRowsPerAttempt;
            var attemptDurations = new List<double>(attempts);
            var generation = Stopwatch.StartNew();
            for (int index = 0; index < attempts; index++)
            {
                var watch = Stopwatch.StartNew();
                await ReplayAttemptAsync(
                    store,
                    evidenceStore,
                    boundaryJournal,
                    runStore,
                    effectStore,
                    repository,
                    root,
                    instance,
                    index,
                    EffectsPerAttempt);
                watch.Stop();
                attemptDurations.Add(watch.Elapsed.TotalMilliseconds);
            }

            generation.Stop();

            // M1's stated distortion: attempt 1 pays the schema creation. Discard it.
            List<double> steadyState = attemptDurations.Skip(1).ToList();

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["targetEvidenceRows"] = targetEvidenceRows,
                ["attemptsReplayed"] = attempts,
                ["generationSeconds"] = Math.Round(generation.Elapsed.TotalSeconds, 3),
                ["m6"] = await CollectMagnitudesAsync(repository, databasePath),
                ["m1"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["attemptWallMsFirst"] = Math.Round(attemptDurations[0], 3),
                    ["attemptWallMsSteadyState"] = Distribution(steadyState),
                    // Measured growth curve: if an attempt's cost were independent of how much history
                    // the workspace already holds, these two would match. They are reported rather than
                    // summarised so supra-constant growth is visible instead of inferred.
                    ["attemptWallMsFirstDecile"] = Distribution(Decile(steadyState, first: true)),
                    ["attemptWallMsLastDecile"] = Distribution(Decile(steadyState, first: false)),
                    ["storeOperationsPerAttempt"] = StoreOperationsPerAttempt(EffectsPerAttempt),
                    ["coreCounters"] = ReadDatabaseCounters(),
                    ["statementsPerAttempt"] = "not measured",
                    ["statementsPerAttemptReason"] =
                        "CanonicalWorkflowPersistenceStore.OpenAsync (CanonicalWorkflowPersistenceStore.cs:1538) "
                        + "opens its own unpooled connection and exposes no connection or command observer, "
                        + "unlike CanonicalEffectWorkStore. SQLitePCLRaw's only statement-level hook is the "
                        + "per-connection authorizer, so no test-side seam can reach the handle a write ran on. "
                        + "Counting them would require adding an observer to production code, which this task forbids.",
                },
                ["m2"] = await MeasureM2Async(repository, store, runStore, databasePath),
                ["m3"] = await MeasureM3Async(repository),
                ["m4"] = await MeasureM4Async(repository, store, root, instance),
            };
        }
        finally
        {
            DeleteFixture(repository.Path);
        }
    }

    /// <summary>
    /// One attempt's worth of writes, in the order and through the methods the runtime uses. Nothing
    /// here reaches past a store into SQL.
    /// </summary>
    private static async Task ReplayAttemptAsync(
        CanonicalWorkflowPersistenceStore store,
        CanonicalTransitionEvidenceStore evidenceStore,
        CanonicalTransitionBoundaryJournal boundaryJournal,
        CanonicalTransitionRunStore runStore,
        CanonicalEffectWorkStore effectStore,
        Repository repository,
        RunRecord root,
        WorkflowInstanceRecord instance,
        int index,
        int effects)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var causality = new CanonicalCausalContext(
            new WorkspaceIdentity(root.WorkspaceId),
            new RunIdentity(root.RunId),
            new WorkflowInstanceIdentity(instance.WorkflowInstanceId),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());
        var transition = new WorkflowTransitionIdentity("ApplyImplementationSlice");
        string snapshotHash = new string('a', 64);

        await store.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
            causality.TransitionRun.Value,
            WorkflowIdentity.Execute,
            new WorkflowStageIdentity("Implementation"),
            transition,
            TransitionDurableState.Started,
            RuntimeOutcomeKind.Waiting,
            now,
            null,
            snapshotHash,
            "Transition started.",
            [$"snapshot:{snapshotHash}"]));

        var attempt = new AttemptRecord(
            causality.Attempt.Value,
            causality.TransitionRun.Value,
            instance.WorkflowInstanceId,
            root.RunId,
            1,
            now,
            null,
            null);
        await store.UpsertAttemptAsync(attempt);

        string sessionId = CausalUlid.NewId("sess");
        await store.UpsertAgentSessionAsync(new AgentSessionRecord(
            sessionId,
            attempt.AttemptId,
            root.WorkspaceId,
            "codex",
            CausalUlid.NewId("thread"),
            "implementer",
            null,
            now,
            null));
        for (int turn = 0; turn < TurnsPerAttempt; turn++)
        {
            await store.AppendAgentTurnAsync(new AgentTurnRecord(
                CausalUlid.NewId("turn"),
                sessionId,
                turn,
                now,
                "Completed",
                new string('b', 64),
                4_000 + turn,
                1_500 + turn,
                2_000 + turn));
        }

        // Three boundary observations: the ones a completed attempt really journals.
        int sequence = 0;
        foreach (TransitionBoundaryKind boundary in new[]
        {
            TransitionBoundaryKind.PreResolution,
            TransitionBoundaryKind.ProviderCompleted,
            TransitionBoundaryKind.CompletionPersisted,
        })
        {
            await boundaryJournal.RecordAsync(
                new TransitionBoundaryObservation(
                    causality,
                    transition,
                    boundary,
                    sequence++,
                    now,
                    snapshotHash,
                    CausalUlid.NewId("providerturn"),
                    [$"boundary:{boundary}"]),
                CancellationToken.None);
        }

        // The captured provider output: the row that carries the workspace's bulk.
        await evidenceStore.RecordRawOutputAsync(
            causality,
            transition,
            new PromptExecutionResult(
                PromptExecutionStatus.Completed,
                RawOutput(index),
                TimeSpan.FromSeconds(42),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["provider"] = "codex",
                    ["model"] = "gpt-5-codex",
                    ["turnId"] = CausalUlid.NewId("turn"),
                }),
            CancellationToken.None);

        foreach (string eventName in new[] { "OutputInterpreted", "ProductsValidated" })
        {
            await evidenceStore.RecordEventAsync(
                new TransitionEvidenceEvent(
                    causality,
                    now,
                    transition,
                    TransitionDurableState.OutputValidated,
                    eventName,
                    $"{eventName} for the replayed attempt.",
                    [".agents/evidence/execution/slice.md"]),
                CancellationToken.None);
        }

        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.ImplementationSlice,
            WorkflowIdentity.Execute,
            transition,
            [WorkflowIdentity.Execute],
            "repository-owned observed artifact evidence",
            "canonical workflow persistence",
            [$".agents/evidence/execution/slice-{index}.md"],
            new string('c', 64),
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$".agents/evidence/execution/slice-{index}.md"]));

        if (effects > 0)
        {
            EffectIntent[] intents = Enumerable
                .Range(0, effects)
                .Select(order => Intent(causality, order, $"slice-{index}-{order}"))
                .ToArray();
            await effectStore.AppendPlanAsync(intents, CancellationToken.None);
            await new EffectWorker(
                "magnitude-harness",
                effectStore,
                new EffectExecutorRegistry([new SucceedingExecutor()]),
                new UnusedReconciler()).RunOnceAsync(CancellationToken.None);
            await new CanonicalEffectPlanSettlementStore(repository)
                .TrySettleAsync(causality.TransitionRun, CancellationToken.None);
        }

        await runStore.PersistStateAsync(
            new TransitionRunStateUpdate(
                causality,
                now,
                transition,
                TransitionDurableState.Completed,
                "Transition completed.",
                [$"snapshot:{snapshotHash}"]),
            CancellationToken.None);
        await store.CompleteAttemptAsync(attempt.AttemptId, now, "Completed", CancellationToken.None);
    }

    private static int StoreOperationsPerAttempt(int effects) =>
        1 // transition run upsert
        + 1 // attempt upsert
        + 1 // agent session upsert
        + TurnsPerAttempt
        + 3 // boundary evidence
        + 1 // raw output evidence
        + 2 // evidence events
        + 1 // product upsert
        + (effects > 0 ? 3 : 0) // append plan, worker pass, settle
        + 1 // PersistStateAsync
        + 1; // CompleteAttemptAsync

    // ------------------------------------------------------------------------------------ M6

    private static async Task<Dictionary<string, object>> CollectMagnitudesAsync(
        Repository repository,
        string databasePath)
    {
        // Every table is counted rather than a guessed list, so a schema rename cannot silently drop
        // a magnitude from the record. Only the non-empty ones are reported.
        var tables = new List<string>();
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var rowCounts = new Dictionary<string, object>(StringComparer.Ordinal);
        long populatedTables = 0;
        foreach (string table in tables)
        {
            long rows = await ScalarLongAsync(databasePath, $"SELECT count(*) FROM \"{table}\";");
            if (rows > 0)
            {
                rowCounts[table] = rows;
                populatedTables++;
            }
        }

        long documentJsonBytes = await ScalarLongAsync(
            databasePath, "SELECT coalesce(sum(length(document_json)), 0) FROM canonical_transition_evidence;");
        long databaseBytes = new FileInfo(databasePath).Length;

        var byEvent = new List<Dictionary<string, object>>();
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT event_name, count(*), sum(length(document_json)), max(length(document_json))
                FROM canonical_transition_evidence
                GROUP BY event_name
                ORDER BY sum(length(document_json)) DESC;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                byEvent.Add(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["eventName"] = reader.GetString(0),
                    ["rows"] = reader.GetInt64(1),
                    ["documentJsonBytes"] = reader.IsDBNull(2) ? 0L : reader.GetInt64(2),
                    ["maxDocumentJsonBytes"] = reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
                });
            }
        }

        (long evidenceFiles, long evidenceBytes) = TreeSize(Path.Combine(repository.Path, ".agents"));
        (long telemetryFiles, long telemetryBytes) = TreeSize(
            Path.Combine(repository.Path, ".LoopRelay", "telemetry"), "*.jsonl");

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["databaseBytes"] = databaseBytes,
            ["pageCount"] = await ScalarLongAsync(databasePath, "SELECT * FROM pragma_page_count();"),
            ["pageSizeBytes"] = await ScalarLongAsync(databasePath, "SELECT * FROM pragma_page_size();"),
            ["freelistPages"] = await ScalarLongAsync(databasePath, "SELECT * FROM pragma_freelist_count();"),
            ["tableCount"] = tables.Count,
            ["populatedTableCount"] = populatedTables,
            ["rowCounts"] = rowCounts,
            ["documentJsonBytes"] = documentJsonBytes,
            ["documentJsonShareOfDatabase"] = databaseBytes == 0
                ? 0d
                : Math.Round((double)documentJsonBytes / databaseBytes, 4),
            ["documentJsonByEventName"] = byEvent,
            ["evidenceTreeFiles"] = evidenceFiles,
            ["evidenceTreeBytes"] = evidenceBytes,
            ["telemetryJsonlFiles"] = telemetryFiles,
            ["telemetryJsonlBytes"] = telemetryBytes,
            ["evidenceTreeNote"] =
                "The replay writes ledger rows, not working-tree artifacts, so the generated workspace "
                + "has no .agents/ tree and no telemetry JSONL. Both are filesystem magnitudes that a "
                + "scripted ledger replay cannot manufacture honestly; see the report.",
        };
    }

    // ------------------------------------------------------------------------------------ M2

    private static async Task<Dictionary<string, object>> MeasureM2Async(
        Repository repository,
        CanonicalWorkflowPersistenceStore store,
        CanonicalTransitionRunStore runStore,
        string databasePath)
    {
        string? runId = await ScalarStringAsync(
            databasePath, "SELECT run_id FROM canonical_transition_runs LIMIT 1;");
        var projection = new CanonicalPersistenceProjection();

        var persist = new List<double>();
        if (runId is not null)
        {
            var causality = new CanonicalCausalContext(
                WorkspaceIdentity.New(),
                RunIdentity.New(),
                WorkflowInstanceIdentity.New(),
                new TransitionRunIdentity(runId),
                AttemptIdentity.New());
            var update = new TransitionRunStateUpdate(
                causality,
                DateTimeOffset.UtcNow,
                new WorkflowTransitionIdentity("ApplyImplementationSlice"),
                TransitionDurableState.Completed,
                "Re-persisted by the M2 harness.",
                ["m2"]);
            await runStore.PersistStateAsync(update, CancellationToken.None); // warm-up, discarded
            for (int index = 0; index < 10; index++)
            {
                var watch = Stopwatch.StartNew();
                await runStore.PersistStateAsync(update, CancellationToken.None);
                watch.Stop();
                persist.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        await projection.ProjectAsync(repository, CancellationToken.None); // warm-up, discarded
        var project = new List<double>();
        for (int index = 0; index < 5; index++)
        {
            var watch = Stopwatch.StartNew();
            await projection.ProjectAsync(repository, CancellationToken.None);
            watch.Stop();
            project.Add(watch.Elapsed.TotalMilliseconds);
        }

        // What LoadSnapshotAsync — the read PersistStateAsync used to depend on, and the read
        // ProjectAsync still performs — must traverse at this scale. Unlike persistStateAsyncMs
        // (n=10) and projectAsyncMs (n=5) above, this is a single, un-repeated sample (n=1): there is
        // no warm-up/discard and no distribution, so it must not be read with the same confidence.
        var snapshotWatch = Stopwatch.StartNew();
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync(CancellationToken.None);
        snapshotWatch.Stop();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["persistStateAsyncMs"] = Distribution(persist),
            ["projectAsyncMs"] = Distribution(project),
            ["loadSnapshotAsyncMs"] = Math.Round(snapshotWatch.Elapsed.TotalMilliseconds, 3),
            ["loadSnapshotAsyncSamples"] = 1,
            ["loadSnapshotAsyncSampleNote"] =
                "single un-repeated sample (n=1), unlike persistStateAsyncMs (n=10) and projectAsyncMs "
                + "(n=5) above; no warm-up call was discarded for this one.",
            ["snapshotTransitionEvidenceRows"] = snapshot.TransitionEvidence.Count,
            ["snapshotTransitionRunRows"] = snapshot.TransitionRuns.Count,
            ["rowsReadPerCall"] = "not measured",
            ["rowsReadPerCallReason"] =
                "Same missing seam as M1: neither CanonicalWorkflowPersistenceStore nor "
                + "CanonicalPersistenceProjection hands out the connection it reads on, so rows-read "
                + "cannot be counted from the test side. Rows present are reported instead and are "
                + "labelled as such.",
        };
    }

    // ------------------------------------------------------------------------------------ M3

    /// <summary>
    /// The per-phase split Wave 3 could not run. Verification and projection are timed by decorating
    /// the two collaborators <see cref="RepositoryObserver"/>'s constructor already accepts, so this
    /// needs no production change. Git is timed by issuing the identical subprocess
    /// <c>RepositoryObserver.ObserveGit</c> issues, and cross-checked against the difference between
    /// observing a workspace with and without a <c>.git</c> directory. Hashing has no seam of its own
    /// and is reported inside a labelled residual.
    /// </summary>
    private static async Task<Dictionary<string, object>> MeasureM3Async(Repository repository)
    {
        var verifier = new TimingStorageVerifier(new FileSystemStorageVerifier());
        var projection = new TimingPersistenceProjection(new CanonicalPersistenceProjection());
        var observer = new RepositoryObserver(verifier, projection);

        await observer.ObserveAsync(repository.Path, CancellationToken.None); // warm-up, discarded
        verifier.Reset();
        projection.Reset();

        var totals = new List<double>();
        const int Iterations = 5;
        for (int index = 0; index < Iterations; index++)
        {
            var watch = Stopwatch.StartNew();
            await observer.ObserveAsync(repository.Path, CancellationToken.None);
            watch.Stop();
            totals.Add(watch.Elapsed.TotalMilliseconds);
        }

        bool gitFired = Directory.Exists(Path.Combine(repository.Path, ".git"));
        double gitMs = MeasureGitStatus(repository.Path);
        double meanTotal = totals.Average();
        double meanVerify = verifier.TotalMs / Iterations;
        double meanProject = projection.TotalMs / Iterations;
        double attributedGit = gitFired && !double.IsNaN(gitMs) ? gitMs : 0d;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["observeAsyncTotalMs"] = Distribution(totals),
            ["verificationMs"] = Math.Round(meanVerify, 3),
            ["projectionMs"] = Math.Round(meanProject, 3),
            // Never NaN: System.Text.Json refuses to serialize it by default, and losing a whole
            // run's results because `git` was absent would be an absurd way to lose measurements.
            ["gitMs"] = double.IsNaN(gitMs) ? "not measured: git could not be started" : Math.Round(gitMs, 3),
            ["residualMs"] = Math.Round(meanTotal - meanVerify - meanProject - attributedGit, 3),
            ["residualContents"] = "product-file enumeration, HashExistingFiles, and observation assembly",
            ["verificationsPerObservation"] = verifier.Calls / (double)Iterations,
            ["projectionsPerObservation"] = projection.Calls / (double)Iterations,
            ["gitMeasurement"] =
                "the same `git status --porcelain=v1 --branch --untracked-files=normal` subprocess "
                + "RepositoryObserver.ObserveGit issues, run in the fixture root",
            ["gitFiredDuringObservation"] = gitFired,
            ["observationsPerCycle"] = "not measured",
            ["observationsPerCycleReason"] =
                "A multi-cycle `run` cannot be driven from this assembly: the kernel loop's collaborators "
                + "and the fake agent runtime live in LoopRelay.Cli / LoopRelay.Cli.Tests, the `run` verb "
                + "exposes no cycle bound, and no existing test drives more than one cycle. The count is "
                + "reported from a re-derived call-site census in the report instead.",
        };
    }

    private static double MeasureGitStatus(string root)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--porcelain=v1");
        startInfo.ArgumentList.Add("--branch");
        startInfo.ArgumentList.Add("--untracked-files=normal");

        var samples = new List<double>();
        for (int index = 0; index < 4; index++)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                using Process? process = Process.Start(startInfo);
                if (process is null)
                {
                    return double.NaN;
                }

                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit(5000);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return double.NaN;
            }

            watch.Stop();
            if (index > 0)
            {
                samples.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        return samples.Count == 0 ? double.NaN : samples.Average();
    }

    // ------------------------------------------------------------------------------------ M4

    /// <summary>
    /// One Execute transition carrying ten evidence candidates, measured through the two observers
    /// <see cref="CanonicalEffectWorkStore"/> already exposes for exactly this purpose: connection
    /// opens are counted from the connection observer and compiled SELECTs from an authorizer
    /// installed on each of them.
    /// </summary>
    private static async Task<Dictionary<string, object>> MeasureM4Async(
        Repository repository,
        CanonicalWorkflowPersistenceStore persistence,
        RunRecord root,
        WorkflowInstanceRecord instance)
    {
        var store = new CanonicalEffectWorkStore(repository);
        var causality = new CanonicalCausalContext(
            new WorkspaceIdentity(root.WorkspaceId),
            new RunIdentity(root.RunId),
            new WorkflowInstanceIdentity(instance.WorkflowInstanceId),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());
        var transition = new WorkflowTransitionIdentity("ApplyImplementationSlice");

        // Settlement reads the transition run it is settling, so the run must exist first.
        await persistence.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
            causality.TransitionRun.Value,
            WorkflowIdentity.Execute,
            new WorkflowStageIdentity("Implementation"),
            transition,
            TransitionDurableState.EffectsPending,
            RuntimeOutcomeKind.EffectsPending,
            DateTimeOffset.UtcNow,
            null,
            new string('a', 64),
            "Transition awaiting effect settlement.",
            ["m4"]));

        EffectIntent[] intents = Enumerable
            .Range(0, M4EffectCandidates)
            .Select(order => Intent(causality, order, $"m4-{order}"))
            .ToArray();
        // The observer is installed only after this call, so the opens AppendPlanAsync itself issues
        // to create the plan are not counted below. That is a deliberate, but previously undisclosed,
        // scope: the reported connectionOpens/selectStatementsCompiled cover settlement only, not plan
        // creation. See excludesPlanCreationOpens(Reason) in the returned dictionary.
        await store.AppendPlanAsync(intents, CancellationToken.None);

        var counter = new StoreAccessCounter();
        store.ConnectionObserverForTesting = counter.Watch;
        store.CommandObserverForTesting = counter.Command;
        var watch = Stopwatch.StartNew();
        try
        {
            await new EffectWorker(
                "m4-harness",
                store,
                new EffectExecutorRegistry([new SucceedingExecutor()]),
                new UnusedReconciler()).RunOnceAsync(CancellationToken.None);
        }
        finally
        {
            store.ConnectionObserverForTesting = null;
            store.CommandObserverForTesting = null;
        }

        watch.Stop();
        const int SettleOpensNotObserved = 1;
        bool settled = await new CanonicalEffectPlanSettlementStore(repository)
            .TrySettleAsync(causality.TransitionRun, CancellationToken.None);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["candidates"] = M4EffectCandidates,
            ["settlementWallMs"] = Math.Round(watch.Elapsed.TotalMilliseconds, 3),
            ["connectionOpens"] = counter.Connections,
            ["connectionOpensPerEffect"] = Math.Round(counter.Connections / (double)M4EffectCandidates, 3),
            ["commandsIssued"] = counter.Commands,
            ["selectStatementsCompiled"] = counter.Selects,
            ["selectStatementsPerEffect"] = Math.Round(counter.Selects / (double)M4EffectCandidates, 3),
            ["settled"] = settled,
            ["uncountedOpens"] = SettleOpensNotObserved,
            ["uncountedOpensReason"] =
                "CanonicalEffectPlanSettlementStore.TrySettleAsync (CanonicalEffectWorkStore.cs:742) is a "
                + "different type with no observer at all, and opens its connection directly rather "
                + "than through OpenAsync, so the connection observer never sees it. It is exactly one "
                + "additional open per settlement attempt, by inspection of that line.",
            ["excludesPlanCreationOpens"] = true,
            ["excludesPlanCreationOpensReason"] =
                "store.ConnectionObserverForTesting is installed after AppendPlanAsync above, so the "
                + "opens/statements/commands counted here cover EffectWorker.RunOnceAsync (settlement) "
                + "only, not the opens AppendPlanAsync itself issues to create the plan. This is a "
                + "defensible scope but was previously undisclosed where the figure is reported.",
        };
    }

    // -------------------------------------------------------------------------------- utilities

    private static string RawOutput(int index)
    {
        var builder = new StringBuilder(RawOutputBytes + 64);
        builder.Append("## Implementation slice ").Append(index).AppendLine();
        while (builder.Length < RawOutputBytes)
        {
            builder.Append("The attempt rewrote the affected call sites and recorded the evidence. ");
        }

        return builder.ToString(0, RawOutputBytes);
    }

    private static EffectIntent Intent(CanonicalCausalContext causality, int order, string key) => new(
        EffectIntentIdentity.New(),
        causality,
        $"filesystem.write.{key}",
        new EffectExecutorKey("filesystem-write"),
        "1",
        new EffectTargetDescriptor("repository", key, $"{{\"relativePath\":\"{key}\"}}"),
        $"{{\"content\":\"{key}\"}}",
        new string('d', 64),
        order,
        [],
        EffectRequiredness.BlockingLocal,
        new EffectCondition("absent", "{\"expected\":\"absent\"}"),
        new EffectCondition("present", "{\"expected\":\"present\"}"),
        "observe-file-before-repeat",
        $"filesystem-write:{key}",
        DateTimeOffset.UtcNow);

    private static Repository CreateRepository(string prefix, bool asGitRepository = false)
    {
        string path = Directory.CreateTempSubdirectory(prefix).FullName;
        if (asGitRepository)
        {
            // A real workspace is a git working copy, and RepositoryObserver.ObserveGit returns
            // early without spawning anything when `.git` is absent. Without this the M3 git phase
            // would be measured as zero and the phase split would be wrong.
            RunGit(path, "init", "--quiet");
            RunGit(path, "config", "user.email", "harness@example.invalid");
            RunGit(path, "config", "user.name", "magnitude harness");
        }

        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    /// <summary>
    /// Best-effort recursive delete of a fixture directory created by <see cref="CreateRepository"/>.
    /// A full run at the default scales creates four such fixtures (~62 MB combined at N = 10,000) and,
    /// before this existed, left every one of them behind in <c>%TEMP%</c> with nothing to remove
    /// them — the report's claim that they were "deleted afterwards" described a manual step, not
    /// something the harness did. Failures are swallowed rather than thrown: a handle that has not
    /// finished releasing on Windows should not fail a 30-minute measurement run over a cleanup step.
    /// </summary>
    private static void DeleteFixture(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort: a file handle may still be releasing.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort: same as above.
        }
    }

    private static void RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process? process = Process.Start(startInfo);
        process?.StandardOutput.ReadToEnd();
        process?.StandardError.ReadToEnd();
        process?.WaitForExit(10_000);
    }

    private static (long Files, long Bytes) TreeSize(string directory, string pattern = "*")
    {
        if (!Directory.Exists(directory))
        {
            return (0, 0);
        }

        long files = 0;
        long bytes = 0;
        foreach (string path in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
        {
            files++;
            bytes += new FileInfo(path).Length;
        }

        return (files, bytes);
    }

    private static async Task<long> ScalarLongAsync(string databasePath, string sql)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(string databasePath, string sql)
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<double> Decile(IReadOnlyList<double> samples, bool first)
    {
        if (samples.Count < 10)
        {
            return samples;
        }

        int size = Math.Max(1, samples.Count / 10);
        return first ? samples.Take(size).ToArray() : samples.TakeLast(size).ToArray();
    }

    private static Dictionary<string, object> Distribution(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal) { ["samples"] = 0 };
        }

        double[] ordered = samples.Order().ToArray();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["samples"] = ordered.Length,
            ["meanMs"] = Math.Round(ordered.Average(), 4),
            ["minMs"] = Math.Round(ordered[0], 4),
            ["medianMs"] = Math.Round(ordered[ordered.Length / 2], 4),
            ["maxMs"] = Math.Round(ordered[^1], 4),
            ["totalMs"] = Math.Round(ordered.Sum(), 3),
        };
    }

    // ------------------------------------------------------------------------------ instruments

    /// <summary>
    /// Counts what a connection really compiles, by installing a SQLite authorizer on it — the same
    /// instrument <c>DurableEffectPlanHydrationTests</c> uses, and the only statement-level hook
    /// SQLitePCLRaw exposes.
    /// </summary>
    private sealed class AuthorizerCounter
    {
        // Held for the counter's lifetime: SQLite keeps calling this while the connection lives.
        private readonly delegate_authorizer _authorizer;
        private int _actions;
        private int _selects;
        private int _reads;

        public AuthorizerCounter() => _authorizer = Authorize;

        public void Watch(SqliteConnection connection) =>
            raw.sqlite3_set_authorizer(connection.Handle, _authorizer, null);

        public Dictionary<string, object> Snapshot(double elapsedMs) =>
            new(StringComparer.Ordinal)
            {
                ["authorizerActions"] = Volatile.Read(ref _actions),
                ["selectStatementsCompiled"] = Volatile.Read(ref _selects),
                ["columnReadsAuthorized"] = Volatile.Read(ref _reads),
                ["elapsedMs"] = Math.Round(elapsedMs, 3),
            };

        private int Authorize(
            object userData, int action, utf8z first, utf8z second, utf8z database, utf8z trigger)
        {
            Interlocked.Increment(ref _actions);
            if (action == raw.SQLITE_SELECT)
            {
                Interlocked.Increment(ref _selects);
            }
            else if (action == raw.SQLITE_READ)
            {
                Interlocked.Increment(ref _reads);
            }

            return raw.SQLITE_OK;
        }
    }

    /// <summary>Counts connection opens, commands, and compiled SELECTs across one effect-store pass.</summary>
    private sealed class StoreAccessCounter
    {
        private readonly delegate_authorizer _authorizer;
        private int _connections;
        private int _commands;
        private int _selects;

        public StoreAccessCounter() => _authorizer = Authorize;

        public int Connections => Volatile.Read(ref _connections);
        public int Commands => Volatile.Read(ref _commands);
        public int Selects => Volatile.Read(ref _selects);

        public void Watch(SqliteConnection connection)
        {
            Interlocked.Increment(ref _connections);
            raw.sqlite3_set_authorizer(connection.Handle, _authorizer, null);
        }

        public void Command(SqliteCommand command) => Interlocked.Increment(ref _commands);

        private int Authorize(
            object userData, int action, utf8z first, utf8z second, utf8z database, utf8z trigger)
        {
            if (action == raw.SQLITE_SELECT)
            {
                Interlocked.Increment(ref _selects);
            }

            return raw.SQLITE_OK;
        }
    }

    private sealed class TimingStorageVerifier(IStorageVerifier _inner) : IStorageVerifier
    {
        private double _totalMs;
        private int _calls;

        public double TotalMs => Volatile.Read(ref _totalMs);
        public int Calls => Volatile.Read(ref _calls);

        public void Reset()
        {
            Volatile.Write(ref _totalMs, 0d);
            Volatile.Write(ref _calls, 0);
        }

        public async Task<StorageVerificationResult> VerifyAsync(
            string repositoryPath,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            StorageVerificationResult result = await _inner.VerifyAsync(repositoryPath, cancellationToken);
            watch.Stop();
            Volatile.Write(ref _totalMs, TotalMs + watch.Elapsed.TotalMilliseconds);
            Interlocked.Increment(ref _calls);
            return result;
        }
    }

    private sealed class TimingPersistenceProjection(ICanonicalPersistenceProjection _inner)
        : ICanonicalPersistenceProjection
    {
        private double _totalMs;
        private int _calls;

        public double TotalMs => Volatile.Read(ref _totalMs);
        public int Calls => Volatile.Read(ref _calls);

        public void Reset()
        {
            Volatile.Write(ref _totalMs, 0d);
            Volatile.Write(ref _calls, 0);
        }

        public async Task<CanonicalPersistenceReadModel> ProjectAsync(
            Repository repository,
            CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();
            CanonicalPersistenceReadModel result = await _inner.ProjectAsync(repository, cancellationToken);
            watch.Stop();
            Volatile.Write(ref _totalMs, TotalMs + watch.Elapsed.TotalMilliseconds);
            Interlocked.Increment(ref _calls);
            return result;
        }
    }

    private sealed class SucceedingExecutor : Orchestration.Effects.IEffectExecutor
    {
        public EffectExecutorKey Key => new("filesystem-write");

        public string Version => "1";

        public Task<EffectExecutionObservation> ExecuteAsync(
            EffectIntent intent,
            CancellationToken cancellationToken) =>
            Task.FromResult(new EffectExecutionObservation(
                EffectLifecycle.Succeeded, "Wrote the file.", ["file:written"], "absent", "present", true));
    }

    private sealed class UnusedReconciler : IEffectReconciler
    {
        public Task<EffectReconciliationObservation> ReconcileAsync(
            EffectIntent intent,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Reconciliation is not expected in the magnitude harness.");
    }
}
