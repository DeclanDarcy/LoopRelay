using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class SessionTelemetryCompositionTests : IDisposable
{
    private readonly string repoPath = Path.Combine(Path.GetTempPath(), "cc-repo-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(repoPath)) Directory.Delete(repoPath, recursive: true); }

    private Repository Repo(string name = "AxiomRepo") =>
        new() { Id = Guid.NewGuid(), Name = name, Path = repoPath };

    private static AgentTurnResult Turn() =>
        new(1, AgentTurnState.Completed, "o", new AgentTokenUsage(10, 2, 0));

    [Fact]
    public async Task CreateRecorder_WhenEnabled_WritesUnderRepoCommandCenterTelemetry()
    {
        ISessionTelemetryRecorder recorder = SessionTelemetryComposition.CreateRecorder(
            Repo(), enabled: true, new FakeCodexUsageProbe(), new EffectiveTokenCostModel(),
            new FakeClock(), new RecordingLoopConsole());

        await recorder.RecordTurnAsync("AxiomRepo", repoPath, new SessionIdentity(Guid.NewGuid()),
            SessionRole.Decision, DateTimeOffset.UnixEpoch, null, Turn(), null, CancellationToken.None);

        string dir = Path.Combine(repoPath, ".commandcenter", "telemetry");
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

        Assert.False(Directory.Exists(Path.Combine(repoPath, ".commandcenter")));
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
