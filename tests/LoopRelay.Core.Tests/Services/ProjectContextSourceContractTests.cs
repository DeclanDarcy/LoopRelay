using LoopRelay.Core.Services.ProjectContext;

namespace LoopRelay.Core.Tests.Services;

public sealed class ProjectContextSourceContractTests
{
    [Fact]
    public void SourceFiles_are_the_canonical_nine_file_order()
    {
        Assert.Equal(".agents/ctx", ProjectContextSourceContract.DirectoryPath);
        Assert.Equal(
        [
            ".agents/ctx/01-purpose.md",
            ".agents/ctx/02-capability-model.md",
            ".agents/ctx/03-invariants.md",
            ".agents/ctx/04-strategic-structure.md",
            ".agents/ctx/05-authority-model.md",
            ".agents/ctx/06-evaluation-model.md",
            ".agents/ctx/07-drift-and-false-success.md",
            ".agents/ctx/08-vocabulary.md",
            ".agents/ctx/09-eval-details.md",
        ], ProjectContextSourceContract.SourceFiles);
    }

    [Theory]
    [InlineData("09-eval-details.md", true)]
    [InlineData("10-extra.md", true)]
    [InlineData("readme.md", false)]
    [InlineData("01-.md", false)]
    public void Numbered_source_detection_is_shape_based(string fileName, bool expected)
    {
        Assert.Equal(expected, ProjectContextSourceContract.IsNumberedSourceFileName(fileName));
    }

    [Fact]
    public void Violation_diagnostics_are_derived_from_the_current_contract()
    {
        string message = ProjectContextSourceContract.BuildViolationMessage(
            [".agents/ctx/09-eval-details.md"],
            [".agents/ctx/10-extra.md"]);

        Assert.Contains("requires 9 files", message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/09-eval-details.md", message, StringComparison.Ordinal);
        Assert.Contains(".agents/ctx/10-extra.md", message, StringComparison.Ordinal);
        Assert.DoesNotContain("01 through 08", message, StringComparison.Ordinal);
    }
}
