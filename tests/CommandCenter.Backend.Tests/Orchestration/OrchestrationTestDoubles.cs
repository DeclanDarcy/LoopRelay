using System.Text;
using System.Text.RegularExpressions;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.Caching.Memory;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>Records every session opened and hands back a disposable fake per open.</summary>
internal sealed class FakeAgentRuntime : IAgentRuntime
{
    public List<AgentSessionSpec> OpenedSpecs { get; } = new();

    public List<FakeAgentSession> Sessions { get; } = new();

    public int OpenCount => OpenedSpecs.Count;

    /// <summary>When set, every opened session blocks its DisposeAsync until this task completes —
    /// lets a test hold a teardown mid-flight to observe the registry's create/teardown serialization.</summary>
    public Task? SessionDisposeGate { get; set; }

    /// <summary>Side effect run during each turn (e.g. simulate Codex writing <c>.agents/plan.md</c>).</summary>
    public Func<Task>? OnTurn { get; set; }

    /// <summary>Stdout chunks streamed (in order) before each turn completes.</summary>
    public IReadOnlyList<string> ScriptedChunks { get; set; } = Array.Empty<string>();

    /// <summary>When set, every turn parks (after recording the prompt) until this task completes — lets a
    /// test hold a planning turn ACTIVE to observe the concurrent-turn (409) rejection and the dispose drain.</summary>
    public Task? TurnGate { get; set; }

    /// <summary>Terminal state every turn reports.</summary>
    public AgentTurnState TurnState { get; set; } = AgentTurnState.Completed;

    /// <summary>The <see cref="AgentTurnResult.Output"/> every turn reports (surfaces as the failed-event detail).</summary>
    public string TurnOutput { get; set; } = string.Empty;

    /// <summary>Usage every turn reports.</summary>
    public AgentTokenUsage TurnUsage { get; set; } = new(10, 20);

    /// <summary>Scripted HELD-OPEN session turns dequeued in order by each session's <see cref="FakeAgentSession.RunTurnAsync"/>
    /// (m5 decision seed + GetNextDecisions, where the two turns need distinct state/output). When empty, a turn
    /// falls back to the shared <see cref="TurnState"/>/<see cref="TurnOutput"/>/<see cref="ScriptedChunks"/> above
    /// (back-compat with the m3 planning tests).</summary>
    public Queue<FakeOneShotTurn> SessionTurns { get; } = new();

    /// <summary>Scripted one-shot turns dequeued in order by <see cref="RunOneShotAsync"/> (m4 milestone
    /// extraction + start execution). When empty, a one-shot completes with no output (back-compat).</summary>
    public Queue<FakeOneShotTurn> OneShotTurns { get; } = new();

    /// <summary>Every one-shot prompt run, in order (lets a test confirm the run reached a phase).</summary>
    public List<string> OneShotPrompts { get; } = new();

    /// <summary>Every one-shot spec, in order (role/sandbox/effort assertions).</summary>
    public List<AgentSessionSpec> OneShotSpecs { get; } = new();

    /// <summary>When set, every one-shot parks (after recording the prompt) until this task completes —
    /// lets a test hold an execution run ACTIVE to observe the dispose drain.</summary>
    public Task? OneShotGate { get; set; }

    /// <summary>Sessions closed through <see cref="CloseSessionAsync"/> (m10 single-sited teardown), in order.</summary>
    public List<IAgentSession> ClosedSessions { get; } = new();

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        OpenedSpecs.Add(spec);
        var session = new FakeAgentSession(spec, this);
        Sessions.Add(session);
        return Task.FromResult<IAgentSession>(session);
    }

    public async ValueTask CloseSessionAsync(IAgentSession session)
    {
        // The fake holds no registry, so closing just disposes (matching the runtime's "not registered ->
        // dispose directly" fallback). Recording the close lets a test assert single-sited teardown happened.
        ClosedSessions.Add(session);
        await session.DisposeAsync().ConfigureAwait(false);
    }

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        OneShotPrompts.Add(prompt);
        OneShotSpecs.Add(spec);

        if (OneShotGate is not null)
        {
            await OneShotGate.ConfigureAwait(false);
        }

        FakeOneShotTurn turn = OneShotTurns.Count > 0 ? OneShotTurns.Dequeue() : new FakeOneShotTurn();

        if (onChunk is not null && turn.Chunks is not null)
        {
            foreach (string chunk in turn.Chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await onChunk(new AgentStreamChunk(0, AgentProcessOutputStream.StandardOutput, chunk)).ConfigureAwait(false);
            }
        }

        if (turn.Effect is not null)
        {
            await turn.Effect().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new AgentTurnResult(0, turn.State, turn.Output, turn.Usage ?? AgentTokenUsage.Zero);
    }
}

