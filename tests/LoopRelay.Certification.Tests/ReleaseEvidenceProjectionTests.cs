using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class ReleaseEvidenceProjectionTests
{
    [Fact]
    public void Temporary_evidence_is_local_only_for_cross_machine_release_credit()
    {
        string path = Path.Combine(Path.GetTempPath(), "workspace", ".tmp", "evidence", "case.json");
        EvidenceDurability durability = ReleaseEvidenceProjection.ClassifyDurability(path);

        Assert.Equal(EvidenceDurability.LocalTemporary, durability);
        Assert.Equal(EvidenceCreditStatus.LocalOnly,
            ReleaseEvidenceProjection.Credit(true, true, durability, crossMachineRequired: true));
        Assert.Equal(EvidenceCreditStatus.Credited,
            ReleaseEvidenceProjection.Credit(true, true, durability, crossMachineRequired: false));
    }

    [Fact]
    public void Exact_profile_requires_static_and_live_evidence_before_promotion()
    {
        ProviderProfileLifecycleProjection projection = ReleaseEvidenceProjection.ProjectProfile(new(
            "gpt-5.3-codex-spark/medium",
            StaticProtocolFixturesPassed: true,
            LiveCapabilityEvidencePassed: false,
            CurrentlyActive: false,
            [], [], [], [], [],
            ReplacementProfileIdentity: null,
            ReplacementEvidencePassed: false));

        Assert.Equal(ProviderProfileLifecycleKind.Unproven, projection.Lifecycle);
    }

    [Fact]
    public void Profile_retirement_is_blocked_by_every_durable_lineage_class()
    {
        ProviderProfileLifecycleProjection projection = ReleaseEvidenceProjection.ProjectProfile(new(
            "old-profile",
            StaticProtocolFixturesPassed: true,
            LiveCapabilityEvidencePassed: true,
            CurrentlyActive: false,
            ["root:1"], ["attempt:1"], ["session:1"], ["recovery:1"], ["evidence:1"],
            ReplacementProfileIdentity: "new-profile",
            ReplacementEvidencePassed: true));

        Assert.Equal(ProviderProfileLifecycleKind.RetirementBlocked, projection.Lifecycle);
        Assert.Equal(5, projection.BlockingReferences.Count);
    }

    [Fact]
    public void Profile_retires_only_after_zero_references_and_proven_replacement()
    {
        ProviderProfileLifecycleProjection projection = ReleaseEvidenceProjection.ProjectProfile(new(
            "old-profile",
            StaticProtocolFixturesPassed: true,
            LiveCapabilityEvidencePassed: true,
            CurrentlyActive: false,
            [], [], [], [], [],
            ReplacementProfileIdentity: "new-profile",
            ReplacementEvidencePassed: true));

        Assert.Equal(ProviderProfileLifecycleKind.Retired, projection.Lifecycle);
        Assert.Empty(projection.BlockingReferences);
    }
}
