using LoopRelay.Infrastructure.Services.Git;

namespace LoopRelay.Infrastructure.Tests.Services;

public sealed class GitPorcelainTests
{
    [Fact]
    public void Rename_line_yields_both_source_and_target_paths()
    {
        IReadOnlyList<string> changed = GitPorcelain.ChangedPaths("R  docs/old.md -> docs/new.md\n");

        Assert.Equal(["docs/old.md", "docs/new.md"], changed);
    }

    [Fact]
    public void Quoted_rename_paths_are_unquoted_on_both_ends()
    {
        IReadOnlyList<string> changed = GitPorcelain.ChangedPaths(
            "R  \"docs/old name.md\" -> \"docs/new name.md\"\n");

        Assert.Equal(["docs/old name.md", "docs/new name.md"], changed);
    }

    [Fact]
    public void Backslashes_are_normalized_to_forward_slashes_on_both_rename_ends()
    {
        IReadOnlyList<string> changed = GitPorcelain.ChangedPaths(
            "R  docs\\old.md -> docs\\sub\\new.md\n M src\\file.cs\n");

        Assert.Equal(["docs/old.md", "docs/sub/new.md", "src/file.cs"], changed);
    }

    [Fact]
    public void Rename_ends_are_deduplicated_against_other_changed_paths()
    {
        IReadOnlyList<string> changed = GitPorcelain.ChangedPaths(
            " M docs/old.md\nR  docs/old.md -> docs/new.md\n M docs/new.md\n");

        Assert.Equal(["docs/old.md", "docs/new.md"], changed);
    }

    [Fact]
    public void Non_rename_lines_keep_their_single_path_behavior()
    {
        IReadOnlyList<string> changed = GitPorcelain.ChangedPaths(" M a.md\n?? b/c.md\nA  d.md\n");

        Assert.Equal(["a.md", "b/c.md", "d.md"], changed);
    }

    [Fact]
    public void Short_and_empty_lines_are_ignored()
    {
        IReadOnlyList<string> changed = GitPorcelain.ChangedPaths("\n M \n M x.md\n");

        Assert.Equal(["x.md"], changed);
    }
}
