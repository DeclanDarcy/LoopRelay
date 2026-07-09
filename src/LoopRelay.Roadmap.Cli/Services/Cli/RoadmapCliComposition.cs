using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationInsightSynthesis;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.EpicTransitions;
using LoopRelay.Roadmap.Cli.Services.Execution;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Roadmap.Cli.Services.Cli;

internal sealed class RoadmapCliComposition : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private RoadmapCliComposition(
        ServiceProvider provider,
        ConsoleLoopConsole console,
        IAgentExecutableResolver executableResolver,
        RoadmapStateMachine machine)
    {
        _provider = provider;
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

        IExecutionEvidenceStore executionEvidence = new FileBackedExecutionEvidenceStore(store, repository);
        var artifacts = new RoadmapArtifacts(store, repository, executionEvidence);
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
        IProjectionManifestStore manifestStore = new ProjectionManifestStore(artifacts);
        IExecutionPreparationManifestStore executionPreparationManifest = new ExecutionPreparationManifestStore(artifacts);
        var executionPreparation = new ExecutionPreparationProvenanceService(artifacts, executionPreparationManifest);
        var validator = new ProjectionValidator();
        var promptRunner = new RoadmapPromptRunner(progressRuntime, repository, console, promptPolicy);
        var projectionCache = new ProjectionCache(artifacts, projectionRegistry, manifestStore, validator, promptRunner);
        var contextBuilder = new RoadmapPromptContextBuilder(artifacts, executionPreparation);
        var inputResolver = new TransitionInputResolver(artifacts, executionPreparation);
        ISelectionProvenanceManifestStore selectionProvenanceManifest = new SelectionProvenanceManifestStore(artifacts);
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
        IRoadmapStateStore stateStore = new State.RoadmapStateStore(artifacts);
        IDecisionLedgerStore decisionLedger = new Decisions.DecisionLedgerStore(artifacts);
        var decisionRecorder = new DecisionRecorder(decisionLedger);
        ITransitionJournalStore journal = new TransitionJournalStore(artifacts);
        var transitionPersistence = new RoadmapTransitionPersistence(
            artifacts,
            manifestStore,
            stateStore,
            decisionLedger,
            journal);
        IArtifactLifecycleStore lifecycle = new ArtifactLifecycleStore(artifacts);
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
        var projectContextLoader = new Projections.ProjectContextLoader(artifacts);
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
        var bundleExtractor = new ArtifactBundles.BundleFileExtractor();
        var splitBundleInterpreter = new Splits.SplitEpicBundleInterpreter();
        var bundleManifest = new BundleManifestWriter(artifacts);
        ISplitFamilyStore splitFamilies = new SplitFamilyStore(artifacts);
        var activeEpicPromotionCoordinator = new ActiveEpicPromotionCoordinator(
            promotion,
            hitlArtifactCapture,
            journal,
            transitionPersistence);
        var createNewEpicTransition = new CreateNewEpicTransition(
            contractRegistry,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            console);
        var activeEpicRewriteTransition = new ActiveEpicRewriteTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            console);
        var epicPreparationAuditTransition = new EpicPreparationAuditTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            hitlArtifactCapture,
            decisionRecorder,
            stateStore,
            transitionPersistence,
            selectionSuperseder,
            activeEpicRewriteTransition,
            console);
        var splitEpicTransition = new SplitEpicTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            bundleExtractor,
            splitBundleInterpreter,
            bundleManifest,
            splitFamilies,
            lifecycle,
            journal,
            transitionPersistence,
            hitlArtifactCapture,
            console);
        var invariants = new InvariantValidator(
            artifacts,
            projectContextLoader,
            projectionRegistry,
            contractRegistry,
            manifestStore,
            lifecycle,
            splitFamilies,
            executionPreparation);
        var generateMilestoneDeepDivesTransition = new GenerateMilestoneDeepDivesTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            contextBuilder,
            promptTransitionRunner,
            bundleExtractor,
            bundleManifest,
            executionPreparation,
            invariants,
            journal,
            lifecycle,
            transitionPersistence,
            hitlArtifactCapture,
            console);
        var roadmapCompletionContextUpdateTransition = new RoadmapCompletionContextUpdateTransition(
            artifacts,
            contractRegistry,
            projectionCache,
            contextBuilder,
            promptTransitionRunner,
            selectionSuperseder,
            decisionRecorder,
            hitlArtifactCapture,
            console);
        var completionCertificationTransition = new CompletionCertificationTransition(
            artifacts,
            projectContextLoader,
            contractRegistry,
            projectionCache,
            contextBuilder,
            inputResolver,
            completionPolicy,
            completionRouter,
            completionArchive,
            transitionPersistence,
            promptTransitionRunner,
            roadmapCompletionContextUpdateTransition,
            decisionRecorder,
            journal,
            lifecycle,
            hitlArtifactCapture,
            console,
            nonImplementationCompletionReview);
        var machine = new RoadmapStateMachine(
            artifacts,
            projectContextLoader,
            contractRegistry,
            stateStore,
            transitionPersistence,
            bootstrapRoadmapCompletionContextTransition,
            selectNextEpicTransition,
            createNewEpicTransition,
            epicPreparationAuditTransition,
            splitEpicTransition,
            generateMilestoneDeepDivesTransition,
            completionCertificationTransition,
            activeSelectionReader,
            startupPlanner,
            resumePlanner,
            unblockPlanner,
            decisionRecorder,
            journal,
            lifecycle,
            console);

        return new RoadmapCliComposition(provider, console, executableResolver, machine);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider.GetService<AgentSessionRegistry>() is { } registry)
        {
            await registry.DisposeAsync();
        }

        await _provider.DisposeAsync();
    }
}
