using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Diagnostics;

public sealed class NullInputWaitObservationSink : IInputWaitObservationSink
{
    public static NullInputWaitObservationSink Instance { get; } = new();

    private NullInputWaitObservationSink() { }

    public ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
