using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Permissions.Models.Configuration;

public sealed record ConfiguredBrainFacts(
    AgentModel? Model,
    AgentEffort? Effort);

public sealed record ConfiguredRuntimeFacts(
    ConfiguredBrainFacts Brain,
    IReadOnlyList<string> SupportedCodexProfiles);

public sealed record ConfigurationCompatibilityWarning(string Code, string Message);

public sealed record ConfigurationSourceProvenance(
    string Path,
    bool IsDefaultTemplate,
    string SchemaVersion);

/// <summary>
/// Pure configuration output. Values describe configured facts and policy inputs; none are an
/// effective runtime or permission decision until Policy Authority resolves them.
/// </summary>
public sealed record CliSettingsLoadResult(
    ConfiguredRuntimeFacts Runtime,
    PermissionPolicyOptions PermissionInputs,
    CliPolicyDocument PolicyInputs,
    IReadOnlyList<ConfigurationCompatibilityWarning> CompatibilityWarnings,
    ConfigurationSourceProvenance Source)
{
    public string Path => Source.Path;

    public bool IsDefaultTemplate => Source.IsDefaultTemplate;
}
