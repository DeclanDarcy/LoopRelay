using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Cli;

internal sealed class LoopCliComposition : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    private LoopCliComposition(
        ServiceProvider provider,
        ConsoleLoopConsole console,
        IAgentExecutableResolver executableResolver,
        LoopRunner loop)
    {
        this.provider = provider;
        Console = console;
        ExecutableResolver = executableResolver;
        Loop = loop;
    }

    public ConsoleLoopConsole Console { get; }

    public IAgentExecutableResolver ExecutableResolver { get; }

    public LoopRunner Loop { get; }

    public static LoopCliComposition Create(Repository repository)
    {
        var services = new ServiceCollection();
        services.AddAgents();
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        services.AddSingleton(new DecisionSessionRouterOptions());
        services.AddSingleton<IDecisionSessionRouter, DecisionSessionRouter>();

        ServiceProvider provider = services.BuildServiceProvider();
        var console = new ConsoleLoopConsole();

        var store = provider.GetRequiredService<IArtifactStore>();
        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var router = provider.GetRequiredService<IDecisionSessionRouter>();
        var processRunner = provider.GetRequiredService<IProcessRunner>();
        var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();

        var artifacts = new LoopArtifacts(store, repository);
        var usageProbe = new CodexUsageProbe(processRunner, executableResolver, repository);
        var telemetryClock = new SystemClock();
        var usageLimit = new UsageLimitDetector(telemetryClock, new TaskDelayScheduler(), console);
        var telemetryRecorder = SessionTelemetryComposition.CreateRecorder(
            repository,
            SessionTelemetryComposition.IsEnabled(),
            usageProbe,
            new EffectiveTokenCostModel(),
            telemetryClock,
            console);
        var gatedRuntime = new GatedAgentRuntime(
            runtime,
            usageLimit,
            telemetryRecorder,
            telemetryClock,
            SessionTelemetryComposition.RepoName(repository));
        var gate = new MilestoneGate(store, repository);
        var changeDetector = new WorkingTreeChangeDetector(processRunner, repository);
        var resumeStore = new FileDecisionSessionResumeStore(repository, console.Warn);
        resumeStore.EnsureDirectoryProtection();
        var execution = new ExecutionStep(gatedRuntime, artifacts, console, repository, changeDetector, gate);
        var decision = new DecisionSession(
            gatedRuntime,
            router,
            artifacts,
            console,
            repository,
            resumeStore: resumeStore,
            resumeEnabled: DecisionResumeComposition.IsEnabled());
        var submodulePublisher = new AgentsSubmodulePublisher(processRunner, repository, console);
        var commitGate = new CommitGate(changeDetector, processRunner, repository, console);
        var loop = new LoopRunner(
            gate,
            artifacts,
            execution,
            decision,
            submodulePublisher,
            commitGate,
            resumeStore,
            console);

        return new LoopCliComposition(provider, console, executableResolver, loop);
    }

    public async ValueTask DisposeAsync()
    {
        await Loop.DisposeAsync();
        if (provider.GetService<AgentSessionRegistry>() is { } registry)
        {
            await registry.DisposeAsync();
        }

        await provider.DisposeAsync();
    }
}
