using LoopRelay.Orchestration.Services.Hitl;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class HitlArtifactCapture(
    ExplicitHitlNonImplementationRequestCaptureService? captureService)
{
    private readonly ExplicitHitlNonImplementationRequestCaptureService? _captureService = captureService;
    public Task CaptureAsync(string sourceArtifactPath, string sourceContent)
    {
        if (_captureService is null || string.IsNullOrWhiteSpace(sourceContent))
        {
            return Task.CompletedTask;
        }

        return _captureService.CaptureFromSourceAsync(sourceArtifactPath, sourceContent);
    }
}
