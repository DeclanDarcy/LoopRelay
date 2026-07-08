using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Infrastructure.Diagnostics;
using LoopRelay.Infrastructure.Artifacts;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapCliComposition : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    private RoadmapCliComposition(
        ServiceProvider provider,
        ConsoleLoopConsole console,
        IAgentExecutableResolver executableResolver,
        RoadmapStateMachine machine)
    {
        this.provider = provider;
        Console = console;
        ExecutableResolver = executableResolver;
        Machine = machine;
    }

    public ConsoleLoopConsole Console { get; }

    public IAgentExecutableResolver ExecutableResolver { get; }

    public RoadmapStateMachine Machine { get; }

    public static RoadmapCliComposition Create(RoadmapCliInvocation invocation)
    {
        var settings = CliSettingsLoader.Load();
        string promptPolicy = ImplementationFirstPromptPolicyComposer.Compose(settings.ArtifactPolicy);
        var services = new ServiceCollection();
        services.AddAgents(settings.Permissions);
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();

        ServiceProvider provider = services.BuildServiceProvider();
        var console = new ConsoleLoopConsole();

        var repository = invocation.Repository;
        var store = provider.GetRequiredService<IArtifactStore>();
        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var tokenEstimator = provider.GetRequiredService<IAgentTokenEstimator>();
        var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();
        var processRunner = provider.GetRequiredService<IProcessRunner>();
        var progressRuntime = new InputWaitProgressAgentRuntime(
            runtime,
            tokenEstimator,
            new ConsoleInputWaitProgressRenderer(console));

        var artifacts = new RoadmapArtifacts(store, repository);
        var repositoryArtifacts = new RepositoryArtifactStore(store, repository);
        var nonImplementationLedger = new NonImplementationReviewLedgerStore(repositoryArtifacts);
        var hitlRequestCapture = new ExplicitHitlNonImplementationRequestCaptureService(
            nonImplementationLedger);
        var hitlArtifactCapture = new HitlArtifactCapture(hitlRequestCapture);
        var nonImplementationReviewRunner = new AgentNonImplementationReviewRunner(progressRuntime, repository);
        var nonImplementationSemanticConfirmer = new NonImplementationSemanticConfirmer(
            nonImplementationLedger,
            nonImplementationReviewRunner);
        var nonImplementationInsightSynthesizer = new NonImplementationInsightSynthesizer(
            nonImplementationLedger,
            nonImplementationReviewRunner,
            repositoryArtifacts);
        var nonImplementationCompletionReview = new NonImplementationCompletionReviewService(
            new RepositoryChangeSetDetector(processRunner, repository),
            new NonImplementationArtifactClassifier(),
            nonImplementationSemanticConfirmer,
            nonImplementationLedger,
            repositoryArtifacts,
            repository.Path,
            nonImplementationInsightSynthesizer);
        var projectionRegistry = new ProjectionRegistry();
        var provenanceFactory = new ProjectionProvenanceFactory(projectionRegistry);
        var contractRegistry = new PromptContractRegistry(projectionRegistry);
        var manifestStore = new ProjectionManifestStore(artifacts);
        var executionPreparationManifest = new ExecutionPreparationManifestStore(artifacts);
        var executionPreparation = new ExecutionPreparationProvenanceService(artifacts, executionPreparationManifest);
        var validator = new ProjectionValidator();
        var promptRunner = new RoadmapPromptRunner(progressRuntime, repository, console, promptPolicy);
        var projectionCache = new ProjectionCache(artifacts, projectionRegistry, manifestStore, validator, promptRunner);
        var contextBuilder = new RoadmapPromptContextBuilder(artifacts, executionPreparation);
        var inputResolver = new TransitionInputResolver(artifacts, executionPreparation);
        var selectionProvenanceManifest = new SelectionProvenanceManifestStore(artifacts);
        var selectionProvenance = new SelectionProvenanceService(
            artifacts,
            selectionProvenanceManifest,
            contextBuilder,
            inputResolver);
        var completionPolicy = new CompletionCertificationPolicy();
        var completionRouter = new CompletionCertificationRouter();
        var completionArchive = new CompletedEpicArchiveService(
            store,
            new AgentCompletionPromptRunner(progressRuntime, repository, promptPolicy),
            new RoadmapCompletionObserver(console));
        var stateStore = new RoadmapStateStore(artifacts);
        var decisionLedger = new DecisionLedgerStore(artifacts);
        var decisionRecorder = new DecisionRecorder(decisionLedger);
        var journal = new TransitionJournalStore(artifacts);
        var transitionPersistence = new RoadmapTransitionPersistence(
            artifacts,
            manifestStore,
            stateStore,
            decisionLedger,
            journal);
        var lifecycle = new ArtifactLifecycleStore(artifacts);
        var promptTransitionRunner = new RoadmapPromptTransitionRunner(
            inputResolver,
            promptRunner,
            journal,
            transitionPersistence);
        var bootstrapRoadmapCompletionContextTransition = new BootstrapRoadmapCompletionContextTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            promptTransitionRunner,
            hitlArtifactCapture,
            lifecycle,
            console);
        var selectNextEpicTransition = new SelectNextEpicTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            contextBuilder,
            stateStore,
            promptTransitionRunner,
            selectionProvenance,
            decisionRecorder,
            hitlArtifactCapture,
            lifecycle,
            console);
        var activeSelectionReader = new ActiveSelectionReader(
            artifacts,
            stateStore,
            selectionProvenance);
        var selectionSuperseder = new SelectionSuperseder(selectionProvenance, lifecycle);
        var startupPlanner = new RoadmapStartupPlanner();
        var projectContextLoader = new ProjectContextLoader(artifacts);
        var resumePlanner = new RoadmapResumePlanner(
            artifacts,
            contractRegistry,
            manifestStore,
            lifecycle,
            provenanceFactory,
            selectionProvenance,
            executionPreparation);
        var unblockPlanner = new RoadmapUnblockPlanner(
            artifacts,
            projectContextLoader,
            contractRegistry,
            completionPolicy,
            completionRouter,
            executionPreparation);
        var promotion = new ArtifactPromotionService(artifacts, lifecycle);
        var bundleExtractor = new BundleFileExtractor();
        var splitBundleInterpreter = new SplitEpicBundleInterpreter();
        var bundleManifest = new BundleManifestWriter(artifacts);
        var splitFamilies = new SplitFamilyStore(artifacts);
        var activeEpicPromotionCoordinator = new ActiveEpicPromotionCoordinator(
            promotion,
            hitlArtifactCapture,
            journal,
            transitionPersistence);
        var invariants = new InvariantValidator(
            artifacts,
            projectContextLoader,
            projectionRegistry,
            contractRegistry,
            manifestStore,
            lifecycle,
            splitFamilies,
            executionPreparation);
        var machine = new RoadmapStateMachine(
            artifacts,
            projectContextLoader,
            contractRegistry,
            projectionCache,
            contextBuilder,
            inputResolver,
            completionPolicy,
            completionRouter,
            completionArchive,
            stateStore,
            transitionPersistence,
            promptTransitionRunner,
            bootstrapRoadmapCompletionContextTransition,
            selectNextEpicTransition,
            activeSelectionReader,
            startupPlanner,
            resumePlanner,
            unblockPlanner,
            selectionSuperseder,
            decisionRecorder,
            journal,
            lifecycle,
            activeEpicPromotionCoordinator,
            bundleExtractor,
            splitBundleInterpreter,
            bundleManifest,
            splitFamilies,
            executionPreparation,
            invariants,
            console,
            hitlArtifactCapture,
            nonImplementationCompletionReview);

        return new RoadmapCliComposition(provider, console, executableResolver, machine);
    }

    public async ValueTask DisposeAsync()
    {
        if (provider.GetService<AgentSessionRegistry>() is { } registry)
        {
            await registry.DisposeAsync();
        }

        await provider.DisposeAsync();
    }
}

internal sealed class RoadmapCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    public void Phase(string phase) => console.Phase(phase);

    public void Info(string text) => console.Info(text);

    public void Warn(string text) => console.Warn(text);
}
