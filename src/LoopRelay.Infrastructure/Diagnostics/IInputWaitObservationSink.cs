using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Diagnostics;

public interface IInputWaitObservationSink
{
    ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken);
}
