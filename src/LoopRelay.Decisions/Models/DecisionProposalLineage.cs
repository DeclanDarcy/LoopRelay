using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionProposalLineage(
    Guid RepositoryId,
    string ProposalId,
    DecisionProposalState CurrentState,
    string CurrentProposalFingerprint,
    DecisionProposal CurrentProposal,
    DecisionReviewStatus Review,
    IReadOnlyList<DecisionProposalLineageEvent> Events,
    IReadOnlyList<DecisionProposalRevisionSnapshot> Revisions,
    IReadOnlyList<DecisionReviewNote> ReviewNotes,
    IReadOnlyList<string> Diagnostics);
