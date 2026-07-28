using System.Text.Json;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class CodexRolloutRepositoryLocateTests
{
    [Fact]
    public async Task Locate_FindsByFilename_WithoutOpeningOtherFiles()
    {
        string home = TempHome();
        string day = Directory.CreateDirectory(Path.Combine(home, "sessions", "2026", "01", "01")).FullName;

        const string targetId = "019d7fc0-f5d3-7e62-910b-f439f33da46c";
        string targetPath = Path.Combine(day, $"rollout-2026-01-01T10-00-00-{targetId}.jsonl");
        string decoy1Path = Path.Combine(day, "rollout-2026-01-01T09-00-00-019d7fe6-f4f7-7010-914f-a72947da8074.jsonl");
        string decoy2Path = Path.Combine(day, "rollout-2026-01-01T11-30-00-019d8258-2038-7452-9ced-aed76cabe923.jsonl");

        // Content is irrelevant to a correct filename-only lookup: garbage bytes prove nothing was parsed.
        File.WriteAllText(targetPath, "not-valid-jsonl-content");
        File.WriteAllText(decoy1Path, "not-valid-jsonl-content");
        File.WriteAllText(decoy2Path, "not-valid-jsonl-content");

        // Hold every file open with an exclusive (non-shared) handle for the whole call. If LocateAsync
        // opens ANY of these files — target or decoy — the attempt throws a sharing-violation IOException,
        // which makes it impossible for the correct path to come back. This is load-bearing, not a counter.
        using FileStream targetLock = OpenExclusive(targetPath);
        using FileStream decoy1Lock = OpenExclusive(decoy1Path);
        using FileStream decoy2Lock = OpenExclusive(decoy2Path);

        string? found = await new CodexRolloutRepository().LocateAsync(home, targetId);

        Assert.Equal(targetPath, found);
    }

    [Fact]
    public async Task Locate_FallsBackToFirstLineProbe()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;

        const string targetId = "thread-not-in-filename";
        string targetPath = Path.Combine(sessions, "rollout.jsonl");
        await File.WriteAllTextAsync(targetPath, SessionMetaLine(targetId) + "\n{\"type\":\"event\"}\n");

        string decoyPath = Path.Combine(sessions, "other.jsonl");
        await File.WriteAllTextAsync(decoyPath, SessionMetaLine("thread-different") + "\n{\"type\":\"event\"}\n");

        string? found = await new CodexRolloutRepository().LocateAsync(home, targetId);

        Assert.Equal(targetPath, found);
    }

    [Fact]
    public async Task Locate_ReturnsNullWhenAbsent()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;
        await File.WriteAllTextAsync(Path.Combine(sessions, "rollout.jsonl"), SessionMetaLine("some-other-thread") + "\n");

        string? found = await new CodexRolloutRepository().LocateAsync(home, "missing-thread");

        Assert.Null(found);
    }

    [Fact]
    public async Task Locate_PicksNewestOnDuplicates()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;

        const string targetId = "019d9999-aaaa-bbbb-cccc-ddddeeeeffff";
        // Filenames intentionally suggest the opposite recency of actual disk mtime, so the test proves
        // the implementation ranks by real file time rather than by the timestamp text in the filename.
        string olderByName = Path.Combine(sessions, $"rollout-2026-06-01T10-00-00-{targetId}.jsonl");
        string newerByName = Path.Combine(sessions, $"rollout-2026-01-01T10-00-00-{targetId}.jsonl");
        File.WriteAllText(olderByName, "irrelevant");
        File.WriteAllText(newerByName, "irrelevant");
        File.SetLastWriteTimeUtc(olderByName, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newerByName, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        string? found = await new CodexRolloutRepository().LocateAsync(home, targetId);

        Assert.Equal(newerByName, found);
    }

    [Fact]
    public async Task Locate_PicksNewestAcrossFilenameAndProbedMatches()
    {
        string home = TempHome();
        string sessions = Directory.CreateDirectory(Path.Combine(home, "sessions")).FullName;

        const string targetId = "019daaaa-bbbb-cccc-dddd-eeeeffff0000";

        // Conforms to the naming convention, resolvable by filename alone — but it is the OLDER duplicate.
        string conformingPath = Path.Combine(sessions, $"rollout-2026-01-01T10-00-00-{targetId}.jsonl");
        File.WriteAllText(conformingPath, "irrelevant");
        File.SetLastWriteTimeUtc(conformingPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Legacy/renamed filename that does not conform to the convention, so it is only discoverable via
        // the first-line session_meta probe. It is the genuinely NEWER duplicate and must win.
        string nonConformingPath = Path.Combine(sessions, "legacy-renamed-rollout.jsonl");
        await File.WriteAllTextAsync(nonConformingPath, SessionMetaLine(targetId) + "\n{\"type\":\"event\"}\n");
        File.SetLastWriteTimeUtc(nonConformingPath, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        string? found = await new CodexRolloutRepository().LocateAsync(home, targetId);

        Assert.Equal(nonConformingPath, found);
    }

    private static FileStream OpenExclusive(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    private static string TempHome() => Directory.CreateTempSubdirectory("codex-rollout-locate-").FullName;

    private static string SessionMetaLine(string threadId) =>
        JsonSerializer.Serialize(new { type = "session_meta", payload = new { id = threadId } });
}
