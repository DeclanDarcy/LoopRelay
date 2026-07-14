using LoopRelay.Application.Contracts;
using Xunit;

namespace LoopRelay.Application.Tests;

public sealed class ApplicationContractsTests
{
    [Fact]
    public void Public_request_matrix_covers_every_supported_use_case_without_cli_types()
    {
        ApplicationRequestContext context = new(ApplicationCorrelationId.New(), "ws", "C:/repo",
            new Dictionary<string, string>(), 4, true);
        LoopRelayRequest[] requests =
        [
            new RunWorkflowRequest(context, RunInvocationMode.Default),
            new CanonicalStatusRequest(context),
            .. Enum.GetValues<StorageOperationKind>().Select(kind => new StorageOperationRequest(context, kind)),
            .. Enum.GetValues<ImportOperationKind>().Select(kind => new ImportOperationRequest(context, kind)),
            .. Enum.GetValues<RecoveryOperationKind>().Select(kind => new RecoveryOperationRequest(context, kind)),
            .. Enum.GetValues<InteractionOperationKind>().Select(kind => new InteractionOperationRequest(context, kind)),
            .. Enum.GetValues<CompletionOperationKind>().Select(kind => new CompletionOperationRequest(context, kind)),
            new CapabilityDiagnosticsRequest(context),
        ];

        Assert.Equal(1 + 1 + 5 + 4 + 3 + 4 + 2 + 1, requests.Length);
        Assert.All(requests, request => Assert.Same(context, request.Context));
        Assert.DoesNotContain(typeof(LoopRelayRequest).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name is "LoopRelay.Cli" or "Microsoft.Data.Sqlite");
    }

    [Fact]
    public void Composition_validation_reports_all_missing_duplicate_and_incompatible_owners()
    {
        ApplicationStartupFailure result = ApplicationCompositionValidator.Validate(
            [("Kernel", "1"), ("Kernel", "1"), ("Effects", "1")],
            new Dictionary<string, string> { ["Kernel"] = "1", ["Effects"] = "2", ["Recovery"] = "1" });

        Assert.Equal(["Recovery"], result.MissingOwners);
        Assert.Equal(["Kernel"], result.DuplicateOwners);
        Assert.Equal(["Effects:required=2:actual=1"], result.VersionIncompatibleOwners);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Application_forwards_the_exact_cancellation_token_to_the_dispatcher()
    {
        var dispatcher = new RecordingDispatcher();
        var application = new LoopRelayApplication(dispatcher);
        using var source = new CancellationTokenSource();
        var request = new CanonicalStatusRequest(new ApplicationRequestContext(
            ApplicationCorrelationId.New(), "workspace", Path.GetFullPath("C:/repo"),
            new Dictionary<string, string>()));

        _ = await application.ExecuteAsync(request, source.Token);

        Assert.Equal(source.Token, dispatcher.Token);
        Assert.Same(request, dispatcher.Request);
    }

    private sealed class RecordingDispatcher : IApplicationUseCaseDispatcher
    {
        public CancellationToken Token { get; private set; }
        public LoopRelayRequest? Request { get; private set; }

        public Task<LoopRelayResult> DispatchAsync(
            LoopRelayRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            Token = cancellationToken;
            return Task.FromResult(new LoopRelayResult(request.Context.Correlation,
                ApplicationOutcomeKind.Completed, "ok", 0, [], [], new Dictionary<string, string>(),
                [], [], [], [], [], []));
        }
    }
}
