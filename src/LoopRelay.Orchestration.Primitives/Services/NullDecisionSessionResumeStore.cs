using System.Text.Json;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;

namespace LoopRelay.Orchestration.Services;

/// <summary>No-op store: nothing is persisted and nothing is ever found (default for tests/compositions
/// that do not opt into resume).</summary>
public sealed class NullDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DecisionSessionResumeState?>(null);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
