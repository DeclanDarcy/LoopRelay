namespace LoopRelay.Projections;

public sealed record ProjectionCausalInputDto(string Kind, string Identity, string Version)
{
    public static ProjectionCausalInputDto FromDomain(ProjectionCausalInput input) =>
        new(input.Kind, input.Identity, input.Version);

    public ProjectionCausalInput ToDomain() => new(Kind, Identity, Version);
}
