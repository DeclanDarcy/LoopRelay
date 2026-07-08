using LoopRelay.Roadmap.Cli;
using BundleFileExtractor = LoopRelay.Roadmap.Cli.BundleFileExtractor;
using SplitEpicBundleInterpreter = LoopRelay.Roadmap.Cli.SplitEpicBundleInterpreter;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class SplitEpicBundleInterpreterTests
{
    [Fact]
    public void Rejects_bundle_with_no_files()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret("# Split Epic Output");

        Assert.False(result.IsValid);
        Assert.Equal(Cli.SplitEpicBundleInterpretationStatus.Invalid, result.Status);
    }

    [Fact]
    public void Recognizes_blocked_split_output()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret("""
                                                             # Split Epic Blocked

                                                             ## Reason

                                                             The selection cannot be split safely.
                                                             """);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.SplitEpicBundleInterpretationStatus.Blocked, result.Status);
    }

    [Fact]
    public void Rejects_spec_only_bundle()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret("""
                                                             # FILE: .agents/specs/split-child.md
                                                             # Spec
                                                             """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Rejections, rejection => rejection.Path == ".agents/specs/split-child.md");
        Assert.Empty(result.ValidatedChildEpics);
    }

    [Fact]
    public void Rejects_direct_active_epic_target()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret($"""
                                                              # FILE: .agents/epic.md
                                                              {RoadmapSamples.ValidEpic("Direct Active Target", "EPIC-DIRECT")}
                                                              """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Rejections, rejection => rejection.Path == Cli.RoadmapArtifactPaths.ActiveEpic);
    }

    [Fact]
    public void Rejects_malformed_child_epic()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret("""
                                                             # FILE: .agents/epic-1.md
                                                             # Epic

                                                             ## Epic Metadata

                                                             | Field | Value |
                                                             |---|---|
                                                             | Epic ID | EPIC-BAD |
                                                             | Status | Authored |
                                                             """);

        Assert.False(result.IsValid);
        Assert.Contains(result.Rejections, rejection => rejection.Path == ".agents/epic-1.md");
    }

    [Fact]
    public void Accepts_valid_single_child_epic()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret($"""
                                                              # FILE: .agents/epic-1.md
                                                              {RoadmapSamples.ValidEpic("First Child", "EPIC-1")}
                                                              """);

        Assert.True(result.IsValid);
        Assert.Equal(".agents/epic-1.md", result.SelectedChild?.Path);
        Assert.Single(result.ValidatedChildEpics);
    }

    [Fact]
    public void Selects_first_valid_child_by_numeric_order()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret($"""
                                                              # FILE: .agents/epic-2.md
                                                              {RoadmapSamples.ValidEpic("Second Child", "EPIC-2")}

                                                              # FILE: .agents/epic-1.md
                                                              {RoadmapSamples.ValidEpic("First Child", "EPIC-1")}
                                                              """);

        Assert.True(result.IsValid);
        Assert.Equal(".agents/epic-1.md", result.SelectedChild?.Path);
        Assert.Equal(new[] { ".agents/epic-1.md", ".agents/epic-2.md" }, result.ValidatedChildEpics.Select(file => file.Path));
    }

    [Fact]
    public void Rejects_mixed_valid_child_and_illegal_target()
    {
        Cli.SplitEpicBundleInterpretation result = Interpret($"""
                                                              # FILE: .agents/epic-1.md
                                                              {RoadmapSamples.ValidEpic("First Child", "EPIC-1")}

                                                              # FILE: .agents/specs/not-a-child.md
                                                              # Not A Child
                                                              """);

        Assert.False(result.IsValid);
        Assert.Single(result.ValidatedChildEpics);
        Assert.Contains(result.Rejections, rejection => rejection.Path == ".agents/specs/not-a-child.md");
    }

    private static Cli.SplitEpicBundleInterpretation Interpret(string markdown)
    {
        Cli.BundleExtractionResult bundle = new BundleFileExtractor().Extract(markdown, Cli.BundleExtractionPolicy.RepositorySafe);
        return new SplitEpicBundleInterpreter().Interpret(bundle, markdown);
    }
}
