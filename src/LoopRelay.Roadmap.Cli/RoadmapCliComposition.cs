using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Core.Artifacts;
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
        var services = new ServiceCollection();
        services.AddAgents();
        services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();

        ServiceProvider provider = services.BuildServiceProvider();
        var console = new ConsoleLoopConsole();

        var repository = invocation.Repository;
        var store = provider.GetRequiredService<IArtifactStore>();
        var runtime = provider.GetRequiredService<IAgentRuntime>();
        var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();

        var artifacts = new RoadmapArtifacts(store, repository);
        var projectionRegistry = new ProjectionRegistry();
        var provenanceFactory = new ProjectionProvenanceFactory(projectionRegistry);
        var contractRegistry = new PromptContractRegistry(projectionRegistry);
        var manifestStore = new ProjectionManifestStore(artifacts);
        var executionPreparationManifest = new ExecutionPreparationManifestStore(artifacts);
        var executionPreparation = new ExecutionPreparationProvenanceService(artifacts, executionPreparationManifest);
        var validator = new ProjectionValidator();
        var promptRunner = new RoadmapPromptRunner(runtime, repository, console);
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
        var stateStore = new RoadmapStateStore(artifacts);
        var decisionLedger = new DecisionLedgerStore(artifacts);
        var journal = new TransitionJournalStore(artifacts);
        var lifecycle = new ArtifactLifecycleStore(artifacts);
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
            resumePlanner,
            completionPolicy,
            completionRouter,
            executionPreparation);
        var promotion = new ArtifactPromotionService(artifacts, lifecycle);
        var bundleExtractor = new BundleFileExtractor();
        var splitBundleInterpreter = new SplitEpicBundleInterpreter();
        var bundleManifest = new BundleManifestWriter(artifacts);
        var splitFamilies = new SplitFamilyStore(artifacts);
        var operationalContext = new OperationalContextGenerator(artifacts, lifecycle, executionPreparation);
        var executionPrompt = new ExecutionPromptGenerator(artifacts, lifecycle, executionPreparation);
        var materializer = new ExecutionCompatibilityMaterializer(artifacts, executionPreparation);
        IRoadmapExecutionBridge executionBridge = new RoadmapExecutionBridge(runtime, artifacts, repository, console);
        var executionInterpreter = new RoadmapExecutionOutcomeInterpreter();
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
            manifestStore,
            projectionCache,
            contextBuilder,
            inputResolver,
            completionPolicy,
            completionRouter,
            promptRunner,
            stateStore,
            startupPlanner,
            resumePlanner,
            unblockPlanner,
            selectionProvenance,
            decisionLedger,
            journal,
            lifecycle,
            promotion,
            bundleExtractor,
            splitBundleInterpreter,
            bundleManifest,
            splitFamilies,
            executionPreparation,
            operationalContext,
            executionPrompt,
            materializer,
            executionBridge,
            executionInterpreter,
            invariants,
            console);

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
