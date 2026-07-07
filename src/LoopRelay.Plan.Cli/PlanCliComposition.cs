using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Diagnostics;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Services;
using LoopRelay.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Plan.Cli;

internal sealed class PlanCliComposition : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    private PlanCliComposition(
        ServiceProvider provider,
        ConsoleLoopConsole console,
        IAgentExecutableResolver executableResolver,
        PlanPipeline pipeline)
    {
        this.provider = provider;
        Console = console;
        ExecutableResolver = executableResolver;
        Pipeline = pipeline;
    }

    public ConsoleLoopConsole Console { get; }

    public IAgentExecutableResolver ExecutableResolver { get; }

    public PlanPipeline Pipeline { get; }

    public static PlanCliComposition Create(Repository repository)
    {
        var services = new ServiceCollection();
        services.AddAgents();
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();

        ServiceProvider provider = services.BuildServiceProvider();
        var console = new ConsoleLoopConsole();

        var store = provider.GetRequiredService<IArtifactStore>();
        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var tokenEstimator = provider.GetRequiredService<IAgentTokenEstimator>();
        var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();
        var processRunner = provider.GetRequiredService<IProcessRunner>();
        ISandboxWorkspaceFactory sandboxFactory = new TempSandboxWorkspaceFactory();

        var artifacts = new PlanArtifacts(store, repository);
        var progressRuntime = new InputWaitProgressAgentRuntime(
            runtime,
            tokenEstimator,
            new ConsoleInputWaitProgressRenderer(console));
        var preflight = new PreflightGate(artifacts);
        var planSession = new PlanSession(progressRuntime, artifacts, console, repository);
        var review = new ReviewStep(progressRuntime, artifacts, console, repository);
        var projectionArtifacts = new ProjectionArtifacts(store, repository);
        var projectionRegistry = ProjectionDefinitionRegistry.CreateDefault();
        var projectionService = new ProjectContextProjectionService(
            projectionArtifacts,
            projectionRegistry,
            new ProjectionManifestStore(projectionArtifacts),
            new ProjectionValidator(projectionRegistry),
            new ProjectionPromptRunner(progressRuntime, repository, console));
        var oneShot = new SandboxedPromptStep(progressRuntime, sandboxFactory, artifacts, console, repository);
        var publisher = new AgentsSubmodulePublisher(processRunner, repository, console);
        var pipeline = new PlanPipeline(
            preflight,
            planSession,
            review,
            projectionService,
            oneShot,
            publisher,
            artifacts,
            console);

        return new PlanCliComposition(provider, console, executableResolver, pipeline);
    }

    public async ValueTask DisposeAsync()
    {
        await Pipeline.DisposeAsync();
        if (provider.GetService<AgentSessionRegistry>() is { } registry)
        {
            await registry.DisposeAsync();
        }

        await provider.DisposeAsync();
    }
}
