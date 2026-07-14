namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record ExplicitHitlNonImplementationRequestCaptureResult(
    int CapturedCount,
    IReadOnlyList<NonImplementationHitlRequestEntry> CapturedRequests);
