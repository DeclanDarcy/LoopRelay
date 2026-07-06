using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class SplitFamilyStoreTests
{
    [Fact]
    public async Task Writes_structured_family_and_uses_json_for_child_lookup()
    {
        using var repo = new TempRepo();
        var store = new Cli.SplitFamilyStore(repo.Artifacts);
        Cli.SplitFamily family = Family("family-1", [".agents/epic-1.md", ".agents/epic-2.md"]);

        string markdownPath = await store.WriteAsync(family);
        repo.Write(markdownPath, "# Split Family\n\n## Child Epics\n\n- .agents/other.md\n");

        Assert.True(await store.ExistsForChildAsync(".agents/epic-2.md"));
        Assert.Contains("\"SchemaVersion\": \"split-family.v1\"", repo.Read(Cli.RoadmapArtifactPaths.SplitFamilyJson("family-1")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrates_valid_legacy_split_family()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.SplitFamily("legacy"), """
                                                                   # Split Family

                                                                   | Field | Value |
                                                                   |---|---|
                                                                   | Family ID | legacy |
                                                                   | Created At | 2026-01-01T00:00:00.0000000+00:00 |
                                                                   | Selected Child | .agents/epic-2.md |
                                                                   | Selected Child Rationale | unblock \| high leverage |

                                                                   ## Proposal

                                                                   Split this epic.

                                                                   ## Child Epics

                                                                   - .agents/epic-1.md
                                                                   - .agents/epic-2.md

                                                                   ## Dependency Order

                                                                   - .agents/epic-1.md
                                                                   - .agents/epic-2.md
                                                                   """);

        Assert.True(await new Cli.SplitFamilyStore(repo.Artifacts).ExistsForChildAsync(".agents/epic-2.md"));
        Assert.True(Exists(repo, Cli.RoadmapArtifactPaths.SplitFamilyJson("legacy")));
    }

    [Fact]
    public async Task Rejects_malformed_legacy_split_family_without_migration()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.SplitFamily("legacy"), """
                                                                   # Split Family

                                                                   | Field | Value |
                                                                   |---|---|
                                                                   | Selected Child Rationale | malformed | rationale |

                                                                   ## Child Epics

                                                                   - .agents/epic-1.md
                                                                   """);

        Cli.RoadmapStepException ex = await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => new Cli.SplitFamilyStore(repo.Artifacts).ExistsForChildAsync(".agents/epic-1.md"));

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Exists(repo, Cli.RoadmapArtifactPaths.SplitFamilyJson("legacy")));
    }

    private static Cli.SplitFamily Family(string familyId, IReadOnlyList<string> childPaths) =>
        new(
            familyId,
            "Proposal | with pipe",
            childPaths,
            childPaths,
            childPaths.Last(),
            "Rationale | with pipe \\ slash",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
