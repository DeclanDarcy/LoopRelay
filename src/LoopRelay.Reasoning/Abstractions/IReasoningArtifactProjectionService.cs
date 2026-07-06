using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningArtifactProjectionService
{
    string RenderEvent(ReasoningEvent reasoningEvent);

    string RenderThread(ReasoningThread thread, IReadOnlyList<ReasoningRelationship> relationships);

    string RenderRelationship(ReasoningRelationship relationship);

    string RenderReconstructionReport(ReasoningReconstructionReport report);

    string RenderCertificationReport(ReasoningCertificationReport report);
}
