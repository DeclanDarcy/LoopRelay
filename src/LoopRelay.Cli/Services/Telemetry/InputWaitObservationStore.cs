using System.Collections.Concurrent;
using LoopRelay.Agents.Primitives;
using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Cli.Services;

internal sealed class InputWaitObservationStore : IInputWaitObservationSink
{
    private readonly ConcurrentDictionary<(Guid SessionId, int TurnIndex), InputWaitObservation> observations = new();

    public ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        observations[(observation.SessionId.Value, observation.TurnIndex)] = observation;
        return ValueTask.CompletedTask;
    }

    public InputWaitObservation? Take(SessionIdentity sessionId, int turnIndex) =>
        observations.TryRemove((sessionId.Value, turnIndex), out InputWaitObservation? observation)
            ? observation
            : null;
}
