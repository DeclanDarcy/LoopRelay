using LoopRelay.Roadmap.Cli;
using EpicAuthoringOutputClassifier = LoopRelay.Roadmap.Cli.EpicAuthoringOutputClassifier;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class EpicArtifactPromotionTests
{
    [Fact]
    public void Classifier_identifies_valid_epic_candidate()
    {
        Cli.ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify(RoadmapSamples.ValidEpic());

        Assert.Equal(Cli.ArtifactOutputKind.Promotable, result.Kind);
    }

    [Fact]
    public void Classifier_identifies_blocked_output()
    {
        Cli.ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify("""
                # Epic Realignment Blocked

                ## Reason

                Audit disposition was not Realign.
                """);

        Assert.Equal(Cli.ArtifactOutputKind.Blocked, result.Kind);
    }

    [Fact]
    public void Classifier_identifies_ambiguous_output()
    {
        Cli.ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify("There is not enough information to continue.");

        Assert.Equal(Cli.ArtifactOutputKind.Ambiguous, result.Kind);
    }

    [Fact]
    public void Classifier_identifies_malformed_epic_output()
    {
        Cli.ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify("""
                # Epic

                ## Epic Metadata

                | Field | Value |
                |---|---|
                | Epic ID | EPIC-1 |
                """);

        Assert.Equal(Cli.ArtifactOutputKind.Malformed, result.Kind);
    }

    [Fact]
    public void Validator_accepts_required_epic_structure()
    {
        Cli.ArtifactValidationResult result = new Cli.EpicArtifactValidator()
            .Validate(RoadmapSamples.ValidEpic());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_missing_milestone_roadmap()
    {
        string content = RoadmapSamples.ValidEpic();
        int milestoneStart = content.IndexOf("## Milestone Roadmap", StringComparison.Ordinal);
        string withoutMilestones = content[..milestoneStart].TrimEnd() + Environment.NewLine;

        Cli.ArtifactValidationResult result = new Cli.EpicArtifactValidator()
            .Validate(withoutMilestones);

        Assert.False(result.IsValid);
        Assert.Contains("Milestone Roadmap", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_rejects_empty_milestone_roadmap()
    {
        string content = """
            # Epic: Empty Milestone Roadmap

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-EMPTY |
            | Status | Authored |

            ## Strategic Purpose

            Purpose.

            ## Desired Capability

            Capability.

            ## Acceptance Criteria

            - Criterion.

            ## Milestone Roadmap

            | Milestone ID | Milestone Name | Purpose | Outcome | Depends On | Completion Signal |
            |---|---|---|---|---|---|
            """;

        Cli.ArtifactValidationResult result = new Cli.EpicArtifactValidator()
            .Validate(content);

        Assert.False(result.IsValid);
        Assert.Contains("at least one milestone row", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_rejects_missing_metadata()
    {
        Cli.ArtifactValidationResult result = new Cli.EpicArtifactValidator()
            .Validate("""
                # Epic: Missing Metadata

                ## Strategic Purpose

                Purpose.

                ## Desired Capability

                Capability.

                ## Acceptance Criteria

                - Criterion.
                """);

        Assert.False(result.IsValid);
        Assert.Contains("Epic Metadata", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_rejects_blocked_output()
    {
        Cli.ArtifactValidationResult result = new Cli.EpicArtifactValidator()
            .Validate("""
                # Create New Epic Blocked

                ## Reason

                Not safe to author.
                """);

        Assert.False(result.IsValid);
        Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
