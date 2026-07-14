using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Cli.Tests.Services.Agents;

internal sealed class FakeAgentSession(FakeAgentRuntime runtime, AgentSessionSpec spec) : IAgentSession
{
    private readonly string threadId = spec.ResumeThreadId ?? $"thread-{runtime.OpenSessions}";
    private int completedTurns;
    private AgentTokenUsage totalUsage = AgentTokenUsage.Zero;

    public SessionIdentity SessionId => spec.SessionId;
    public string RepositoryId => spec.RepositoryId;
    public SessionRole Role => spec.Role;
    public AgentSessionMode Mode => AgentSessionMode.Persistent;
    public AgentProcessState State => AgentProcessState.Running;
    public int CompletedTurns => completedTurns;
    public AgentTokenUsage TotalUsage => totalUsage;
    public string? ThreadId => threadId;

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default)
    {
        AgentTurnResult result = runtime.RunSessionTurn(spec, prompt) with { TurnIndex = completedTurns };
        completedTurns++;
        totalUsage = totalUsage.Add(result.Usage);
        return Task.FromResult(result);
    }

    public Task CancelAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
