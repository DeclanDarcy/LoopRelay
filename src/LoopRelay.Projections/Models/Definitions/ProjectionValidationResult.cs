namespace LoopRelay.Projections.Models;

public sealed record ProjectionValidationResult(bool IsValid, string? Error)
{
    public static ProjectionValidationResult Valid() => new(true, null);

    public static ProjectionValidationResult Invalid(string error) => new(false, error);
}
