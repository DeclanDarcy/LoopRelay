using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

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

        BundleExtractionResult result = new Cli.Services.BundleFileExtractor().Extract(markdown);

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

        Assert.Throws<RoadmapStepException>(() => new Cli.Services.BundleFileExtractor().Extract(markdown));
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

        Assert.Throws<RoadmapStepException>(() => new Cli.Services.BundleFileExtractor().Extract(markdown));
    }

    [Fact]
    public void No_files_is_typed_blocked_result()
    {
        BundleExtractionResult result = new Cli.Services.BundleFileExtractor().Extract("# Blocked");

        Assert.True(result.IsBlocked);
    }

    [Fact]
    public void Repository_safe_policy_extracts_candidates_without_roadmap_allowlist_semantics()
    {
        string markdown = """
        # FILE: docs/split-output.md
        A
        """;

        BundleExtractionResult result = new Cli.Services.BundleFileExtractor().Extract(markdown, BundleExtractionPolicy.RepositorySafe);

        Assert.False(result.IsBlocked);
        Assert.Equal("docs/split-output.md", Assert.Single(result.Files).Path);
    }
}
