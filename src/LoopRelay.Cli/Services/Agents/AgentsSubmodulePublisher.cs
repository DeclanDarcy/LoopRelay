using LoopRelay.Agents.Abstractions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Models.Git;

namespace LoopRelay.Cli.Services;

internal sealed class AgentsSubmodulePublisher
{
    public const string ContextUpdateMessage = "Orchestration loop: context update before execution";
    public const string ExecutionHandoffMessage = "Orchestration loop: execution handoff";
    public const string CompletionCertificationMessage = "Orchestration loop: completion certification";
    public const string PartialExitMessage = "Orchestration loop: partial state on interrupted exit";
    public const string GitlinkPointerMessage = "Orchestration loop: record .agents submodule pointer";

    private readonly Infrastructure.Services.Git.AgentsSubmodulePublisher publisher;

    public AgentsSubmodulePublisher(IProcessRunner processRunner, Repository repository, ILoopConsole console)
    {
        publisher = new Infrastructure.Services.Git.AgentsSubmodulePublisher(
            processRunner,
            repository,
            console,
            new AgentsSubmodulePublisherOptions(ActorName: "loop"));
    }

    public async Task<bool> PublishAsync(string commitMessage, CancellationToken cancellationToken)
    {
        try
        {
            bool committed = await publisher.PublishAgentsAsync(commitMessage, cancellationToken);
            if (committed)
            {
                await publisher.RecordParentGitlinkAsync(GitlinkPointerMessage, cancellationToken);
            }

            return committed;
        }
        catch (AgentsSubmodulePublisherException ex)
        {
            throw new LoopStepException(ex.Message, ex);
        }
    }
}
