using CommandCenter.Core.Repositories;
using CommandCenter.Workflow.Abstractions;
using Microsoft.Extensions.Hosting;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowRecoveryHostedService(
    IRepositoryService repositoryService,
    IWorkflowRecoveryService recoveryService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();
        foreach (Repository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await recoveryService.RecoverCurrentWorkflowAsync(repository.Id);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Startup recovery is reconstructable audit evidence. A repository
                // with incomplete local evidence must not prevent the backend from
                // serving unrelated domain endpoints.
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
