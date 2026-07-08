using LoopRelay.Infrastructure.Console;

namespace LoopRelay.Infrastructure.Diagnostics;

public interface IInputWaitProgressRenderer
{
    TimeSpan RefreshInterval { get; }

    void Started(InputWaitProgressSnapshot snapshot);

    void Waiting(InputWaitProgressSnapshot snapshot);

    void FirstOutput(InputWaitProgressSnapshot snapshot);

    void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot);
}
