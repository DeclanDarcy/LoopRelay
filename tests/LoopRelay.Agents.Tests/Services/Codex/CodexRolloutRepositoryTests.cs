using System.Text.Json;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexRolloutRepositoryTests
{
    [Fact]
    public async Task ExactThreadIdWinsOverNewerSameDirectoryRollout()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions", "2026", "01")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sessions, "older.jsonl"), Rollout("thread-target", "target reply"));
        string newer = Path.Combine(sessions, "newer.jsonl");
        await File.WriteAllTextAsync(newer, Rollout("thread-other", "wrong reply"));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow.AddHours(1));

        CodexRolloutReadResult result = await new CodexRolloutRepository().ReadExactAsync(home, "thread-target");

        Assert.Equal(CodexRolloutReadStatus.Complete, result.Status);
        Assert.Single(result.Records);
        Assert.Equal("target reply", result.Records[0].Text);
        Assert.DoesNotContain(result.Records, record => record.Text == "wrong reply");
    }

    [Fact]
    public async Task TruncatedTailReturnsPartialWithLastVerifiedBoundary()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sessions, "rollout.jsonl"),
            Rollout("thread-1", "reply").TrimEnd('\n') + "\n{\"type\":\"response_item\"");

        CodexRolloutReadResult result = await new CodexRolloutRepository().ReadExactAsync(home, "thread-1");

        Assert.Equal(CodexRolloutReadStatus.Partial, result.Status);
        Assert.Equal("line:2", result.VerifiedBoundary);
        Assert.Contains("truncated-tail", result.Omissions);
    }

    [Fact]
    public async Task MalformedMiddleNeverPassesAsPartialOrComplete()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        string content = Rollout("thread-1", "first").TrimEnd('\n')
            + "\nnot-json\n"
            + JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "agent_message", message = "later" } });
        await File.WriteAllTextAsync(Path.Combine(sessions, "rollout.jsonl"), content);

        CodexRolloutReadResult result = await new CodexRolloutRepository().ReadExactAsync(home, "thread-1");

        Assert.Equal(CodexRolloutReadStatus.Corrupt, result.Status);
        Assert.Contains("malformed-middle", result.Omissions);
    }

    [Fact]
    public async Task DuplicateExactIdsAreAmbiguous()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sessions, "one.jsonl"), Rollout("thread-1", "one"));
        await File.WriteAllTextAsync(Path.Combine(sessions, "two.jsonl"), Rollout("thread-1", "two"));

        CodexRolloutReadResult result = await new CodexRolloutRepository().ReadExactAsync(home, "thread-1");

        Assert.Equal(CodexRolloutReadStatus.Ambiguous, result.Status);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task EncryptedReasoningIsOmittedFromPublicRecords()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        string content = JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = "thread-1" } }) + "\n"
            + JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "reasoning", encrypted_content = "secret" } });
        await File.WriteAllTextAsync(Path.Combine(sessions, "rollout.jsonl"), content);

        CodexRolloutReadResult result = await new CodexRolloutRepository().ReadExactAsync(home, "thread-1");

        Assert.Empty(result.Records);
        Assert.Contains("hidden-reasoning", result.Omissions);
    }

    [Fact]
    public async Task ToolRecordsBecomeBoundedFactsWithoutRawArgumentsOrOutput()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        string content = JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = "thread-1" } }) + "\n"
            + JsonSerializer.Serialize(new
            {
                type = "response_item",
                payload = new { type = "function_call", id = "tool-1", arguments = "SECRET ARGUMENTS" },
            }) + "\n"
            + JsonSerializer.Serialize(new
            {
                type = "event_msg",
                payload = new { type = "exec_command_end", output = "SECRET OUTPUT" },
            });
        await File.WriteAllTextAsync(Path.Combine(sessions, "rollout.jsonl"), content);

        CodexRolloutReadResult result = await new CodexRolloutRepository().ReadExactAsync(home, "thread-1");

        Assert.Equal(["function_call:recorded", "exec_command_end:recorded"],
            result.Records.Select(record => record.Text));
        string recovered = string.Join('|', result.Records.Select(record => record.Text));
        Assert.DoesNotContain("SECRET", recovered);
    }

    private static string TempHome() => Directory.CreateTempSubdirectory("codex-rollout-repository-").FullName;

    private static string Rollout(string threadId, string reply) =>
        JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = threadId } }) + "\n"
        + JsonSerializer.Serialize(new
        {
            type = "response_item",
            payload = new
            {
                type = "message",
                role = "assistant",
                content = new[] { new { type = "output_text", text = reply } },
            },
        }) + "\n";
}
