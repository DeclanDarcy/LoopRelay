namespace LoopRelay.Roadmap.Cli;

internal interface IArtifactValidator
{
    ArtifactValidationResult Validate(string content);
}
