using System.Text;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Extensions;
using CommandCenter.Agents.Services;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.DependencyInjection;

// Codex output and our own messages contain non-ASCII text (curly quotes, em dashes). Decode the codex
// child process' stdout and render our console output as UTF-8 instead of the host's legacy OEM code
// page, which would otherwise show mojibake (e.g. "I'll" -> "IΓÇÖll"). Console.OutputEncoding is also the
// code page the redirected child stdout reader inherits, so this fixes the decode at the source too.
// Guarded for when output is redirected to a file/pipe (no console code page to set).
try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Output is redirected and has no console to reconfigure — safe to ignore.
}

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
var processRunner = provider.GetRequiredService<IProcessRunner>();
var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();

var artifacts = new LoopArtifacts(store, repository);
// The Codex usage gate runs before EVERY codex turn/one-shot (a single iteration invokes codex many times
// and the warm decision session is reused across iterations), so it wraps the runtime rather than gating
// once at the top of the loop.
var usageProbe = new CodexUsageProbe(processRunner, executableResolver, repository);
var usageGate = new UsageGate(usageProbe, new TaskDelayScheduler(), console);
var telemetryClock = new SystemClock();
var telemetryRecorder = SessionTelemetryComposition.CreateRecorder(
    repository, SessionTelemetryComposition.IsEnabled(),
    usageProbe, new EffectiveTokenCostModel(), telemetryClock, console);
var gatedRuntime = new GatedAgentRuntime(
    runtime, usageGate, telemetryRecorder, telemetryClock, SessionTelemetryComposition.RepoName(repository));
var gate = new MilestoneGate(store, repository);
var execution = new ExecutionStep(gatedRuntime, artifacts, console, repository);
var decision = new DecisionSession(gatedRuntime, router, artifacts, console, repository);
var submodulePublisher = new AgentsSubmodulePublisher(processRunner, repository, console);
var commitGate = new CommitGate(processRunner, repository, console);
var loop = new LoopRunner(gate, artifacts, execution, decision, submodulePublisher, commitGate, console);

// --- Ctrl+C: cancel the loop AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    console.Warn("Ctrl+C received — terminating codex sessions...");
    cts.Cancel();
};

console.Info($"CommandCenter.CLI starting for {repository.Path}");
// Surface which codex binary is actually launched: a batch shim (e.g. codex.cmd) does NOT forward the
// stdin EOF that `codex exec -` needs to begin, so it hangs. A native codex.exe (set CODEX_EXECUTABLE)
// works. This line lets you confirm at a glance which one the loop will use.
console.Info($"Codex executable: {executableResolver.Resolve()}");

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
    case LoopOutcome.Stalled:
        console.Warn("Loop stopped: no substantive changes across consecutive iterations.");
        return 3;
    default:
        console.Error("Run failed. See the error above.");
        return 1;
}
