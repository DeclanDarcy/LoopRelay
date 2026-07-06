using LoopRelay.Roadmap.Cli;
using BundleFileExtractor = LoopRelay.Roadmap.Cli.BundleFileExtractor;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class BundleFileExtractorTests
{
    [Fact]
    public void Extracts_multiple_spec_files()
    {
        string markdown = """
        # FILE: .agents/specs/a.md
        A

        # FILE: .agents/specs/b.md
        B
        """;

        Cli.BundleExtractionResult result = new BundleFileExtractor().Extract(markdown);

        Assert.False(result.IsBlocked);
        Assert.Equal(2, result.Files.Count);
    }

    [Fact]
    public void Rejects_path_traversal()
    {
        string markdown = """
        # FILE: .agents/specs/../evil.md
        A
        """;

        Assert.Throws<Cli.RoadmapStepException>(() => new BundleFileExtractor().Extract(markdown));
    }

    [Fact]
    public void Rejects_duplicate_paths()
    {
        string markdown = """
        # FILE: .agents/specs/a.md
        A
        # FILE: .agents/specs/a.md
        B
        """;

        Assert.Throws<Cli.RoadmapStepException>(() => new BundleFileExtractor().Extract(markdown));
    }

    [Fact]
    public void No_files_is_typed_blocked_result()
    {
        Cli.BundleExtractionResult result = new BundleFileExtractor().Extract("# Blocked");

        Assert.True(result.IsBlocked);
    }

    [Fact]
    public void Repository_safe_policy_extracts_candidates_without_roadmap_allowlist_semantics()
    {
        string markdown = """
        # FILE: docs/split-output.md
        A
        """;

        Cli.BundleExtractionResult result = new BundleFileExtractor().Extract(markdown, Cli.BundleExtractionPolicy.RepositorySafe);

        Assert.False(result.IsBlocked);
        Assert.Equal("docs/split-output.md", Assert.Single(result.Files).Path);
    }
}
