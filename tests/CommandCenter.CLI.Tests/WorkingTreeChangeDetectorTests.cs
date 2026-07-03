using CommandCenter.Cli;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class WorkingTreeChangeDetectorTests
{
    private static WorkingTreeChangeDetector New(FakeProcessRunner fake) =>
        new(fake, new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" });

    /// <summary>Scripts a runner whose `git status` always returns the given porcelain; everything else succeeds.</summary>
    private static FakeProcessRunner StatusRunner(string porcelain) => new()
    {
        Handler = (_, args) => args[0] == "status"
            ? FakeProcessRunner.Ok(porcelain)
            : FakeProcessRunner.Ok()
    };

    [Fact]
    public async Task AgentsFilterBoundary_ExactNameAndSubpathsFiltered_SiblingNamesAreRealChanges()
    {
        // The filter is exact-".agents"-or-".agents/"-prefix. A sibling path whose name merely STARTS
        // with ".agents" (e.g. ".agents-notes.md") is real work and must survive — collapsing the two
        // clauses to StartsWith(".agents") would classify such an iteration as "no progress" for BOTH
        // consumers: the handoff turn would claim nothing happened and CommitGate would stall the loop.
        var detector = New(StatusRunner(" M .agents\n M .agents/operational_context.md\n M .agents-notes.md\n?? .agentsx/file.md"));

        IReadOnlyList<string> changed = await detector.GetRealChangedPathsAsync();

        Assert.Equal(new[] { ".agents-notes.md", ".agentsx/file.md" }, changed);
    }
}
