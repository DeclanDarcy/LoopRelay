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
}
