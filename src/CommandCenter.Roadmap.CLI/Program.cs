using System.Text;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Extensions;
using CommandCenter.Agents.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Roadmap.Cli;
using Microsoft.Extensions.DependencyInjection;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Output is redirected.
}

if (!CliArguments.TryParse(args, out RoadmapCliInvocation invocation, out string error))
{
    Console.Error.WriteLine(error);
    return 2;
}

Repository repository = invocation.Repository;

var services = new ServiceCollection();
services.AddAgents();
services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
await using ServiceProvider provider = services.BuildServiceProvider();

var console = new ConsoleLoopConsole();
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
var selectionProvenance = new SelectionProvenanceService(artifacts, selectionProvenanceManifest, contextBuilder, inputResolver);
var completionPolicy = new CompletionCertificationPolicy();
var completionRouter = new CompletionCertificationRouter();
var stateStore = new RoadmapStateStore(artifacts);
var decisionLedger = new DecisionLedgerStore(artifacts);
var journal = new TransitionJournalStore(artifacts);
var lifecycle = new ArtifactLifecycleStore(artifacts);
var startupPlanner = new RoadmapStartupPlanner();
var projectContextLoader = new ProjectContextLoader(artifacts);
var resumePlanner = new RoadmapResumePlanner(artifacts, contractRegistry, manifestStore, lifecycle, provenanceFactory, selectionProvenance, executionPreparation);
var unblockPlanner = new RoadmapUnblockPlanner(artifacts, projectContextLoader, contractRegistry, resumePlanner, completionPolicy, completionRouter, executionPreparation);
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
var invariants = new InvariantValidator(artifacts, projectContextLoader, projectionRegistry, contractRegistry, manifestStore, lifecycle, splitFamilies, executionPreparation);
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

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    console.Warn("Ctrl+C received; cancelling roadmap state machine...");
    cts.Cancel();
};

console.Info($"CommandCenter.Roadmap.CLI {invocation.Command.ToString().ToLowerInvariant()} starting for {repository.Path}");
console.Info($"Codex executable: {executableResolver.Resolve()}");

RoadmapOutcome outcome;
try
{
    outcome = await machine.ExecuteAsync(invocation.Command, cts.Token);
}
finally
{
    if (provider.GetService<AgentSessionRegistry>() is { } registry)
    {
        await registry.DisposeAsync();
    }
}

switch (outcome)
{
    case RoadmapOutcome.Completed:
    case RoadmapOutcome.Paused:
        return 0;
    case RoadmapOutcome.PreflightBlocked:
        return 4;
    case RoadmapOutcome.Cancelled:
        return 130;
    default:
        return 1;
}
