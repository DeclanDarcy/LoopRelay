using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Extensions;
using CommandCenter.Agents.Services;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;

if (!CliArguments.TryParse(args, out Repository repository, out string error))
{
    Console.Error.WriteLine(error);
    return 2;
}

// --- Composition: only the building blocks the serial loop needs (no Generic Host, no orchestrator/registry). ---
var services = new ServiceCollection();
services.AddAgents();                                                  // IAgentRuntime + codex runtime
services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
services.AddSingleton(new DecisionSessionRouterOptions());
services.AddSingleton<IDecisionSessionRouter, DecisionSessionRouter>();
await using ServiceProvider provider = services.BuildServiceProvider();

var console = new ConsoleLoopConsole();
var store = provider.GetRequiredService<IArtifactStore>();
var runtime = provider.GetRequiredService<IAgentRuntime>();
var router = provider.GetRequiredService<IDecisionSessionRouter>();

var artifacts = new LoopArtifacts(store, repository);
var gate = new MilestoneGate(store, repository);
var execution = new ExecutionStep(runtime, artifacts, console, repository);
var decision = new DecisionSession(runtime, router, artifacts, console, repository);
await using var loop = new LoopRunner(gate, artifacts, execution, decision, console);

// --- Ctrl+C: cancel the loop AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    console.Warn("Ctrl+C received — terminating codex sessions...");
    cts.Cancel();
};

console.Info($"CommandCenter.CLI starting for {repository.Path}");

LoopOutcome outcome;
try
{
    outcome = await loop.RunAsync(cts.Token);
}
finally
{
    // Explicitly close the warm decision session (kills its codex process tree); also dispose the
    // AgentSessionRegistry via the provider as a belt-and-suspenders teardown for any straggler.
    await loop.DisposeAsync();
    if (provider.GetService<AgentSessionRegistry>() is { } registry)
    {
        await registry.DisposeAsync();
    }
}

switch (outcome)
{
    case LoopOutcome.EpicCompleted:
        Console.WriteLine("Epic completed. Press any key to exit.");
        Console.ReadKey(intercept: true);
        return 0;
    case LoopOutcome.Cancelled:
        console.Warn("Run cancelled. Codex sessions terminated.");
        return 130;
    default:
        console.Error("Run failed. See the error above.");
        return 1;
}
