using LoopRelay.Application.Contracts;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Cli.Tests.Services.Cli;

internal enum TestApplicationCommandKind
{
    Run,
    Status,
    StorageInit,
    StorageImport,
    StorageExport,
    StorageSync,
    StorageVerify,
    StorageMigrate,
    InteractionList,
    InteractionShow,
    InteractionRespond,
}

internal sealed record TestApplicationCommand(
    TestApplicationCommandKind Kind,
    IReadOnlyList<string> Arguments,
    InvocationModeKind? BoundedWorkflowMode = null);

internal sealed record TestApplicationInvocation(
    Repository Repository,
    WorkflowInvocation WorkflowInvocation,
    TestApplicationCommand Command,
    IReadOnlyList<PolicyOverride>? PolicyOverrides = null,
    bool Interactive = false)
{
    public LoopRelayRequest ToPublicRequest()
    {
        var context = new ApplicationRequestContext(
            ApplicationCorrelationId.New(),
            Repository.Id == Guid.Empty ? $"workspace-path:{Path.GetFullPath(Repository.Path)}" : Repository.Id.ToString("N"),
            Path.GetFullPath(Repository.Path),
            (PolicyOverrides ?? []).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            Interactive: Interactive);
        return Command.Kind switch
        {
            TestApplicationCommandKind.Run => new RunWorkflowRequest(context, PublicMode(), WorkflowName()),
            TestApplicationCommandKind.Status => new CanonicalStatusRequest(context, PublicMode(), WorkflowName()),
            TestApplicationCommandKind.StorageVerify => new StorageOperationRequest(context, StorageOperationKind.Verify),
            TestApplicationCommandKind.StorageInit => new StorageOperationRequest(context, StorageOperationKind.Initialize),
            TestApplicationCommandKind.StorageMigrate => new StorageOperationRequest(context, StorageOperationKind.Migrate),
            TestApplicationCommandKind.StorageExport => new StorageOperationRequest(context, StorageOperationKind.Export,
                Command.Arguments.FirstOrDefault()),
            TestApplicationCommandKind.StorageSync => new StorageOperationRequest(context, StorageOperationKind.Sync),
            TestApplicationCommandKind.StorageImport => new ImportOperationRequest(context, ImportKind(),
                Command.Arguments.Skip(1).FirstOrDefault()),
            TestApplicationCommandKind.InteractionList => new InteractionOperationRequest(context, InteractionOperationKind.List),
            TestApplicationCommandKind.InteractionShow => new InteractionOperationRequest(context, InteractionOperationKind.Show,
                Command.Arguments.FirstOrDefault()),
            TestApplicationCommandKind.InteractionRespond => new InteractionOperationRequest(context, InteractionOperationKind.Respond,
                Command.Arguments.FirstOrDefault(), Command.Arguments.Skip(1).FirstOrDefault()),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private RunInvocationMode PublicMode() => WorkflowInvocation.Mode switch
    {
        InvocationModeKind.ForcedTraditionalChain => RunInvocationMode.ForcedTraditional,
        InvocationModeKind.ForcedEvalChain => RunInvocationMode.ForcedEval,
        InvocationModeKind.BoundedTraditional or InvocationModeKind.BoundedEval or
            InvocationModeKind.BoundedPlan or InvocationModeKind.BoundedExecute => RunInvocationMode.BoundedWorkflow,
        _ => RunInvocationMode.Default,
    };

    private string? WorkflowName() => WorkflowInvocation.Mode switch
    {
        InvocationModeKind.BoundedTraditional => "TraditionalRoadmap",
        InvocationModeKind.BoundedEval => "EvalRoadmap",
        InvocationModeKind.BoundedPlan => "Plan",
        InvocationModeKind.BoundedExecute => "Execute",
        _ => null,
    };

    private ImportOperationKind ImportKind() => Command.Arguments.FirstOrDefault() switch
    {
        "preview" => ImportOperationKind.Preview,
        "execute" => ImportOperationKind.Execute,
        "verify" => ImportOperationKind.Verify,
        _ => ImportOperationKind.Detect,
    };
}

internal static class TestApplicationInvocationExtensions
{
    public static Task<int> RunAsync(
        this UnifiedCliRunner runner,
        TestApplicationInvocation invocation,
        CancellationToken cancellationToken) =>
        runner.RunAsync(invocation.ToPublicRequest(), cancellationToken);
}
