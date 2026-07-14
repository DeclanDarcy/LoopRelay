using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Tests.Services.Telemetry;


/// <summary>A clock a test can set to any instant (drives day-rotation + record timestamps).</summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
}
