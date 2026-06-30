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
}
