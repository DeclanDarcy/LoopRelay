using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Abstractions.Diagnostics;

public interface IInputWaitProgressRenderer
{
    TimeSpan RefreshInterval { get; }

    void Started(InputWaitProgressSnapshot snapshot);

    void Waiting(InputWaitProgressSnapshot snapshot);

    void FirstOutput(InputWaitProgressSnapshot snapshot);

    void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot);
}
