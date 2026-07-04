using System.Reflection;
using System.Text.Json;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// m10 (D) deterministic fallback certification: (1) the live <see cref="RepositoryOrchestrator"/> / Orchestration
/// runtime author decision proposals via Codex (the held-open Decision session's RunTurnAsync), NOT via the offline
/// <see cref="IDecisionGenerationService"/>/<see cref="IRecommendationService"/> — those are the offline
/// EndpointFamily.Decisions FALLBACK authoring path only; and (2) the governed estimator fallback ((len+3)/4) fires
/// EXACTLY when Codex token authority is absent (observed usage ALWAYS wins when present — never inverted).
/// </summary>
public sealed class OrchestrationDeterministicFallbackTests
{
    // ---------------------------------------------------------------------------------------------------------
    // LAYERING: the orchestrator's decision authoring goes through the Agents runtime (session.RunTurnAsync), so it
    // must take NO type dependency on the offline decision-generation services. Asserted over the compiled type's
    // constructor parameters, fields, and method signatures — a DI-injected dependency would surface in all three.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void RepositoryOrchestrator_takes_no_dependency_on_the_offline_decision_generation_services()
    {
        Type orchestrator = typeof(RepositoryOrchestrator);
        Type[] forbidden = { typeof(IDecisionGenerationService), typeof(IRecommendationService) };

        // Constructor parameters (the DI injection surface).
        foreach (ConstructorInfo ctor in orchestrator.GetConstructors())
        {
            foreach (ParameterInfo parameter in ctor.GetParameters())
            {
                Assert.DoesNotContain(parameter.ParameterType, forbidden);
            }
        }

        // Instance + static fields (a captured/cached service handle).
        foreach (FieldInfo field in orchestrator.GetFields(
                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Assert.DoesNotContain(field.FieldType, forbidden);
        }

        // Method parameters + return types (a service passed through any method).
        foreach (MethodInfo method in orchestrator.GetMethods(
                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            Assert.DoesNotContain(method.ReturnType, forbidden);
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Assert.DoesNotContain(parameter.ParameterType, forbidden);
            }
        }
    }

    [Fact]
    public void The_orchestration_runtime_assembly_does_not_consume_the_offline_decision_services_in_orchestrator_types()
    {
        // The live authoring path is the Decision session's RunTurnAsync; the offline DecisionGenerationService /
        // RecommendationService live in CommandCenter.Decisions and are the EndpointFamily.Decisions fallback only.
        // No orchestration *Orchestrator/*Runtime type may field-reference either service interface.
        Assembly orchestration = typeof(RepositoryOrchestrator).Assembly;
        Type[] forbidden = { typeof(IDecisionGenerationService), typeof(IRecommendationService) };

        foreach (Type type in orchestration.GetTypes()
                     .Where(t => t.Name.Contains("Orchestrator") || t.Name.Contains("Runtime")))
        {
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.DoesNotContain(field.FieldType, forbidden);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // ESTIMATOR FALLBACK (exact value): when the process emits NO token usage, the governed (len+3)/4 estimate is
    // used for BOTH prompt and output — and ONLY then (a process that DOES report usage uses the observed value).
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CodexAppServerSession_falls_back_to_the_governed_estimate_when_no_token_usage_is_reported()
    {
        const string prompt = "estimate me please";       // (18 + 3) / 4 = 5
        const string reply = "deterministic reply text";  // (24 + 3) / 4 = 6
        var process = new NoUsageAppServerProcess(reply);
        await using var session = new CodexAppServerSession(Spec(), process, new DeterministicAgentTokenEstimator());

        AgentTurnResult result = await session.RunTurnAsync(prompt);

        Assert.Equal((prompt.Length + 3) / 4, result.Usage.PromptTokens);
        Assert.Equal((reply.Length + 3) / 4, result.Usage.OutputTokens);
    }

    [Fact]
    public async Task AgentSession_oneshot_falls_back_to_the_governed_estimate_when_no_boundary_usage_is_reported()
    {
        const string prompt = "one shot prompt body";     // (20 + 3) / 4 = 5
        const string reply = "one shot reply body!!";      // (21 + 3) / 4 = 6
        var process = new NoUsageOneShotProcess(reply);
        await using var session = new AgentSession(
            Spec(), AgentSessionMode.OneShot, process, new CodexEventTurnBoundaryDetector(), new DeterministicAgentTokenEstimator());

        AgentTurnResult result = await session.RunTurnAsync(prompt);

        Assert.Equal((prompt.Length + 3) / 4, result.Usage.PromptTokens);
        Assert.Equal((reply.Length + 3) / 4, result.Usage.OutputTokens);
    }

    [Fact]
    public async Task Observed_token_usage_always_wins_over_the_estimate_when_present()
    {
        // The certification's "do not invert this": a process that DOES report usage uses the observed value, never
        // the (len+3)/4 estimate — so the fallback fires strictly in the absence of Codex authority.
        const string prompt = "estimate me please";
        var process = new ReportsUsageAppServerProcess(promptTokens: 999, outputTokens: 777);
        await using var session = new CodexAppServerSession(Spec(), process, new DeterministicAgentTokenEstimator());

        AgentTurnResult result = await session.RunTurnAsync(prompt);

        Assert.Equal(999, result.Usage.PromptTokens);  // observed, not (18+3)/4 == 5
        Assert.Equal(777, result.Usage.OutputTokens);
    }

    // ---- helpers / fakes ----

    private static AgentSessionSpec Spec() => new(
        SessionIdentity.New(),
        "repo-estimate",
        SessionRole.OperationalExecution,
        new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
        new EffortProfile(AgentEffortLevel.Medium),
        workingDirectory: "/repo");

    /// <summary>App-server process that does the handshake and a turn with a delta but emits NO tokenUsage frame.</summary>
    private sealed class NoUsageAppServerProcess(string reply) : AppServerProcessBase
    {
        protected override void OnTurnStart(long id, int index)
        {
            EmitResponse(id, new { turn = new { id = $"u{index}", status = "inProgress" } });
            EmitNotification("item/agentMessage/delta", new { itemId = $"i{index}", delta = reply });
            // No thread/tokenUsage/updated frame: the session must fall back to the deterministic estimate.
            EmitNotification("turn/completed", new { turn = new { id = $"u{index}", status = "completed" } });
        }
    }

    /// <summary>App-server process that reports explicit token usage (observed authority present).</summary>
    private sealed class ReportsUsageAppServerProcess(int promptTokens, int outputTokens) : AppServerProcessBase
    {
        protected override void OnTurnStart(long id, int index)
        {
            EmitResponse(id, new { turn = new { id = $"u{index}", status = "inProgress" } });
            EmitNotification("item/agentMessage/delta", new { itemId = $"i{index}", delta = "x" });
            EmitNotification("thread/tokenUsage/updated", new { tokenUsage = new { last = new { inputTokens = promptTokens, outputTokens } } });
            EmitNotification("turn/completed", new { turn = new { id = $"u{index}", status = "completed" } });
        }
    }

    private abstract class AppServerProcessBase : IAgentProcess
    {
        private readonly System.Threading.Channels.Channel<string> output =
            System.Threading.Channels.Channel.CreateUnbounded<string>(
                new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int turnCounter;

        public int ProcessId => 9001;
        public AgentProcessState State { get; private set; } = AgentProcessState.Running;
        public int? ExitCode => null;
        public bool HasExited => State != AgentProcessState.Running;
        public Task Completion => completion.Task;

        protected abstract void OnTurnStart(long id, int index);

        public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) =>
            WritePromptAsync(standardInput, cancellationToken);

        public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                React(line);
            }

            return Task.CompletedTask;
        }

        public Task CompleteInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async IAsyncEnumerable<string> ReadOutputLinesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (string line in output.Reader.ReadAllAsync(cancellationToken))
            {
                yield return line;
            }
        }

        public ValueTask DisposeAsync()
        {
            State = AgentProcessState.Disposed;
            output.Writer.TryComplete();
            completion.TrySetResult();
            return ValueTask.CompletedTask;
        }

        protected void EmitResponse(long id, object result) =>
            Emit(new Dictionary<string, object?> { ["id"] = id, ["result"] = result });

        protected void EmitNotification(string method, object @params) =>
            Emit(new Dictionary<string, object?> { ["method"] = method, ["params"] = @params });

        private void Emit(object frame) => output.Writer.TryWrite(JsonSerializer.Serialize(frame));

        private void React(string line)
        {
            long id = 0;
            string? method;
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                method = root.TryGetProperty("method", out JsonElement m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                if (root.TryGetProperty("id", out JsonElement i) && i.ValueKind == JsonValueKind.Number)
                {
                    id = i.GetInt64();
                }
            }
            catch (JsonException)
            {
                return;
            }

            switch (method)
            {
                case "initialize":
                    EmitResponse(id, new { userAgent = "x", codexHome = "h", platformFamily = "windows", platformOs = "windows" });
                    break;
                case "thread/start":
                    EmitResponse(id, new { thread = new { id = "thread-est" } });
                    break;
                case "turn/start":
                    OnTurnStart(id, ++turnCounter);
                    break;
            }
        }
    }

    /// <summary>One-shot exec process: emits a single agent-message line + a turn.completed WITHOUT usage, then ends.</summary>
    private sealed class NoUsageOneShotProcess(string reply) : IAgentProcess
    {
        private readonly System.Threading.Channels.Channel<string> output =
            System.Threading.Channels.Channel.CreateUnbounded<string>(
                new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId => 9002;
        public AgentProcessState State { get; private set; } = AgentProcessState.Running;
        public int? ExitCode => null;
        public bool HasExited => State != AgentProcessState.Running;
        public Task Completion => completion.Task;

        public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
        {
            // The agent reply (exec --json agent message), then a turn.completed carrying NO usage fields.
            output.Writer.TryWrite(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = reply } }));
            output.Writer.TryWrite(JsonSerializer.Serialize(new { type = "turn.completed", turn = new { status = "completed" } }));
            return Task.CompletedTask;
        }

        public Task CompleteInputAsync(CancellationToken cancellationToken = default)
        {
            output.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadOutputLinesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (string line in output.Reader.ReadAllAsync(cancellationToken))
            {
                yield return line;
            }
        }

        public ValueTask DisposeAsync()
        {
            State = AgentProcessState.Disposed;
            output.Writer.TryComplete();
            completion.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
