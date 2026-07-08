using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.ArtifactManagement;

public sealed class BundleManifestWriterTests
{
    [Fact]
    public async Task Writes_bundle_manifest_for_extracted_specs()
    {
        using var repo = new TempRepo();
        var result = BundleExtractionResult.Extracted([
            new ExtractedBundleFile(".agents/specs/a.md", "A", RoadmapHash.Sha256("A")),
        ]);

        await new BundleManifestWriter(repo.Artifacts).WriteAsync(".agents/specs/bundle-manifest.md", "Prompt", ".agents/projections/p.md", result, "Valid");

        Assert.Contains(".agents/specs/a.md", repo.Read(".agents/specs/bundle-manifest.md"), StringComparison.Ordinal);
    }
}
