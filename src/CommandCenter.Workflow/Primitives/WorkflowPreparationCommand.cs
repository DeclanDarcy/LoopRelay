namespace CommandCenter.Workflow.Primitives;

public enum WorkflowPreparationCommand
{
    None,
    GenerateDecisionReviewArtifacts,
    GenerateOperationalContextProposal,
    PrepareExecutionCommit
}
