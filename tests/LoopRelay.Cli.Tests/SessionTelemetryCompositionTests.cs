using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Services;
using LoopRelay.Agents.Models;
using LoopRelay.Cli;
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
        Cli.ISessionTelemetryRecorder recorder = Cli.SessionTelemetryComposition.CreateRecorder(
            Repo(), enabled: true, new FakeCodexUsageProbe(), new EffectiveTokenCostModel(),
            new FakeClock(), new RecordingLoopConsole());

        await recorder.RecordTurnAsync("AxiomRepo", repoPath, new SessionIdentity(Guid.NewGuid()),
            SessionRole.Decision, DateTimeOffset.UnixEpoch, null, Turn(), CancellationToken.None);

        string dir = Path.Combine(repoPath, ".LoopRelay", "telemetry");
        Assert.True(Directory.Exists(dir));
        string file = Assert.Single(Directory.EnumerateFiles(dir, "sessions.*.jsonl").ToList());

        // End-to-end through the REAL cost model + real sink: verify the row content, not just the file's
        // existence. Usage (prompt 10, output 2, cached 0) => effective = 10 + 0*0.10 + 2 = 12.0.
        string line = Assert.Single(File.ReadAllLines(file));
        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement r = doc.RootElement;
        Assert.Equal("AxiomRepo", r.GetProperty("repoName").GetString());
        Assert.Equal("Decision", r.GetProperty("sessionType").GetString());
        Assert.Equal(10, r.GetProperty("promptTokens").GetInt32());
        Assert.Equal(12.0, r.GetProperty("effectiveTokens").GetDouble());
    }

    [Fact]
    public async Task CreateRecorder_WhenDisabled_IsANullRecorderThatWritesNothing()
    {
        Cli.ISessionTelemetryRecorder recorder = Cli.SessionTelemetryComposition.CreateRecorder(
            Repo(), enabled: false, new FakeCodexUsageProbe(), new EffectiveTokenCostModel(),
            new FakeClock(), new RecordingLoopConsole());

        await recorder.RecordTurnAsync("AxiomRepo", repoPath, new SessionIdentity(Guid.NewGuid()),
            SessionRole.Decision, DateTimeOffset.UnixEpoch, null, Turn(), CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(repoPath, ".LoopRelay")));
        Assert.IsType<Cli.NullSessionTelemetryRecorder>(recorder);
    }

    [Fact]
    public void RepoName_FallsBackToTheFolderNameWhenRepositoryNameIsBlank()
    {
        Assert.Equal("AxiomRepo", Cli.SessionTelemetryComposition.RepoName(Repo("AxiomRepo")));
        string folder = Path.GetFileName(repoPath);
        Assert.Equal(folder, Cli.SessionTelemetryComposition.RepoName(new Repository { Id = Guid.NewGuid(), Name = "", Path = repoPath }));
    }
}
