using LoopRelay.Agents.Services.Codex.Compatibility;

namespace LoopRelay.Agents.Compatibility.Tests;

public sealed class CodexCompatibilityManifestCachingTests
{
    [Fact]
    public void LoadEmbedded_ReturnsSameInstanceAcrossCalls()
    {
        CodexCompatibilityManifest first = CodexCompatibilityManifest.LoadEmbedded();
        CodexCompatibilityManifest second = CodexCompatibilityManifest.LoadEmbedded();

        Assert.Same(first, second);
    }

    [Fact]
    public void LoadEmbeddedUncached_StillParsesAFreshInstanceEveryCall()
    {
        // The escape hatch keeps doing real work — a fresh resource-stream parse and full
        // validation/duplicate-grouping pass — so parse/validation-failure tests stay meaningful
        // even though the public, cached LoadEmbedded() now memoizes.
        CodexCompatibilityManifest first = CodexCompatibilityManifest.LoadEmbeddedUncached();
        CodexCompatibilityManifest second = CodexCompatibilityManifest.LoadEmbeddedUncached();

        Assert.NotSame(first, second);
        Assert.Equal(first.Entries.Count, second.Entries.Count);
        Assert.NotEmpty(first.Entries);
    }
}
