using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Services.Telemetry;

internal sealed class CompositeSessionTelemetrySink(IReadOnlyList<ISessionTelemetrySink> sinks) : ISessionTelemetrySink
{
    public void Append(SessionTelemetryRecord record)
    {
        Exception? firstFailure = null;
        foreach (ISessionTelemetrySink sink in sinks)
        {
            try
            {
                sink.Append(record);
            }
            catch (Exception ex)
            {
                firstFailure ??= ex;
            }
        }

        if (firstFailure is not null)
        {
            throw firstFailure;
        }
    }
}
