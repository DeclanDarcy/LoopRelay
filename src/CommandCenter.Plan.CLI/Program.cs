using System.Text;
using CommandCenter.Core.Repositories;
using CommandCenter.Plan.Cli;

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

var console = new ConsoleLoopConsole();
console.Info($"CommandCenter.Plan.CLI starting for {repository.Path}");

// TODO(m1-m6): wire PlanArtifacts / PreflightGate / PlanSession / ReviewStep / SandboxedPromptStep /
// PlanPipeline and run the pipeline here. m0 only scaffolds the project, argument parsing, and console
// plumbing.
return 0;
