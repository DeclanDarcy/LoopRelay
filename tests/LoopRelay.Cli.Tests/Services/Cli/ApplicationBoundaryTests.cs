using LoopRelay.Cli.Services.Application;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Resolution;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class ApplicationBoundaryTests
{
    [Theory]
    [InlineData(0, typeof(RunWorkflowCommand))]
    [InlineData(1, typeof(StatusQuery))]
    [InlineData(6, typeof(StorageVerifyCommand))]
    [InlineData(2, typeof(StorageInitCommand))]
    [InlineData(3, typeof(StorageImportCommand))]
    [InlineData(4, typeof(StorageExportCommand))]
    [InlineData(5, typeof(StorageSyncCommand))]
    public void Request_factory_translates_cli_commands_to_explicit_application_requests(
        int commandValue,
        Type expected)
    {
        var command = (UnifiedCliCommandKind)commandValue;
        ApplicationRequest request = ApplicationRequestFactory.Create(Invocation(command));

        Assert.IsType(expected, request);
    }

    [Fact]
    public async Task Runner_only_forwards_request_renders_typed_result_and_returns_suggested_exit_code()
    {
        var application = new RecordingApplication(new ApplicationCommandResult(
            ApplicationOutcome.CannotProceed,
            4,
            ["application message"],
            ["application error"],
            ["evidence"],
            ["warning"],
            ["effect"],
            ["action"]));
        var output = new StringWriter();
        var error = new StringWriter();
        var runner = new UnifiedCliRunner(application, output, error);
        UnifiedCliInvocation invocation = Invocation(UnifiedCliCommandKind.StorageVerify);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.IsType<StorageVerifyCommand>(application.Request);
        Assert.Contains("application message", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("application error", error.ToString(), StringComparison.Ordinal);
    }

    private static UnifiedCliInvocation Invocation(UnifiedCliCommandKind command) => new(
        new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = "/repo",
        },
        new WorkflowInvocation(InvocationModeKind.BoundedPlan),
        new UnifiedCliCommand(command, []));

    private sealed class RecordingApplication(ApplicationCommandResult result) : ILoopRelayApplication
    {
        public ApplicationRequest? Request { get; private set; }

        public Task<ApplicationCommandResult> ExecuteAsync(
            ApplicationRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
