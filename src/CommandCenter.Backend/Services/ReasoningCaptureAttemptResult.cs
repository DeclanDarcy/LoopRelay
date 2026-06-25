using CommandCenter.Reasoning.Models;

namespace CommandCenter.Backend.Services;

public enum ReasoningCaptureAttemptOutcome
{
    Captured,
    Skipped,
    Duplicate
}

public sealed record ReasoningCaptureAttemptResult(
    ReasoningCaptureMode AttemptedCaptureMode,
    ReasoningCaptureAttemptOutcome Result,
    string SourceTransition,
    string? SourceArtifact,
    DateTimeOffset? SourceTimestamp,
    string? CaptureReason,
    string? SkipReason,
    string? DuplicateSignal,
    ReasoningReference? ExistingEventReference,
    ReasoningReference? CapturedEventReference,
    IReadOnlyList<string> Diagnostics);
