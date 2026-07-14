using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;

namespace LoopRelay.Cli.Tests.Services.Decisions;

internal sealed class FakeDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public DecisionSessionResumeState? State { get; set; }
    public List<DecisionSessionResumeState> Written { get; } = new();
    public int ClearCalls { get; private set; }

    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(State);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        Written.Add(state);
        State = state;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCalls++;
        State = null;
        return Task.CompletedTask;
    }
}
