using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

internal sealed class HitlArtifactCapture(
    ExplicitHitlNonImplementationRequestCaptureService? captureService)
{
    public Task CaptureAsync(string sourceArtifactPath, string sourceContent)
    {
        if (captureService is null || string.IsNullOrWhiteSpace(sourceContent))
        {
            return Task.CompletedTask;
        }

        return captureService.CaptureFromSourceAsync(sourceArtifactPath, sourceContent);
    }
}
