using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Infrastructure.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Roadmap.Cli;
using BundleFileExtractor = LoopRelay.Roadmap.Cli.BundleFileExtractor;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.DecisionLedgerStore;
using ExecutionCompatibilityMaterializer = LoopRelay.Roadmap.Cli.ExecutionCompatibilityMaterializer;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;
using SplitEpicBundleInterpreter = LoopRelay.Roadmap.Cli.SplitEpicBundleInterpreter;

namespace LoopRelay.Roadmap.Cli.Tests;

internal static class StateMachineFactory
{
    public static Cli.RoadmapStateMachine Create(
        TempRepo repo,
        IAgentRuntime runtime,
        Cli.ILoopConsole? console = null,
        ExplicitHitlNonImplementationRequestCaptureService? hitlRequestCapture = null,
        ICompletedEpicArchiveService? completionArchive = null)
    {
        Cli.ILoopConsole effectiveConsole = console ?? new TestConsole();
        var projections = new Cli.ProjectionRegistry();
        var contracts = new Cli.PromptContractRegistry(projections);
        var manifest = new Cli.ProjectionManifestStore(repo.Artifacts);
        var executionPreparationManifest = new Cli.ExecutionPreparationManifestStore(repo.Artifacts);
        var executionPreparation = new Cli.ExecutionPreparationProvenanceService(repo.Artifacts, executionPreparationManifest);
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        var decisionRecorder = new Cli.DecisionRecorder(decisionLedger);
        var journal = new Cli.TransitionJournalStore(repo.Artifacts);
        var transitionPersistence = new Cli.RoadmapTransitionPersistence(
            repo.Artifacts,
            manifest,
            stateStore,
            decisionLedger,
            journal);
        var split = new Cli.SplitFamilyStore(repo.Artifacts);
        var loader = new ProjectContextLoader(repo.Artifacts);
        var runner = new Cli.RoadmapPromptRunner(runtime, repo.Repository, effectiveConsole);
        var projectionCache = new Cli.ProjectionCache(repo.Artifacts, projections, manifest, new Cli.ProjectionValidator(), runner);
        var contextBuilder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, executionPreparation);
        var inputResolver = new Cli.TransitionInputResolver(repo.Artifacts, executionPreparation);
        var promptTransitionRunner = new Cli.RoadmapPromptTransitionRunner(
            inputResolver,
            runner,
            journal,
            transitionPersistence);
        var hitlArtifactCapture = new Cli.HitlArtifactCapture(hitlRequestCapture);
        var bootstrapRoadmapCompletionContextTransition = new Cli.BootstrapRoadmapCompletionContextTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            promptTransitionRunner,
            hitlArtifactCapture,
            lifecycle,
            effectiveConsole);
        var selectionProvenance = new Cli.SelectionProvenanceService(
            repo.Artifacts,
            new Cli.SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
        var selectNextEpicTransition = new Cli.SelectNextEpicTransition(
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
        var activeSelectionReader = new Cli.ActiveSelectionReader(
            repo.Artifacts,
            stateStore,
            selectionProvenance);
        var selectionSuperseder = new Cli.SelectionSuperseder(selectionProvenance, lifecycle);
        var activeEpicPromotionCoordinator = new Cli.ActiveEpicPromotionCoordinator(
            new Cli.ArtifactPromotionService(repo.Artifacts, lifecycle),
            hitlArtifactCapture,
            journal,
            transitionPersistence);
        var createNewEpicTransition = new Cli.CreateNewEpicTransition(
            contracts,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            effectiveConsole);
        var activeEpicRewriteTransition = new Cli.ActiveEpicRewriteTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            effectiveConsole);
        var epicPreparationAuditTransition = new Cli.EpicPreparationAuditTransition(
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
        var bundleManifest = new Cli.BundleManifestWriter(repo.Artifacts);
        var splitEpicTransition = new Cli.SplitEpicTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            activeSelectionReader,
            promptTransitionRunner,
            activeEpicPromotionCoordinator,
            bundleExtractor,
            splitBundleInterpreter,
            bundleManifest,
            split,
            lifecycle,
            journal,
            transitionPersistence,
            hitlArtifactCapture,
            effectiveConsole);
        var invariants = new Cli.InvariantValidator(repo.Artifacts, loader, projections, contracts, manifest, lifecycle, split, executionPreparation);
        var generateMilestoneDeepDivesTransition = new Cli.GenerateMilestoneDeepDivesTransition(
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
        var roadmapCompletionContextUpdateTransition = new Cli.RoadmapCompletionContextUpdateTransition(
            repo.Artifacts,
            contracts,
            projectionCache,
            contextBuilder,
            promptTransitionRunner,
            selectionSuperseder,
            decisionRecorder,
            hitlArtifactCapture,
            effectiveConsole);
        var completionCertificationTransition = new Cli.CompletionCertificationTransition(
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
        var resumePlanner = new Cli.RoadmapResumePlanner(repo.Artifacts, contracts, manifest, lifecycle, new Cli.ProjectionProvenanceFactory(projections), selectionProvenance, executionPreparation);
        var unblockPlanner = new Cli.RoadmapUnblockPlanner(repo.Artifacts, loader, contracts, new CompletionCertificationPolicy(), new CompletionCertificationRouter(), executionPreparation);
        return new Cli.RoadmapStateMachine(
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
            new Cli.RoadmapStartupPlanner(),
            resumePlanner,
            unblockPlanner,
            decisionRecorder,
            journal,
            lifecycle,
            effectiveConsole);
    }
}
