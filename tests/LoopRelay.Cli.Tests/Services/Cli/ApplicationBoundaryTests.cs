using LoopRelay.Cli.Services.Application;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Application.Contracts;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Resolution;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class ApplicationBoundaryTests
{
    [Fact]
    public async Task Runner_only_forwards_request_renders_typed_result_and_returns_suggested_exit_code()
    {
        var application = new RecordingApplication(new LoopRelayResult(
            ApplicationCorrelationId.New(), ApplicationOutcomeKind.SpecificCannotProceed,
            "cannot proceed", 4, ["application message"], ["application error"],
            new Dictionary<string, string>(), ["evidence"], ["warning"], ["effect"], [], [], ["action"]));
        var output = new StringWriter();
        var error = new StringWriter();
        var runner = new UnifiedCliRunner(application, output, error);
        var request = new StorageOperationRequest(new ApplicationRequestContext(
            ApplicationCorrelationId.New(), "workspace", Path.GetFullPath("C:/repo"),
            new Dictionary<string, string>()), StorageOperationKind.Verify);

        int exitCode = await runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(4, exitCode);
        StorageOperationRequest forwarded = Assert.IsType<StorageOperationRequest>(application.Request);
        Assert.Equal(StorageOperationKind.Verify, forwarded.Operation);
        Assert.Contains("application message", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("application error", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CompletionDecisionKind.CertifiedCandidate, ApplicationOutcomeKind.EffectsPending, 3)]
    [InlineData(CompletionDecisionKind.Continue, ApplicationOutcomeKind.Waiting, 3)]
    [InlineData(CompletionDecisionKind.Waiting, ApplicationOutcomeKind.Waiting, 3)]
    [InlineData(CompletionDecisionKind.Failed, ApplicationOutcomeKind.Failed, 4)]
    [InlineData(CompletionDecisionKind.Cancelled, ApplicationOutcomeKind.Cancelled, 130)]
    [InlineData(CompletionDecisionKind.SpecificCannotProceed, ApplicationOutcomeKind.SpecificCannotProceed, 4)]
    public async Task Completion_decisions_cross_the_public_application_boundary_with_typed_exit_semantics(
        CompletionDecisionKind kind,
        ApplicationOutcomeKind expectedOutcome,
        int expectedExitCode)
    {
        string path = Directory.CreateTempSubdirectory("looprelay-application-completion").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
        CompletionCannotProceedReason? reason = kind == CompletionDecisionKind.SpecificCannotProceed
            ? CompletionCannotProceedReason.MissingEvidence : null;
        CompletionDecision decision = new CompletionAuthority().Decide(new CompletionDecisionInput(
            RunIdentity.New(), AttemptIdentity.New(),
            Cancelled: kind == CompletionDecisionKind.Cancelled,
            Failed: kind == CompletionDecisionKind.Failed,
            Waiting: kind == CompletionDecisionKind.Waiting,
            ContinueExecution: kind == CompletionDecisionKind.Continue,
            CannotProceedReason: reason,
            EvidenceIdentities: ["evidence:completion"],
            GateIdentities: ["gate:completion"],
            ReviewIdentities: ["review:completion"]), DateTimeOffset.UtcNow);
        await new CanonicalCompletionAuthorityStore(repository).AppendDecisionAsync(decision);
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new FakeAgentRuntime(new MemoryArtifactStore()));
        var application = new LoopRelayApplication(new CanonicalCliApplicationService(composition));
        var request = new CompletionOperationRequest(
            new ApplicationRequestContext(
                ApplicationCorrelationId.New(), repository.Id.ToString("N"), path,
                new Dictionary<string, string>()),
            CompletionOperationKind.Status);

        LoopRelayResult result = await application.ExecuteAsync(request);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedExitCode, result.SuggestedExitCode);
        Assert.Equal(decision.Identity.Value, result.CausalIdentities["completionDecision"]);
        Assert.Contains("evidence:completion", result.EvidenceIdentities);
    }

    [Theory]
    [InlineData(CompletionSettlementKind.EffectsPending, ApplicationOutcomeKind.EffectsPending, 3)]
    [InlineData(CompletionSettlementKind.RecoveryRequired, ApplicationOutcomeKind.RecoveryRequired, 4)]
    [InlineData(CompletionSettlementKind.Failed, ApplicationOutcomeKind.Failed, 4)]
    [InlineData(CompletionSettlementKind.Cancelled, ApplicationOutcomeKind.Cancelled, 130)]
    [InlineData(CompletionSettlementKind.SpecificCannotProceed, ApplicationOutcomeKind.SpecificCannotProceed, 4)]
    public async Task Completion_settlements_cross_persistence_projection_and_application_result(
        CompletionSettlementKind settlementKind,
        ApplicationOutcomeKind expectedOutcome,
        int expectedExitCode)
    {
        string path = Directory.CreateTempSubdirectory("looprelay-application-settlement").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
        CompletionDecision decision = new CompletionAuthority().Decide(new CompletionDecisionInput(
            RunIdentity.New(), AttemptIdentity.New(), false, false, false, false, null,
            ["evidence:completion"], ["gate:completion"], ["review:completion"]), DateTimeOffset.UtcNow);
        CompletionCertificate certificate = CompletionCertificate.Create(decision, DateTimeOffset.UtcNow);
        CompletionClosurePlan plan = CompletionClosurePlan.Build(
            decision, certificate, nestedAgentsChanged: true, parentRepositoryChanged: true,
            DateTimeOffset.UtcNow);
        var store = new CanonicalCompletionAuthorityStore(repository);
        await store.PersistCertifiedCandidateAsync(decision, certificate, plan);
        var settlement = new CompletionSettlement(
            CompletionSettlementIdentity.New(),
            plan.Identity,
            settlementKind,
            [plan.Operations[0].Identity],
            ["effect:test"],
            settlementKind == CompletionSettlementKind.SpecificCannotProceed
                ? CompletionCannotProceedReason.GateRejected : null,
            DateTimeOffset.UtcNow);
        await store.AppendSettlementAsync(decision, certificate, plan, settlement, []);
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new FakeAgentRuntime(new MemoryArtifactStore()));
        var application = new LoopRelayApplication(new CanonicalCliApplicationService(composition));

        LoopRelayResult result = await application.ExecuteAsync(new CompletionOperationRequest(
            new ApplicationRequestContext(
                ApplicationCorrelationId.New(), repository.Id.ToString("N"), path,
                new Dictionary<string, string>()),
            CompletionOperationKind.Status));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedExitCode, result.SuggestedExitCode);
        Assert.Equal(settlement.Identity.Value, result.CausalIdentities["completionSettlement"]);
        Assert.Contains(plan.Operations[0].Identity, result.PendingEffects);
    }

    [Theory]
    [InlineData(CompletionCannotProceedReason.MissingEvidence)]
    [InlineData(CompletionCannotProceedReason.InvalidEvidence)]
    [InlineData(CompletionCannotProceedReason.GateRejected)]
    [InlineData(CompletionCannotProceedReason.ReviewRejected)]
    [InlineData(CompletionCannotProceedReason.AmbiguousEvidence)]
    [InlineData(CompletionCannotProceedReason.DirtyInputSurface)]
    [InlineData(CompletionCannotProceedReason.UnsupportedProviderCapability)]
    [InlineData(CompletionCannotProceedReason.StorageUnavailable)]
    public async Task Every_completion_cannot_proceed_reason_survives_persistence_projection_and_application_rendering(
        CompletionCannotProceedReason reason)
    {
        string path = Directory.CreateTempSubdirectory("looprelay-application-cannot-proceed").FullName;
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
        CompletionDecision decision = new CompletionAuthority().Decide(new CompletionDecisionInput(
            RunIdentity.New(), AttemptIdentity.New(), false, false, false, false, reason,
            [$"evidence:{reason}"], ["gate:completion"], ["review:completion"]), DateTimeOffset.UtcNow);
        await new CanonicalCompletionAuthorityStore(repository).AppendDecisionAsync(decision);
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new FakeAgentRuntime(new MemoryArtifactStore()));
        var application = new LoopRelayApplication(new CanonicalCliApplicationService(composition));

        LoopRelayResult result = await application.ExecuteAsync(new CompletionOperationRequest(
            new ApplicationRequestContext(
                ApplicationCorrelationId.New(), repository.Id.ToString("N"), path,
                new Dictionary<string, string>()),
            CompletionOperationKind.Status));

        Assert.Equal(ApplicationOutcomeKind.SpecificCannotProceed, result.Outcome);
        Assert.Equal(4, result.SuggestedExitCode);
        Assert.Contains(reason.ToString(), result.Reason, StringComparison.Ordinal);
        Assert.Contains($"evidence:{reason}", result.EvidenceIdentities);
    }

    [Theory]
    [InlineData("recovery-inspect")]
    [InlineData("recovery-plan")]
    [InlineData("recovery-execute")]
    [InlineData("interaction-cancel")]
    [InlineData("import-preview")]
    [InlineData("import-execute")]
    [InlineData("import-verify")]
    public async Task Missing_public_operation_identity_is_a_typed_result_not_an_exception(string operation)
    {
        string path = Directory.CreateTempSubdirectory("looprelay-application-missing-id").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new FakeAgentRuntime(new MemoryArtifactStore()));
        var application = new LoopRelayApplication(new CanonicalCliApplicationService(composition));
        ApplicationRequestContext context = Context(repository);
        LoopRelayRequest request = operation switch
        {
            "recovery-inspect" => new RecoveryOperationRequest(context, RecoveryOperationKind.Inspect),
            "recovery-plan" => new RecoveryOperationRequest(context, RecoveryOperationKind.Plan),
            "recovery-execute" => new RecoveryOperationRequest(context, RecoveryOperationKind.Execute),
            "interaction-cancel" => new InteractionOperationRequest(context, InteractionOperationKind.Cancel),
            "import-preview" => new ImportOperationRequest(context, ImportOperationKind.Preview),
            "import-execute" => new ImportOperationRequest(context, ImportOperationKind.Execute),
            _ => new ImportOperationRequest(context, ImportOperationKind.Verify),
        };

        LoopRelayResult result = await application.ExecuteAsync(request);

        Assert.Equal(ApplicationOutcomeKind.SpecificCannotProceed, result.Outcome);
        Assert.Equal(4, result.SuggestedExitCode);
        Assert.NotEmpty(result.RequiredActions);
    }

    [Fact]
    public async Task Missing_recovery_case_and_interaction_are_typed_at_the_application_boundary()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-application-missing-record").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new FakeAgentRuntime(new MemoryArtifactStore()));
        var application = new LoopRelayApplication(new CanonicalCliApplicationService(composition));

        LoopRelayResult recovery = await application.ExecuteAsync(new RecoveryOperationRequest(
            Context(repository), RecoveryOperationKind.Inspect, "missing"));
        LoopRelayResult interaction = await application.ExecuteAsync(new InteractionOperationRequest(
            Context(repository), InteractionOperationKind.Cancel, "missing"));

        Assert.Equal(ApplicationOutcomeKind.SpecificCannotProceed, recovery.Outcome);
        Assert.Equal(ApplicationOutcomeKind.SpecificCannotProceed, interaction.Outcome);
        Assert.Equal(4, recovery.SuggestedExitCode);
        Assert.Equal(4, interaction.SuggestedExitCode);
    }

    [Fact]
    public async Task Capability_diagnostics_exposes_exact_runtime_and_role_policy_evidence()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-application-capabilities").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new FakeAgentRuntime(new MemoryArtifactStore()));
        var application = new LoopRelayApplication(new CanonicalCliApplicationService(composition));

        LoopRelayResult result = await application.ExecuteAsync(new CapabilityDiagnosticsRequest(
            Context(repository), IncludePrerequisites: false));

        Assert.Equal(ApplicationOutcomeKind.Completed, result.Outcome);
        Assert.Equal(composition.RuntimeProfile.Value, result.CausalIdentities["runtimeProfile"]);
        Assert.Equal(composition.AgentRolePolicy.Identity, result.CausalIdentities["rolePolicy"]);
    }

    private static ApplicationRequestContext Context(Repository repository) => new(
        ApplicationCorrelationId.New(), repository.Id.ToString("N"), Path.GetFullPath(repository.Path),
        new Dictionary<string, string>());

    private sealed class RecordingApplication(LoopRelayResult result) : LoopRelay.Application.Contracts.ILoopRelayApplication
    {
        public LoopRelayRequest? Request { get; private set; }

        public Task<LoopRelayResult> ExecuteAsync(
            LoopRelayRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
