namespace CommandCenter.Workflow.Primitives;

public readonly record struct WorkflowPreparationFingerprint(string Value)
{
    public override string ToString() => Value;

    public static WorkflowPreparationFingerprint FromEvaluation(
        Guid repositoryId,
        WorkflowStage stage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        WorkflowPreparationCommand command,
        bool canPrepare,
        string reason,
        IReadOnlyList<string> projectionInputs,
        IReadOnlyList<string> openGateIds)
    {
        string normalized = string.Join(
            '\n',
            "preparation-evaluation",
            repositoryId,
            stage,
            progressState,
            blockingGate,
            command,
            canPrepare,
            reason,
            $"inputs:{string.Join("|", projectionInputs)}",
            $"open-gates:{string.Join("|", openGateIds)}");

        return new WorkflowPreparationFingerprint(
            WorkflowFingerprint.FromNormalizedEvidence(normalized).Value);
    }
}
