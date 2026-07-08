namespace LoopRelay.Projections.Primitives;

public enum ProjectionRefreshPolicy
{
    BlockWhenStale,
    RegenerateWhenStale,
    AllowStale,
}
