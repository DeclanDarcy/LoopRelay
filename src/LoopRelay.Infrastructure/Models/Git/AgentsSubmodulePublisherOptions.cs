namespace LoopRelay.Infrastructure.Models.Git;

public sealed record AgentsSubmodulePublisherOptions(
    string AgentsDirectory = ".agents",
    string ActorName = "workflow");