/// <summary>One scripted one-shot turn: its terminal state, output, optional streamed chunks, an optional
/// side effect (e.g. simulate Codex writing milestone files or the handoff), and reported usage.</summary>
internal sealed record FakeOneShotTurn(
    AgentTurnState State = AgentTurnState.Completed,
    string Output = "",
    Func<Task>? Effect = null,
    IReadOnlyList<string>? Chunks = null,
    AgentTokenUsage? Usage = null);

internal sealed class FakeAgentSession : IAgentSession
{
    private readonly AgentSessionSpec spec;
    private readonly FakeAgentRuntime runtime;
    private int turnCount;

    public FakeAgentSession(AgentSessionSpec spec, FakeAgentRuntime runtime)
    {
        this.spec = spec;
        this.runtime = runtime;
        SessionId = spec.SessionId;
    }

    public bool Disposed { get; private set; }

    /// <summary>Every prompt this session was asked to run, in order.</summary>
    public List<string> Prompts { get; } = new();

    public SessionIdentity SessionId { get; }

    public string RepositoryId => spec.RepositoryId;

    public SessionRole Role => spec.Role;

    public AgentSessionMode Mode => AgentSessionMode.Persistent;

    public AgentProcessState State => Disposed ? AgentProcessState.Disposed : AgentProcessState.Running;

    public int CompletedTurns => turnCount;

    public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        Prompts.Add(prompt);

        if (runtime.TurnGate is not null)
        {
            await runtime.TurnGate.ConfigureAwait(false);
        }

        FakeOneShotTurn? scripted = runtime.SessionTurns.Count > 0 ? runtime.SessionTurns.Dequeue() : null;

        if (onChunk is not null)
        {
            foreach (string chunk in scripted?.Chunks ?? runtime.ScriptedChunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await onChunk(new AgentStreamChunk(turnCount, AgentProcessOutputStream.StandardOutput, chunk));
            }
        }

        if (scripted?.Effect is not null)
        {
            await scripted.Effect();
        }
        else if (runtime.OnTurn is not null)
        {
            await runtime.OnTurn();
        }

        cancellationToken.ThrowIfCancellationRequested();
        int index = turnCount++;
        return scripted is not null
            ? new AgentTurnResult(index, scripted.State, scripted.Output, scripted.Usage ?? runtime.TurnUsage)
            : new AgentTurnResult(index, runtime.TurnState, runtime.TurnOutput, runtime.TurnUsage);
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (runtime.SessionDisposeGate is not null)
        {
            await runtime.SessionDisposeGate.ConfigureAwait(false);
        }

        Disposed = true;
    }
}

