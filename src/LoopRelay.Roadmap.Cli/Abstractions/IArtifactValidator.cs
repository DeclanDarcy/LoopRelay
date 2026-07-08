using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Abstractions;

internal interface IArtifactValidator
{
    ArtifactValidationResult Validate(string content);
}
