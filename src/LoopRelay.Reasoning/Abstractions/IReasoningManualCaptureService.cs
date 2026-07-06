using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningManualCaptureService
{
    IReadOnlyList<ManualReasoningCaptureTemplate> ListTemplates();

    Task<ReasoningEvent> CaptureAsync(Guid repositoryId, ManualReasoningCaptureCommand command);
}
