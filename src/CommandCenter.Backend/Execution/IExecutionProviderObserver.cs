namespace CommandCenter.Backend.Execution;

public interface IExecutionProviderObserver
{
    Task OnStdOutAsync(string text);

    Task OnStdErrAsync(string text);

    Task OnProviderExitedAsync(int? exitCode);

    Task OnProviderCancelledAsync(string reason);
}
