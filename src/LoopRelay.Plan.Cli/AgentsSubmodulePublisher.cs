using LoopRelay.Agents.Abstractions;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Git;

namespace LoopRelay.Plan.Cli;

internal sealed class AgentsSubmodulePublisher
{
    public const string ArchivePreviousEpicMessage = "Plan pipeline: archive previous epic";
    public const string WritePlanMessage = "Plan pipeline: write plan";
    public const string GenerateAdversarialReviewProjectionMessage = "Plan pipeline: generate adversarial review projection";
    public const string RevisePlanMessage = "Plan pipeline: revise plan and seed operational context";
    public const string CollectDetailsMessage = "Plan pipeline: collect details";
    public const string ExtractMilestonesMessage = "Plan pipeline: extract milestones";
    public const string ExtractDetailsMessage = "Plan pipeline: extract details";
    public const string GitlinkPointerMessage = "Plan pipeline: record .agents submodule pointer";

    private readonly Infrastructure.Git.AgentsSubmodulePublisher publisher;

    public AgentsSubmodulePublisher(IProcessRunner processRunner, Repository repository, ILoopConsole console)
    {
        publisher = new Infrastructure.Git.AgentsSubmodulePublisher(
            processRunner,
            repository,
            console,
            new AgentsSubmodulePublisherOptions(ActorName: "pipeline"));
    }

    public async Task<bool> PublishAgentsAsync(string commitMessage, CancellationToken cancellationToken)
    {
        try
        {
            return await publisher.PublishAgentsAsync(commitMessage, cancellationToken);
        }
        catch (AgentsSubmodulePublisherException ex)
        {
            throw new PlanStepException(ex.Message, ex);
        }
    }

    public async Task RecordParentGitlinkAsync(CancellationToken cancellationToken)
    {
        try
        {
            await publisher.RecordParentGitlinkAsync(GitlinkPointerMessage, cancellationToken);
        }
        catch (AgentsSubmodulePublisherException ex)
        {
            throw new PlanStepException(ex.Message, ex);
        }
    }
}
