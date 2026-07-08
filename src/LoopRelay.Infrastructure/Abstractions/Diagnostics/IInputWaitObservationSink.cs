using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Abstractions.Diagnostics;

public interface IInputWaitObservationSink
{
    ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken);
}
