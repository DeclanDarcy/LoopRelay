using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

namespace LoopRelay.Roadmap.Cli.Abstractions;

internal interface IArtifactValidator
{
    ArtifactValidationResult Validate(string content);
}
