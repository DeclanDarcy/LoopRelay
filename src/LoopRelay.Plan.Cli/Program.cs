using System.Text;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Plan.Cli;
using LoopRelay.Plan.Cli.Primitives;
using LoopRelay.Plan.Cli.Services;
using LoopRelay.Plan.Cli.Services.Cli;

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

await using var composition = PlanCliComposition.Create(repository);
var console = composition.Console;
var pipeline = composition.Pipeline;

// --- Ctrl+C: cancel the pipeline AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    console.Warn("Ctrl+C received — terminating codex sessions...");
    cts.Cancel();
};

console.Info($"LoopRelay.Plan.Cli starting for {repository.Path}");
// Surface which codex binary is actually launched: a batch shim (e.g. codex.cmd) does NOT forward the
// stdin EOF that `codex exec -` needs to begin, so it hangs. A native codex.exe (set CODEX_EXECUTABLE)
// works. This line lets you confirm at a glance which one the pipeline will use.
console.Info($"Codex executable: {composition.ExecutableResolver.Resolve()}");

PlanOutcome outcome;
outcome = await pipeline.RunAsync(cts.Token);

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
