using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationInsightSynthesis;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Cli.Services.Cli;

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
        var repositoryArtifacts = new RepositoryArtifactStore(store, repository);
        var nonImplementationLedger = new NonImplementationReviewLedgerStore(repositoryArtifacts);
        var hitlRequestCapture = new ExplicitHitlNonImplementationRequestCaptureService(
            nonImplementationLedger);
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
        var repositoryChangeSetDetector = new RepositoryChangeSetDetector(processRunner, repository);
        var nonImplementationReviewRunner = new AgentNonImplementationReviewRunner(gatedRuntime, repository);
        var nonImplementationSemanticConfirmer = new NonImplementationSemanticConfirmer(
            nonImplementationLedger,
            nonImplementationReviewRunner);
        var projectionArtifacts = new ProjectionArtifacts(store, repository);
        var projectionRegistry = ProjectionDefinitionRegistry.CreateDefault();
        var projectionService = new ProjectContextProjectionService(
            projectionArtifacts,
            projectionRegistry,
            new ProjectionManifestStore(projectionArtifacts),
            new ProjectionValidator(projectionRegistry),
            new ProjectionPromptRunner(gatedRuntime, repository, console));
        var completionPromptRunner = new AgentCompletionPromptRunner(gatedRuntime, repository, promptPolicy);
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
        var nonImplementationInsightSynthesizer = new NonImplementationInsightSynthesizer(
            nonImplementationLedger,
            nonImplementationReviewRunner,
            repositoryArtifacts);
        var postExecutionReview = new NonImplementationPostExecutionReviewService(
            new RepositorySliceBaselineStore(
                repositoryChangeSetDetector,
                repositoryArtifacts),
            new NonImplementationArtifactClassifier(),
            nonImplementationSemanticConfirmer,
            repositoryArtifacts);
        var completionReview = new NonImplementationCompletionReviewService(
            repositoryChangeSetDetector,
            new NonImplementationArtifactClassifier(),
            nonImplementationSemanticConfirmer,
            nonImplementationLedger,
            repositoryArtifacts,
            repository.Path,
            nonImplementationInsightSynthesizer);
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
            promptPolicy: promptPolicy,
            hitlRequestCapture: hitlRequestCapture);
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
            postExecutionReview,
            completionReview,
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
