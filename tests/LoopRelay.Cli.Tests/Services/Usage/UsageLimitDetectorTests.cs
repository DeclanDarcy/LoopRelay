using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services;
using Xunit;

namespace LoopRelay.Cli.Tests.Services;

public class UsageLimitDetectorTests
{
    // Captured verbatim from a real `codex exec` run on 2026-07-04 (exit code 1, stderr).
    private const string RealCodexError =
        "ERROR: You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase " +
        "more credits or try again at Jul 7th, 2026 9:13 AM.";

    private static (UsageLimitDetector Detector, FakeClock Clock, FakeUsageDelay Delay, RecordingLoopConsole Con) New(
        TimeZoneInfo? timeZone = null)
    {
        var clock = new FakeClock(); // 2026-07-01T00:00:00Z
        var delay = new FakeUsageDelay();
        var con = new RecordingLoopConsole();
        return (new UsageLimitDetector(clock, delay, con, timeZone ?? TimeZoneInfo.Utc), clock, delay, con);
    }

    private static AgentTurnResult Failed(string? diagnostics) =>
        new(0, AgentTurnState.Failed, string.Empty, new AgentTokenUsage(0, 0), diagnostics);

    [Fact]
    public void Detect_WhenTheTurnCompleted_ReturnsNull()
    {
        var t = New();

        Assert.Null(t.Detector.Detect(Turns.Completed("done")));
    }

    [Fact]
    public void Detect_WhenTheTurnFailedWithoutDiagnostics_ReturnsNull()
    {
        var t = New();

        Assert.Null(t.Detector.Detect(Failed(null)));
    }

    [Fact]
    public void Detect_WhenTheFailureIsUnrelated_ReturnsNull()
    {
        var t = New();

        Assert.Null(t.Detector.Detect(Failed("ERROR: Not inside a trusted directory.")));
    }

    [Fact]
    public void Detect_ParsesTheAdvertisedRetryTimeFromTheRealCodexError()
    {
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(RealCodexError));

