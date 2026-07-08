using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ArtifactOutputClassification(
    ArtifactOutputKind Kind,
    string Reason);
