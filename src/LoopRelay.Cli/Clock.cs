namespace LoopRelay.Cli;

/// <summary>Wall clock, abstracted so telemetry timestamps and day-rotation are deterministic under test.</summary>
internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}
