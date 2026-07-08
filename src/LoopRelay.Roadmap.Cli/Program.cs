using System.Text;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

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
