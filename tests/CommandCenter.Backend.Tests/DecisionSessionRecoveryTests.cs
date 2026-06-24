using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionRecoveryTests
{
    [Fact]
    public async Task ActiveSessionRecoversAfterRestart()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        var restartedRecovery = new DecisionSessionRecoveryService(
            harness.RepositoryService,
            harness.RepositoryStore,
            TimeProvider.System);

        DecisionSessionRecoveryResult result = await restartedRecovery.RecoverAsync(harness.Repository.Id);
        DecisionSessionRecoveryHistory history = await restartedRecovery.GetHistoryAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(active.Id, result.ActiveSessionId);
        Assert.Equal(1, result.ActiveSessionCount);
        Assert.Single(history.Results);
    }

    [Fact]
    public async Task CompletedTransferRecoversReplacementAsActive()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession source = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        source = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, source.Id);
        source = await harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, source.Id, "transfer pressure");
        DecisionSession replacement = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "decision-session-transfer");
        replacement = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, replacement.Id);
        source = await harness.Registry.MarkTransferredAsync(harness.Repository.Id, source.Id, replacement.Id, "transfer pressure");
        await harness.RepositoryStore.WriteTransferAsync(
            harness.Repository,
            CreateTransfer(harness.Repository.Id, source.Id, replacement.Id, now, succeeded: true));

        DecisionSessionRecoveryResult result = await harness.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(replacement.Id, result.ActiveSessionId);
        Assert.Contains(result.Diagnostics.TransferAssessments, assessment => assessment.Status == "Completed");
    }

    [Fact]
    public async Task TransferPendingAfterRestartEmitsDiagnostics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        DecisionSession active = await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
        await harness.Registry.MarkTransferPendingAsync(harness.Repository.Id, active.Id, "transfer pressure");

        DecisionSessionRecoveryResult result = await harness.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Findings, finding => finding.Code == "NoActiveSession");
        Assert.Contains(result.Findings, finding => finding.Code == "PendingBeforeArtifact");
        Assert.Contains(result.Diagnostics.TransferAssessments, assessment => assessment.Status == "PendingBeforeArtifact");
    }

    [Fact]
    public async Task DuplicateActiveSessionsProduceRecoveryFinding()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession first = DecisionSession.Create(harness.Repository.Id, "test", now) with
        {
            State = DecisionSessionState.Active,
            ActivatedAt = now
        };
        DecisionSession second = DecisionSession.Create(harness.Repository.Id, "test", now.AddMinutes(1)) with
        {
            State = DecisionSessionState.Active,
            ActivatedAt = now.AddMinutes(1)
        };
        await harness.WriteRegistryAsync([first, second]);

        DecisionSessionRecoveryResult result = await harness.Recovery.RecoverAsync(harness.Repository.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Findings, finding =>
            finding.Code == "RegistryInvalid" &&
            finding.Message.Contains("More than one active decision session", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostedRecoveryContinuesAfterRepositoryFailure()
    {
        Repository first = new() { Id = Guid.NewGuid(), Name = "first", Path = "first" };
        Repository second = new() { Id = Guid.NewGuid(), Name = "second", Path = "second" };
        var repositoryService = new DecisionSessionTestRepositoryService(first, second);
        var recoveryService = new ThrowingOnceRecoveryService(first.Id);
        var hosted = new DecisionSessionRecoveryHostedService(repositoryService, recoveryService);

        await hosted.StartAsync(CancellationToken.None);

        Assert.Contains(second.Id, recoveryService.RecoveredRepositories);
    }

    private static DecisionSessionTransfer CreateTransfer(
        Guid repositoryId,
        CommandCenter.DecisionSessions.Primitives.DecisionSessionId sourceSessionId,
        CommandCenter.DecisionSessions.Primitives.DecisionSessionId targetSessionId,
        DateTimeOffset now,
        bool succeeded)
    {
        string transferId = $"transfer.{now.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json";
        var started = new DecisionSessionTransferEvent(
            $"{transferId}.started",
            DecisionSessionTransferEventType.Started,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            null,
            now,
            "Decision session transfer started.",
            []);
        var completed = new DecisionSessionTransferEvent(
            $"{transferId}.completed",
            succeeded ? DecisionSessionTransferEventType.Completed : DecisionSessionTransferEventType.Failed,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            null,
            now.AddSeconds(1),
            "Decision session transfer completed.",
            []);
        return new DecisionSessionTransfer(
            transferId,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            null,
            now,
            now.AddSeconds(1),
            succeeded,
            [started, completed],
            []);
    }

    private sealed class ThrowingOnceRecoveryService(Guid repositoryIdToThrow) : IDecisionSessionRecoveryService
    {
        public List<Guid> RecoveredRepositories { get; } = [];

        public Task<DecisionSessionDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }

        public Task<DecisionSessionRecoveryResult> RecoverAsync(Guid repositoryId)
        {
            if (repositoryId == repositoryIdToThrow)
            {
                throw new InvalidOperationException("repository recovery failed");
            }

            RecoveredRepositories.Add(repositoryId);
            return Task.FromResult(new DecisionSessionRecoveryResult(
                "recovery.test.json",
                repositoryId,
                true,
                null,
                0,
                [],
                new DecisionSessionRecoveryDiagnostics(
                    repositoryId,
                    DateTimeOffset.UtcNow,
                    new DecisionSessionDiagnostics(repositoryId, true, 0, 0, [], [], DateTimeOffset.UtcNow),
                    [],
                    []),
                [],
                DateTimeOffset.UtcNow));
        }

        public Task<DecisionSessionRecoveryResult> GetRecoveryAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }

        public Task<DecisionSessionRecoveryHistory> GetHistoryAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }

        public Task<DecisionSessionRecoveryDiagnostics> GetRecoveryDiagnosticsAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }
}
