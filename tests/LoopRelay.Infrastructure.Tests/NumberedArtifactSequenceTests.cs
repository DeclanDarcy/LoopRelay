using System.Text.RegularExpressions;
using LoopRelay.Infrastructure.Artifacts;

namespace LoopRelay.Infrastructure.Tests;

public sealed class NumberedArtifactSequenceTests
{
    [Fact]
    public void NextNumberReturnsOneAfterHighestObservedNumber()
    {
        var regex = new Regex(@"^e(\d{4})\.md$", RegexOptions.CultureInvariant);

        int next = NumberedArtifactSequence.NextNumber(
            [".agents/evidence/e0002.md", ".agents/evidence/e0010.md", ".agents/evidence/readme.md"],
            regex);

        Assert.Equal(11, next);
    }
}