/// <summary>In-memory artifact store. <see cref="ExistsResult"/> forces an existence answer for the
/// plan-status gate; writes are observable through <see cref="ReadAsync"/> and <see cref="ExistsAsync"/>.</summary>
internal sealed class FakeArtifactStore : IArtifactStore
{
    private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);

    /// <summary>When true, every path reports as existing regardless of writes (drives plan-status tests).</summary>
    public bool ExistsResult { get; set; }

    /// <summary>
    /// m10 recovery-window hook (additive, default-null => existing tests unaffected): when set and the predicate
    /// matches the write path, <see cref="WriteAsync"/> throws an <see cref="IOException"/> BEFORE persisting, so a
    /// test can model an intermediate store-write failure mid-rotation/mid-window. Modeled on the existing
    /// PathFailingArtifactStore (OperationalContextGenerationTests).
    /// </summary>
    public Func<string, bool>? FailWriteOn { get; set; }

    /// <summary>
    /// m10 recovery-window hook (additive, default-null => existing tests unaffected): when set and the predicate
    /// matches the delete path, <see cref="DeleteAsync"/> throws an <see cref="IOException"/> instead of removing the
    /// file, so a test can model a failed delete (e.g. the live-handoff delete in a rotation block).
    /// </summary>
    public Func<string, bool>? FailDeleteOn { get; set; }

    public List<string> ExistsQueries { get; } = new();

    public List<string> WriteQueries { get; } = new();

    public List<string> DeleteQueries { get; } = new();

    public List<string> ListQueries { get; } = new();

    public Task<bool> ExistsAsync(string path)
    {
        ExistsQueries.Add(path);
        return Task.FromResult(ExistsResult || files.ContainsKey(path));
    }

    public Task<string?> ReadAsync(string path) =>
        Task.FromResult(files.TryGetValue(path, out string? content) ? content : null);

    public Task WriteAsync(string path, string content)
    {
        // Recovery-window hook: fail BEFORE recording the write or mutating the file, so the path is neither queued
        // nor persisted — exactly what a real store does when the OS rejects the write mid-window (default-null: no-op).
        if (FailWriteOn is not null && FailWriteOn(path))
        {
            throw new IOException($"Configured write failure for {System.IO.Path.GetFileName(path)}.");
        }

        WriteQueries.Add(path);
        files[path] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path)
    {
        DeleteQueries.Add(path);

        // Recovery-window hook: fail the delete AFTER recording the attempt but WITHOUT removing the file, so the
        // (e.g. live handoff) artifact stays readable — modeling a failed rotation delete (default-null: no-op).
        if (FailDeleteOn is not null && FailDeleteOn(path))
        {
            throw new IOException($"Configured delete failure for {System.IO.Path.GetFileName(path)}.");
        }

        files.Remove(path);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
    {
        ListQueries.Add(path);
        string directory = Normalize(path).TrimEnd('/');
        Regex pattern = GlobToRegex(searchPattern);

        var matches = new List<string>();
        foreach (string key in files.Keys)
        {
            string normalizedKey = Normalize(key);
            int lastSlash = normalizedKey.LastIndexOf('/');
            string keyDirectory = lastSlash >= 0 ? normalizedKey[..lastSlash] : string.Empty;
            string fileName = lastSlash >= 0 ? normalizedKey[(lastSlash + 1)..] : normalizedKey;
            if (string.Equals(keyDirectory, directory, StringComparison.OrdinalIgnoreCase) && pattern.IsMatch(fileName))
            {
                matches.Add(key);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(matches);
    }

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static Regex GlobToRegex(string searchPattern)
    {
        var builder = new StringBuilder("^");
        foreach (char character in searchPattern)
        {
            builder.Append(character switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(character.ToString()),
            });
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase);
    }
}

/// <summary>
/// Scriptable decision-session router (m7). Returns <see cref="Route"/> by default; when <see cref="Routes"/>
/// is non-empty it dequeues one route per call (so a test can route iteration 1 to Continue and iteration 2 to
/// Transfer), falling back to <see cref="Route"/> once drained. Every <see cref="RouterInputs"/> it was asked
/// to evaluate is recorded, and <see cref="Throw"/> forces a routing fault to exercise the degrade-to-Continue
/// guard.
/// </summary>
internal sealed class FakeDecisionSessionRouter : IDecisionSessionRouter
{
    public DecisionRoute Route { get; set; } = DecisionRoute.Continue;

    public Queue<DecisionRoute> Routes { get; } = new();

    public List<RouterInputs> EvaluatedInputs { get; } = new();

    public int EvaluateCount => EvaluatedInputs.Count;

    public bool Throw { get; set; }

    public DecisionRoute Evaluate(RouterInputs inputs)
    {
        EvaluatedInputs.Add(inputs);
        if (Throw)
        {
            throw new InvalidOperationException("router fault");
        }

        return Routes.Count > 0 ? Routes.Dequeue() : Route;
    }
}

/// <summary>Records each commit+push and returns a configurable result (default: success, pushed).</summary>
internal sealed class FakePlanArtifactPublisher : IPlanArtifactPublisher
{
    public List<(string Message, IReadOnlyList<string> Paths)> Publications { get; } = new();

    public PlanPublicationResult Result { get; set; } = PlanPublicationResult.Success("commit-sha", pushed: true);

    public int PublishCount => Publications.Count;

    public Task<PlanPublicationResult> PublishAsync(
        Repository repository,
        string commitMessage,
        IReadOnlyList<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default)
    {
        Publications.Add((commitMessage, repositoryRelativePaths));
        return Task.FromResult(Result);
    }
}

internal static class OrchestrationTestFactory
{
    public static Repository Repository(string? path = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "fixture",
            Path = path ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        };

    public static MemoryCache Cache() => new(new MemoryCacheOptions());

    public static RepositoryOrchestratorRegistry Registry(
        FakeAgentRuntime? runtime = null,
        FakeArtifactStore? store = null,
        MemoryCache? cache = null,
        IPlanArtifactPublisher? publisher = null,
        IDecisionSessionRouter? router = null,
        OrchestrationFeatureFlags? flags = null) =>
        new(
            runtime ?? new FakeAgentRuntime(),
            store ?? new FakeArtifactStore(),
            cache ?? Cache(),
            publisher ?? new FakePlanArtifactPublisher(),
            router ?? new FakeDecisionSessionRouter(),
            flags ?? new OrchestrationFeatureFlags());

    public static RepositoryOrchestrator Orchestrator(
        string? repositoryId = null,
        FakeAgentRuntime? runtime = null,
        FakeArtifactStore? store = null,
        MemoryCache? cache = null,
        IPlanArtifactPublisher? publisher = null,
        IDecisionSessionRouter? router = null,
        OrchestrationFeatureFlags? flags = null) =>
        new(
            repositoryId ?? Guid.NewGuid().ToString("D"),
            runtime ?? new FakeAgentRuntime(),
            store ?? new FakeArtifactStore(),
            cache ?? Cache(),
            publisher ?? new FakePlanArtifactPublisher(),
            router ?? new FakeDecisionSessionRouter(),
            flags ?? new OrchestrationFeatureFlags());
}
