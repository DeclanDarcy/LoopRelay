using System.Text;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;

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

if (invocation.Command == RoadmapCliCommand.Semantic)
{
    var semanticConsole = new ConsoleLoopConsole();
    semanticConsole.Info($"LoopRelay.Roadmap.Cli semantic starting for {repository.Path}");
    var artifacts = new RoadmapArtifacts(new FileSystemArtifactStore(), repository);
    var executor = new RepositoryWorkSemanticExecutor(artifacts, semanticConsole);
    RepositoryWorkSemanticExecutionResult result = await executor.ExecuteAsync(
        RepositoryWorkSemanticRequest.Default,
        CancellationToken.None);

    return result.AdmissionOutcome switch
    {
        RepositoryWorkAdmissionOutcome.Admitted => 0,
        RepositoryWorkAdmissionOutcome.ReportOnly => 0,
        RepositoryWorkAdmissionOutcome.Blocked => 4,
        RepositoryWorkAdmissionOutcome.Denied => 1,
        RepositoryWorkAdmissionOutcome.Unsupported => 1,
        _ => 1,
    };
}

if (invocation.Command == RoadmapCliCommand.SemanticRoadmapTransitionStatus)
{
    var semanticConsole = new ConsoleLoopConsole();
    semanticConsole.Info($"LoopRelay.Roadmap.Cli semantic roadmap-transition status starting for {repository.Path}");
    var artifacts = new RoadmapArtifacts(new FileSystemArtifactStore(), repository);
    var stateStore = new RoadmapStateStore(artifacts);
    var executor = new RoadmapTransitionStatusSemanticExecutor(
        artifacts,
        stateStore,
        new RoadmapStartupPlanner(),
        semanticConsole);
    RoadmapTransitionStatusSemanticExecutionResult result = await executor.ExecuteAsync(
        RoadmapTransitionStatusSemanticRequest.Default,
        CancellationToken.None);

    if (result.Completed)
    {
        return 0;
    }

    return result.AdmissionOutcome switch
    {
        RepositoryWorkAdmissionOutcome.ReportOnly => 4,
        RepositoryWorkAdmissionOutcome.Blocked => 4,
        RepositoryWorkAdmissionOutcome.Denied => 1,
        RepositoryWorkAdmissionOutcome.Unsupported => 1,
        _ => 1,
    };
}

await using var composition = RoadmapCliComposition.Create(invocation);
var console = composition.Console;
var machine = composition.Machine;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    console.Warn("Ctrl+C received; cancelling roadmap state machine...");
    cts.Cancel();
};

console.Info($"LoopRelay.Roadmap.Cli {invocation.Command.ToString().ToLowerInvariant()} starting for {repository.Path}");
console.Info($"Codex executable: {composition.ExecutableResolver.Resolve()}");

RoadmapOutcome outcome;
outcome = await machine.ExecuteAsync(invocation.Command, cts.Token);

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
