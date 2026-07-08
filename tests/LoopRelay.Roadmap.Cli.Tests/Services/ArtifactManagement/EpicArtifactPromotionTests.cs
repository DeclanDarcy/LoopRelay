using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Tests.Services.State;
using EpicAuthoringOutputClassifier = LoopRelay.Roadmap.Cli.Services.ArtifactManagement.EpicAuthoringOutputClassifier;

namespace LoopRelay.Roadmap.Cli.Tests.Services.ArtifactManagement;

public sealed class EpicArtifactPromotionTests
{
    [Fact]
    public void Classifier_identifies_valid_epic_candidate()
    {
        ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify(RoadmapSamples.ValidEpic());

        Assert.Equal(ArtifactOutputKind.Promotable, result.Kind);
    }

    [Fact]
    public void Classifier_identifies_blocked_output()
    {
        ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify("""
                # Epic Realignment Blocked

                ## Reason

                Audit disposition was not Realign.
                """);

        Assert.Equal(ArtifactOutputKind.Blocked, result.Kind);
    }

    [Fact]
    public void Classifier_identifies_ambiguous_output()
    {
        ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify("There is not enough information to continue.");

        Assert.Equal(ArtifactOutputKind.Ambiguous, result.Kind);
    }

    [Fact]
    public void Classifier_identifies_malformed_epic_output()
    {
        ArtifactOutputClassification result = new EpicAuthoringOutputClassifier()
            .Classify("""
                # Epic

                ## Epic Metadata

                | Field | Value |
                |---|---|
                | Epic ID | EPIC-1 |
                """);

        Assert.Equal(ArtifactOutputKind.Malformed, result.Kind);
    }

    [Fact]
    public void Validator_accepts_required_epic_structure()
    {
        ArtifactValidationResult result = new EpicArtifactValidator()
            .Validate(RoadmapSamples.ValidEpic());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_missing_milestone_roadmap()
    {
        string content = RoadmapSamples.ValidEpic();
        int milestoneStart = content.IndexOf("## Milestone Roadmap", StringComparison.Ordinal);
        string withoutMilestones = content[..milestoneStart].TrimEnd() + Environment.NewLine;

        ArtifactValidationResult result = new EpicArtifactValidator()
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

        ArtifactValidationResult result = new EpicArtifactValidator()
            .Validate(content);

        Assert.False(result.IsValid);
        Assert.Contains("at least one milestone row", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_rejects_missing_metadata()
    {
        ArtifactValidationResult result = new EpicArtifactValidator()
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
        ArtifactValidationResult result = new EpicArtifactValidator()
            .Validate("""
                # Create New Epic Blocked

                ## Reason

                Not safe to author.
                """);

        Assert.False(result.IsValid);
        Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
