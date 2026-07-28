using LoopRelay.Cli.Services.Agents;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Agents;

public class FileSystemCodexRolloutLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-codex-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    // Writes a rollout file whose first line is a codex session_meta record.
    private string WriteRollout(string name, string cwd, DateTimeOffset started, string? dayDirectory = null)
    {
        string day = Path.Combine(root, dayDirectory ?? Path.Combine("2026", "07", "01"));
        Directory.CreateDirectory(day);
        string file = Path.Combine(day, name);
        string meta =
            "{\"timestamp\":\"" + started.UtcDateTime.ToString("o") + "\",\"type\":\"session_meta\"," +
            "\"payload\":{\"session_id\":\"" + Guid.NewGuid().ToString() + "\",\"cwd\":" +
            System.Text.Json.JsonSerializer.Serialize(cwd) + ",\"timestamp\":\"" + started.UtcDateTime.ToString("o") + "\"}}";
        File.WriteAllText(file, meta + "\n{\"type\":\"event\"}\n");
        return file;
    }

    // Codex files rollouts under sessions/YYYY/MM/DD.
    private static string DayDirectory(DateTimeOffset day) => Path.Combine(
        day.UtcDateTime.Year.ToString("D4"), day.UtcDateTime.Month.ToString("D2"), day.UtcDateTime.Day.ToString("D2"));

    [Fact]
    public void Resolve_ReturnsRolloutWhoseCwdMatchesAndStartedAfterOpen()
    {
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-a.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        string? found = new FileSystemCodexRolloutLocator(root)
            .Resolve(cwd, new DateTimeOffset(2026, 7, 1, 9, 59, 0, TimeSpan.Zero));

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_IgnoresRolloutsForADifferentCwd()
    {
        WriteRollout("rollout-a.jsonl", Path.Combine(root, "other"), new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        string? found = new FileSystemCodexRolloutLocator(root)
            .Resolve(Path.Combine(root, "work"), new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Null(found);
    }

    [Fact]
    public void Resolve_WhenMultipleMatch_ReturnsTheNewest()
    {
        string cwd = Path.Combine(root, "work");
        WriteRollout("rollout-old.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));
        string newer = WriteRollout("rollout-new.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero));

        string? found = new FileSystemCodexRolloutLocator(root)
            .Resolve(cwd, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(newer, found);
    }

    [Fact]
    public void Resolve_WhenRootMissing_ReturnsNull()
    {
        string? found = new FileSystemCodexRolloutLocator(Path.Combine(root, "nope"))
            .Resolve(root, DateTimeOffset.MinValue);
        Assert.Null(found);
    }

    [Fact]
    public void Resolve_SkipsFilesWithMalformedFirstLine()
    {
        string day = Path.Combine(root, "2026", "07", "01");
        Directory.CreateDirectory(day);
        File.WriteAllText(Path.Combine(day, "rollout-bad.jsonl"), "not json\n");

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(root, DateTimeOffset.MinValue);
        Assert.Null(found);
    }

    [Fact]
    public void Resolve_NeverOpensRolloutsFiledUnderADayDirectoryOlderThanTheSession()
    {
        DateTimeOffset opened = DateTimeOffset.UtcNow.AddMinutes(-1);
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-current.jsonl", cwd, opened.AddSeconds(30), DayDirectory(opened));
        // A decoy that would win outright on content — it claims the newest start time of all — but is filed
        // two years back. Only a locator that opened and parsed it could prefer it, so the day bound holding
        // is exactly the assertion that it was never opened.
        WriteRollout("rollout-decoy.jsonl", cwd, opened.AddHours(1), DayDirectory(opened.AddYears(-2)));

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_FindsARolloutThatAlreadyExistedWhenTheSessionOpened()
    {
        // The common case: codex creates the rollout while the app-server is still starting, and openedAtUtc
        // is only stamped once that returns — so the rollout is always slightly older than the session.
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-a.jsonl", cwd, opened.AddMinutes(-1), DayDirectory(opened));
        File.SetCreationTimeUtc(expected, opened.UtcDateTime.AddMinutes(-1));
        File.SetLastWriteTimeUtc(expected, opened.UtcDateTime.AddMinutes(-1));

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_FindsARolloutStartedJustWithinTheStartTolerance()
    {
        // Pins the accepting edge of the 5-minute StartTolerance window: a rollout that started
        // just inside it (here, one second short of the 5-minute limit) must still be matched.
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout(
            "rollout-a.jsonl", cwd, opened.AddMinutes(-5).AddSeconds(1), DayDirectory(opened));

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_RejectsARolloutStartedBeyondTheStartTolerance()
    {
        // Pins the rejecting edge of the 5-minute StartTolerance window. Without this, nothing in
        // the suite proves a rollout started outside the window is excluded -- StartTolerance
        // could be widened to TimeSpan.MaxValue and every other test here would still pass, which
        // would let a predecessor session's rollout in the same working directory be matched and
        // recorded into durable evidence.
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        WriteRollout("rollout-a.jsonl", cwd, opened.AddMinutes(-6), DayDirectory(opened));

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Null(found);
    }

    [Fact]
    public void Resolve_FindsARolloutFiledUnderThePrecedingCalendarDay()
    {
        // Codex names day directories from local time while openedAtUtc is UTC, so around midnight the two
        // disagree by a day in either direction. The day floor is set a full day back to absorb that.
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-a.jsonl", cwd, opened.AddSeconds(-30), DayDirectory(opened.AddDays(-1)));

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_SkipsFilesWhoseFileTimesPredateTheSessionWithoutOpeningThem()
    {
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string file = WriteRollout("rollout-a.jsonl", cwd, opened.AddMinutes(1), DayDirectory(opened));
        DateTime longAgo = opened.UtcDateTime.AddDays(-30);
        File.SetCreationTimeUtc(file, longAgo);
        File.SetLastWriteTimeUtc(file, longAgo);

        // Neither file time is anywhere near the session, so the file is skipped before it is opened and the
        // (contradictory) start time on its first line is never read.
        Assert.Null(new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened));
    }

    [Fact]
    public void Resolve_StillFindsARolloutFiledDirectlyInItsMonthDirectory()
    {
        // No day directory means no day for the floor to judge, so the file has to survive the walk rather
        // than fall through the gap between "this month" and "the days in it".
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string month = Path.Combine(opened.UtcDateTime.Year.ToString("D4"), opened.UtcDateTime.Month.ToString("D2"));
        string expected = WriteRollout("rollout-a.jsonl", cwd, opened.AddSeconds(30), month);

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_StillFindsARolloutFiledDirectlyInItsYearDirectory()
    {
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout(
            "rollout-a.jsonl", cwd, opened.AddSeconds(30), opened.UtcDateTime.Year.ToString("D4"));

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_StillScansDirectoriesThatAreNotNamedForADate()
    {
        // A layout the date bound cannot reason about must never silently lose a rollout: the per-file time
        // bound is what keeps those directories cheap, not exclusion.
        DateTimeOffset opened = DateTimeOffset.UtcNow;
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-a.jsonl", cwd, opened.AddSeconds(30), "archived");

        string? found = new FileSystemCodexRolloutLocator(root).Resolve(cwd, opened);

        Assert.Equal(expected, found);
    }
}
