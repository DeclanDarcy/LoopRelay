namespace CommandCenter.Workflow.Primitives;

public enum WorkflowPreparationCommand
{
    None,
    DiscoverDecisionCandidates,
    GenerateDecisionProposal,
    GenerateOperationalContextProposal,
    PrepareExecutionCommit
}
