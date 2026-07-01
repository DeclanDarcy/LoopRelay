using System;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class CodexStatusParserTests
{
    [Fact]
    public void Parse_ReadsBothLimitPercentsAndResetDurations()
    {
        var now = new DateTime(2026, 7, 1, 8, 0, 0);
        string text =
            "Codex usage\n" +
            "5h limit: 42% (resets 09:30 on 1 Jul)\n" +
            "Weekly limit: 88% (resets 14:00 on 5 Jul)\n";

        CodexUsageStatus? status = CodexStatusParser.Parse(text, now);

        Assert.NotNull(status);
        Assert.Equal(42, status!.FiveHourRemainingPercent);
        Assert.Equal(TimeSpan.FromMinutes(90), status.FiveHourTimeUntilReset);
        Assert.Equal(88, status.WeeklyRemainingPercent);
        Assert.Equal(new DateTime(2026, 7, 5, 14, 0, 0) - now, status.WeeklyTimeUntilReset);
    }

    [Fact]
    public void Parse_ReadsZeroPercentWhenLimitExhausted()
    {
        var now = new DateTime(2026, 7, 1, 8, 0, 0);
        string text =
            "5h limit: 0% (resets 12:00 on 1 Jul)\n" +
            "Weekly limit: 0% (resets 12:00 on 3 Jul)\n";

        CodexUsageStatus? status = CodexStatusParser.Parse(text, now);

        Assert.NotNull(status);
        Assert.Equal(0, status!.FiveHourRemainingPercent);
        Assert.Equal(0, status.WeeklyRemainingPercent);
    }

    [Fact]
    public void Parse_ReturnsNullWhenAWeeklyLineIsMissing()
    {
        var now = new DateTime(2026, 7, 1, 8, 0, 0);
        string text = "5h limit: 10% (resets 09:00 on 1 Jul)\n";

        CodexUsageStatus? status = CodexStatusParser.Parse(text, now);

        Assert.Null(status);
    }

    [Fact]
    public void Parse_ReturnsNullForUnrelatedText()
    {
        var now = new DateTime(2026, 7, 1, 8, 0, 0);

        Assert.Null(CodexStatusParser.Parse("no limits here\njust noise\n", now));
    }

    [Fact]
    public void Parse_RollsResetTimeIntoNextYearWhenItHasAlreadyPassedThisYear()
    {
        // Now is late 31 Dec; a "1 Jan" reset stamped with the current year would be in the past,
        // so the parser must roll it forward a year to yield a positive duration.
        var now = new DateTime(2026, 12, 31, 23, 0, 0);
        string text =
            "5h limit: 0% (resets 00:30 on 1 Jan)\n" +
            "Weekly limit: 5% (resets 00:30 on 1 Jan)\n";

        CodexUsageStatus? status = CodexStatusParser.Parse(text, now);

        Assert.NotNull(status);
        Assert.Equal(TimeSpan.FromMinutes(90), status!.FiveHourTimeUntilReset);
    }

    [Fact]
    public void Parse_IgnoresLeadingWhitespaceOnLimitLines()
    {
        var now = new DateTime(2026, 7, 1, 8, 0, 0);
        string text =
            "   5h limit: 100% (resets 09:00 on 1 Jul)\n" +
            "   Weekly limit: 100% (resets 09:00 on 1 Jul)\n";

        CodexUsageStatus? status = CodexStatusParser.Parse(text, now);

        Assert.NotNull(status);
        Assert.Equal(100, status!.FiveHourRemainingPercent);
        Assert.Equal(100, status.WeeklyRemainingPercent);
    }
}
