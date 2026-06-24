namespace CommandCenter.Workflow.Primitives;

public readonly record struct WorkflowContinuationFingerprint(string Value)
{
    public override string ToString() => Value;

    public static WorkflowContinuationFingerprint FromEvaluation(
        Guid repositoryId,
        WorkflowStage fromStage,
        WorkflowStage? toStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        bool canAdvance,
        string stopReason,
        IReadOnlyList<string> projectionInputs,
        IReadOnlyList<string> openGateIds)
    {
        string normalized = string.Join(
            '\n',
            "continuation-evaluation",
            repositoryId,
            fromStage,
            toStage?.ToString() ?? "none",
            progressState,
            blockingGate,
            canAdvance,
            stopReason,
            $"inputs:{string.Join("|", projectionInputs)}",
            $"open-gates:{string.Join("|", openGateIds)}");

        return new WorkflowContinuationFingerprint(
            WorkflowFingerprint.FromNormalizedEvidence(normalized).Value);
    }
}
