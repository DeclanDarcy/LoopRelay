namespace LoopRelay.Projections;

public enum ProjectionRefreshPolicy
{
    BlockWhenStale,
    RegenerateWhenStale,
    AllowStale,
}
