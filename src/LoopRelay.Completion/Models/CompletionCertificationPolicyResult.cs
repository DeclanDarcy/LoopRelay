namespace LoopRelay.Completion;

public sealed record CompletionCertificationPolicyResult(
    bool IsValid,
    CompletionEvaluationDecision Decision,
    CompletionCertificationPolicyRule? Rule,
    string? RejectionReason)
{
    public static CompletionCertificationPolicyResult Valid(
        CompletionEvaluationDecision decision,
        CompletionCertificationPolicyRule rule) =>
        new(
            true,
            decision,
            rule,
            null);

    public static CompletionCertificationPolicyResult Invalid(
        CompletionEvaluationDecision decision,
        string reason) =>
        new(
            false,
            decision,
            null,
            reason);
}
