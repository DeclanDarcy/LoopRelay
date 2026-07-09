using System.Text;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Roadmap.Cli;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Cli;
using LoopRelay.Roadmap.Cli.Services.Persistence;

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

if (invocation.Command is RoadmapCliCommand.StorageInit
    or RoadmapCliCommand.StorageImport
    or RoadmapCliCommand.StorageExport
    or RoadmapCliCommand.StorageSync
    or RoadmapCliCommand.StorageVerify)
{
    var storageConsole = new ConsoleLoopConsole();
    var sqlite = new WorkspaceSqliteStore();
    var sync = new WorkspaceSyncService(sqlite);
    var verification = new WorkspaceVerificationService(sqlite);
    var artifacts = new RoadmapArtifacts(new FileSystemArtifactStore(), repository);
    WorkspaceSyncOptions syncOptions = (invocation.StorageOptions ?? RoadmapStorageOptions.Default).ToSyncOptions();
    WorkspaceVerificationOptions verificationOptions =
        (invocation.StorageOptions ?? RoadmapStorageOptions.Default).ToVerificationOptions();
    using var storageCts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        storageConsole.Warn("Ctrl+C received; cancelling roadmap storage operation...");
        storageCts.Cancel();
    };

    if (invocation.Command == RoadmapCliCommand.StorageVerify)
    {
        WorkspaceVerificationResult verificationResult = await verification.VerifyAsync(
            artifacts,
            verificationOptions,
            storageCts.Token);
        storageConsole.Info(verificationResult.Summary);
        foreach (WorkspaceVerificationFinding finding in verificationResult.Findings)
        {
            storageConsole.Warn(
                $"{finding.Kind}: {finding.Domain} {finding.Identity} ({finding.Rule}) current={finding.CurrentState}; expected={finding.ExpectedState}; action={finding.RecommendedAction}");
        }

        storageConsole.Info($"Database: {WorkspaceDatabaseLocator.Resolve(repository)}");
        return verificationResult.Success ? 0 : 1;
    }

    WorkspaceSqliteOperationResult result = invocation.Command switch
    {
        RoadmapCliCommand.StorageInit => await sqlite.InitializeAsync(repository, storageCts.Token),
        RoadmapCliCommand.StorageImport => await sync.ImportAsync(artifacts, syncOptions, storageCts.Token),
        RoadmapCliCommand.StorageExport => await sync.ExportAsync(artifacts, syncOptions, storageCts.Token),
        RoadmapCliCommand.StorageSync => await sync.SyncAsync(artifacts, syncOptions, storageCts.Token),
        _ => throw new InvalidOperationException("Unsupported storage command."),
    };
    storageConsole.Info($"{result.Category}: {result.Message}");
    storageConsole.Info($"Database: {result.DatabasePath}");
    return result.Category is WorkspaceStorageResultCategory.Initialized
        or WorkspaceStorageResultCategory.Imported
        or WorkspaceStorageResultCategory.Exported
        or WorkspaceStorageResultCategory.Unchanged
        ? 0
        : 1;
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
