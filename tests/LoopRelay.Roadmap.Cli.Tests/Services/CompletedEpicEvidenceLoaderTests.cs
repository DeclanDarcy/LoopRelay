using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class CompletedEpicEvidenceLoaderTests
{
    [Fact]
    public async Task Missing_archive_renders_explicit_empty_evidence()
    {
        using var repo = new TempRepo();

        string rendered = await new Cli.CompletedEpicEvidenceLoader(repo.Artifacts).RenderAsync();

        Assert.Contains("# Completed Epic Evidence", rendered, StringComparison.Ordinal);
        Assert.Contains("No completed epic markdown files were found under `.agents/archive/epics/*.md`.", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flat_archive_markdown_files_are_included_and_nested_files_are_ignored()
    {
        using var repo = new TempRepo();
        repo.Write(".agents/archive/epics/002-beta.md", RoadmapSamples.ValidEpic("Archived Beta", "EPIC-BETA"));
        repo.Write(".agents/archive/epics/001-alpha.md", RoadmapSamples.ValidEpic("Archived Alpha", "EPIC-ALPHA"));
        repo.Write(".agents/archive/epics/old/plan.md", RoadmapSamples.ValidEpic("Nested Archive", "EPIC-NESTED"));

        string rendered = await new Cli.CompletedEpicEvidenceLoader(repo.Artifacts).RenderAsync();

        Assert.Contains("Completed epic source glob: `.agents/archive/epics/*.md`", rendered, StringComparison.Ordinal);
        Assert.Contains("| Source Path | .agents/archive/epics/001-alpha.md |", rendered, StringComparison.Ordinal);
        Assert.Contains("| Source Path | .agents/archive/epics/002-beta.md |", rendered, StringComparison.Ordinal);
        Assert.True(
            rendered.IndexOf(".agents/archive/epics/001-alpha.md", StringComparison.Ordinal) <
            rendered.IndexOf(".agents/archive/epics/002-beta.md", StringComparison.Ordinal));
        Assert.DoesNotContain(".agents/archive/epics/old/plan.md", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unstructured_archive_markdown_is_included_with_unclear_quality()
    {
        using var repo = new TempRepo();
        repo.Write(".agents/archive/epics/broken.md", "loose notes without epic metadata");

        string rendered = await new Cli.CompletedEpicEvidenceLoader(repo.Artifacts).RenderAsync();

        Assert.Contains("| Source Path | .agents/archive/epics/broken.md |", rendered, StringComparison.Ordinal);
        Assert.Contains("| Evidence Quality | Unclear |", rendered, StringComparison.Ordinal);
        Assert.Contains("loose notes without epic metadata", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Per_file_budget_adds_visible_truncation_marker()
    {
        using var repo = new TempRepo();
        string longSection = new('a', Cli.CompletedEpicEvidenceLoader.MaxRenderedContentPerEpic + 100);
        repo.Write(".agents/archive/epics/huge.md", $"""
            # Epic: Huge

            ## Strategic Purpose

            {longSection}
            """);

        string rendered = await new Cli.CompletedEpicEvidenceLoader(repo.Artifacts).RenderAsync();

        Assert.Contains("Per-epic evidence truncated", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Total_budget_adds_visible_truncation_marker()
    {
        using var repo = new TempRepo();
        string longSection = new('b', Cli.CompletedEpicEvidenceLoader.MaxRenderedContentPerEpic);
        for (int index = 0; index < 8; index++)
        {
            repo.Write($".agents/archive/epics/{index:000}.md", $"""
                # Epic: Huge {index}

                ## Strategic Purpose

                {longSection}
                """);
        }

        string rendered = await new Cli.CompletedEpicEvidenceLoader(repo.Artifacts).RenderAsync();

        Assert.Contains("archive exceeded the total evidence budget", rendered, StringComparison.Ordinal);
    }
}
