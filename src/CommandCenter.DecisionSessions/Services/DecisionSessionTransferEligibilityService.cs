using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionTransferEligibilityService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionRecoveryService recoveryService,
    IDecisionSessionLifecyclePolicy lifecyclePolicy,
    IDecisionSessionEvidenceReader evidenceReader,
    TimeProvider timeProvider) : IDecisionSessionTransferEligibilityService
{
    public async Task<DecisionSessionTransferEligibilitySnapshot> CheckAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset checkedAt = timeProvider.GetUtcNow();
        var warnings = new List<string>();

        try
        {
            _ = await sessionRepository.ReadTransferEligibilitySnapshotAsync(repository);
        }
        catch (DecisionSessionValidationException exception)
        {
            warnings.Add($"Existing transfer eligibility snapshot is invalid and was rebuilt: {exception.Message}");
        }
        catch (JsonException exception)
        {
            warnings.Add($"Existing transfer eligibility snapshot JSON is invalid and was rebuilt: {exception.Message}");
        }

        DecisionSessionDiagnostics registryDiagnostics = await recoveryService.GetDiagnosticsAsync(repositoryId);
        IReadOnlyList<DecisionSession> sessions = [];
        DecisionSession? activeSession = null;
        DecisionSession? sourceSession = null;
        if (registryDiagnostics.IsValid)
        {
            sessions = await sessionRepository.ListAsync(repository);
            activeSession = sessions.SingleOrDefault(session => session.State == DecisionSessionState.Active);
            sourceSession = activeSession ?? sessions.FirstOrDefault(session => session.State == DecisionSessionState.TransferPending);
        }

        DecisionSessionLifecycleSnapshot? policySnapshot = null;
        if (registryDiagnostics.IsValid && activeSession is not null)
        {
            policySnapshot = await lifecyclePolicy.EvaluateAsync(repositoryId);
        }

        DecisionSessionLifecycleEvaluation policyEvaluation = policySnapshot?.Evaluation ?? CreateUnavailablePolicyEvaluation(checkedAt);
        var findings = new List<DecisionSessionTransferEligibilityFinding>();
        DecisionSessionEvidence? evidence = null;

        AddRegistryFindings(registryDiagnostics, findings);

        if (registryDiagnostics.IsValid)
        {
            if (sourceSession?.State == DecisionSessionState.TransferPending)
            {
                findings.Add(Deferred("transfer-pending", "The source decision session is already transfer-pending."));
            }
            else if (activeSession is null)
            {
                findings.Add(Blocked("no-active-session", "No active decision session exists for this repository."));
            }
        }

        if (policySnapshot is not null && policyEvaluation.Decision == DecisionSessionLifecycleDecision.Continue)
        {
            findings.Add(Info("policy-continue", "Lifecycle policy decided Continue; transfer eligibility is not applicable."));
            return await PersistAsync(
                repository,
                checkedAt,
                DecisionSessionTransferEligibilityStatus.NotApplicable,
                policyEvaluation,
                activeSession,
                sourceSession,
                evidence,
                registryDiagnostics,
                findings,
                warnings);
        }

        if (findings.All(finding => !IsBlocking(finding)))
        {
            try
            {
                evidence = await evidenceReader.ReadAsync(repository, activeSession, checkedAt);
                if (evidence.OperationalContextRevisionCount <= 0)
                {
                    findings.Add(Blocked("operational-context-unavailable", "Operational context evidence is unavailable for continuity transfer."));
                }

                if (evidence.EvidenceItemCount <= 0)
                {
                    findings.Add(Blocked("continuity-artifact-preflight-failed", "Continuity artifact generation cannot produce a valid artifact without repository evidence."));
                }
            }
            catch (IOException exception)
            {
                findings.Add(Deferred("repository-unavailable", $"Repository evidence could not be read right now: {exception.Message}"));
            }
            catch (UnauthorizedAccessException exception)
            {
                findings.Add(Deferred("repository-locked", $"Repository evidence is not currently accessible: {exception.Message}"));
            }
            catch (InvalidOperationException exception)
            {
                findings.Add(Blocked("continuity-evidence-invalid", $"Continuity evidence is invalid: {exception.Message}"));
            }
        }

        DecisionSessionTransferEligibilityStatus status = ResolveStatus(policySnapshot, findings);
        if (status == DecisionSessionTransferEligibilityStatus.Eligible)
        {
            findings.Add(Info("eligible", "All transfer eligibility preconditions passed."));
        }

        return await PersistAsync(
            repository,
            checkedAt,
            status,
            policyEvaluation,
            activeSession,
            sourceSession,
            evidence,
            registryDiagnostics,
            findings,
            warnings);
    }

    private async Task<DecisionSessionTransferEligibilitySnapshot> PersistAsync(
        Repository repository,
        DateTimeOffset checkedAt,
        DecisionSessionTransferEligibilityStatus status,
        DecisionSessionLifecycleEvaluation policyEvaluation,
        DecisionSession? activeSession,
        DecisionSession? sourceSession,
        DecisionSessionEvidence? evidence,
        DecisionSessionDiagnostics registryDiagnostics,
        IReadOnlyList<DecisionSessionTransferEligibilityFinding> findings,
        IReadOnlyList<string> warnings)
    {
        var eligibility = new DecisionSessionTransferEligibility(
            status,
            policyEvaluation,
            sourceSession?.Id ?? activeSession?.Id,
            findings,
            checkedAt);
        var inputs = new DecisionSessionTransferEligibilityInputs(
            policyEvaluation,
            registryDiagnostics,
            activeSession,
            evidence);
        var diagnostics = new DecisionSessionTransferEligibilityDiagnostics(
            repository.Id,
            checkedAt,
            inputs,
            [
                "Transfer eligibility is an operational gate and does not change lifecycle policy decisions.",
                "Eligible means transfer execution may proceed, not that transfer is preferable.",
                "Blocked and Deferred statuses prevent transfer execution without mutating registry state.",
                "Continuity artifact checks are preflight checks until transfer execution creates the canonical artifact."
            ],
            warnings);
        var snapshot = new DecisionSessionTransferEligibilitySnapshot(repository.Id, eligibility, diagnostics, checkedAt);
        await sessionRepository.WriteTransferEligibilitySnapshotAsync(repository, snapshot);
        return snapshot;
    }

    private static void AddRegistryFindings(
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

    private static DecisionSessionTransferEligibilityStatus ResolveStatus(
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

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
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
}
