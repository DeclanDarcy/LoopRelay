using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Cli.Tests.Services.Support;
using LoopRelay.Cli.Tests.Services.Telemetry;
using LoopRelay.Cli.Tests.Services.Usage;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Agents;

/// <summary>
/// Pins PERF-08's caller-side contract end to end: a real <see cref="GatedAgentSession"/> driving a real
/// <see cref="SessionTelemetryRecorder"/> resolves the codex rollout path on the first turn only. Nothing
/// about the rollout store is stubbed out — the store is a real directory tree on disk, and the assertions
/// are about which file the second turn names, which only a second sweep could change.
/// </summary>
public class SessionRolloutPathCachingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-cache-" + Guid.NewGuid().ToString("N"));

    public SessionRolloutPathCachingTests() => Directory.CreateDirectory(root);

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    private static SessionTelemetryRecorder Recorder(
        ICodexRolloutLocator locator, FakeSessionTelemetrySink sink, ProviderEnvironmentConfiguration? home = null) =>
        new(new FakeCodexUsageProbe(), locator, sink, new StubCostModel(), new FakeClock(),
            new RecordingLoopConsole(), home);

    private static GatedAgentSession Session(StubAgentSession inner, ISessionTelemetryRecorder recorder) =>
        new(inner, new NoOpUsageLimitDetector(), recorder, "myrepo", "/work", DateTimeOffset.UnixEpoch);

    private string WriteRollout(string day, string fileName, string threadId)
    {
        string directory = Path.Combine(root, "sessions", day);
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, fileName);
        File.WriteAllText(
            file,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"" + threadId + "\",\"cwd\":\"/work\"," +
            "\"timestamp\":\"2026-07-01T10:00:00.0000000Z\"}}\n");
        return file;
    }

    [Fact]
    public async Task SecondTurn_DoesNotConsultTheRolloutLocatorAgain()
    {
        string resolved = Path.Combine(root, "rollout.jsonl");
        File.WriteAllText(resolved, "{}\n");
        var locator = new FakeCodexRolloutLocator { Path = resolved };
        var sink = new FakeSessionTelemetrySink();
        GatedAgentSession session = Session(new StubAgentSession(threadId: null), Recorder(locator, sink));

        await session.RunTurnAsync("p1");
        await session.RunTurnAsync("p2");

        Assert.Equal(1, locator.Calls);
        Assert.Equal(2, sink.Records.Count);
        Assert.All(sink.Records, record => Assert.Equal(resolved, record.CodexLogPath));
    }

    [Fact]
    public async Task SecondTurn_KeepsTheFirstTurnsRolloutEvenWhenANewerOneAppears()
    {
        string first = WriteRollout(
            Path.Combine("2026", "07", "01"), "rollout-2026-07-01T10-00-00-thread-x.jsonl", "thread-x");
        var sink = new FakeSessionTelemetrySink();
        GatedAgentSession session = Session(
            new StubAgentSession("thread-x"),
            Recorder(new FakeCodexRolloutLocator(), sink, new ProviderEnvironmentConfiguration(root, "test")));

        await session.RunTurnAsync("p1");
        string newer = WriteRollout(
            Path.Combine("2026", "07", "02"), "rollout-2026-07-02T10-00-00-thread-x.jsonl", "thread-x");
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddHours(-1));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);
        await session.RunTurnAsync("p2");

        // A second sweep would name `newer` — the store lookup returns the newest match for a thread id.
        // Both rows naming `first` is what "resolved once per session" looks like from the outside.
        Assert.Equal(2, sink.Records.Count);
        Assert.All(sink.Records, record => Assert.Equal(first, record.CodexLogPath));
    }

    [Fact]
    public async Task SecondTurn_WhenTheCachedRolloutWasDeleted_ResolvesTheReplacement()
    {
        string first = WriteRollout(
            Path.Combine("2026", "07", "01"), "rollout-2026-07-01T10-00-00-thread-x.jsonl", "thread-x");
        var sink = new FakeSessionTelemetrySink();
        GatedAgentSession session = Session(
            new StubAgentSession("thread-x"),
            Recorder(new FakeCodexRolloutLocator(), sink, new ProviderEnvironmentConfiguration(root, "test")));

        await session.RunTurnAsync("p1");
        File.Delete(first);
        string replacement = WriteRollout(
            Path.Combine("2026", "07", "02"), "rollout-2026-07-02T10-00-00-thread-x.jsonl", "thread-x");
        await session.RunTurnAsync("p2");

        // The cache is only good while its file is: a rotated-away rollout is dropped rather than stamped
        // onto every later row, and the turn itself is untouched by any of it.
        Assert.Equal(first, sink.Records[0].CodexLogPath);
        Assert.Equal(replacement, sink.Records[1].CodexLogPath);
    }

    private sealed class StubAgentSession(string? threadId) : IAgentSession
    {
        private int turns;

        public SessionIdentity SessionId { get; } = new(Guid.NewGuid());
        public string RepositoryId => "repo";
        public SessionRole Role => SessionRole.Decision;
        public AgentSessionMode Mode => AgentSessionMode.Persistent;
        public AgentProcessState State => AgentProcessState.Running;
        public int CompletedTurns => turns;
        public AgentTokenUsage TotalUsage => new(0, 0);
        public string? ThreadId => threadId;

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentTurnResult(++turns, AgentTurnState.Completed, "out", new AgentTokenUsage(1, 1)));

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoOpUsageLimitDetector : IUsageLimitDetector
    {
        public UsageLimitHit? Detect(AgentTurnResult result) => null;

        public Task WaitOutAsync(UsageLimitHit hit, CancellationToken cancellationToken) => Task.CompletedTask;

        public void WarnRetriesExhausted(int retries) { }
    }
}
