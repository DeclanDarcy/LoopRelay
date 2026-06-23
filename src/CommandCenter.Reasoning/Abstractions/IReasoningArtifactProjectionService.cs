using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningArtifactProjectionService
{
    string RenderEvent(ReasoningEvent reasoningEvent);

    string RenderThread(ReasoningThread thread, IReadOnlyList<ReasoningRelationship> relationships);

    string RenderRelationship(ReasoningRelationship relationship);

    string RenderCertificationReport(ReasoningCertificationReport report);
}
