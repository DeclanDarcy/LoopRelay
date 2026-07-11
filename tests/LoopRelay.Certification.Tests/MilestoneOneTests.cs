using LoopRelay.Certification;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class MilestoneOneTests
{
    [Fact]
    public void RepositoryAndScenarioHaveStableIndependentIdentity()
    {
        ComposedCaseIdentity first = FixtureComposer.ValidateAndIdentify(
            FixtureRepository.MinimalText,
            FixtureScenario.StatusCanary);
        ComposedCaseIdentity second = FixtureComposer.ValidateAndIdentify(
            FixtureRepository.MinimalText,
            FixtureScenario.StatusCanary);

        Assert.Equal(first, second);
        Assert.Equal("minimal-text", first.RepositoryIdentity);
        Assert.Equal("status-canary", first.ScenarioIdentity);
    }

    [Fact]
    public void AmbiguousOverlayAuthorityFailsBeforeExecution()
    {
        var scenario = new FixtureScenario("conflict", "1",
        [
            new ScenarioOverlay("first", "1", CaseAuthority.Persistence),
            new ScenarioOverlay("second", "1", CaseAuthority.Persistence),
        ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            FixtureComposer.ValidateAndIdentify(FixtureRepository.MinimalText, scenario));

        Assert.Contains("ambiguous owners", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitOverlayPrecedenceResolvesAuthorityOwnership()
    {
        var scenario = new FixtureScenario("precedence", "1",
        [
            new ScenarioOverlay("base", "1", CaseAuthority.Persistence),
            new ScenarioOverlay("override", "1", CaseAuthority.Persistence, Precedence: 1),
        ]);

        ComposedCaseIdentity identity = FixtureComposer.ValidateAndIdentify(FixtureRepository.MinimalText, scenario);

        Assert.False(string.IsNullOrWhiteSpace(identity.CompositionDigest));
    }

    [Fact]
    public void FixturePathEscapeFailsClosed()
    {
        var repository = new FixtureRepository("escape", "1", [new FixtureFile("../outside.txt", "bad")]);

        Assert.Throws<InvalidOperationException>(() =>
            FixtureComposer.ValidateAndIdentify(repository, FixtureScenario.StatusCanary));
    }

    [Fact]
    public async Task RepeatedMaterializationHasSameHashAndCleanReset()
    {
        string root = Path.Combine(Path.GetTempPath(), "looprelay-certification-tests", Guid.NewGuid().ToString("N"));
        try
        {
            string first = Path.Combine(root, "first");
            string second = Path.Combine(root, "second");
            await FixtureComposer.MaterializeAsync(FixtureRepository.MinimalText, first, CancellationToken.None);
            await FixtureComposer.MaterializeAsync(FixtureRepository.MinimalText, second, CancellationToken.None);

            Assert.Equal(FileObserver.Digest(FileObserver.Observe(first)), FileObserver.Digest(FileObserver.Observe(second)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void NormalizerRemovesCasePathAndVolatileIdentities()
    {
        string root = Path.Combine(Path.GetTempPath(), "case-root");
        string input = $"Repository: {root}\r\nRun: 798e5446-522f-4b8d-9baa-1d5cbca97bf0 at 2026-07-10T12:34:56Z";

        string normalized = EvidenceNormalizer.Normalize(input, root);

        Assert.Equal("Repository: <CASE>\nRun: <GUID> at <TIMESTAMP>", normalized);
    }

    [Theory]
    [InlineData("api_key=super-secret-value", "credential-or-secret-pattern")]
    [InlineData("PATH=/unsafe/bin", "environment-dump-pattern")]
    [InlineData("hidden reasoning: do not retain", "hidden-reasoning-pattern")]
    public void PrivacyScannerRejectsSeededLeaks(string evidence, string expected)
    {
        IReadOnlyList<string> findings = PrivacyScanner.Scan(evidence, Path.GetTempPath());

        Assert.Contains(expected, findings);
    }

    [Fact]
    public void CoverageLedgerIsProductionDerivedAndKeepsUncoveredSetVisible()
    {
        string workspace = FindWorkspaceRoot();

        CoverageLedger ledger = CoverageLedgerBuilder.Build(workspace);

        Assert.Contains(ledger.Obligations, item => item.Dimension == "workflow" && item.Identity == "Execute");
        Assert.Contains(ledger.Obligations, item => item.Dimension == "transition" && item.Identity.Contains("Execute/ExecuteImplementationSlice", StringComparison.Ordinal));
        Assert.Contains(ledger.Obligations, item => item.Dimension == "known-risk");
        Assert.NotEmpty(ledger.Uncovered);
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoopRelay.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Workspace root not found.");
    }
}
