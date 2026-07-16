using System.Text.Json;
using LoopRelay.Agents.Services.Codex;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class CertificationFailureDiagnosisTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "looprelay-certification-diagnosis-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(root)) return;
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }

    [Theory]
    [InlineData(false, false, "no-live-provider-invoked")]
    [InlineData(true, true, "confirmed-quota-exhaustion")]
    public async Task Structured_bypasses_retain_the_case_without_opening_telemetry_or_agent(
        bool providerInvoked,
        bool quota,
        string reason)
    {
        string fixture = Fixture();
        var agent = new RecordingAgent("{}");
        var diagnoser = new CertificationFailureDiagnoser(agent);

        CertificationDiagnosisOutcome outcome = await diagnoser.DiagnoseIfNeededAsync(
            Context(fixture, providerInvoked, quota),
            CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.NotNeeded, outcome.Status.Disposition);
        Assert.Equal(reason, outcome.Status.BypassOrFailureReason);
        Assert.Equal(0, agent.Calls);
        Assert.True(Directory.Exists(Path.Combine(outcome.AttemptRecord, "retained-case")));
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "failure.json")));
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "diagnosis-status.json")));
        Assert.False(File.Exists(Path.Combine(outcome.AttemptRecord, "telemetry-reference.json")));
        Assert.False(File.Exists(Path.Combine(outcome.AttemptRecord, "diagnosis.json")));
    }

    [Fact]
    public async Task Successful_result_is_rejected_before_retention_or_diagnostic_work()
    {
        string fixture = Fixture();
        var agent = new RecordingAgent("{}");

        await Assert.ThrowsAsync<ArgumentException>(() => new CertificationFailureDiagnoser(agent)
            .DiagnoseIfNeededAsync(
                Context(fixture, true, false) with { Classification = CertificationClassification.Passed },
                CancellationToken.None));

        Assert.Equal(0, agent.Calls);
        Assert.False(Directory.Exists(Path.Combine(root, "evidence")));
    }

    [Fact]
    public async Task Motivating_wrong_path_is_diagnosed_from_one_exact_failed_turn()
    {
        string fixture = Fixture();
        Directory.CreateDirectory(Path.Combine(fixture, "agents", "handoffs"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture, "agents", "handoffs", "handoff.md"),
            "# Handoff\n");
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1",
            "Write agents/handoffs/handoff.md",
            "wrote agents/handoffs/handoff.md successfully");
        string unrelated = Rollout(codexHome, "thread-nearby", "turn-nearby", "unrelated", "unrelated");
        Telemetry(fixture, "unrelated-invocation", "thread-nearby", "turn-nearby", unrelated);
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);
        Telemetry(fixture, "invocation-1", "thread-nearby", "turn-nearby", unrelated, role: "diagnosis");
        var agent = new RecordingAgent(DiagnosisJson("invocation-1"));
        var diagnoser = new CertificationFailureDiagnoser(agent);

        CertificationDiagnosisOutcome outcome = await diagnoser.DiagnoseIfNeededAsync(
            Context(fixture, providerInvoked: true, quota: false),
            CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Completed, outcome.Status.Disposition);
        Assert.Equal(1, agent.Calls);
        Assert.Equal(CertificationRolloutResolutionMethod.ExactThread,
            outcome.Diagnosis!.TelemetryResolution.Method);
        Assert.Equal("thread-1", outcome.Diagnosis.TelemetryResolution.ProviderThreadId);
        Assert.Equal("turn-1", outcome.Diagnosis.TelemetryResolution.ProviderTurnId);
        Assert.Contains(outcome.Diagnosis.Facts,
            fact => fact.Text.Contains("required .agents/handoffs/handoff.md", StringComparison.Ordinal));
        Assert.Contains("leading dot", outcome.Diagnosis.FirstObservedContractDivergence!.Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "session-segment.private.jsonl")));
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "diagnosis.json")));
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "diagnosis.md")));
        Assert.False(Directory.Exists(Path.Combine(outcome.AttemptRecord, ".diagnostic-scratch")));
        Assert.True(File.Exists(Path.Combine(
            outcome.AttemptRecord, "retained-case", "agents", "handoffs", "handoff.md")));
        Assert.False(File.Exists(Path.Combine(
            outcome.AttemptRecord, "retained-case", ".agents", "handoffs", "handoff.md")));
    }

    [Fact]
    public async Task Exact_and_recorded_rollout_disagreement_is_ambiguous_and_opens_neither()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string exact = Rollout(codexHome, "thread-1", "turn-1", "exact", "exact");
        string other = Rollout(codexHome, "thread-2", "turn-2", "other", "other");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", other);
        var agent = new RecordingAgent(DiagnosisJson("invocation-1"));

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(agent)
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Unavailable, outcome.Status.Disposition);
        Assert.Equal(CertificationTelemetryResolutionStatus.Ambiguous,
            outcome.Diagnosis!.TelemetryResolution.Status);
        Assert.Equal(0, agent.Calls);
        Assert.False(File.Exists(Path.Combine(outcome.AttemptRecord, "session-segment.private.jsonl")));
        Assert.NotEqual(exact, other);
    }

    [Fact]
    public async Task Partial_rollout_supplies_complete_bounded_events_with_an_explicit_status()
    {
        string fixture = Fixture();
        Directory.CreateDirectory(Path.Combine(fixture, "agents", "handoffs"));
        await File.WriteAllTextAsync(Path.Combine(fixture, "agents", "handoffs", "handoff.md"), "# Handoff\n");
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "Write agents/handoffs/handoff.md", "success");
        await File.AppendAllTextAsync(rollout, "{malformed-tail");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(
                new RecordingAgent(DiagnosisJson("invocation-1")))
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Completed, outcome.Status.Disposition);
        Assert.Equal(CertificationTelemetryResolutionStatus.Partial,
            outcome.Diagnosis!.TelemetryResolution.Status);
        Assert.Contains("truncated-tail", outcome.Diagnosis.TelemetryResolution.Warnings);
    }

    [Fact]
    public async Task Corrupt_middle_rollout_is_unavailable_and_not_passed_to_the_agent()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "call", "output");
        string[] lines = await File.ReadAllLinesAsync(rollout);
        await File.WriteAllLinesAsync(rollout, lines.Take(3).Concat(["{malformed-middle", lines[3], lines[4]]));
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);
        var agent = new RecordingAgent(DiagnosisJson("invocation-1"));

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(agent)
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Unavailable, outcome.Status.Disposition);
        Assert.Equal(CertificationTelemetryResolutionStatus.Corrupt,
            outcome.Diagnosis!.TelemetryResolution.Status);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task Permission_denied_exact_resolution_is_explicit_and_recorded_path_does_not_override_it()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "call", "output");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);
        var agent = new RecordingAgent(DiagnosisJson("invocation-1"));
        var resolver = new StubRolloutResolver(new CodexRolloutReadResult(
            CodexRolloutReadStatus.PermissionDenied,
            "thread-1",
            rollout,
            [],
            null,
            null,
            [],
            "UnauthorizedAccessException"));

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(
                agent,
                new CertificationPrivateRolloutReader(),
                resolver)
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationTelemetryResolutionStatus.PermissionDenied,
            outcome.Diagnosis!.TelemetryResolution.Status);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task Missing_exact_telemetry_is_reported_absent_without_guessing_by_time_or_workspace()
    {
        string fixture = Fixture();
        var agent = new RecordingAgent(DiagnosisJson("invocation-1"));

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(agent)
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationTelemetryResolutionStatus.Absent,
            outcome.Diagnosis!.TelemetryResolution.Status);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task Missing_recorded_turn_is_distinct_and_no_adjacent_turn_is_substituted()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "adjacent-turn", "adjacent", "adjacent");
        Telemetry(fixture, "invocation-1", "thread-1", "missing-turn", rollout);
        var agent = new RecordingAgent(DiagnosisJson("invocation-1"));

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(agent)
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Unavailable, outcome.Status.Disposition);
        Assert.Equal(CertificationTelemetryResolutionStatus.TurnAbsent,
            outcome.Diagnosis!.TelemetryResolution.Status);
        Assert.Equal(0, agent.Calls);
    }

    [Fact]
    public async Task Allowed_recorded_path_is_used_only_when_exact_thread_resolution_is_absent()
    {
        string fixture = Fixture();
        Directory.CreateDirectory(Path.Combine(fixture, "agents", "handoffs"));
        await File.WriteAllTextAsync(Path.Combine(fixture, "agents", "handoffs", "handoff.md"), "# Handoff\n");
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "different-thread", "turn-1",
            "Write agents/handoffs/handoff.md", "success");
        Telemetry(fixture, "invocation-1", "missing-exact-thread", "turn-1", rollout);

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(
                new RecordingAgent(DiagnosisJson("invocation-1")))
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Completed, outcome.Status.Disposition);
        Assert.Equal(CertificationRolloutResolutionMethod.RecordedPath,
            outcome.Diagnosis!.TelemetryResolution.Method);
    }

    [Fact]
    public async Task Private_segment_pairs_calls_redacts_secrets_and_excludes_hidden_reasoning()
    {
        string codexHome = Path.Combine(root, "codex-home");
        string path = Path.Combine(codexHome, "sessions", "2026", "07", "15", "rollout-thread.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "private.txt");
        string[] lines =
        [
            JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = "thread-1" } }),
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started", turn_id = "turn-1" } }),
            JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "reasoning", encrypted_content = "never expose" } }),
            JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "function_call", name = "write_file", call_id = "call-1", arguments = $"path=.agents/handoffs/handoff.md password=supersecret profile={profilePath}" } }),
            JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "function_call_output", call_id = "call-1", output = "success" } }),
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete", turn_id = "turn-1" } }),
        ];
        await File.WriteAllLinesAsync(path, lines);

        CertificationPrivateSessionSegment segment = await new CertificationPrivateRolloutReader()
            .ReadAsync(path, "turn-1", partialFromResolution: false, CancellationToken.None);

        Assert.True(segment.TurnFound);
        CertificationPrivateSessionEvent call = Assert.Single(segment.Events, item => item.Kind == "tool-call");
        CertificationPrivateSessionEvent output = Assert.Single(segment.Events, item => item.Kind == "tool-output");
        Assert.Equal(call.EventOrdinal, output.PairedEventOrdinal);
        Assert.Contains(".agents/handoffs/handoff.md", call.Content, StringComparison.Ordinal);
        Assert.Contains("<REDACTED>", call.Content, StringComparison.Ordinal);
        Assert.Contains("<USER_PROFILE>", call.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("supersecret", call.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("never expose", string.Join('\n', segment.Events.Select(item => item.Content)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Private_reader_excludes_unrelated_turns_and_records_event_limit_truncation()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "bounded-rollout.jsonl");
        var lines = new List<string>
        {
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started", turn_id = "other" } }),
            JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "function_call", call_id = "other-call", arguments = "UNRELATED" } }),
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete", turn_id = "other" } }),
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started", turn_id = "target" } }),
        };
        for (int index = 0; index < 8; index++)
            lines.Add(JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "function_call", call_id = $"target-{index}", arguments = $"TARGET-{index}" } }));
        lines.Add(JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete", turn_id = "target" } }));
        await File.WriteAllLinesAsync(path, lines);

        CertificationPrivateSessionSegment segment = await new CertificationPrivateRolloutReader(maxEvents: 3)
            .ReadAsync(path, "target", false, CancellationToken.None);

        Assert.True(segment.Truncated);
        Assert.Equal(3, segment.Events.Count);
        Assert.DoesNotContain("UNRELATED", string.Join('\n', segment.Events.Select(item => item.Content)),
            StringComparison.Ordinal);
        Assert.All(segment.Events, item => Assert.Contains("TARGET", item.Content, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Malformed_agent_output_preserves_failure_and_records_unavailable()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "call", "output");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(
                new RecordingAgent("not-json"))
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Unavailable, outcome.Status.Disposition);
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "failure.json")));
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "diagnosis.json")));
    }

    [Fact]
    public async Task Explicit_request_reuses_retained_session_evidence_without_rerunning_the_fixture()
    {
        string fixture = Fixture();
        Directory.CreateDirectory(Path.Combine(fixture, "agents", "handoffs"));
        await File.WriteAllTextAsync(Path.Combine(fixture, "agents", "handoffs", "handoff.md"), "# Handoff\n");
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "Write agents/handoffs/handoff.md", "success");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);
        CertificationDiagnosisOutcome first = await new CertificationFailureDiagnoser(
                new RecordingAgent(DiagnosisJson("invocation-1")))
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);
        File.Delete(rollout);
        var replayAgent = new RecordingAgent(DiagnosisJson("invocation-1"));
        CertificationFailureContext replay = Context(
            Path.Combine(first.AttemptRecord, "retained-case"),
            true,
            false) with
        {
            ExplicitRequest = true,
            ExistingAttemptRecord = first.AttemptRecord,
        };

        CertificationDiagnosisOutcome second = await new CertificationFailureDiagnoser(replayAgent)
            .DiagnoseIfNeededAsync(replay, CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Completed, second.Status.Disposition);
        Assert.Equal(1, replayAgent.Calls);
        Assert.False(File.Exists(rollout));
    }

    [Fact]
    public async Task Agent_write_outside_scratch_is_rejected_and_retained_case_stays_unchanged()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "call", "output");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(new EscapingAgent())
            .DiagnoseIfNeededAsync(Context(fixture, true, false), CancellationToken.None);

        Assert.Equal(CertificationDiagnosisDisposition.Unavailable, outcome.Status.Disposition);
        Assert.Equal("fixture\n", await File.ReadAllTextAsync(
            Path.Combine(outcome.AttemptRecord, "retained-case", "README.md")));
    }

    [Fact]
    public async Task Operator_cancellation_preserves_failure_and_records_terminal_unavailable()
    {
        string fixture = Fixture();
        string codexHome = Path.Combine(root, "codex-home");
        string rollout = Rollout(codexHome, "thread-1", "turn-1", "call", "output");
        Telemetry(fixture, "invocation-1", "thread-1", "turn-1", rollout);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        CertificationDiagnosisOutcome outcome = await new CertificationFailureDiagnoser(new WaitingAgent())
            .DiagnoseIfNeededAsync(Context(fixture, true, false), cancellation.Token);

        Assert.Equal(CertificationDiagnosisDisposition.Unavailable, outcome.Status.Disposition);
        Assert.Equal("operator-cancelled", outcome.Status.BypassOrFailureReason);
        Assert.True(File.Exists(Path.Combine(outcome.AttemptRecord, "failure.json")));
    }

    [Fact]
    public async Task Passing_attempt_cannot_overwrite_an_existing_failed_attempt()
    {
        string fixture = Fixture();
        CertificationFailureContext context = Context(fixture, false, false);
        var diagnoser = new CertificationFailureDiagnoser(new RecordingAgent("{}"));
        CertificationDiagnosisOutcome first = await diagnoser.DiagnoseIfNeededAsync(context, CancellationToken.None);
        string original = await File.ReadAllTextAsync(Path.Combine(first.AttemptRecord, "failure.json"));

        await Assert.ThrowsAsync<CertificationRetentionException>(() =>
            diagnoser.DiagnoseIfNeededAsync(context, CancellationToken.None));

        Assert.Equal(original, await File.ReadAllTextAsync(Path.Combine(first.AttemptRecord, "failure.json")));
    }

    [Theory]
    [InlineData(CertificationDiagnosisDisposition.NotNeeded, false)]
    [InlineData(CertificationDiagnosisDisposition.Completed, true)]
    [InlineData(CertificationDiagnosisDisposition.Inconclusive, true)]
    [InlineData(CertificationDiagnosisDisposition.Unavailable, true)]
    public void Repeat_guard_requires_a_terminal_diagnostic_attempt(
        CertificationDiagnosisDisposition disposition,
        bool expected)
    {
        var status = new CertificationDiagnosisStatus(disposition, "id", null, DateTimeOffset.UtcNow);
        Assert.Equal(expected, CertificationRepeatGuard.MayAutomaticallyAdvance(
            new CertificationDiagnosisOutcome(status, root)));
    }

    private string Fixture()
    {
        string fixture = Path.Combine(root, "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, ".agents"));
        File.WriteAllText(Path.Combine(fixture, "README.md"), "fixture\n");
        return fixture;
    }

    private CertificationFailureContext Context(string fixture, bool providerInvoked, bool quota) => new(
        "invocation-1",
        providerInvoked,
        quota ? CertificationClassification.ProviderRegression : CertificationClassification.ProductRegression,
        quota,
        "Certification required .agents/handoffs/handoff.md and reported it absent.",
        quota ? ["used-percent:100", "last-agent-message:null"] : ["required-path:.agents/handoffs/handoff.md"],
        quota ? "Wait until provider quota resets." : null,
        new
        {
            classification = quota ? "ProviderRegression" : "ProductRegression",
            failedTransition = "GenerateHandoff",
            requiredPath = ".agents/handoffs/handoff.md",
        },
        root,
        fixture,
        Path.Combine(root, "codex-home"),
        "codex",
        [],
        "GenerateHandoff");

    private static void Telemetry(
        string fixture,
        string invocation,
        string thread,
        string turn,
        string rollout,
        string role = "product")
    {
        string directory = Path.Combine(fixture, ".LoopRelay", "telemetry");
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "sessions.2026-07-15.0000.jsonl"),
            JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow,
                sessionId = "cli-session-1",
                turnIndex = 1,
                providerThreadId = thread,
                providerTurnId = turn,
                codexLogPath = rollout,
                certificationInvocationId = invocation,
                invocationRole = role,
            }) + "\n");
    }

    private static string Rollout(
        string codexHome,
        string thread,
        string turn,
        string call,
        string output)
    {
        string path = Path.Combine(codexHome, "sessions", "2026", "07", "15",
            $"rollout-{thread}.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path,
        [
            JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = thread } }),
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started", turn_id = turn } }),
            JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "function_call", name = "write_file", call_id = "call-1", arguments = call } }),
            JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "function_call_output", call_id = "call-1", output } }),
            JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete", turn_id = turn } }),
        ]);
        return path;
    }

    private static string DiagnosisJson(string invocation) => JsonSerializer.Serialize(new
    {
        disposition = "Completed",
        invocationId = invocation,
        summary = "The GenerateHandoff path-writing behavior wrote the handoff to the wrong relative path.",
        facts = new object[]
        {
            new
            {
                text = "Certification required .agents/handoffs/handoff.md and reported it absent.",
                citations = new[] { new { source = "failure", location = "input/failure.json#/requiredPath" } },
            },
            new
            {
                text = "The failed turn records a successful write to agents/handoffs/handoff.md.",
                citations = new[] { new { source = "session", location = "rollout#line:3" } },
            },
            new
            {
                text = "The retained case contains agents/handoffs/handoff.md.",
                citations = new[] { new { source = "retained-case", location = "input/retained-case/agents/handoffs/handoff.md" } },
            },
            new
            {
                text = "The retained case lacks .agents/handoffs/handoff.md.",
                citations = new[] { new { source = "retained-case", location = "retained-case-observations.json#/path=.agents/handoffs/handoff.md" } },
            },
        },
        inferences = new[]
        {
            new
            {
                text = "Inference: the write path diverged from the required contract.",
                citations = new[]
                {
                    new { source = "failure", location = "input/failure.json#/requiredPath" },
                    new { source = "session", location = "rollout#line:3" },
                },
            },
        },
        missingEvidence = new[] { "The bounded evidence does not prove why the path was changed." },
        firstObservedContractDivergence = new
        {
            text = "The first observed contract divergence is omission of the leading dot in the write path.",
            citations = new[]
            {
                new { source = "failure", location = "input/failure.json#/requiredPath" },
                new { source = "session", location = "rollout#line:3" },
            },
        },
    });

    private sealed class RecordingAgent(string response) : ICertificationDiagnosticAgent
    {
        public int Calls { get; private set; }

        public Task<string> AnalyzeAsync(
            CertificationDiagnosticRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Assert.True(Directory.Exists(Path.Combine(request.ScratchRoot, "input")));
            return Task.FromResult(response);
        }
    }

    private sealed class EscapingAgent : ICertificationDiagnosticAgent
    {
        public Task<string> AnalyzeAsync(
            CertificationDiagnosticRequest request,
            CancellationToken cancellationToken)
        {
            string retained = Path.Combine(
                Path.GetDirectoryName(request.ScratchRoot)!,
                "retained-case",
                "README.md");
            File.WriteAllText(retained, "mutated\n");
            return Task.FromResult("{}");
        }
    }

    private sealed class WaitingAgent : ICertificationDiagnosticAgent
    {
        public async Task<string> AnalyzeAsync(
            CertificationDiagnosticRequest request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "{}";
        }
    }

    private sealed class StubRolloutResolver(CodexRolloutReadResult result) : ICertificationRolloutResolver
    {
        public Task<CodexRolloutReadResult> ReadExactAsync(
            string codexHome,
            string threadId,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
