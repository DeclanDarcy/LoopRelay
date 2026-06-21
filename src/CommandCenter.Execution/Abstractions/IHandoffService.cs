namespace CommandCenter.Execution.Abstractions;

public interface IHandoffService
{
    Task ProcessProviderCompletionAsync(Guid sessionId);
}
