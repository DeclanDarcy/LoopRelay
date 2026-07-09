using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Persistence;

public sealed class SplitFamilyLegacyMigrationConformanceTests
{
    [Fact]
    public async Task Valid_legacy_markdown_migrates_to_structured_json_without_reallocating_identity()
    {
        using var repo = new TempRepo();
        string legacyPath = RoadmapArtifactPaths.SplitFamily("legacy");
        string legacyMarkdown = LegacyMarkdown("legacy");
        repo.Write(legacyPath, legacyMarkdown);

        Assert.True(await new SplitFamilyStore(repo.Artifacts).ExistsForChildAsync(".agents/epic-2.md"));

        string jsonPath = RoadmapArtifactPaths.SplitFamilyJson("legacy");
        Assert.True(Exists(repo, jsonPath));
        Assert.Equal(legacyMarkdown, repo.Read(legacyPath));

        string migratedJson = repo.Read(jsonPath);
        Assert.EndsWith(Environment.NewLine, migratedJson, StringComparison.Ordinal);

        SplitFamilyPersistenceDocument? document = JsonSerializer.Deserialize<SplitFamilyPersistenceDocument>(
            migratedJson,
            RoadmapJson.Options);

        Assert.NotNull(document);
        Assert.Equal(SplitFamilyPersistenceDocument.CurrentSchemaVersion, document!.SchemaVersion);
        Assert.Equal("legacy", document.Family.FamilyId);
        Assert.Equal("Split | with retained body.", document.Family.Proposal);
        Assert.Equal(
            [".agents/epic-1.md", ".agents/epic-2.md"],
            document.Family.ChildEpicPaths);
        Assert.Equal(
            [".agents/epic-2.md", ".agents/epic-1.md"],
            document.Family.DependencyOrder);
        Assert.Equal(".agents/epic-2.md", document.Family.SelectedChildPath);
        Assert.Equal("unblock | high \\ leverage", document.Family.SelectedChildRationale);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00.0000000+00:00"), document.Family.CreatedAt);
    }

    [Fact]
    public async Task Existing_structured_json_has_authority_over_same_family_legacy_markdown()
    {
        using var repo = new TempRepo();
        var store = new SplitFamilyStore(repo.Artifacts);
        await store.WriteAsync(
            new SplitFamily(
                "legacy",
                "Structured family wins.",
                [".agents/json-child.md"],
                [".agents/json-child.md"],
                ".agents/json-child.md",
                "structured",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        string before = repo.Read(RoadmapArtifactPaths.SplitFamilyJson("legacy"));
        repo.Write(RoadmapArtifactPaths.SplitFamily("legacy"), LegacyMarkdown("legacy"));

        Assert.False(await store.ExistsForChildAsync(".agents/epic-2.md"));
        Assert.Equal(before, repo.Read(RoadmapArtifactPaths.SplitFamilyJson("legacy")));
    }

    [Fact]
    public async Task Blank_legacy_markdown_is_ignored_without_structured_json_write()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.SplitFamily("blank"), " \r\n\t\r\n");

        Assert.False(await new SplitFamilyStore(repo.Artifacts).ExistsForChildAsync(".agents/epic-1.md"));
        Assert.False(Exists(repo, RoadmapArtifactPaths.SplitFamilyJson("blank")));
    }

    [Fact]
    public async Task Invalid_legacy_markdown_does_not_write_structured_json()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.SplitFamily("legacy"), """
                                                               # Split Family

                                                               | Field | Value |
                                                               |---|---|
                                                               | Family ID | legacy |
                                                               | Created At | 2026-01-01T00:00:00.0000000+00:00 |
                                                               | Selected Child | .agents/missing.md |

                                                               ## Proposal

                                                               Split this epic.

                                                               ## Child Epics

                                                               - .agents/epic-1.md
                                                               """);

        RoadmapStepException ex = await Assert.ThrowsAsync<RoadmapStepException>(
            () => new SplitFamilyStore(repo.Artifacts).ExistsForChildAsync(".agents/epic-1.md"));

        Assert.Contains("cannot be migrated", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Selected child `.agents/missing.md` must be present in child epic paths", ex.Message, StringComparison.Ordinal);
        Assert.False(Exists(repo, RoadmapArtifactPaths.SplitFamilyJson("legacy")));
    }

    private static string LegacyMarkdown(string familyId) =>
        $$"""
          # Split Family

          | Field | Value |
          |---|---|
          | Family ID | {{familyId}} |
          | Created At | 2026-01-01T00:00:00.0000000+00:00 |
          | Selected Child | .agents/epic-2.md |
          | Selected Child Rationale | unblock \| high \\ leverage |

          ## Proposal

          Split | with retained body.

          ## Child Epics

          - .agents/epic-1.md
          - .agents/epic-2.md

          ## Dependency Order

          - .agents/epic-2.md
          - .agents/epic-1.md
          """;

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
