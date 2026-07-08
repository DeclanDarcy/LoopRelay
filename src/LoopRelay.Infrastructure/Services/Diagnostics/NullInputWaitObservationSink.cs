using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class NullInputWaitObservationSink : IInputWaitObservationSink
{
    public static NullInputWaitObservationSink Instance { get; } = new();

    private NullInputWaitObservationSink() { }

    public ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
