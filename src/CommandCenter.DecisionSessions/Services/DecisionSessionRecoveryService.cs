using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionRecoveryService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    TimeProvider timeProvider,
    IDecisionSessionMetricsService? metricsService = null,
    IDecisionSessionEconomicsService? economicsService = null,
    IDecisionSessionCoherenceService? coherenceService = null,
    IDecisionSessionLifecyclePolicy? lifecyclePolicy = null,
    IDecisionSessionEvidenceReader? evidenceReader = null) : IDecisionSessionRecoveryService
{
    /// <summary>
    /// Hand-bumped version identifying the metrics/economics/coherence/lifecycle analysis formulas. Bump this
    /// constant whenever any of those derived-snapshot formulas change so that warm-restart recovery rebuilds
    /// snapshots stamped under an older formula version instead of skipping them.
    /// </summary>
    private const string AnalysisOptionsVersion = "decision-sessions.analysis.v1";

    /// <summary>
    /// Repository-relative SOURCE directories the evidence reader scans. The decision-session snapshot output
    /// directory (.agents/decision-sessions) is deliberately NOT in this set: writing a snapshot must not bump
    /// the probed source mtime, which would self-invalidate the warm-restart cache forever.
    /// </summary>
    private static readonly string[] SourceDirectories =
    [
        ArtifactPath.CombineRelative(".agents", "decisions"),
        ArtifactPath.CombineRelative(".agents", "reasoning"),
        ArtifactPath.CombineRelative(".agents", "operational_context")
    ];

    private const string OperationalContextCurrentRelativePath = ".agents/operational_context.md";

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
        IReadOnlyList<string> rebuildDiagnostics = validation.IsValid
            ? await RebuildDerivedSnapshotsAsync(repository, registryDiagnostics, sessions, activeSession, now, findings)
            : ["Derived snapshots were not rebuilt because authoritative registry evidence is invalid."];
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
                ? "Decision-session recovery assessed registry, transfer, continuity, and derived snapshot evidence."
                : "Decision-session recovery found invalid registry evidence.",
            warnings.Concat(rebuildDiagnostics).Distinct(StringComparer.Ordinal).ToArray());
        var diagnostics = new DecisionSessionRecoveryDiagnostics(
            repositoryId,
            now,
            registryDiagnostics,
            transferAssessments,
            warnings.Concat(rebuildDiagnostics).Distinct(StringComparer.Ordinal).ToArray());
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

    private async Task<IReadOnlyList<string>> RebuildDerivedSnapshotsAsync(
        Repository repository,
        DecisionSessionDiagnostics registryDiagnostics,
        IReadOnlyList<DecisionSession> sessions,
        DecisionSession? activeSession,
        DateTimeOffset recoveredAt,
        List<DecisionSessionRecoveryFinding> findings)
    {
        var diagnostics = new List<string>();
        DecisionSessionLifecycleSnapshot? policySnapshot;

        if (await CanSkipDerivedRebuildAsync(repository))
        {
            // Warm restart: source .agents evidence is unchanged since the last stamped rebuild, so the
            // four count-derived snapshots (metrics/economics/coherence/lifecycle) are byte-for-byte current.
            // Skip the expensive evidence rescans entirely and read the persisted lifecycle policy snapshot
            // to feed transfer eligibility (one small file, no O(files) evidence scan).
            AddSkippedFinding(
                "MetricsSnapshotNotRebuilt",
                "Metrics snapshot was not rebuilt because source evidence is unchanged since the last stamped rebuild.",
                findings,
                diagnostics);
            AddSkippedFinding(
                "EconomicsSnapshotNotRebuilt",
                "Economics snapshot was not rebuilt because source evidence is unchanged since the last stamped rebuild.",
                findings,
                diagnostics);
            AddSkippedFinding(
                "CoherenceSnapshotNotRebuilt",
                "Coherence snapshot was not rebuilt because source evidence is unchanged since the last stamped rebuild.",
                findings,
                diagnostics);
            AddSkippedFinding(
                "LifecyclePolicySnapshotNotRebuilt",
                "Lifecycle policy snapshot was not rebuilt because source evidence is unchanged since the last stamped rebuild.",
                findings,
                diagnostics);
            policySnapshot = await sessionRepository.ReadLifecyclePolicySnapshotAsync(repository);
        }
        else
        {
            // Probe BEFORE rebuilding so the stamp reflects the source tree the rebuild consumed. If a source
            // file changes during the rebuild, the stamp will be older than the next probe => next boot rebuilds
            // (conservative; never serves stale data).
            DateTimeOffset? sourceMaxWriteUtc = ProbeSourceMaxWriteUtc(repository);
            await RebuildSnapshotAsync(
                "MetricsSnapshotRebuilt",
                "Metrics snapshot was rebuilt from decision, reasoning, and continuity evidence.",
                () => metricsService?.GetMetricsAsync(repository.Id),
                findings,
                diagnostics);
            await RebuildSnapshotAsync(
                "EconomicsSnapshotRebuilt",
                "Economics snapshot was rebuilt from metrics evidence.",
                () => economicsService?.GetEconomicsAsync(repository.Id),
                findings,
                diagnostics);
            await RebuildSnapshotAsync(
                "CoherenceSnapshotRebuilt",
                "Coherence snapshot was rebuilt from metrics, economics, and reasoning graph evidence.",
                () => coherenceService?.GetCoherenceAsync(repository.Id),
                findings,
                diagnostics);

            if (activeSession is null)
            {
                policySnapshot = null;
                AddSkippedFinding(
                    "LifecyclePolicySnapshotNotRebuilt",
                    "Lifecycle policy snapshot was not rebuilt because no active decision session exists.",
                    findings,
                    diagnostics);
            }
            else
            {
                policySnapshot = await RebuildSnapshotAsync(
                    "LifecyclePolicySnapshotRebuilt",
                    "Lifecycle policy snapshot was rebuilt from analysis evidence.",
                    () => lifecyclePolicy?.EvaluateAsync(repository.Id),
                    findings,
                    diagnostics);
            }

            // Stamp LAST: economics/coherence/lifecycle rebuilds each re-derive metrics and re-write the
            // (unstamped) metrics snapshot, so the staleness stamp must be applied after they all complete.
            await StampMetricsSnapshotAsync(repository, sourceMaxWriteUtc);
        }

        await RebuildTransferEligibilitySnapshotAsync(
            repository,
            registryDiagnostics,
            sessions,
            activeSession,
            policySnapshot,
            recoveredAt,
            findings,
            diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Decides whether the four count-derived snapshots can be left untouched on this boot. Returns true only
    /// when the persisted metrics snapshot deserialized cleanly, carries a non-null source stamp equal to the
    /// freshly probed source mtime, and carries the current analysis-options version. Any failure to read the
    /// stamp (missing/corrupt/foreign snapshot) or an unprobeable source tree forces a full rebuild.
    /// </summary>
    private async Task<bool> CanSkipDerivedRebuildAsync(Repository repository)
    {
        // The metrics service is the head of the derived chain; if it is unavailable we have nothing to skip.
        if (metricsService is null)
        {
            return false;
        }

        DateTimeOffset? probedSourceMaxWriteUtc = ProbeSourceMaxWriteUtc(repository);
        if (probedSourceMaxWriteUtc is null)
        {
            return false;
        }

        DecisionSessionMetricsSnapshotStamp? stamp;
        try
        {
            stamp = await sessionRepository.ReadMetricsSnapshotStampAsync(repository);
        }
        catch (DecisionSessionValidationException)
        {
            return false;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }

        if (stamp?.SourceMaxWriteUtc is null || stamp.AnalysisOptionsVersion is null)
        {
            return false;
        }

        return stamp.SourceMaxWriteUtc == probedSourceMaxWriteUtc &&
            string.Equals(stamp.AnalysisOptionsVersion, AnalysisOptionsVersion, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the maximum last-write timestamp over the SOURCE evidence directories/files only, using direct
    /// System.IO. Returns null when none of the probed source locations exist (cannot determine freshness =>
    /// caller must rebuild). The snapshot output directory is never probed, so snapshot writes cannot bump it.
    /// </summary>
    private static DateTimeOffset? ProbeSourceMaxWriteUtc(Repository repository)
    {
        DateTimeOffset? max = null;
        bool sawAny = false;

        foreach (string relativeDirectory in SourceDirectories)
        {
            string directory = ArtifactPath.ResolveRepositoryPath(repository, relativeDirectory);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            sawAny = true;
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                DateTimeOffset writeTime = File.GetLastWriteTimeUtc(file);
                if (max is null || writeTime > max)
                {
                    max = writeTime;
                }
            }
        }

        string operationalContextCurrent = ArtifactPath.ResolveRepositoryPath(repository, OperationalContextCurrentRelativePath);
        if (File.Exists(operationalContextCurrent))
        {
            sawAny = true;
            DateTimeOffset writeTime = File.GetLastWriteTimeUtc(operationalContextCurrent);
            if (max is null || writeTime > max)
            {
                max = writeTime;
            }
        }

        if (!sawAny)
        {
            return null;
        }

        // An existing-but-empty source tree (directories present, no files) yields no timestamp; treat that as
        // unprobeable so recovery rebuilds rather than skipping against an absent stamp comparison.
        return max;
    }

    /// <summary>
    /// Re-writes the metrics snapshot (just rebuilt by the metrics service, which persisted it unstamped) with
    /// the warm-restart staleness stamp so the next boot can recognize an unchanged source tree and skip.
    /// </summary>
    private async Task StampMetricsSnapshotAsync(Repository repository, DateTimeOffset? sourceMaxWriteUtc)
    {
        if (sourceMaxWriteUtc is null)
        {
            return;
        }

        DecisionSessionMetricsSnapshot? snapshot = await sessionRepository.ReadMetricsSnapshotAsync(repository);
        if (snapshot is null)
        {
            return;
        }

        await sessionRepository.WriteMetricsSnapshotAsync(repository, snapshot, sourceMaxWriteUtc, AnalysisOptionsVersion);
    }

    private async Task<T?> RebuildSnapshotAsync<T>(
        string code,
        string successMessage,
        Func<Task<T>?> rebuild,
        List<DecisionSessionRecoveryFinding> findings,
        List<string> diagnostics)
    {
        Task<T>? rebuildTask = rebuild();
        if (rebuildTask is null)
        {
            string message = $"{successMessage[..successMessage.IndexOf(" was", StringComparison.Ordinal)]} was not rebuilt because the rebuild service is unavailable.";
            AddSkippedFinding(code.Replace("Rebuilt", "NotRebuilt", StringComparison.Ordinal), message, findings, diagnostics);
            return default;
        }

        try
        {
            T snapshot = await rebuildTask;
            findings.Add(new DecisionSessionRecoveryFinding(code, "Info", successMessage, null, null));
            diagnostics.Add(successMessage);
            return snapshot;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or KeyNotFoundException)
        {
            string message = $"{successMessage[..successMessage.IndexOf(" was", StringComparison.Ordinal)]} rebuild failed: {exception.Message}";
            findings.Add(new DecisionSessionRecoveryFinding(code.Replace("Rebuilt", "RebuildFailed", StringComparison.Ordinal), "Warning", message, null, null));
            diagnostics.Add(message);
            return default;
        }
    }

    private async Task RebuildTransferEligibilitySnapshotAsync(
        Repository repository,
        DecisionSessionDiagnostics registryDiagnostics,
        IReadOnlyList<DecisionSession> sessions,
        DecisionSession? activeSession,
        DecisionSessionLifecycleSnapshot? policySnapshot,
        DateTimeOffset checkedAt,
        List<DecisionSessionRecoveryFinding> findings,
        List<string> diagnostics)
    {
        if (evidenceReader is null)
        {
            AddSkippedFinding(
                "TransferEligibilitySnapshotNotRebuilt",
                "Transfer eligibility snapshot was not rebuilt because the evidence reader is unavailable.",
                findings,
                diagnostics);
            return;
        }

        DecisionSession? sourceSession = activeSession ?? sessions.FirstOrDefault(session => session.State == DecisionSessionState.TransferPending);
        DecisionSessionLifecycleEvaluation policyEvaluation = policySnapshot?.Evaluation ?? CreateUnavailablePolicyEvaluation(checkedAt);
        var eligibilityFindings = new List<DecisionSessionTransferEligibilityFinding>();
        DecisionSessionEvidence? evidence = null;
        AddEligibilityRegistryFindings(registryDiagnostics, eligibilityFindings);

        if (registryDiagnostics.IsValid)
        {
            if (sourceSession?.State == DecisionSessionState.TransferPending)
            {
                eligibilityFindings.Add(Deferred("transfer-pending", "The source decision session is already transfer-pending."));
            }
            else if (activeSession is null)
            {
                eligibilityFindings.Add(Blocked("no-active-session", "No active decision session exists for this repository."));
            }
        }

        if (policySnapshot is not null && policyEvaluation.Decision == DecisionSessionLifecycleDecision.Continue)
        {
            eligibilityFindings.Add(Info("policy-continue", "Lifecycle policy decided Continue; transfer eligibility is not applicable."));
        }
        else if (eligibilityFindings.All(finding => !IsBlocking(finding)))
        {
            try
            {
                evidence = await evidenceReader.ReadAsync(repository, activeSession, checkedAt);
                if (evidence.OperationalContextRevisionCount <= 0)
                {
                    eligibilityFindings.Add(Blocked("operational-context-unavailable", "Operational context evidence is unavailable for continuity transfer."));
                }

                if (evidence.EvidenceItemCount <= 0)
                {
                    eligibilityFindings.Add(Blocked("continuity-artifact-preflight-failed", "Continuity artifact generation cannot produce a valid artifact without repository evidence."));
                }
            }
            catch (IOException exception)
            {
                eligibilityFindings.Add(Deferred("repository-unavailable", $"Repository evidence could not be read right now: {exception.Message}"));
            }
            catch (UnauthorizedAccessException exception)
            {
                eligibilityFindings.Add(Deferred("repository-locked", $"Repository evidence is not currently accessible: {exception.Message}"));
            }
            catch (InvalidOperationException exception)
            {
                eligibilityFindings.Add(Blocked("continuity-evidence-invalid", $"Continuity evidence is invalid: {exception.Message}"));
            }
        }

        DecisionSessionTransferEligibilityStatus status = ResolveEligibilityStatus(policySnapshot, eligibilityFindings);
        if (status == DecisionSessionTransferEligibilityStatus.Eligible)
        {
            eligibilityFindings.Add(Info("eligible", "All transfer eligibility preconditions passed."));
        }

        var eligibility = new DecisionSessionTransferEligibility(
            status,
            policyEvaluation,
            sourceSession?.Id ?? activeSession?.Id,
            eligibilityFindings,
            checkedAt);
        var inputs = new DecisionSessionTransferEligibilityInputs(
            policyEvaluation,
            registryDiagnostics,
            activeSession,
            evidence);
        var eligibilityDiagnostics = new DecisionSessionTransferEligibilityDiagnostics(
            repository.Id,
            checkedAt,
            inputs,
            [
                "Transfer eligibility is an operational gate and does not change lifecycle policy decisions.",
                "Eligible means transfer execution may proceed, not that transfer is preferable.",
                "Blocked and Deferred statuses prevent transfer execution without mutating registry state.",
                "Continuity artifact checks are preflight checks until transfer execution creates the canonical artifact."
            ],
            []);
        await sessionRepository.WriteTransferEligibilitySnapshotAsync(
            repository,
            new DecisionSessionTransferEligibilitySnapshot(repository.Id, eligibility, eligibilityDiagnostics, checkedAt));

        const string message = "Transfer eligibility snapshot was rebuilt from registry, policy, and continuity evidence.";
        findings.Add(new DecisionSessionRecoveryFinding("TransferEligibilitySnapshotRebuilt", "Info", message, sourceSession?.Id, null));
        diagnostics.Add(message);
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

    private static void AddSkippedFinding(
        string code,
        string message,
        List<DecisionSessionRecoveryFinding> findings,
        List<string> diagnostics)
    {
        findings.Add(new DecisionSessionRecoveryFinding(code, "Warning", message, null, null));
        diagnostics.Add(message);
    }

    private static void AddEligibilityRegistryFindings(
        DecisionSessionDiagnostics registryDiagnostics,
        List<DecisionSessionTransferEligibilityFinding> findings)
    {
        if (registryDiagnostics.IsValid)
        {
            return;
        }

        foreach (string error in registryDiagnostics.Errors)
        {
            string code = error.Contains("More than one active", StringComparison.OrdinalIgnoreCase)
                ? "duplicate-active-sessions"
                : "registry-invalid";
            findings.Add(Blocked(code, error));
        }
    }

    private static DecisionSessionTransferEligibilityStatus ResolveEligibilityStatus(
        DecisionSessionLifecycleSnapshot? policySnapshot,
        IReadOnlyList<DecisionSessionTransferEligibilityFinding> findings)
    {
        if (findings.Any(finding => string.Equals(finding.Severity, "Blocked", StringComparison.Ordinal)))
        {
            return DecisionSessionTransferEligibilityStatus.Blocked;
        }

        if (findings.Any(finding => string.Equals(finding.Severity, "Deferred", StringComparison.Ordinal)))
        {
            return DecisionSessionTransferEligibilityStatus.Deferred;
        }

        if (policySnapshot?.Evaluation.Decision == DecisionSessionLifecycleDecision.Continue)
        {
            return DecisionSessionTransferEligibilityStatus.NotApplicable;
        }

        return policySnapshot?.Evaluation.Decision == DecisionSessionLifecycleDecision.Transfer
            ? DecisionSessionTransferEligibilityStatus.Eligible
            : DecisionSessionTransferEligibilityStatus.Blocked;
    }

    private static bool IsBlocking(DecisionSessionTransferEligibilityFinding finding)
    {
        return string.Equals(finding.Severity, "Blocked", StringComparison.Ordinal) ||
            string.Equals(finding.Severity, "Deferred", StringComparison.Ordinal);
    }

    private static DecisionSessionLifecycleEvaluation CreateUnavailablePolicyEvaluation(DateTimeOffset checkedAt)
    {
        return new DecisionSessionLifecycleEvaluation(
            DecisionSessionLifecycleDecision.Transfer,
            0m,
            0m,
            "Lifecycle policy evaluation was unavailable because registry preconditions failed before policy could safely run.",
            ["Policy decision unavailable; eligibility is blocked before transfer execution."],
            checkedAt);
    }

    private static DecisionSessionTransferEligibilityFinding Blocked(string code, string message)
    {
        return new DecisionSessionTransferEligibilityFinding(code, "Blocked", message);
    }

    private static DecisionSessionTransferEligibilityFinding Deferred(string code, string message)
    {
        return new DecisionSessionTransferEligibilityFinding(code, "Deferred", message);
    }

    private static DecisionSessionTransferEligibilityFinding Info(string code, string message)
    {
        return new DecisionSessionTransferEligibilityFinding(code, "Info", message);
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
