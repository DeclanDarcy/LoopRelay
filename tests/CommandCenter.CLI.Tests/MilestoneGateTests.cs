using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class MilestoneGateTests
{
    private static (MilestoneGate Gate, IArtifactStore Store, Repository Repo) NewGate()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new MilestoneGate(store, repo), store, repo);
    }

    private static string Resolve(Repository repo, string rel) =>
        ArtifactPath.ResolveRepositoryPath(repo, rel);

    [Theory]
    [InlineData("- [x] a\n- [x] b", 2, 2)]
    [InlineData("- [ ] a\n- [x] b", 2, 1)]
    [InlineData("- [X] a", 1, 1)]
    [InlineData("* [x] a\n+ [x] b", 0, 0)]              // non-hyphen bullets ignored
    [InlineData("```\n- [ ] fenced\n```\n- [x] real", 1, 1)] // fenced lines ignored
    [InlineData("- [-] partial\n- [/] partial", 0, 0)]  // unknown marks ignored
    public void CountCheckboxes_MatchesBackendRule(string content, int total, int completed)
    {
        var (t, c) = MilestoneGate.CountCheckboxes(content);
        Assert.Equal(total, t);
        Assert.Equal(completed, c);
    }

    [Fact]
    public async Task IsEpicComplete_AllMilestonesFullyChecked_ReturnsTrue()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a\n- [x] b");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m2.md"), "- [x] c");

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_OneMilestoneIncomplete_ReturnsFalse()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m2.md"), "- [ ] c");

        Assert.False(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_NoMilestoneFiles_ReturnsFalse()
    {
        var (gate, _, _) = NewGate();
        Assert.False(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_MilestoneWithZeroCheckboxes_ReturnsFalse()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "# heading only, no tasks");
        Assert.False(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_UncheckedPlanBox_BlocksEvenWhenMilestonesComplete()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "- [ ] open item in the plan");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.False(await gate.IsEpicCompleteAsync());   // aggregate: plan.md still has an unchecked box
    }

    [Fact]
    public async Task IsEpicComplete_PlanAndMilestonesAllChecked_ReturnsTrue()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "- [x] plan item");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task IsEpicComplete_PointerIndexPlanWithNoCheckboxes_DoesNotBlock()
    {
        var (gate, store, repo) = NewGate();
        // plan.md rewritten into a milestone-pointer index by ExtractMilestones => zero checkboxes.
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "# Plan\n(See ./milestones/m1.md)");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    // ----- Incremental short-circuit (timestamp-keyed) tests -----

    private static readonly DateTime T0 = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 30, 13, 0, 0, DateTimeKind.Utc);

    private static (MilestoneGate Gate, CountingStore Store, Repository Repo, Dictionary<string, DateTime> Mtimes)
        NewTrackedGate()
    {
        var store = new CountingStore(new MemoryArtifactStore());
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var mtimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        DateTime? Mtime(string path) => mtimes.TryGetValue(path, out var v) ? v : (DateTime?)null;
        return (new MilestoneGate(store, repo, Mtime), store, repo, mtimes);
    }

    [Fact]
    public async Task T1_ShortCircuit_UnchangedIncompleteFile_SkipsAllReads()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        string m2 = Resolve(repo, ".agents/milestones/m2.md");
        await store.WriteAsync(m1, "- [ ] a");      // one unchecked box
        await store.WriteAsync(m2, "- [x] b");      // fully checked
        mtimes[m1] = T0;
        mtimes[m2] = T0;

        Assert.False(await gate.IsEpicCompleteAsync());
        int readsAfterFirst = store.Reads;
        int listsAfterFirst = store.Lists;
        Assert.True(readsAfterFirst > 0);           // first call actually parsed

        // Second call, nothing changed: must short-circuit with NO new reads or lists.
        Assert.False(await gate.IsEpicCompleteAsync());
        Assert.Equal(readsAfterFirst, store.Reads);
        Assert.Equal(listsAfterFirst, store.Lists);
    }

    [Fact]
    public async Task T2_ModifiedFile_Reparses_NowComplete()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        string m2 = Resolve(repo, ".agents/milestones/m2.md");
        await store.WriteAsync(m1, "- [ ] a");
        await store.WriteAsync(m2, "- [x] b");
        mtimes[m1] = T0;
        mtimes[m2] = T0;

        Assert.False(await gate.IsEpicCompleteAsync());
        int readsAfterFirst = store.Reads;
        int listsAfterFirst = store.Lists;

        // Complete m1 and bump its mtime so the cache misses and re-parses.
        await store.WriteAsync(m1, "- [x] a");
        mtimes[m1] = T1;

        Assert.True(await gate.IsEpicCompleteAsync());
        Assert.True(store.Reads > readsAfterFirst);  // it re-parsed
        Assert.True(store.Lists > listsAfterFirst);  // full-parse path called ListAsync (short-circuit would have skipped it)
    }

    [Fact]
    public async Task T3_NoTimestamps_FullParseEveryCall_BehaviorUnchanged()
    {
        var store = new CountingStore(new MemoryArtifactStore());
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var gate = new MilestoneGate(store, repo, _ => null);   // timestamps always unavailable
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a");

        Assert.False(await gate.IsEpicCompleteAsync());
        int readsAfterFirst = store.Reads;
        Assert.False(await gate.IsEpicCompleteAsync());
        Assert.True(store.Reads > readsAfterFirst);  // no caching: re-parsed

        await store.WriteAsync(m1, "- [x] a");
        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task T4_NewIncompleteFileAfterTrackedCompletes_StillFalseThenTrue()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        string m2 = Resolve(repo, ".agents/milestones/m2.md");
        await store.WriteAsync(m1, "- [ ] a");
        mtimes[m1] = T0;

        Assert.False(await gate.IsEpicCompleteAsync());   // tracks m1 as incomplete

        // Add m2 (incomplete) while m1 is unchanged: short-circuit fires on unchanged m1
        // and must consult ONLY the mtime provider — no reads or lists touch the new m2.
        await store.WriteAsync(m2, "- [ ] b");
        mtimes[m2] = T0;
        int readsBeforeShortCircuit = store.Reads;
        int listsBeforeShortCircuit = store.Lists;
        Assert.False(await gate.IsEpicCompleteAsync());
        Assert.Equal(readsBeforeShortCircuit, store.Reads);   // short-circuit: no new reads
        Assert.Equal(listsBeforeShortCircuit, store.Lists);   // short-circuit: no new lists

        // Complete m1, bump its mtime: cache misses, full parse finds m2 incomplete.
        await store.WriteAsync(m1, "- [x] a");
        mtimes[m1] = T1;
        Assert.False(await gate.IsEpicCompleteAsync());

        // Complete m2, bump its mtime: now everything checked.
        await store.WriteAsync(m2, "- [x] b");
        mtimes[m2] = T1;
        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task T5_DeletedTrackedFile_DoesNotFalselyShortCircuit()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        string m2 = Resolve(repo, ".agents/milestones/m2.md");
        await store.WriteAsync(m1, "- [ ] a");      // unchecked -> tracked incomplete
        await store.WriteAsync(m2, "- [x] b");      // fully checked
        mtimes[m1] = T0;
        mtimes[m2] = T0;

        Assert.False(await gate.IsEpicCompleteAsync());

        // m1 vanishes: remove from store AND drop its mtime (provider now returns null for m1).
        await store.DeleteAsync(m1);
        mtimes.Remove(m1);

        // A vanished tracked file must force a correct re-parse, not a stale false.
        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task T6_DeletedTrackedPlanFile_DoesNotFalselyShortCircuit()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string planPath = Resolve(repo, ".agents/plan.md");
        string m1 = Resolve(repo, ".agents/milestones/m1.md");

        // plan.md has one unchecked box (tracked incomplete); m1 is fully checked.
        await store.WriteAsync(planPath, "- [ ] open plan item");
        await store.WriteAsync(m1, "- [x] a");
        mtimes[planPath] = T0;
        mtimes[m1] = T0;

        Assert.False(await gate.IsEpicCompleteAsync());   // plan.md tracked as incomplete

        // plan.md vanishes: remove from store AND drop its mtime so provider returns null.
        await store.DeleteAsync(planPath);
        mtimes.Remove(planPath);

        // The vanished tracked plan.md must force a correct full re-parse, not a stale false.
        Assert.True(await gate.IsEpicCompleteAsync());    // only fully-checked m1 remains
    }
}

/// <summary>
/// IArtifactStore decorator that forwards to an inner store and counts ReadAsync / ListAsync calls,
/// so tests can prove the gate's short-circuit skipped all I/O on an unchanged-and-incomplete epic.
/// </summary>
internal sealed class CountingStore(IArtifactStore inner) : IArtifactStore
{
    public int Reads { get; private set; }

    public int Lists { get; private set; }

    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);

    public Task<string?> ReadAsync(string path)
    {
        Reads++;
        return inner.ReadAsync(path);
    }

    public Task WriteAsync(string path, string content) => inner.WriteAsync(path, content);

    public Task DeleteAsync(string path) => inner.DeleteAsync(path);

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        Lists++;
        return inner.ListAsync(path, searchPattern);
    }

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => inner.ListDirectoriesAsync(path);
}
