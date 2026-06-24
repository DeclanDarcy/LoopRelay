using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionRecoveryService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    TimeProvider timeProvider) : IDecisionSessionRecoveryService
{
    public async Task<DecisionSessionDiagnostics> GetDiagnosticsAsync(Guid repositoryId)
    {
        DecisionSessionRecoveryResult result = await AssessAsync(repositoryId, persist: false);
        return result.Diagnostics.RegistryDiagnostics;
    }

    public async Task<DecisionSessionRecoveryResult> RecoverAsync(Guid repositoryId)
    {
        return await AssessAsync(repositoryId, persist: true);
    }

    public async Task<DecisionSessionRecoveryResult> GetRecoveryAsync(Guid repositoryId)
    {
        return await AssessAsync(repositoryId, persist: false);
    }

    public async Task<DecisionSessionRecoveryHistory> GetHistoryAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<DecisionSessionRecoveryResult> results = await sessionRepository.ListRecoveryResultsAsync(repository);
        return new DecisionSessionRecoveryHistory(repositoryId, results, timeProvider.GetUtcNow());
    }

    public async Task<DecisionSessionRecoveryDiagnostics> GetRecoveryDiagnosticsAsync(Guid repositoryId)
    {
        return (await GetRecoveryAsync(repositoryId)).Diagnostics;
    }

    private async Task<DecisionSessionRecoveryResult> AssessAsync(Guid repositoryId, bool persist)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset now = timeProvider.GetUtcNow();
        DecisionSessionValidationResult validation = await ValidateAsync(repository);
        IReadOnlyList<DecisionSession> sessions = validation.IsValid
            ? await sessionRepository.ListAsync(repository)
            : [];
        IReadOnlyList<DecisionSessionTransfer> transfers = validation.IsValid
            ? await sessionRepository.ListTransfersAsync(repository)
            : [];
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts = validation.IsValid
            ? await sessionRepository.ListContinuityArtifactsAsync(repository)
            : [];

        int activeSessionCount = sessions.Count(session => session.State == DecisionSessionState.Active);
        DecisionSession? activeSession = activeSessionCount == 1
            ? sessions.Single(session => session.State == DecisionSessionState.Active)
            : null;
        var registryDiagnostics = new DecisionSessionDiagnostics(
            repositoryId,
            validation.IsValid,
            sessions.Count,
            activeSessionCount,
            validation.Errors,
            validation.Warnings,
            now);

        var findings = new List<DecisionSessionRecoveryFinding>();
        foreach (string error in validation.Errors)
        {
            findings.Add(new DecisionSessionRecoveryFinding("RegistryInvalid", "Error", error, null, null));
        }

        foreach (string warning in validation.Warnings)
        {
            findings.Add(new DecisionSessionRecoveryFinding("RegistryWarning", "Warning", warning, null, null));
        }

        if (validation.IsValid && activeSessionCount == 0)
        {
            findings.Add(new DecisionSessionRecoveryFinding(
                "NoActiveSession",
                "Warning",
                "No active decision session exists for this repository.",
                null,
                null));
        }

        if (validation.IsValid && activeSessionCount > 1)
        {
            findings.Add(new DecisionSessionRecoveryFinding(
                "DuplicateActiveSessions",
                "Error",
                "More than one active decision session exists for this repository.",
                null,
                null));
        }

        TransferRecoveryAssessment[] transferAssessments = validation.IsValid
            ? AssessTransfers(repositoryId, sessions, transfers, artifacts, findings)
            : [];
        IReadOnlyList<string> warnings = findings
            .Where(finding => !string.Equals(finding.Severity, "Info", StringComparison.OrdinalIgnoreCase))
            .Select(finding => finding.Message)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var recoveryEvent = new DecisionSessionRecoveryEvent(
            CreateEventId(now),
            repositoryId,
            persist ? "RecoveryPersisted" : "RecoveryAssessed",
            now,
            validation.IsValid
                ? "Decision-session recovery assessed registry, transfer, and continuity evidence."
                : "Decision-session recovery found invalid registry evidence.",
            warnings);
        var diagnostics = new DecisionSessionRecoveryDiagnostics(
            repositoryId,
            now,
            registryDiagnostics,
            transferAssessments,
            warnings);
        var result = new DecisionSessionRecoveryResult(
            CreateRecoveryId(now),
            repositoryId,
            findings.All(finding => !string.Equals(finding.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            activeSession?.Id,
            activeSessionCount,
            findings,
            diagnostics,
            [recoveryEvent],
            now);

        if (persist)
        {
            await sessionRepository.WriteRecoveryResultAsync(repository, result);
        }

        return result;
    }

    private static TransferRecoveryAssessment[] AssessTransfers(
        Guid repositoryId,
        IReadOnlyList<DecisionSession> sessions,
        IReadOnlyList<DecisionSessionTransfer> transfers,
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts,
        List<DecisionSessionRecoveryFinding> findings)
    {
        var assessments = new List<TransferRecoveryAssessment>();
        foreach (DecisionSessionTransfer transfer in transfers)
        {
            DecisionSession? target = transfer.TargetSessionId is null
                ? null
                : sessions.FirstOrDefault(session => session.Id == transfer.TargetSessionId);
            bool artifactExists = transfer.ContinuityArtifactId is not null &&
                artifacts.Any(artifact => string.Equals(artifact.ArtifactId, transfer.ContinuityArtifactId, StringComparison.Ordinal));
            string status;
            string message;
            if (transfer.Succeeded && target?.State == DecisionSessionState.Active)
            {
                status = "Completed";
                message = "Completed transfer recovered replacement as active.";
            }
            else if (transfer.Succeeded)
            {
                status = "CompletedWithMissingActiveReplacement";
                message = "Completed transfer does not currently point to an active replacement session.";
                findings.Add(new DecisionSessionRecoveryFinding(
                    "CompletedTransferMissingActiveReplacement",
                    "Warning",
                    message,
                    transfer.TargetSessionId,
                    transfer.TransferId));
            }
            else if (transfer.Events.Any(transferEvent => transferEvent.EventType == DecisionSessionTransferEventType.Failed))
            {
                status = "Failed";
                message = "Failed transfer is available as recovery evidence.";
                findings.Add(new DecisionSessionRecoveryFinding(
                    "FailedTransferEvidence",
                    "Warning",
                    message,
                    transfer.SourceSessionId,
                    transfer.TransferId));
            }
            else if (!artifactExists)
            {
                status = "InterruptedBeforeArtifact";
                message = "Transfer evidence indicates interruption before a continuity artifact was available.";
                findings.Add(new DecisionSessionRecoveryFinding(
                    "TransferInterruptedBeforeArtifact",
                    "Warning",
                    message,
                    transfer.SourceSessionId,
                    transfer.TransferId));
            }
            else
            {
                status = "InterruptedWithArtifact";
                message = "Transfer evidence includes a continuity artifact but no completed replacement activation.";
                findings.Add(new DecisionSessionRecoveryFinding(
                    "TransferInterruptedWithArtifact",
                    "Warning",
                    message,
                    transfer.SourceSessionId,
                    transfer.ContinuityArtifactId));
            }

            assessments.Add(new TransferRecoveryAssessment(
                transfer.TransferId,
                transfer.SourceSessionId,
                transfer.TargetSessionId,
                transfer.ContinuityArtifactId,
                status,
                message,
                transfer.Events));
        }

        foreach (DecisionSession pendingSession in sessions.Where(session => session.State == DecisionSessionState.TransferPending))
        {
            DecisionSessionTransfer? transfer = transfers
                .Where(item => item.SourceSessionId == pendingSession.Id)
                .OrderBy(item => item.StartedAt)
                .LastOrDefault();
            DecisionSessionContinuityArtifact? artifact = transfer?.ContinuityArtifactId is null
                ? artifacts
                    .Where(item => item.SourceSessionId == pendingSession.Id)
                    .OrderBy(item => item.CreatedAt)
                    .LastOrDefault()
                : artifacts.FirstOrDefault(item => string.Equals(item.ArtifactId, transfer.ContinuityArtifactId, StringComparison.Ordinal));
            string status = ResolvePendingStatus(transfer, artifact, sessions);
            string message = status switch
            {
                "PendingBeforeArtifact" => "Transfer-pending session restarted before a continuity artifact was available.",
                "PendingWithArtifactNoReplacement" => "Transfer-pending session has a continuity artifact but no replacement session.",
                "PendingWithRetiredSourceNoActiveReplacement" => "Transfer-pending evidence points to a replacement that is not active.",
                _ => "Transfer-pending session requires recovery inspection."
            };

            findings.Add(new DecisionSessionRecoveryFinding(
                status,
                "Warning",
                message,
                pendingSession.Id,
                artifact?.ArtifactId ?? transfer?.TransferId));
            if (transfer is null)
            {
                assessments.Add(new TransferRecoveryAssessment(
                    null,
                    pendingSession.Id,
                    null,
                    artifact?.ArtifactId,
                    status,
                    message,
                    []));
            }
        }

        return assessments
            .OrderBy(assessment => assessment.TransferId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(assessment => assessment.SourceSessionId.Value)
            .ToArray();
    }

    private static string ResolvePendingStatus(
        DecisionSessionTransfer? transfer,
        DecisionSessionContinuityArtifact? artifact,
        IReadOnlyList<DecisionSession> sessions)
    {
        if (artifact is null)
        {
            return "PendingBeforeArtifact";
        }

        if (transfer?.TargetSessionId is null && artifact.TargetSessionId is null)
        {
            return "PendingWithArtifactNoReplacement";
        }

        DecisionSessionId? targetSessionId = transfer?.TargetSessionId ?? artifact.TargetSessionId;
        DecisionSession? target = targetSessionId is null
            ? null
            : sessions.FirstOrDefault(session => session.Id == targetSessionId);
        return target?.State == DecisionSessionState.Active
            ? "PendingWithActiveReplacement"
            : "PendingWithRetiredSourceNoActiveReplacement";
    }

    private async Task<DecisionSessionValidationResult> ValidateAsync(Repository repository)
    {
        if (sessionRepository is FileSystemDecisionSessionRepository fileSystemRepository)
        {
            return await fileSystemRepository.ValidateAsync(repository);
        }

        try
        {
            IReadOnlyList<DecisionSession> sessions = await sessionRepository.ListAsync(repository);
            int activeCount = sessions.Count(session => session.State == DecisionSessionState.Active);
            return activeCount > 1
                ? new DecisionSessionValidationResult(false, ["More than one active decision session exists for this repository."], [])
                : DecisionSessionValidationResult.Valid;
        }
        catch (InvalidOperationException exception)
        {
            return new DecisionSessionValidationResult(false, [exception.Message], []);
        }
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static string CreateRecoveryId(DateTimeOffset timestamp)
    {
        return $"recovery.{timestamp.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.json";
    }

    private static string CreateEventId(DateTimeOffset timestamp)
    {
        return $"recovery-event.{timestamp.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}";
    }
}
