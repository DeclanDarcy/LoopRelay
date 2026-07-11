using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Agents;

/// <summary>
/// M7/D3: the operational wrappers are composed once at the runtime boundary, each under its
/// policy toggle — a policy field that skipped composition would be a configured value with no
/// production effect.
/// </summary>
public sealed class OperationalRuntimeCompositionTests
{
    [Fact]
    public void Default_policy_composes_the_full_operational_chain()
    {
        var runtime = new CapabilityOnlyRuntime();

        IAgentRuntime composed = Compose(runtime);

        // Telemetry/usage-limit gate outermost of the operational layers; capability identity
        // forwards untouched through the whole chain.
        Assert.IsType<GatedAgentRuntime>(composed);
        Assert.Same(runtime.Capabilities, composed.Capabilities);
    }

    [Fact]
    public void Disabling_every_runtime_toggle_returns_the_bare_runtime()
    {
        var runtime = new CapabilityOnlyRuntime();

        IAgentRuntime composed = Compose(
            runtime,
            ("runtime.sessionTelemetry", "false"),
            ("runtime.usageLimitWaitRetry", "false"),
            ("runtime.inputWaitReporting", "false"));

        Assert.Same(runtime, composed);
    }

    [Fact]
    public void Input_wait_reporting_alone_composes_only_the_progress_wrapper()
    {
        var runtime = new CapabilityOnlyRuntime();

        IAgentRuntime composed = Compose(
            runtime,
            ("runtime.sessionTelemetry", "false"),
            ("runtime.usageLimitWaitRetry", "false"));

        Assert.IsType<InputWaitProgressAgentRuntime>(composed);
    }

    [Fact]
    public void Usage_limit_retry_alone_still_composes_the_gate()
    {
        var runtime = new CapabilityOnlyRuntime();

        IAgentRuntime composed = Compose(
            runtime,
            ("runtime.sessionTelemetry", "false"),
            ("runtime.inputWaitReporting", "false"));

        Assert.IsType<GatedAgentRuntime>(composed);
    }

    private static IAgentRuntime Compose(
        IAgentRuntime runtime,
        params (string Key, string Value)[] policyOverrides)
    {
        string repo = Directory.CreateTempSubdirectory("cc-operational-runtime").FullName;
        ResolvedOperationalPolicy policy = OperationalPolicyResolver.Resolve(
            CliPolicyDocument.Empty,
            "settings:test",
            policyOverrides
                .Select(item => new PolicyOverride(item.Key, item.Value, "flag:--policy", IsExplicit: true))
                .ToArray(),
            PermissionPolicyFactory.Minimum);
        return OperationalRuntimeComposition.Compose(
            runtime,
            policy,
            new Repository
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileName(repo),
                Path = repo,
            },
            new ProcessRunner(),
            new ConsoleLoopConsole(TextWriter.Null, TextWriter.Null));
    }

    private sealed class CapabilityOnlyRuntime : IAgentRuntime
    {
        public AgentRuntimeCapabilities Capabilities { get; } = new("test", true, true, true);

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition tests never open sessions.");

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition tests never run turns.");

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
    }
}
