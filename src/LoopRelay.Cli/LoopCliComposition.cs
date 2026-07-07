using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Diagnostics;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Configuration;
using LoopRelay.Projections;
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
        var settings = CliSettingsLoader.Load();
        string promptPolicy = ImplementationFirstPromptPolicyComposer.Compose(settings.ArtifactPolicy);
        var services = new ServiceCollection();
        services.AddAgents(settings.Permissions);
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
        services.AddSingleton(new DecisionSessionRouterOptions());
        services.AddSingleton<IDecisionSessionRouter, DecisionSessionRouter>();

        ServiceProvider provider = services.BuildServiceProvider();
        var console = new ConsoleLoopConsole();

        var store = provider.GetRequiredService<IArtifactStore>();
        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var tokenEstimator = provider.GetRequiredService<IAgentTokenEstimator>();
        var router = provider.GetRequiredService<IDecisionSessionRouter>();
        var processRunner = provider.GetRequiredService<IProcessRunner>();
        var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();

        var artifacts = new LoopArtifacts(store, repository);
        var inputWaitObservations = new InputWaitObservationStore();
        var progressRuntime = new InputWaitProgressAgentRuntime(
            runtime,
            tokenEstimator,
            new ConsoleInputWaitProgressRenderer(console),
            inputWaitObservations);
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
            progressRuntime,
            usageLimit,
            telemetryRecorder,
            telemetryClock,
            SessionTelemetryComposition.RepoName(repository),
            inputWaitObservations);
        var gate = new MilestoneGate(store, repository);
        var changeDetector = new WorkingTreeChangeDetector(processRunner, repository);
        var resumeStore = new FileDecisionSessionResumeStore(repository, console.Warn);
        resumeStore.EnsureDirectoryProtection();
        var projectionArtifacts = new ProjectionArtifacts(store, repository);
        var projectionRegistry = ProjectionDefinitionRegistry.CreateDefault();
        var projectionService = new ProjectContextProjectionService(
            projectionArtifacts,
            projectionRegistry,
            new ProjectionManifestStore(projectionArtifacts),
            new ProjectionValidator(projectionRegistry),
            new ProjectionPromptRunner(gatedRuntime, repository, console));
        var completionPromptRunner = new AgentCompletionPromptRunner(gatedRuntime, repository);
        var completionObserver = new ConsoleCompletionObserver(console);
        var completionArchive = new CompletedEpicArchiveService(
            store,
            completionPromptRunner,
            completionObserver);
        var completionCertification = new CompletionCertificationService(
            store,
            projectionService,
            completionPromptRunner,
            completionArchive,
            observer: completionObserver);
        var execution = new ExecutionStep(
            gatedRuntime,
            artifacts,
            console,
            repository,
            changeDetector,
            gate,
            promptPolicy);
        var decision = new DecisionSession(
            gatedRuntime,
            router,
            artifacts,
            console,
            repository,
            resumeStore: resumeStore,
            projectionService: projectionService,
            resumeEnabled: DecisionResumeComposition.IsEnabled(),
            promptPolicy: promptPolicy);
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
            completionCertification,
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

internal sealed class ConsoleCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    public void Phase(string phase) => console.Phase(phase);

    public void Info(string text) => console.Info(text);

    public void Warn(string text) => console.Warn(text);
}
