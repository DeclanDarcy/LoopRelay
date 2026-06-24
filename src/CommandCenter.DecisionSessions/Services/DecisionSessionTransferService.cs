using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionTransferService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionRegistry registry,
    IDecisionSessionTransferEligibilityService eligibilityService,
    IDecisionSessionContinuityCaptureService continuityCapture,
    IDecisionSessionContinuityIntegrationService continuityIntegration,
    IDecisionSessionContinuityArtifactService artifactService,
    TimeProvider timeProvider) : IDecisionSessionTransferService
{
    public async Task<DecisionSessionTransferResult> ExecuteAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession? activeSession = await sessionRepository.GetActiveAsync(repository);
        if (activeSession is null)
        {
            throw new DecisionSessionConflictException($"No active decision session exists for repository: {repositoryId}");
        }

        DecisionSessionTransferEligibilitySnapshot eligibilitySnapshot = await eligibilityService.CheckAsync(repositoryId);
        if (eligibilitySnapshot.Eligibility.PolicyEvaluation.Decision != DecisionSessionLifecycleDecision.Transfer)
        {
            throw new DecisionSessionConflictException("Decision session transfer requires a Transfer policy decision.");
        }

        if (eligibilitySnapshot.Eligibility.Status != DecisionSessionTransferEligibilityStatus.Eligible)
        {
            return CreateBlockedResult(repositoryId, eligibilitySnapshot.Eligibility, activeSession);
        }

        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        string transferId = CreateTransferId(startedAt, activeSession.Id);
        var events = new List<DecisionSessionTransferEvent>();
        var diagnostics = new List<string>
        {
            "Transfer execution requires Transfer policy and Eligible transfer eligibility.",
            "The source session is marked TransferPending before continuity artifact capture.",
            "Replacement activation happens only after the source is no longer Active."
        };

        DecisionSession? sourceSession = activeSession;
        DecisionSession? replacementSession = null;
        DecisionSessionContinuityArtifact? artifact = null;
        try
        {
            sourceSession = await registry.MarkTransferPendingAsync(repositoryId, activeSession.Id, eligibilitySnapshot.Eligibility.PolicyEvaluation.Reason);
            artifact = await continuityCapture.CaptureAsync(repositoryId, sourceSession.Id);
            DecisionSessionTransferEvent started = CreateEvent(
                transferId,
                DecisionSessionTransferEventType.Started,
                repositoryId,
                sourceSession.Id,
                null,
                artifact.ArtifactId,
                "Decision session transfer started.",
                diagnostics);
            events.Add(started);
            await sessionRepository.WriteTransferAsync(
                repository,
                CreateTransfer(transferId, repositoryId, sourceSession.Id, null, artifact.ArtifactId, startedAt, null, false, events, diagnostics));

            IReadOnlyList<string> integrationDiagnostics = await continuityIntegration.IntegrateAsync(repositoryId, artifact);
            diagnostics.AddRange(integrationDiagnostics);
            DecisionSession replacement = await registry.CreateSessionAsync(repositoryId, "decision-session-transfer");
            replacementSession = await registry.ActivateSessionAsync(repositoryId, replacement.Id);
            artifact = await artifactService.AttachTargetSessionAsync(repositoryId, artifact.ArtifactId, replacementSession.Id);
            sourceSession = await registry.MarkTransferredAsync(
                repositoryId,
                sourceSession.Id,
                replacementSession.Id,
                eligibilitySnapshot.Eligibility.PolicyEvaluation.Reason);
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            events.Add(CreateEvent(
                transferId,
                DecisionSessionTransferEventType.Completed,
                repositoryId,
                sourceSession.Id,
                replacementSession.Id,
                artifact.ArtifactId,
                "Decision session transfer completed.",
                diagnostics));
            DecisionSessionTransfer completedTransfer = CreateTransfer(
                transferId,
                repositoryId,
                sourceSession.Id,
                replacementSession.Id,
                artifact.ArtifactId,
                startedAt,
                completedAt,
                true,
                events,
                diagnostics);
            await sessionRepository.WriteTransferAsync(repository, completedTransfer);
            return new DecisionSessionTransferResult(
                true,
                completedTransfer,
                new DecisionSessionTransferDiagnostics(repositoryId, completedAt, eligibilitySnapshot.Eligibility, events, diagnostics),
                sourceSession,
                replacementSession,
                artifact);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DateTimeOffset failedAt = timeProvider.GetUtcNow();
            diagnostics.Add($"Transfer failed: {exception.Message}");
            events.Add(CreateEvent(
                transferId,
                DecisionSessionTransferEventType.Failed,
                repositoryId,
                activeSession.Id,
                replacementSession?.Id,
                artifact?.ArtifactId,
                "Decision session transfer failed.",
                diagnostics));
            DecisionSessionTransfer failedTransfer = CreateTransfer(
                transferId,
                repositoryId,
                activeSession.Id,
                replacementSession?.Id,
                artifact?.ArtifactId,
                startedAt,
                failedAt,
                false,
                events,
                diagnostics);
            await sessionRepository.WriteTransferAsync(repository, failedTransfer);
            return new DecisionSessionTransferResult(
                false,
                failedTransfer,
                new DecisionSessionTransferDiagnostics(repositoryId, failedAt, eligibilitySnapshot.Eligibility, events, diagnostics),
                sourceSession,
                replacementSession,
                artifact);
        }
    }

    public async Task<IReadOnlyList<DecisionSessionTransfer>> ListAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await sessionRepository.ListTransfersAsync(repository);
    }

    public async Task<DecisionSessionTransferDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSessionTransferEligibilitySnapshot eligibilitySnapshot = await eligibilityService.CheckAsync(repositoryId);
        IReadOnlyList<DecisionSessionTransfer> transfers = await sessionRepository.ListTransfersAsync(repository);
        DecisionSessionTransferEvent[] events = transfers
            .SelectMany(transfer => transfer.Events)
            .OrderBy(transferEvent => transferEvent.OccurredAt)
            .ThenBy(transferEvent => transferEvent.EventId, StringComparer.Ordinal)
            .ToArray();
        string[] warnings = transfers
            .Where(transfer => !transfer.Succeeded)
            .SelectMany(transfer => transfer.Diagnostics)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new DecisionSessionTransferDiagnostics(
            repositoryId,
            timeProvider.GetUtcNow(),
            eligibilitySnapshot.Eligibility,
            events,
            warnings);
    }

    private static DecisionSessionTransferResult CreateBlockedResult(
        Guid repositoryId,
        DecisionSessionTransferEligibility eligibility,
        DecisionSession activeSession)
    {
        var diagnostics = new DecisionSessionTransferDiagnostics(
            repositoryId,
            eligibility.CheckedAt,
            eligibility,
            [],
            ["Transfer execution did not mutate registry state because eligibility was not Eligible."]);
        return new DecisionSessionTransferResult(false, null, diagnostics, activeSession, null, null);
    }

    private static DecisionSessionTransfer CreateTransfer(
        string transferId,
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionId? targetSessionId,
        string? artifactId,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt,
        bool succeeded,
        IReadOnlyList<DecisionSessionTransferEvent> events,
        IReadOnlyList<string> diagnostics)
    {
        return new DecisionSessionTransfer(
            transferId,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            artifactId,
            startedAt,
            completedAt,
            succeeded,
            events.ToArray(),
            diagnostics.ToArray());
    }

    private DecisionSessionTransferEvent CreateEvent(
        string transferId,
        DecisionSessionTransferEventType eventType,
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionId? targetSessionId,
        string? artifactId,
        string message,
        IReadOnlyList<string> diagnostics)
    {
        DateTimeOffset occurredAt = timeProvider.GetUtcNow();
        return new DecisionSessionTransferEvent(
            $"{transferId}.{eventType.ToString().ToLowerInvariant()}.{occurredAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}",
            eventType,
            repositoryId,
            sourceSessionId,
            targetSessionId,
            artifactId,
            occurredAt,
            message,
            diagnostics.ToArray());
    }

    private static string CreateTransferId(DateTimeOffset startedAt, DecisionSessionId sourceSessionId)
    {
        return $"transfer.{startedAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json";
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }
}
