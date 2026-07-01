using System;
using System.Text.Json;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class SessionTelemetryRecordTests
{
    private static SessionTelemetryRecord Sample(string? path = "/logs/rollout.jsonl",
        int? pre5h = 55, int? post5h = 54) =>
        new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            "myrepo", path, "sid-1", "Decision", 2,
            100, 20, 30, 97.0, pre5h, post5h, 80, 79);

    [Fact]
    public void SerializesToSingleCamelCaseJsonLine()
    {
        string json = JsonSerializer.Serialize(Sample(), SessionTelemetryJson.Options);

        Assert.DoesNotContain("\n", json);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        Assert.Equal("myrepo", r.GetProperty("repoName").GetString());
        Assert.Equal("/logs/rollout.jsonl", r.GetProperty("codexLogPath").GetString());
        Assert.Equal("Decision", r.GetProperty("sessionType").GetString());
        Assert.Equal(2, r.GetProperty("turnIndex").GetInt32());
        Assert.Equal(100, r.GetProperty("promptTokens").GetInt32());
        Assert.Equal(30, r.GetProperty("cachedTokens").GetInt32());
        Assert.Equal(97.0, r.GetProperty("effectiveTokens").GetDouble());
        Assert.Equal(55, r.GetProperty("preFiveHourPercent").GetInt32());
        Assert.Equal(80, r.GetProperty("preWeeklyPercent").GetInt32());
    }

    [Fact]
    public void EmitsNullsForAbsentCapacityAndPath()
    {
        string json = JsonSerializer.Serialize(Sample(path: null, pre5h: null, post5h: null),
            SessionTelemetryJson.Options);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        Assert.Equal(JsonValueKind.Null, r.GetProperty("codexLogPath").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("preFiveHourPercent").ValueKind);
    }
}
