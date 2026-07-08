using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Console;

namespace LoopRelay.Infrastructure.Git;

public sealed record AgentsSubmodulePublisherOptions(
    string AgentsDirectory = ".agents",
    string ActorName = "workflow");
