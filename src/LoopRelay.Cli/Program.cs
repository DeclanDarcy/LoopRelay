using System.Text;
using LoopRelay.Core.Repositories;
using LoopRelay.Cli;

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

await using var composition = LoopCliComposition.Create(repository);
var console = composition.Console;
var loop = composition.Loop;

// --- Ctrl+C: cancel the loop AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    console.Warn("Ctrl+C received — terminating codex sessions...");
    cts.Cancel();
};

console.Info($"LoopRelay.Cli starting for {repository.Path}");
// Surface which codex binary is actually launched: a batch shim (e.g. codex.cmd) does NOT forward the
// stdin EOF that `codex exec -` needs to begin, so it hangs. A native codex.exe (set CODEX_EXECUTABLE)
// works. This line lets you confirm at a glance which one the loop will use.
console.Info($"Codex executable: {composition.ExecutableResolver.Resolve()}");

LoopOutcome outcome;
outcome = await loop.RunAsync(cts.Token);

switch (outcome)
{
    case LoopOutcome.EpicCompleted:
        Console.WriteLine("Epic completed. Press any key to exit.");
        Console.ReadKey(intercept: true);
        return 0;
    case LoopOutcome.CompletionBlocked:
        console.Warn("Completion certification blocked epic closure. Review the evidence path reported above.");
        return 4;
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
