using System.Text;
using LoopRelay.Cli.Services.Cli;

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

if (!CliArguments.TryParse(args, out UnifiedCliInvocation invocation, out string error))
{
    Console.Error.WriteLine(error);
    return 2;
}

await using UnifiedCliComposition unifiedComposition = UnifiedCliComposition.CreateProduction(
    invocation.Repository, Console.Out, Console.Error);
var runner = new UnifiedCliRunner(unifiedComposition, Console.Out, Console.Error);

// --- Ctrl+C: cancel the loop AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    Console.Error.WriteLine("Ctrl+C received - terminating LoopRelay work...");
    cts.Cancel();
};

return await runner.RunAsync(invocation, cts.Token);
