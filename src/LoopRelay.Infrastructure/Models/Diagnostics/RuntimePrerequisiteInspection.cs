using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Models.Diagnostics;

/// <summary>
/// The host-facing projection of the runtime profile selected by Runtime Authority. It contains
/// only the identity, provider, and declared launch capabilities needed for prerequisite
/// inspection; policy and raw configuration are deliberately absent.
/// </summary>
public sealed record ResolvedRuntimeHostProfile(
    RuntimeProfileIdentity Identity,
    AgentRuntimeCapabilities Capabilities)
{
    public string Provider => Capabilities.Provider;
}

public enum RuntimePrerequisiteOverallStatus
{
    Satisfied,
    Degraded,
    Unsatisfied,
}

public enum RuntimePrerequisiteFindingCode
{
    MissingRequiredExecutable,
    InvalidProviderInstallation,
    MissingOptionalRuntimeDirectory,
    InsufficientLaunchCapability,
    UnsupportedProvider,
    UnknownProviderState,
}

public sealed record RuntimePrerequisiteFinding(
    RuntimePrerequisiteFindingCode Code,
    string Id,
    RuntimePrerequisiteFindingSeverity Severity,
    string Message);

/// <summary>
/// Immutable provider-scoped evidence. Consumers decide how to render or map the status; the
/// doctor and provider inspectors never produce CLI outcomes or exit codes.
/// </summary>
public sealed record RuntimePrerequisiteInspection(
    RuntimeProfileIdentity RuntimeProfile,
    string Provider,
    DateTimeOffset InspectedAt,
    IReadOnlyList<RuntimePrerequisiteFinding> Findings,
    RuntimePrerequisiteOverallStatus OverallStatus);
