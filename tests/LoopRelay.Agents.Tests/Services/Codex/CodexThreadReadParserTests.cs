using System.Text.Json;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexThreadReadParserTests
{
    [Fact]
    public void PublicMessagesRemainOrderedAndReasoningIsOmitted()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {
              "thread": {
                "id": "thread-1",
                "turns": [
                  { "items": [
                    { "type": "userMessage", "id": "u1", "role": "user", "text": "request" },
                    { "type": "reasoning", "encrypted_content": "never expose" },
                    { "type": "agentMessage", "id": "a1", "role": "assistant", "text": "answer" }
                  ]}
                ]
              }
            }
            """);

        CodexThreadReadResult result = new CodexThreadReadParser().Parse(document.RootElement, "thread-1");

        Assert.Equal(CodexThreadReadStatus.Partial, result.Status);
        Assert.Equal(["request", "answer"], result.Records.Select(record => record.Text));
        Assert.Contains(result.Omissions, omission => omission.StartsWith("hidden-reasoning", StringComparison.Ordinal));
        Assert.DoesNotContain("never expose", string.Join('|', result.Records.Select(record => record.Text)));
    }

    [Fact]
    public void MismatchedIdentityCannotProduceContent()
    {
        using JsonDocument document = JsonDocument.Parse("""
            { "thread": { "id": "other", "turns": [] } }
            """);

        CodexThreadReadResult result = new CodexThreadReadParser().Parse(document.RootElement, "thread-1");

        Assert.Equal(CodexThreadReadStatus.IdentityMismatch, result.Status);
        Assert.Empty(result.Records);
    }

    [Fact]
    public void MissingTurnsIsTypedCorruption()
    {
        using JsonDocument document = JsonDocument.Parse("""
            { "thread": { "id": "thread-1" } }
            """);

        CodexThreadReadResult result = new CodexThreadReadParser().Parse(document.RootElement, "thread-1");

        Assert.Equal(CodexThreadReadStatus.Corrupt, result.Status);
        Assert.NotNull(result.Diagnostic);
    }

    [Fact]
    public void ToolFileCompactionAndFailureRecordsAreBoundedStructuralFacts()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {
              "thread": {
                "id": "thread-1",
                "turns": [
                  { "id": "turn-1", "status": "failed", "items": [
                    { "type": "commandExecution", "id": "c1", "status": "completed", "aggregatedOutput": "SECRET RAW OUTPUT" },
                    { "type": "fileChange", "id": "f1", "status": "completed", "changes": ["private contents"] },
                    { "type": "contextCompaction", "id": "x1" }
                  ]}
                ]
              }
            }
            """);

        CodexThreadReadResult result = new CodexThreadReadParser().Parse(document.RootElement, "thread-1");

        Assert.Equal(
            ["commandExecution:status=completed", "fileChange:status=completed", "context-compaction", "turn-status:failed"],
            result.Records.Select(record => record.Text));
        Assert.DoesNotContain("SECRET RAW OUTPUT", string.Join('|', result.Records.Select(record => record.Text)));
        Assert.DoesNotContain("private contents", string.Join('|', result.Records.Select(record => record.Text)));
    }
}
