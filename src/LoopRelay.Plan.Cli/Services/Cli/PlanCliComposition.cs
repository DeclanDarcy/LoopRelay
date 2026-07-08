using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Plan.Cli.Services.Agents;
using LoopRelay.Plan.Cli.Services.Execution;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Plan.Cli.Services.Cli;

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
        var settings = CliSettingsLoader.Load();
        string promptPolicy = ImplementationFirstPromptPolicyComposer.Compose(settings.ArtifactPolicy);
        var services = new ServiceCollection();
        services.AddAgents(settings.Permissions);
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();

        ServiceProvider provider = services.BuildServiceProvider();
        var console = new ConsoleLoopConsole();

        var store = provider.GetRequiredService<IArtifactStore>();
        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var tokenEstimator = provider.GetRequiredService<IAgentTokenEstimator>();
        var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();
        var processRunner = provider.GetRequiredService<IProcessRunner>();

        var artifacts = new PlanArtifacts(store, repository);
        var hitlRequestCapture = new ExplicitHitlNonImplementationRequestCaptureService(
            new NonImplementationReviewLedgerStore(new RepositoryArtifactStore(store, repository)));
        var progressRuntime = new InputWaitProgressAgentRuntime(
            runtime,
            tokenEstimator,
            new ConsoleInputWaitProgressRenderer(console));
        var preflight = new PreflightGate(artifacts);
        var planSession = new PlanSession(progressRuntime, artifacts, console, repository, promptPolicy, hitlRequestCapture);
        var review = new ReviewStep(progressRuntime, artifacts, console, repository);
        var projectionArtifacts = new ProjectionArtifacts(store, repository);
        var projectionRegistry = ProjectionDefinitionRegistry.CreateDefault();
        var projectionService = new ProjectContextProjectionService(
            projectionArtifacts,
            projectionRegistry,
            new ProjectionManifestStore(projectionArtifacts),
            new ProjectionValidator(projectionRegistry),
            new ProjectionPromptRunner(progressRuntime, repository, console));
        var artifactOperation = new PermissionedArtifactOperationStep(progressRuntime, store, artifacts, console, repository);
        var publisher = new AgentsSubmodulePublisher(processRunner, repository, console);
        var pipeline = new PlanPipeline(
            preflight,
            planSession,
            review,
            projectionService,
            artifactOperation,
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
