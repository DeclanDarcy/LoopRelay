namespace LoopRelay.Certification;

public enum ProviderProfileLifecycleKind
{
    Unproven,
    Promoted,
    Active,
    RetirementBlocked,
    Retired,
}

public sealed record ProviderProfileLifecycleInput(
    string ProfileIdentity,
    bool StaticProtocolFixturesPassed,
    bool LiveCapabilityEvidencePassed,
    bool CurrentlyActive,
    IReadOnlyList<string> ActiveRootReferences,
    IReadOnlyList<string> AttemptReferences,
    IReadOnlyList<string> SessionReferences,
    IReadOnlyList<string> RecoveryPlanReferences,
    IReadOnlyList<string> EvidenceClaimReferences,
    string? ReplacementProfileIdentity,
    bool ReplacementEvidencePassed);

public sealed record ProviderProfileLifecycleProjection(
    string ProfileIdentity,
    ProviderProfileLifecycleKind Lifecycle,
    IReadOnlyList<string> BlockingReferences,
    string Reason);

public static class ReleaseEvidenceProjection
{
    public static ProviderProfileLifecycleProjection ProjectProfile(ProviderProfileLifecycleInput input)
    {
        string[] references = input.ActiveRootReferences
            .Concat(input.AttemptReferences)
            .Concat(input.SessionReferences)
            .Concat(input.RecoveryPlanReferences)
            .Concat(input.EvidenceClaimReferences)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!input.StaticProtocolFixturesPassed || !input.LiveCapabilityEvidencePassed)
            return new(input.ProfileIdentity, ProviderProfileLifecycleKind.Unproven, references,
                "Exact profile promotion requires passing static protocol fixtures and live capability evidence.");
        if (input.CurrentlyActive)
            return new(input.ProfileIdentity, ProviderProfileLifecycleKind.Active, references,
                "Exact profile is proven and active.");
        if (references.Length > 0 || string.IsNullOrWhiteSpace(input.ReplacementProfileIdentity) ||
            !input.ReplacementEvidencePassed)
            return new(input.ProfileIdentity, ProviderProfileLifecycleKind.RetirementBlocked, references,
                "Retirement requires zero durable lineage references and a proven replacement profile.");
        return new(input.ProfileIdentity, ProviderProfileLifecycleKind.Retired, [],
            $"Profile retired after replacement `{input.ReplacementProfileIdentity}` was proven.");
    }

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
