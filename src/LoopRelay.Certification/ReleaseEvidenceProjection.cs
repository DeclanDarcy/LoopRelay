namespace LoopRelay.Certification;

public static class ReleaseEvidenceProjection
{
    public static EvidenceDurability ClassifyDurability(string evidencePath)
    {
        string normalized = Path.GetFullPath(evidencePath).Replace('\\', '/');
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, ".tmp", StringComparison.OrdinalIgnoreCase))
            ? EvidenceDurability.LocalTemporary
            : EvidenceDurability.CrossMachineDurable;
    }

    public static EvidenceCreditStatus Credit(
        bool exactScopeCurrent,
        bool evidencePassed,
        EvidenceDurability durability,
        bool crossMachineRequired) =>
        !evidencePassed ? EvidenceCreditStatus.Uncredited
        : !exactScopeCurrent ? EvidenceCreditStatus.Stale
        : crossMachineRequired && durability == EvidenceDurability.LocalTemporary
            ? EvidenceCreditStatus.LocalOnly
            : EvidenceCreditStatus.Credited;
}
