using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Core.Tests.Models.Identity;

public sealed class CausalUlidTests
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Fact]
    public void NewUlid_ReturnsTwentySixCrockfordCharacters()
    {
        string ulid = CausalUlid.NewUlid();

        Assert.Equal(26, ulid.Length);
        Assert.All(ulid, character => Assert.Contains(character, CrockfordAlphabet));
    }

    [Fact]
    public void NewUlid_ProducesUniqueValues()
    {
        HashSet<string> minted = [];
        for (int index = 0; index < 1000; index++)
        {
            Assert.True(minted.Add(CausalUlid.NewUlid()));
        }
    }

    [Fact]
    public void NewUlid_EncodesCurrentUnixMillisecondsInTimestampPrefix()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string ulid = CausalUlid.NewUlid();
        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        long encoded = DecodeTimestamp(ulid);

        Assert.InRange(encoded, before, after);
    }

    [Fact]
    public async Task NewUlid_TimestampPrefixOrdersAcrossMillisecondBoundaries()
    {
        string earlier = CausalUlid.NewUlid();
        await Task.Delay(50);
        string later = CausalUlid.NewUlid();

        Assert.True(string.CompareOrdinal(earlier[..10], later[..10]) <= 0);
    }

    [Fact]
    public void NewId_PrependsPrefixWithUnderscore()
    {
        string id = CausalUlid.NewId("tr");

        Assert.StartsWith("tr_", id, StringComparison.Ordinal);
        Assert.Equal(2 + 1 + 26, id.Length);
        Assert.All(id[3..], character => Assert.Contains(character, CrockfordAlphabet));
    }

    [Theory]
    [InlineData("")]
    [InlineData("WS")]
    [InlineData("tr1")]
    [InlineData("tr_")]
    [InlineData("w s")]
    public void NewId_RejectsInvalidPrefixes(string prefix)
    {
        Assert.Throws<ArgumentException>(() => CausalUlid.NewId(prefix));
    }

    [Fact]
    public void IdentityFactories_MintExpectedPrefixes()
    {
        Assert.StartsWith("ws_", WorkspaceIdentity.New().Value, StringComparison.Ordinal);
        Assert.StartsWith("run_", RunIdentity.New().Value, StringComparison.Ordinal);
        Assert.StartsWith("wfi_", WorkflowInstanceIdentity.New().Value, StringComparison.Ordinal);
        Assert.StartsWith("tr_", TransitionRunIdentity.New().Value, StringComparison.Ordinal);
        Assert.StartsWith("att_", AttemptIdentity.New().Value, StringComparison.Ordinal);
        Assert.StartsWith("ses_", AgentSessionIdentity.New().Value, StringComparison.Ordinal);
        Assert.StartsWith("turn_", TurnIdentity.New().Value, StringComparison.Ordinal);
        Assert.False(WorkspaceIdentity.New().IsEmpty);
        Assert.True(default(WorkspaceIdentity).IsEmpty);
    }

    private static long DecodeTimestamp(string ulid)
    {
        long value = 0;
        foreach (char character in ulid[..10])
        {
            value = (value << 5) | (uint)CrockfordAlphabet.IndexOf(character, StringComparison.Ordinal);
        }

        return value;
    }
}
