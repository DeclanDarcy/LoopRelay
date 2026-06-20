namespace CommandCenter.Backend.Execution;

public interface IHandoffService
{
    Task ProcessProviderCompletionAsync(Guid sessionId);
}