        // Clock is 2026-07-01T00:00Z and the injected zone is UTC, so "Jul 7th, 2026 9:13 AM" is 6d 9h 13m out.
        Assert.NotNull(hit);
        Assert.Equal(new DateTimeOffset(2026, 7, 7, 9, 13, 0, TimeSpan.Zero), hit!.RetryAt);
        Assert.Equal(new TimeSpan(6, 9, 13, 0), hit.Wait);
    }

    [Fact]
    public void Detect_TreatsTheAdvertisedTimeAsCodexLocalViaTheInjectedTimeZone()
    {
        // codex prints the retry time as a local wall-clock time with no zone marker; the detector must
        // anchor it in the injected zone (production: TimeZoneInfo.Local), not UTC.
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("+02", TimeSpan.FromHours(2), "+02", "+02");
        var t = New(plusTwo);

        UsageLimitHit? hit = t.Detector.Detect(Failed(RealCodexError));

        Assert.Equal(new DateTimeOffset(2026, 7, 7, 9, 13, 0, TimeSpan.FromHours(2)), hit!.RetryAt);
        Assert.Equal(new TimeSpan(6, 7, 13, 0), hit.Wait);
    }

    [Fact]
    public void Detect_MatchesTheProtocolVariantWithoutTheStderrErrorPrefix()
    {
        // App-server turns surface the failure as a JSON-RPC error message, not a stderr tail — same
        // sentence, no "ERROR:" prefix.
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(
            "You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase " +
            "more credits or try again at Jul 7th, 2026 9:13 AM."));

        Assert.NotNull(hit);
        Assert.Equal(new DateTimeOffset(2026, 7, 7, 9, 13, 0, TimeSpan.Zero), hit!.RetryAt);
    }

    [Fact]
    public void Detect_FindsTheMessageInsideAMultiLineStderrTail()
    {
        // The stderr tail retains up to 8KB and codex printed the line twice in the captured run; the
        // detector must match within surrounding noise, not require an exact-line fixture.
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(
            $"Reading additional input from stdin...\n{RealCodexError}\n{RealCodexError}\n"));

        Assert.NotNull(hit);
        Assert.Equal(new DateTimeOffset(2026, 7, 7, 9, 13, 0, TimeSpan.Zero), hit!.RetryAt);
    }

    [Theory]
    [InlineData("Jul 1st, 2026 9:13 AM", 1)]
    [InlineData("Jul 2nd, 2026 9:13 AM", 2)]
    [InlineData("Jul 3rd, 2026 9:13 AM", 3)]
    [InlineData("Jul 21st, 2026 9:13 AM", 21)]
    [InlineData("Jul 23rd, 2026 9:13 AM", 23)]
    public void Detect_StripsEveryOrdinalSuffixShape(string when, int expectedDay)
    {
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed($"You've hit your usage limit. Try again at {when}."));

        Assert.Equal(new DateTimeOffset(2026, 7, expectedDay, 9, 13, 0, TimeSpan.Zero), hit!.RetryAt);
    }

    [Fact]
    public void Detect_WhenTheAdvertisedTimeJustPassed_WaitsTheMinimumFloor()
    {
        var t = New();
        t.Clock.UtcNow = new DateTimeOffset(2026, 7, 7, 9, 14, 0, TimeSpan.Zero); // 1 minute past 9:13 AM

        UsageLimitHit? hit = t.Detector.Detect(Failed(RealCodexError));

        // A just-elapsed retry time (reset-boundary race, small clock skew) must not produce a zero or
        // negative wait — that would tight-loop the retries straight through the cap.
        Assert.Equal(UsageLimitDetector.MinimumWait, hit!.Wait);
    }

    [Fact]
    public void Detect_WhenTheAdvertisedTimeIsLongPast_FallsBackToTheDefaultWait()
    {
        var t = New();
        t.Clock.UtcNow = new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero); // 47 minutes past 9:13 AM

        UsageLimitHit? hit = t.Detector.Detect(Failed(RealCodexError));

        // A long-past parse means the advertised time is stale or mis-anchored (wrong zone is whole hours
        // off) — trust it and the minimum floor would burn every retry in minutes; the fallback wait at
        // least keeps riding out a short outage.
        Assert.Null(hit!.RetryAt);
        Assert.Equal(UsageLimitDetector.FallbackWait, hit.Wait);
    }

    [Fact]
    public void Detect_AnchorsADatelessTimeToTheNextUpcomingInstant()
    {
        // codex may advertise a same-window reset as a bare time of day; "3:47 AM" printed at 23:00 means
        // TOMORROW 03:47, not a past instant today (which would collapse the wait to the 1-minute floor
        // and burn the retries hours before the real reset).
        var t = New();
        t.Clock.UtcNow = new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero);

        UsageLimitHit? hit = t.Detector.Detect(Failed("You've hit your usage limit. Try again at 3:47 AM."));

        Assert.Equal(new DateTimeOffset(2026, 7, 2, 3, 47, 0, TimeSpan.Zero), hit!.RetryAt);
        Assert.Equal(new TimeSpan(4, 47, 0), hit.Wait);
    }

    [Fact]
    public void Detect_AnchorsADatelessTimeStillAheadToToday()
    {
        var t = New();
        t.Clock.UtcNow = new DateTimeOffset(2026, 7, 1, 1, 0, 0, TimeSpan.Zero);

        UsageLimitHit? hit = t.Detector.Detect(Failed("You've hit your usage limit. Try again at 3:47 AM."));

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 3, 47, 0, TimeSpan.Zero), hit!.RetryAt);
    }

    [Fact]
    public void Detect_NormalizesLowercasePeriodStyleMeridiems()
    {
        // "9:13 a.m." would otherwise be cut at its first period by the sentence-terminated capture.
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(
            "You've hit your usage limit. Try again at Jul 7th, 2026 9:13 a.m."));

        Assert.Equal(new DateTimeOffset(2026, 7, 7, 9, 13, 0, TimeSpan.Zero), hit!.RetryAt);
    }

    [Fact]
    public void Detect_ParsesTheNewestRetryTimeWhenTheTailHoldsSeveral()
    {
        // Diagnostics can be the codex process's CUMULATIVE stderr tail: a warm session that already waited
        // out a 5h reset and then hit the weekly limit holds BOTH messages, oldest first. The newest
        // advertised time is the operative one — parsing the first would floor a past time and burn the
        // remaining retries in minutes.
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(
            "You've hit your usage limit. Try again at Jul 5th, 2026 9:00 AM.\n" +
            "You've hit your usage limit. Try again at Jul 7th, 2026 9:13 AM.\n"));

        Assert.Equal(new DateTimeOffset(2026, 7, 7, 9, 13, 0, TimeSpan.Zero), hit!.RetryAt);
    }

    [Fact]
    public void Detect_WhenTheRetryTimeIsUnparseable_FallsBackToTheDefaultWait()
    {
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(
            "You've hit your usage limit. Try again at half past never."));

        Assert.NotNull(hit);
        Assert.Null(hit!.RetryAt);
        Assert.Equal(UsageLimitDetector.FallbackWait, hit.Wait);
    }

    [Fact]
    public void Detect_WhenTheMessageOmitsTheRetryClause_FallsBackToTheDefaultWait()
    {
        var t = New();

        UsageLimitHit? hit = t.Detector.Detect(Failed(
            "You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits."));

        Assert.NotNull(hit);
        Assert.Null(hit!.RetryAt);
        Assert.Equal(UsageLimitDetector.FallbackWait, hit.Wait);
    }

    [Fact]
    public void Detect_IsSilent()
    {
        // Detection runs on every failed turn result, including ones the caller decides not to retry
        // (dead session); only WaitOutAsync may talk to the console.
        var t = New();

        t.Detector.Detect(Failed(RealCodexError));

        Assert.Empty(t.Con.Events);
    }

    [Fact]
    public async Task WaitOut_WarnsHowLongAndDelaysExactlyTheHitsWait()
    {
        var t = New();
        var hit = new UsageLimitHit(TimeSpan.FromMinutes(90), new DateTimeOffset(2026, 7, 1, 1, 30, 0, TimeSpan.Zero));

        await t.Detector.WaitOutAsync(hit, CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromMinutes(90) }, t.Delay.Delays);
        Assert.Contains(t.Con.Events, e => e.Kind == "warn" && e.Text.Contains("1h 30m") && e.Text.Contains("usage limit"));
    }

    [Fact]
    public async Task WaitOut_RendersMultiDayWaitsWithADaysComponent()
    {
        var t = New();
        var hit = new UsageLimitHit(new TimeSpan(2, 3, 4, 0), null);

        await t.Detector.WaitOutAsync(hit, CancellationToken.None);

        Assert.Contains(t.Con.Events, e => e.Kind == "warn" && e.Text.Contains("2d 3h 4m"));
    }

    [Fact]
    public async Task WaitOut_WhenTheRetryTimeWasUnparseable_SaysSoInTheWarning()
    {
        var t = New();
        var hit = new UsageLimitHit(UsageLimitDetector.FallbackWait, RetryAt: null);

        await t.Detector.WaitOutAsync(hit, CancellationToken.None);

        var warn = Assert.Single(t.Con.Events, e => e.Kind == "warn");
        Assert.Contains("retry time", warn.Text);
    }

    [Fact]
    public void WarnRetriesExhausted_SaysTheLimitOutlastedTheRetries()
    {
        // Without this, a persistently limited codex surfaces as a generic step failure and the operator
        // cannot tell "retries capped on quota" from an unrelated crash.
        var t = New();

        t.Detector.WarnRetriesExhausted(3);

        var warn = Assert.Single(t.Con.Events, e => e.Kind == "warn");
        Assert.Contains("3", warn.Text);
        Assert.Contains("usage limit", warn.Text);
    }

    [Fact]
    public async Task WaitOut_HonorsCancellation()
    {
        var t = New();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            t.Detector.WaitOutAsync(new UsageLimitHit(TimeSpan.FromMinutes(5), null), cts.Token));
    }
}
