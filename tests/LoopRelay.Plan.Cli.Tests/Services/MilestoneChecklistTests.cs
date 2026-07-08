using LoopRelay.Plan.Cli;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests;

public class MilestoneChecklistTests
{
    [Theory]
    [InlineData("- [x] a\n- [x] b", 2, 2)]
    [InlineData("- [ ] a\n- [x] b", 2, 1)]
    [InlineData("- [X] a", 1, 1)]
    [InlineData("* [x] a\n+ [x] b", 0, 0)] // non-hyphen bullets rejected
    [InlineData("```\n- [ ] fenced\n```\n- [x] real", 1, 1)] // fenced lines ignored
    [InlineData("- [-] partial\n- [/] partial", 0, 0)] // unrecognized marks ignored
    [InlineData("-[x] a", 0, 0)] // missing space after '-' rejected
    [InlineData("- [x]a", 0, 0)] // missing trailing space rejected
    [InlineData("", 0, 0)]
    [InlineData("   - [x] indented\n", 1, 1)] // TrimStart tolerates leading whitespace
    public void CountCheckboxes_MatchesCanonicalRule(string content, int total, int completed)
    {
        (int actualTotal, int actualCompleted) = Cli.MilestoneChecklist.CountCheckboxes(content);

        Assert.Equal(total, actualTotal);
        Assert.Equal(completed, actualCompleted);
    }

    [Fact]
    public void CountCheckboxes_UnterminatedFence_TreatsRestOfContentAsFenced()
    {
        (int total, int completed) = Cli.MilestoneChecklist.CountCheckboxes("```\n- [x] never counted");

        Assert.Equal(0, total);
        Assert.Equal(0, completed);
    }
}
