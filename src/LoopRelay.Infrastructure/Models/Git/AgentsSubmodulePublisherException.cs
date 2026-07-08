namespace LoopRelay.Infrastructure.Models.Git;

public sealed class AgentsSubmodulePublisherException : Exception
{
    public AgentsSubmodulePublisherException(string message) : base(message)
    {
    }
}
