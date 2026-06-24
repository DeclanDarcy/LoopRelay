using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using Microsoft.Extensions.Hosting;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionRecoveryHostedService(
    IRepositoryService repositoryService,
    IDecisionSessionRecoveryService recoveryService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Repository> repositories = await repositoryService.GetAllAsync();
        foreach (Repository repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await recoveryService.RecoverAsync(repository.Id);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Recovery evidence is reconstructable. A repository with corrupt
                // lifecycle files must not prevent unrelated repositories from loading.
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
