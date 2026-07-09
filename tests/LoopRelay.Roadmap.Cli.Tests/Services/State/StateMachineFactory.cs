using LoopRelay.Agents.Abstractions;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.EpicTransitions;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using BundleFileExtractor = LoopRelay.Roadmap.Cli.Services.ArtifactBundles.BundleFileExtractor;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.Services.Decisions.DecisionLedgerStore;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;
using SplitEpicBundleInterpreter = LoopRelay.Roadmap.Cli.Services.Splits.SplitEpicBundleInterpreter;

namespace LoopRelay.Roadmap.Cli.Tests.Services.State;

internal static class StateMachineFactory
{
    public static RoadmapStateMachine Create(
        TempRepo repo,
        IAgentRuntime runtime,
        ILoopConsole? console = null,
        ExplicitHitlNonImplementationRequestCaptureService? hitlRequestCapture = null,
        ICompletedEpicArchiveService? completionArchive = null)
    {
        ILoopConsole effectiveConsole = console ?? new TestConsole();
        var projections = new ProjectionRegistry();
        var contracts = new PromptContractRegistry(projections);
        var manifest = new ProjectionManifestStore(repo.Artifacts);
        var executionPreparationManifest = new ExecutionPreparationManifestStore(repo.Artifacts);
        var executionPreparation = new ExecutionPreparationProvenanceService(repo.Artifacts, executionPreparationManifest);
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        var decisionRecorder = new DecisionRecorder(decisionLedger);
        var journal = new TransitionJournalStore(repo.Artifacts);
        var split = new SplitFamilyStore(repo.Artifacts);
        var transitionPersistence = new RoadmapTransitionPersistence(
            repo.Artifacts,
            manifest,
            stateStore,
            decisionLedger,
            journal,
            split);
        var loader = new ProjectContextLoader(repo.Artifacts);
        var runner = new RoadmapPromptRunner(runtime, repo.Repository, effectiveConsole);
        var projectionCache = new ProjectionCache(repo.Artifacts, projections, manifest, new ProjectionValidator(), runner);
        var contextBuilder = new RoadmapPromptContextBuilder(repo.Artifacts, executionPreparation);
        var inputResolver = new TransitionInputResolver(repo.Artifacts, executionPreparation);
        var promptTransitionRunner = new RoadmapPromptTransitionRunner(
            inputResolver,
            runner,
            journal,
            transitionPersistence);
        var hitlArtifactCapture = new HitlArtifactCapture(hitlRequestCapture);
        var bootstrapRoadmapCompletionContextTransition = new BootstrapRoadmapCompletionContextTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            promptTransitionRunner,
            hitlArtifactCapture,
            lifecycle,
            effectiveConsole);
        var selectionProvenance = new SelectionProvenanceService(
            repo.Artifacts,
            new SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
        var selectNextEpicTransition = new SelectNextEpicTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            stateStore,
            promptTransitionRunner,
            selectionProvenance,
            decisionRecorder,
            hitlArtifactCapture,
            lifecycle,
            effectiveConsole);
        var activeSelectionReader = new ActiveSelectionReader(
            repo.Artifacts,
            stateStore,
            selectionProvenance);
        var selectionSuperseder = new SelectionSuperseder(selectionProvenance, lifecycle);
        var activeEpicPromotionCoordinator = new ActiveEpicPromotionCoordinator(
            new ArtifactPromotionService(repo.Artifacts, lifecycle),
            hitlArtifactCapture,
            journal,
            transitionPersistence);
        var createNewEpicTransition = new CreateNewEpicTransition(
            contracts,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            effectiveConsole);
        var activeEpicRewriteTransition = new ActiveEpicRewriteTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            effectiveConsole);
        var epicPreparationAuditTransition = new EpicPreparationAuditTransition(
            repo.Artifacts,
            contracts,
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
            effectiveConsole);
        var bundleExtractor = new BundleFileExtractor();
        var splitBundleInterpreter = new SplitEpicBundleInterpreter();
        var bundleManifest = new BundleManifestWriter(repo.Artifacts);
        var splitEpicTransition = new SplitEpicTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            bundleExtractor,
            splitBundleInterpreter,
            split,
            lifecycle,
            journal,
            transitionPersistence,
            hitlArtifactCapture,
            effectiveConsole);
        var invariants = new InvariantValidator(repo.Artifacts, loader, projections, contracts, manifest, lifecycle, split, executionPreparation);
        var generateMilestoneDeepDivesTransition = new GenerateMilestoneDeepDivesTransition(
            repo.Artifacts,
            contracts,
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
            effectiveConsole);
        var roadmapCompletionContextUpdateTransition = new RoadmapCompletionContextUpdateTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            promptTransitionRunner,
            selectionSuperseder,
            decisionRecorder,
            hitlArtifactCapture,
            effectiveConsole);
        var completionCertificationTransition = new CompletionCertificationTransition(
            repo.Artifacts,
            loader,
            contracts,
            projectionCache,
            contextBuilder,
            inputResolver,
            new CompletionCertificationPolicy(),
            new CompletionCertificationRouter(),
            completionArchive ?? new FakeCompletedEpicArchiveService(),
            transitionPersistence,
            promptTransitionRunner,
            roadmapCompletionContextUpdateTransition,
            decisionRecorder,
            journal,
            lifecycle,
            hitlArtifactCapture,
            effectiveConsole);
        var resumePlanner = new RoadmapResumePlanner(repo.Artifacts, contracts, manifest, lifecycle, new ProjectionProvenanceFactory(projections), selectionProvenance, executionPreparation);
        var unblockPlanner = new RoadmapUnblockPlanner(repo.Artifacts, loader, contracts, new CompletionCertificationPolicy(), new CompletionCertificationRouter(), executionPreparation);
        return new RoadmapStateMachine(
            repo.Artifacts,
            loader,
            contracts,
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
            new RoadmapStartupPlanner(),
            resumePlanner,
            unblockPlanner,
            journal,
            lifecycle,
            effectiveConsole);
    }
}
