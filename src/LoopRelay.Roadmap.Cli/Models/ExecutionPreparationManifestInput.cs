using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionPreparationManifestInput(
    string Kind,
    string Identity,
    string Version);
