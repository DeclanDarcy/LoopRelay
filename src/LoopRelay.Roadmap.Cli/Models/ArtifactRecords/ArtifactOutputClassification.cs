using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

namespace LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

internal sealed record ArtifactOutputClassification(
    ArtifactOutputKind Kind,
    string Reason);
