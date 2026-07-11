using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Execution;

internal sealed class ScriptedAgentRuntime(params AgentTurnResult[] results) : IAgentRuntime
{
    public AgentRuntimeCapabilities Capabilities { get; } = new("test", true, true, true);

    private readonly Queue<AgentTurnResult> results = new(results);

    public int OneShotCalls { get; private set; }
    public int OpenSessionCalls { get; private set; }
    public int PersistentTurnCalls { get; private set; }
    public int CloseSessionCalls { get; private set; }
    public List<string> Prompts { get; } = [];
    public List<AgentSessionSpec> OneShotSpecs { get; } = [];
    public List<AgentSessionSpec> OpenedSpecs { get; } = [];

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        OpenSessionCalls++;
        OpenedSpecs.Add(spec);
        return Task.FromResult<IAgentSession>(new ScriptedAgentSession(this, spec));
    }

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        OneShotCalls++;
        OneShotSpecs.Add(spec);
        Prompts.Add(prompt);
        AgentTurnResult result = results.Count == 0
            ? Completed(string.Empty)
            : results.Dequeue();
        return Task.FromResult(result);
    }

    public async ValueTask CloseSessionAsync(IAgentSession session)
    {
        CloseSessionCalls++;
        await session.DisposeAsync();
    }

    public static AgentTurnResult Completed(string output) => new(0, AgentTurnState.Completed, output, AgentTokenUsage.Zero);

    public static AgentTurnResult Failed(string diagnostics = "failed") => new(0, AgentTurnState.Failed, string.Empty, AgentTokenUsage.Zero, diagnostics);

    private Task<AgentTurnResult> RunPersistentTurnAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk,
        CancellationToken cancellationToken)
    {
        PersistentTurnCalls++;
        Prompts.Add(prompt);
        AgentTurnResult result = results.Count == 0
            ? Completed(string.Empty)
            : results.Dequeue();
        return Task.FromResult(result);
    }

    private sealed class ScriptedAgentSession(ScriptedAgentRuntime runtime, AgentSessionSpec spec) : IAgentSession
    {
        public SessionIdentity SessionId => spec.SessionId;
        public string RepositoryId => spec.RepositoryId;
        public SessionRole Role => spec.Role;
        public AgentSessionMode Mode => AgentSessionMode.Persistent;
        public AgentProcessState State => AgentProcessState.Exited;
        public int CompletedTurns => 0;
        public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;
        public string? ThreadId => null;

        public Task<AgentTurnResult> RunTurnAsync(string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default) =>
            runtime.RunPersistentTurnAsync(spec, prompt, onChunk, cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
