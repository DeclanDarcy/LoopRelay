namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ExecutionPreparationManifestInput(
    string Kind,
    string Identity,
    string Version);
