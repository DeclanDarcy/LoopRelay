using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Cli.Services.Decisions.Recovery;
using LoopRelay.Orchestration.Recovery;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Decisions;

public sealed class RecoveryEnvelopeTests
{
    [Fact]
    public void CanonicalEnvelopeIsStableUnderShuffledSourceEnumeration()
    {
        RecoverySourceObservation first = Source(0, "ThreadRead", "first");
        RecoverySourceObservation second = Source(1, "Repository", "second");
        var builder = new RecoveryEnvelopeBuilder();

        RecoveryEnvelope ordered = builder.Build("marker", "scope", "thread", [first, second], 10_000, 1_000);
        RecoveryEnvelope shuffled = builder.Build("marker", "scope", "thread", [second, first], 10_000, 1_000);

        Assert.Equal(ordered.Digest, shuffled.Digest);
        Assert.Equal(ordered.CanonicalJson, shuffled.CanonicalJson);
    }

    [Fact]
    public void SecretsEnvironmentDumpsBase64AndHiddenReasoningNeverEnterEnvelope()
    {
        string largeBase64 = new('A', 300);
        RecoverySourceObservation source = Source(0, "RolloutSalvage", "safe", additional:
        [
            "Authorization: Bearer secret-value",
            "PATH_SECRET=value",
            largeBase64,
            "encrypted_content hidden reasoning",
        ]);

        RecoveryEnvelope envelope = new RecoveryEnvelopeBuilder().Build(
            "marker", "scope", "thread", [source], 10_000, 1_000);

        Assert.Contains("safe", envelope.CanonicalJson);
        Assert.DoesNotContain("secret-value", envelope.CanonicalJson);
        Assert.DoesNotContain(largeBase64, envelope.CanonicalJson);
        Assert.DoesNotContain("encrypted_content", envelope.CanonicalJson);
        Assert.Contains(envelope.Omissions, omission => omission.Contains("sensitive-or-unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateItemsAreDeduplicatedAndRecordedAsAnOmission()
    {
        RecoverySourceObservation source = Source(0, "ThreadRead", "same", additional: ["same"]);

        RecoveryEnvelope envelope = new RecoveryEnvelopeBuilder().Build(
            "marker", "scope", "thread", [source], 10_000, 1_000);

        Assert.Single(envelope.Items);
        Assert.Contains("ThreadRead:duplicate-item", envelope.Omissions);
    }

    [Fact]
    public void UnknownOrInsufficientContextBudgetFailsClosed()
    {
        RecoverySourceObservation source = Source(0, "Repository", "content");
        var builder = new RecoveryEnvelopeBuilder();

        Assert.Throws<RecoveryEnvelopeException>(() =>
            builder.Build("marker", "scope", "thread", [source], null, 1_000));
        Assert.Throws<RecoveryEnvelopeException>(() =>
            builder.Build("marker", "scope", "thread", [source], 2_000, 1_000, mandatoryOverheadTokens: 1_500));
    }

    [Fact]
    public void BudgetOverflowIsExplicitAndDowngradesCompleteness()
    {
        RecoverySourceObservation source = Source(
            0,
            "ThreadRead",
            string.Concat(Enumerable.Repeat("oversized recovery text ", 300)),
            additional: ["small"]);

        RecoveryEnvelope envelope = new RecoveryEnvelopeBuilder().Build(
            "marker", "scope", "thread", [source], 3_100, 1_000, mandatoryOverheadTokens: 2_000);

        Assert.Single(envelope.Items);
        Assert.Equal("small", envelope.Items[0].Text);
        Assert.Equal(RecoveryCompleteness.Selective, envelope.Completeness);
        Assert.Contains(envelope.Omissions, omission => omission.Contains("budget-overflow", StringComparison.Ordinal));
    }

    [Fact]
    public void OversizedMandatoryRepositoryContentRefusesRecovery()
    {
        RecoverySourceObservation repository = Source(
            2, "Repository", string.Concat(Enumerable.Repeat("mandatory repository context ", 400)));
        RecoverySourceObservation publicThread = Source(0, "ThreadRead", "small public context");

        RecoveryEnvelopeException exception = Assert.Throws<RecoveryEnvelopeException>(() =>
            new RecoveryEnvelopeBuilder().Build(
                "marker", "scope", "thread", [publicThread, repository],
                3_200, 1_000, mandatoryOverheadTokens: 2_000));

        Assert.Contains("Mandatory repository", exception.Message, StringComparison.Ordinal);
    }

    private static RecoverySourceObservation Source(
        int order,
        string kind,
        string text,
        IReadOnlyList<string>? additional = null)
    {
        string digest = new string((char)('a' + order), 64);
        var records = new List<SessionContentRecord>
        {
            new(0, "message", "assistant", text, null, new Dictionary<string, string>()),
        };
        int index = 1;
        foreach (string item in additional ?? [])
        {
            records.Add(new SessionContentRecord(index++, "message", "assistant", item, null, new Dictionary<string, string>()));
        }

        var descriptor = new RecoverySourceDescriptor(
            order, kind, kind.ToLowerInvariant(), digest, "boundary", "normalizer.v1",
            kind == "Repository" ? RecoveryCompleteness.RepositoryOnly : RecoveryCompleteness.Full,
            [], new Dictionary<string, string>());
        return new RecoverySourceObservation(descriptor, records);
    }
}
