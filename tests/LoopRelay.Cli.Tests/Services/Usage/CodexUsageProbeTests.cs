using System.Diagnostics;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Core.Models.Repositories;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Usage;

public class CodexUsageProbeTests
{
    private static Repository Repo() => new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    // A well-formed app-server `account/rateLimits/read` response (the id:2 line).
    private static string RateLimitsResponse(int primaryUsed, int secondaryUsed) =>
        ("""{"id":2,"result":{"rateLimits":{"primary":{"usedPercent":P_USED,"resetsAt":0},"secondary":{"usedPercent":S_USED,"resetsAt":0}}}}""")
            .Replace("P_USED", primaryUsed.ToString())
            .Replace("S_USED", secondaryUsed.ToString());

    [Fact]
    public async Task Query_ParsesTheRateLimitsJsonReturnedByTheReader()
    {
        var probe = new CodexUsageProbe(_ => Task.FromResult<string?>(RateLimitsResponse(75, 30)));

        CodexUsageStatus? status = await probe.QueryAsync(CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal(25, status!.FiveHourRemainingPercent);   // 100 - 75
        Assert.Equal(70, status.WeeklyRemainingPercent);      // 100 - 30
    }

    [Fact]
    public async Task Query_ReturnsNullWhenTheReaderYieldsNothing()
    {
        var probe = new CodexUsageProbe(_ => Task.FromResult<string?>(null));

        Assert.Null(await probe.QueryAsync(CancellationToken.None));
    }

    // --- Fail-open: any response the parser cannot turn into a snapshot must yield null, never throw. ---

    [Theory]
    [InlineData("codex is starting...\n")]                          // not JSON
    [InlineData("""{"id":2,"result":{}}""")]                        // no rateLimits object
    [InlineData("""{"id":2,"result":{"rateLimits":{}}}""")]         // rateLimits but no windows
    [InlineData("""{"id":2,"result":{"rateLimits":{"primary":null,"secondary":null}}}""")] // both windows null
    public async Task Query_FailsOpenReturningNullWhenTheResponseHasNoUsableLimits(string response)
    {
        var probe = new CodexUsageProbe(_ => Task.FromResult<string?>(response));

        Assert.Null(await probe.QueryAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Query_FailsOpenWhenTheReaderThrows()
    {
        // A bad codex binary (Win32Exception) or a codex child that dies at launch (IOException on a closed
        // stdin pipe) must NOT crash the loop — the probe swallows it and reports "unknown".
        var probe = new CodexUsageProbe(_ => throw new IOException("broken pipe"));

        Assert.Null(await probe.QueryAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Query_PropagatesCallerRequestedCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var probe = new CodexUsageProbe(ct => throw new OperationCanceledException(ct));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.QueryAsync(cts.Token));
    }

    // --- Live app-server path (production ctor): initialize, read, skip notifications, stop at id:2. ---

    [Fact]
    public async Task Query_LiveAppServer_InitializesThenReadsRateLimitsAndStops()
    {
        var process = new FakeAgentProcess(new[]
        {
            """{"id":1,"result":{"userAgent":"x","codexHome":"c","platformFamily":"windows","platformOs":"windows"}}""",
            """{"method":"remoteControl/status/changed","params":{"status":"disabled"}}""", // notification: skipped
            """{"id":2,"result":{"rateLimits":{"primary":{"usedPercent":30,"resetsAt":0},"secondary":{"usedPercent":20,"resetsAt":0}}}}""",
            """{"id":99,"result":{}}""", // must never be read
        });
        var runner = new FakeInteractiveProcessRunner(process);
        var probe = new CodexUsageProbe(runner, new FakeExecutableResolver { Executable = "codex.exe" }, Repo());

        CodexUsageStatus? status = await probe.QueryAsync(CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal(70, status!.FiveHourRemainingPercent);
        Assert.Equal(80, status.WeeklyRemainingPercent);
        Assert.Equal(3, process.LinesEmitted);              // stopped as soon as the id:2 response arrived
        Assert.Contains(process.PromptsWritten, p => p.Contains("\"method\":\"initialize\""));
        Assert.Contains(process.PromptsWritten, p => p.Contains("account/rateLimits/read"));
        Assert.Equal("codex.exe", runner.InteractiveCalls[0].FileName);
        Assert.Equal(new[] { "app-server", "--listen", "stdio://" }, runner.InteractiveCalls[0].Args);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task Query_LiveAppServer_FailsOpenWhenTheSessionHangsPastTheTimeout()
    {
        // The server answers initialize but never returns the rateLimits response; the read must time out
        // and fail open rather than wedging the orchestration loop forever.
        var process = new FakeAgentProcess(
            new[] { """{"id":1,"result":{}}""" }, hangAfterLines: true);
        var runner = new FakeInteractiveProcessRunner(process);
        var probe = new CodexUsageProbe(
            runner, new FakeExecutableResolver(), Repo(), scrapeTimeout: TimeSpan.FromMilliseconds(100));

        var sw = Stopwatch.StartNew();
        CodexUsageStatus? status = await probe.QueryAsync(CancellationToken.None);
        sw.Stop();

        Assert.Null(status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"read should have timed out quickly, took {sw.Elapsed}");
    }
}
