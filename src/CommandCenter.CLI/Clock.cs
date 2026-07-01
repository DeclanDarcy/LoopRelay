using System;

namespace CommandCenter.Cli;

/// <summary>Wall clock, abstracted so telemetry timestamps and day-rotation are deterministic under test.</summary>
internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
