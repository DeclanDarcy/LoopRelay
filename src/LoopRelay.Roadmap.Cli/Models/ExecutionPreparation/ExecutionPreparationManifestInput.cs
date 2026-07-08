namespace LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;

internal sealed record ExecutionPreparationManifestInput(
    string Kind,
    string Identity,
    string Version);
