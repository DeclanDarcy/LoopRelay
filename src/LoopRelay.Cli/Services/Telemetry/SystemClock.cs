using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Telemetry;
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
