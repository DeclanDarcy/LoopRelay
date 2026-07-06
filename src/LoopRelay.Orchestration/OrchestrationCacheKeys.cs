namespace LoopRelay.Orchestration;

/// <summary>
/// Reserved <c>IMemoryCache</c> keys. <see cref="PlanRun"/> reserves the <c>{repositoryId}:Plan</c>
/// slot the milestone calls out for the active execution run. The cache holds only transient run
/// projections; durable authority always lives in repository artifacts.
/// </summary>
public static class OrchestrationCacheKeys
{
    public static string PlanRun(string repositoryId) => $"{repositoryId}:Plan";
}
