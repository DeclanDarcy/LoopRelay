using System.Text;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Extensions;
using CommandCenter.Agents.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Services;
using CommandCenter.Plan.Cli;
using Microsoft.Extensions.DependencyInjection;

// Codex output and our own messages contain non-ASCII text (curly quotes, em dashes). Decode the codex
// child process' stdout and render our console output as UTF-8 instead of the host's legacy OEM code
// page, which would otherwise show mojibake (e.g. "I'll" -> "IΓÇÖll"). Guarded for when output is
// redirected to a file/pipe (no console code page to set).
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

// --- Composition: only the building blocks the planning pipeline needs (no Generic Host, no orchestrator/
// registry, no usage gate/telemetry — see the plan's Non-Goals). ---
var services = new ServiceCollection();
services.AddAgents();                                                  // IAgentRuntime + codex runtime
services.AddSingleton<IArtifactStore, FileSystemArtifactStore>();
await using ServiceProvider provider = services.BuildServiceProvider();

var console = new ConsoleLoopConsole();
var store = provider.GetRequiredService<IArtifactStore>();
var runtime = provider.GetRequiredService<IAgentRuntime>();
var executableResolver = provider.GetRequiredService<IAgentExecutableResolver>();
var processRunner = provider.GetRequiredService<IProcessRunner>();
ISandboxWorkspaceFactory sandboxFactory = new TempSandboxWorkspaceFactory();

var artifacts = new PlanArtifacts(store, repository);
var preflight = new PreflightGate(artifacts);
var planSession = new PlanSession(runtime, artifacts, console, repository);
var review = new ReviewStep(runtime, artifacts, console, repository);
var oneShot = new SandboxedPromptStep(runtime, sandboxFactory, artifacts, console, repository);
var publisher = new AgentsSubmodulePublisher(processRunner, repository, console);
var rollover = new EpicRolloverStep(processRunner, artifacts, console, repository);
// Shared with CommandCenter.CLI: the loop's decision-session resume state, cleared at the epic boundary.
var resumeStore = new FileDecisionSessionResumeStore(repository, console.Warn);
var pipeline = new PlanPipeline(rollover, preflight, planSession, review, oneShot, publisher, artifacts, resumeStore, console);

// --- Ctrl+C: cancel the pipeline AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    console.Warn("Ctrl+C received — terminating codex sessions...");
    cts.Cancel();
};

console.Info($"CommandCenter.Plan.CLI starting for {repository.Path}");
// Surface which codex binary is actually launched: a batch shim (e.g. codex.cmd) does NOT forward the
// stdin EOF that `codex exec -` needs to begin, so it hangs. A native codex.exe (set CODEX_EXECUTABLE)
// works. This line lets you confirm at a glance which one the pipeline will use.
console.Info($"Codex executable: {executableResolver.Resolve()}");

PlanOutcome outcome;
try
{
    outcome = await pipeline.RunAsync(cts.Token);
}
finally
{
    // Explicitly close the warm planning session (kills its codex process tree); also dispose the
    // AgentSessionRegistry via the provider as a belt-and-suspenders teardown for any straggler.
    await pipeline.DisposeAsync();
    if (provider.GetService<AgentSessionRegistry>() is { } registry)
    {
        await registry.DisposeAsync();
    }
}

switch (outcome)
{
    case PlanOutcome.Completed:
        console.Info("Planning pipeline completed.");
        return 0;
    case PlanOutcome.PreflightBlocked:
        console.Error("Preflight blocked the run. See the violations above.");
        return 4;
    case PlanOutcome.Cancelled:
        console.Warn("Run cancelled. Codex sessions terminated.");
        return 130;
    default:
        console.Error("Run failed. See the error above.");
        return 1;
}
