namespace CommandCenter.Backend.Execution;

using CommandCenter.Backend.Repositories;

public interface IGitService
{
    Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository);
}
