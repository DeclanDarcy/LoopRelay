using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Abstractions;

internal interface IArtifactOutputClassifier
{
    ArtifactOutputClassification Classify(string content);
}
