using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class MilestoneGateTests
{
    private static (Cli.MilestoneGate Gate, IArtifactStore Store, Repository Repo) NewGate()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new Cli.MilestoneGate(store, repo), store, repo);
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
        (int t, int c, _) = Cli.MilestoneGate.CountCheckboxes(content);
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
    public async Task IsEpicComplete_IgnoresPlanCheckboxes_OnlyMilestonesCount()
    {
        var (gate, store, repo) = NewGate();
        // Agents never tick plan.md's boxes, so an unchecked plan box must NOT block completion — only
        // the milestone files count. With every milestone checked, the epic is complete despite the plan.
        await store.WriteAsync(Resolve(repo, ".agents/plan.md"), "- [ ] open item in the plan");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"), "- [x] a");

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    // ----- Incremental short-circuit (timestamp-keyed) tests -----

    private static readonly DateTime T0 = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 30, 13, 0, 0, DateTimeKind.Utc);

    private static (Cli.MilestoneGate Gate, CountingStore Store, Repository Repo, Dictionary<string, DateTime> Mtimes)
        NewTrackedGate()
    {
        var store = new CountingStore(new MemoryArtifactStore());
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var mtimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        DateTime? Mtime(string path) => mtimes.TryGetValue(path, out var v) ? v : (DateTime?)null;
        return (new Cli.MilestoneGate(store, repo, Mtime), store, repo, mtimes);
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
        var gate = new Cli.MilestoneGate(store, repo, _ => null);   // timestamps always unavailable
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

    // ----- Unticked-item collection (feeds the GenerateNoChangesHandoff prompt) -----

    [Fact]
    public async Task U1_GetUntickedItems_AggregatesAcrossFiles_FileListingThenDocumentOrder()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"),
            "# m1\n- [ ] first open\n- [x] done\n- [ ] second open");
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m2.md"),
            "- [X] done too\n- [ ] third open");

        IReadOnlyList<string> items = await gate.GetUntickedItemsAsync();

        Assert.Equal(new[] { "- [ ] first open", "- [ ] second open", "- [ ] third open" }, items);
    }

    [Fact]
    public async Task U2_GetUntickedItems_ExcludesFencedNonCheckboxUnknownMark_TrimsIndentation()
    {
        var (gate, store, repo) = NewGate();
        await store.WriteAsync(Resolve(repo, ".agents/milestones/m1.md"),
            "```\n- [ ] fenced\n```\n* [ ] star bullet\n- [-] partial\nprose line\n  - [ ] indented real  ");

        IReadOnlyList<string> items = await gate.GetUntickedItemsAsync();

        // The surviving item is stored as its TRIMMED full line — indentation and trailing spaces dropped.
        Assert.Equal(new[] { "- [ ] indented real" }, items);
    }

    [Fact]
    public async Task U3_GetUntickedItems_UnchangedStamp_ServesCachedItemsWithoutRereading()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a\n- [x] b");
        mtimes[m1] = T0;

        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());
        int readsAfterFirst = store.Reads;
        Assert.True(readsAfterFirst > 0);           // first call actually read + parsed

        // Unchanged stamp: the listing still runs (new files must stay discoverable) but the file is NOT re-read.
        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());
        Assert.Equal(readsAfterFirst, store.Reads);
    }

    [Fact]
    public async Task U4_GetUntickedItems_AdvancedStamp_Reparses_TickedBoxDisappears()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a\n- [ ] b");
        mtimes[m1] = T0;

        Assert.Equal(new[] { "- [ ] a", "- [ ] b" }, await gate.GetUntickedItemsAsync());
        int readsAfterFirst = store.Reads;

        // Ticking a box (as the work turn does) advances the stamp, which must force the re-parse.
        await store.WriteAsync(m1, "- [x] a\n- [ ] b");
        mtimes[m1] = T1;

        Assert.Equal(new[] { "- [ ] b" }, await gate.GetUntickedItemsAsync());
        Assert.True(store.Reads > readsAfterFirst);
    }

    [Fact]
    public async Task U5_GetUntickedItems_NullStamp_RereadsEveryCall()
    {
        var store = new CountingStore(new MemoryArtifactStore());
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var gate = new Cli.MilestoneGate(store, repo, _ => null);   // timestamps always unavailable
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a");

        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());
        int readsAfterFirst = store.Reads;
        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());
        Assert.True(store.Reads > readsAfterFirst);  // unknown stamp is never cached: re-read
    }

    [Fact]
    public async Task U6_EpicGateFullParse_PrimesTheUntickedCache()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a");
        mtimes[m1] = T0;

        Assert.False(await gate.IsEpicCompleteAsync());   // full parse remembers m1's items
        int readsAfterGate = store.Reads;

        // The unticked lookup is served entirely from the epic gate's parse — no new reads.
        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());
        Assert.Equal(readsAfterGate, store.Reads);
    }

    [Fact]
    public async Task U7_UntickedLookup_PrimesTheEpicGateShortCircuit()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a");
        mtimes[m1] = T0;

        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());
        int reads = store.Reads;
        int lists = store.Lists;

        // The remembered still-incomplete file lets the epic gate short-circuit with NO I/O at all.
        Assert.False(await gate.IsEpicCompleteAsync());
        Assert.Equal(reads, store.Reads);
        Assert.Equal(lists, store.Lists);
    }

    [Fact]
    public async Task U8_UntickedLookup_NeverCachesAFullyCheckedFile_EpicGateStaysSound()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        string m2 = Resolve(repo, ".agents/milestones/m2.md");
        await store.WriteAsync(m1, "- [x] a");      // fully checked — must NOT enter the incomplete cache
        await store.WriteAsync(m2, "- [ ] b");
        mtimes[m1] = T0;
        mtimes[m2] = T0;

        Assert.Equal(new[] { "- [ ] b" }, await gate.GetUntickedItemsAsync());

        // The work turn ticks m2's last box (its stamp advances; m1 is never written again). If the
        // unticked lookup had wrongly cached the fully-checked m1 as "incomplete", the epic gate would
        // short-circuit on m1's unchanged stamp and report false forever — the epic could never complete.
        await store.WriteAsync(m2, "- [x] b");
        mtimes[m2] = T1;

        Assert.True(await gate.IsEpicCompleteAsync());
    }

    [Fact]
    public async Task U9_UntickedLookup_WarmCache_StillLists_NewFileStaysDiscoverable()
    {
        var (gate, store, repo, mtimes) = NewTrackedGate();
        string m1 = Resolve(repo, ".agents/milestones/m1.md");
        await store.WriteAsync(m1, "- [ ] a");
        mtimes[m1] = T0;

        Assert.Equal(new[] { "- [ ] a" }, await gate.GetUntickedItemsAsync());   // warms m1's cache entry
        int readsAfterFirst = store.Reads;
        int listsAfterFirst = store.Lists;

        // A new milestone file appears while m1's cache entry is hot and unchanged. The lookup must
        // still run the listing (new files must stay discoverable) and surface m2's open items —
        // serving a warm-cache union without listing would hide the new milestone from the handoff.
        string m2 = Resolve(repo, ".agents/milestones/m2.md");
        await store.WriteAsync(m2, "- [ ] b");
        mtimes[m2] = T0;

        Assert.Equal(new[] { "- [ ] a", "- [ ] b" }, await gate.GetUntickedItemsAsync());
        Assert.True(store.Lists > listsAfterFirst);       // the listing ran
        Assert.Equal(readsAfterFirst + 1, store.Reads);   // only the new m2 was read; m1 served from cache
    }
}
