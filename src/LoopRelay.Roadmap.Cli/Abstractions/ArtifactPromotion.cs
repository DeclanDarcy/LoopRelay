namespace LoopRelay.Roadmap.Cli;

internal interface IArtifactOutputClassifier
{
    ArtifactOutputClassification Classify(string content);
}
