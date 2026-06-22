using CommandCenter.Execution.Abstractions;
using Microsoft.Extensions.Hosting;

namespace CommandCenter.Execution.Services;

public sealed class ExecutionSessionRecoveryHostedService(
    IExecutionSessionService executionSessionService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return executionSessionService.RecoverAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
