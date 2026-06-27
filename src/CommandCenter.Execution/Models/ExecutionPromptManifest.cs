namespace CommandCenter.Execution.Models;

public sealed class ExecutionPromptManifest
{
    public const string NoProviderDivergenceSignalDiagnostic = "NoProviderDivergenceSignal";

    public Guid SessionId { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public string PromptText { get; init; } = string.Empty;

    public string? PromptArtifactPath { get; init; }

    public IReadOnlyList<ExecutionPromptManifestArtifact> RequestedArtifacts { get; init; } =
        Array.Empty<ExecutionPromptManifestArtifact>();

    public long RequestedContextBytes { get; init; }

    public long RequestedContextCharacters { get; init; }

    public IReadOnlyList<ExecutionPromptManifestArtifact> DeliveredArtifacts { get; init; } =
        Array.Empty<ExecutionPromptManifestArtifact>();

    public long DeliveredContextBytes { get; init; }

    public long DeliveredContextCharacters { get; init; }

    public bool DirtyRepositoryAtRequestTime { get; init; }

    public bool? DirtyRepositoryAtDeliveryTime { get; init; }

    public int GovernedDecisionCountRequested { get; init; }

    public int GovernedDecisionCountDelivered { get; init; }

    public string? OperationalContextSourceRequested { get; init; }

    public string? OperationalContextSourceDelivered { get; init; }

    public string? HandoffSourceRequested { get; init; }

    public string? HandoffSourceDelivered { get; init; }

    public string ProviderDeliveryStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> ProviderAdjustments { get; init; } = Array.Empty<string>();

    public string? DivergenceReason { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}
