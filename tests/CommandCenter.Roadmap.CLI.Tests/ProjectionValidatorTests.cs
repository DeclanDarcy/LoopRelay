using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ProjectionValidatorTests
{
    [Fact]
    public void Validate_accepts_structurally_valid_projection()
    {
        ProjectionValidationResult result = new ProjectionValidator().Validate("SelectNextEpic", ProjectionSamples.Valid("SelectNextEpic"));

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void Validate_rejects_missing_required_section()
    {
        string projection = ProjectionSamples.Valid("SelectNextEpic").Replace("## Canonical Vocabulary", "## Vocabulary", StringComparison.Ordinal);

        ProjectionValidationResult result = new ProjectionValidator().Validate("SelectNextEpic", projection);

        Assert.False(result.IsValid);
        Assert.Contains("Canonical Vocabulary", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_forbidden_runtime_state_section()
    {
        string projection = ProjectionSamples.Valid("SelectNextEpic") + "\n## Current Roadmap Completion State\nstate";

        ProjectionValidationResult result = new ProjectionValidator().Validate("SelectNextEpic", projection);

        Assert.False(result.IsValid);
    }
}
