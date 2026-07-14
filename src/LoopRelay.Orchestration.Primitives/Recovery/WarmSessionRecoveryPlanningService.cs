using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Orchestration.Recovery;

public sealed class WarmSessionRecoveryPlanningService(ICanonicalRecoveryStore _store)
{
    public async Task<CanonicalRecoveryPlan> PlanResumeAsync(
        CanonicalCausalContext causality,
        string sessionIdentity,
        string resolvedPolicyIdentity,
        SessionContinuityProfile exactProfile,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken = default)
    {
        var subject = new RecoveryCausalSubject(causality, SessionIdentity: sessionIdentity);
        CanonicalRecoveryClassification classification = await new CanonicalRecoveryCaseRecorder(_store).RecordAsync(
            RecoveryScopeKind.WarmSession,
            subject,
            new RecoveryDurableFacts(
                RecoveryScopeKind.WarmSession, subject, EvidenceComplete: true, Corrupt: false,
                Authorized: true, ValidInFlightCorrelation: false,
                OutwardStarted: false, OutwardAccepted: false, ProviderOutcomeUnknown: false,
                TerminalProviderResult: false, RawOutputDurable: false, OutputPromoted: false,
                ExplicitFailure: true, ExplicitCancellation: false,
                RecoveryCancellationBoundary.None, RequiredEffects: 0, SucceededEffects: 0,
                CompletionClosureStarted: false, CompletionClosureSettled: false,
                Evidence: evidence),
            cancellationToken);
        var authority = new RecoveryPlanningAuthority(
            resolvedPolicyIdentity,
            exactProfile.Digest,
            ExactProfileSupported:
                exactProfile.Operation(SessionContinuityOperation.Resume).Status == SessionOperationSupport.Supported,
            CertifiedReconstructionAvailable: false,
            RetryAllowed: false,
            new HashSet<CanonicalRecoveryAction>
            {
                CanonicalRecoveryAction.ResumeSession,
                CanonicalRecoveryAction.RequestHumanDecision,
            },
            [exactProfile.EvidenceSource, $"profile:{exactProfile.Digest}"]);
        return await new CanonicalRecoveryCoordinator(_store).PlanAsync(
            new RecoveryPlanRequest(classification.Case, authority),
            cancellationToken);
    }
}
