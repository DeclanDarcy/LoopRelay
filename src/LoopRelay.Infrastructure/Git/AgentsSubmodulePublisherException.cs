using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Console;

namespace LoopRelay.Infrastructure.Git;

public sealed class AgentsSubmodulePublisherException : Exception
{
    public AgentsSubmodulePublisherException(string message) : base(message)
    {
    }
}
