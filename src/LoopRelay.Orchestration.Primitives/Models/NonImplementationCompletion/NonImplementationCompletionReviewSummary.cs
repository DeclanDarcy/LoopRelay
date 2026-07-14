using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationCompletion;

public sealed record NonImplementationCompletionReviewSummary(
    string RefreshId,
    int CurrentChangedFileCount,
    int ClassifiedFileCount,
    int SemanticCandidateCount,
    int ConfirmedThisRefreshCount,
    int ReusedSemanticDispositionCount,
    int UnresolvedConfirmedNonImplementationCount,
    int UnresolvedSemanticUncertaintyCount,
    int ResolvedFileDecisionCount,
    int AppliedDeleteCount,
    bool SynthesisDecisionRequired,
    NonImplementationSynthesisDecision? SynthesisDecision);
