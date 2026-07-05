using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

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
