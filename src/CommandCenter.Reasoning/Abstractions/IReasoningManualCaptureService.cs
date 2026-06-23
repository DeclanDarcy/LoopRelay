using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningManualCaptureService
{
    IReadOnlyList<ManualReasoningCaptureTemplate> ListTemplates();

    Task<ReasoningEvent> CaptureAsync(Guid repositoryId, ManualReasoningCaptureCommand command);
}
