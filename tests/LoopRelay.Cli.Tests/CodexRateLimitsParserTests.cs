using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

/// <summary>
/// Parsing the codex app-server <c>account/rateLimits/read</c> response into a <see cref="CodexUsageStatus"/>.
/// The snapshot reports capacity USED (usedPercent); the gate wants capacity REMAINING, so remaining = 100 - used.
/// <c>primary</c> is the 5h window, <c>secondary</c> is the weekly window; <c>resetsAt</c> is unix seconds.
/// </summary>
public class CodexRateLimitsParserTests
{
    // A stable reference instant so reset arithmetic is deterministic.
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_782_800_000);

    private static string Envelope(long primaryReset, int primaryUsed, long secondaryReset, int secondaryUsed) =>
        ("""{"id":2,"result":{"rateLimits":{"limitId":"codex","primary":{"usedPercent":P_USED,"windowDurationMins":300,"resetsAt":P_RESET},"secondary":{"usedPercent":S_USED,"windowDurationMins":10080,"resetsAt":S_RESET},"planType":"pro"}}}""")
            .Replace("P_USED", primaryUsed.ToString())
            .Replace("P_RESET", primaryReset.ToString())
            .Replace("S_USED", secondaryUsed.ToString())
            .Replace("S_RESET", secondaryReset.ToString());

    [Fact]
    public void Parse_MapsPrimaryToFiveHourAndSecondaryToWeekly_WithRemainingAndReset()
    {
        string json = Envelope(
            primaryReset: Now.ToUnixTimeSeconds() + 1800,   // +30m
            primaryUsed: 30,                                 // -> 70% remaining
            secondaryReset: Now.ToUnixTimeSeconds() + 7200,  // +2h
            secondaryUsed: 20);                              // -> 80% remaining

        Cli.CodexUsageStatus? status = Cli.CodexRateLimitsParser.Parse(json, Now);

        Assert.NotNull(status);
        Assert.Equal(70, status!.FiveHourRemainingPercent);
        Assert.Equal(TimeSpan.FromMinutes(30), status.FiveHourTimeUntilReset);
        Assert.Equal(80, status.WeeklyRemainingPercent);
        Assert.Equal(TimeSpan.FromHours(2), status.WeeklyTimeUntilReset);
    }

    [Fact]
    public void Parse_TreatsHundredPercentUsedAsZeroRemaining()
    {
        string json = Envelope(Now.ToUnixTimeSeconds() + 60, 100, Now.ToUnixTimeSeconds() + 60, 100);

        Cli.CodexUsageStatus? status = Cli.CodexRateLimitsParser.Parse(json, Now);

        Assert.Equal(0, status!.FiveHourRemainingPercent);
        Assert.Equal(0, status.WeeklyRemainingPercent);
    }

    [Fact]
    public void Parse_AcceptsABareSnapshotWithoutTheJsonRpcEnvelope()
    {
        string json =
            """{"primary":{"usedPercent":10,"resetsAt":0},"secondary":{"usedPercent":25,"resetsAt":0}}""";

        Cli.CodexUsageStatus? status = Cli.CodexRateLimitsParser.Parse(json, Now);

        Assert.Equal(90, status!.FiveHourRemainingPercent);
        Assert.Equal(75, status.WeeklyRemainingPercent);
    }

    [Fact]
    public void Parse_TreatsAMissingWindowAsFullCapacity_NeverAsExhausted()
    {
        // Only the 5h window is reported. The weekly window must default to full (100%), never to 0% —
        // defaulting to exhausted would wedge the loop on absent data.
        string json =
            ("""{"id":2,"result":{"rateLimits":{"primary":{"usedPercent":40,"resetsAt":P_RESET}}}}""")
                .Replace("P_RESET", (Now.ToUnixTimeSeconds() + 300).ToString());

        Cli.CodexUsageStatus? status = Cli.CodexRateLimitsParser.Parse(json, Now);

        Assert.Equal(60, status!.FiveHourRemainingPercent);
        Assert.Equal(100, status.WeeklyRemainingPercent);
        Assert.Equal(TimeSpan.Zero, status.WeeklyTimeUntilReset);
    }

    [Fact]
    public void Parse_NullResetsAtYieldsZeroTimeUntilReset()
    {
        string json =
            """{"rateLimits":{"primary":{"usedPercent":0,"resetsAt":null},"secondary":{"usedPercent":0,"resetsAt":null}}}""";

        Cli.CodexUsageStatus? status = Cli.CodexRateLimitsParser.Parse(json, Now);

        Assert.Equal(TimeSpan.Zero, status!.FiveHourTimeUntilReset);
        Assert.Equal(TimeSpan.Zero, status.WeeklyTimeUntilReset);
    }

    [Theory]
    [InlineData("""{"id":2,"result":{}}""")]                       // no rateLimits
    [InlineData("""{"id":2,"result":{"rateLimits":{}}}""")]        // rateLimits but no windows
    [InlineData("""{"rateLimits":{"primary":null,"secondary":null}}""")] // both windows null
    [InlineData("this is not json")]                                // malformed
    [InlineData("")]                                                // empty
    public void Parse_ReturnsNullWhenNoUsableRateLimitsArePresent(string json)
    {
        Assert.Null(Cli.CodexRateLimitsParser.Parse(json, Now));
    }

    [Fact]
    public void Parse_ParsesTheRealCapturedAppServerResponse()
    {
        // Verbatim id:2 line captured from a live `account/rateLimits/read` on codex-cli 0.142.5.
        string json =
            """{"id":2,"result":{"rateLimits":{"limitId":"codex","limitName":null,"primary":{"usedPercent":4,"windowDurationMins":300,"resetsAt":1782890631},"secondary":{"usedPercent":5,"windowDurationMins":10080,"resetsAt":1783440798},"credits":{"hasCredits":false,"unlimited":false,"balance":"0"},"individualLimit":null,"planType":"pro","rateLimitReachedType":null},"rateLimitResetCredits":{"availableCount":2}}}""";
        var now = DateTimeOffset.FromUnixTimeSeconds(1782890631 - 3600); // one hour before the 5h reset

        Cli.CodexUsageStatus? status = Cli.CodexRateLimitsParser.Parse(json, now);

        Assert.NotNull(status);
        Assert.Equal(96, status!.FiveHourRemainingPercent);
        Assert.Equal(TimeSpan.FromHours(1), status.FiveHourTimeUntilReset);
        Assert.Equal(95, status.WeeklyRemainingPercent);
    }
}
