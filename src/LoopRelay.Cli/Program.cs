using System.Text;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Cli.Surface;
using LoopRelay.Application.Contracts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Permissions.Models.Configuration;

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

if (!CliRequestParser.TryParse(args, out ParsedCliRequest parsed, out string error))
{
    Console.Error.WriteLine(error);
    return 2;
}

LoopRelayCompositionRoot composition;
try
{
    var repository = new Repository
    {
        Id = Guid.NewGuid(),
        Name = Path.GetFileName(parsed.RepositoryPath.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
        Path = parsed.RepositoryPath,
    };
    PolicyOverride[] overrides = parsed.Request.Context.PolicyOverrides
        .Select(item => new PolicyOverride(item.Key, item.Value, "cli-surface", IsExplicit: true))
        .ToArray();
    composition = LoopRelayCompositionRoot.CreateProduction(
        repository,
        overrides,
        Console.Out,
        Console.Error);
}
catch (Exception exception) when (exception is CliSettingsException or PolicyResolutionException)
{
    // A configured value is either demonstrably effective or explicitly rejected — rejection is
    // a clean CLI error, not a crash dump.
    Console.Error.WriteLine(exception.Message);
    return 2;
}

await using LoopRelayCompositionRoot unifiedComposition = composition;
var application = new LoopRelayApplication(new CanonicalCliApplicationService(unifiedComposition));
var runner = new UnifiedCliRunner(application, Console.Out, Console.Error);

// --- Ctrl+C: cancel the loop AND let session disposal kill the codex child processes. ---
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;                 // do not let the runtime hard-kill us before codex teardown
    Console.Error.WriteLine("Ctrl+C received - terminating LoopRelay work...");
    cts.Cancel();
};

return await runner.RunAsync(parsed.Request, cts.Token);
