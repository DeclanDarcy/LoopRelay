namespace LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

internal sealed record ArtifactValidationResult(
    bool IsValid,
    string? Error)
{
    public static ArtifactValidationResult Valid() => new(true, null);

    public static ArtifactValidationResult Invalid(string error) => new(false, error);
}
