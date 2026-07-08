using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class BundleManifestWriterTests
{
    [Fact]
    public async Task Writes_bundle_manifest_for_extracted_specs()
    {
        using var repo = new TempRepo();
        var result = Cli.BundleExtractionResult.Extracted([
            new Cli.ExtractedBundleFile(".agents/specs/a.md", "A", Cli.RoadmapHash.Sha256("A")),
        ]);

        await new Cli.BundleManifestWriter(repo.Artifacts).WriteAsync(".agents/specs/bundle-manifest.md", "Prompt", ".agents/projections/p.md", result, "Valid");

        Assert.Contains(".agents/specs/a.md", repo.Read(".agents/specs/bundle-manifest.md"), StringComparison.Ordinal);
    }
}
