using LoopRelay.Agents.Abstractions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Models.Git;

namespace LoopRelay.Cli.Services.Agents;

internal interface IAgentsSubmodulePublishPreflight
{
    Task EnsureFreshExportAsync(CancellationToken cancellationToken);
}

internal sealed class NullAgentsSubmodulePublishPreflight : IAgentsSubmodulePublishPreflight
{
    public Task EnsureFreshExportAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class AgentsSubmodulePublisher
{
    public const string ContextUpdateMessage = "Orchestration loop: context update before execution";
    public const string ExecutionHandoffMessage = "Orchestration loop: execution handoff";
    public const string CompletionCertificationMessage = "Orchestration loop: completion certification";
    public const string PartialExitMessage = "Orchestration loop: partial state on interrupted exit";
    public const string GitlinkPointerMessage = "Orchestration loop: record .agents submodule pointer";

    private readonly Infrastructure.Services.Git.AgentsSubmodulePublisher _publisher;
    private readonly IAgentsSubmodulePublishPreflight _preflight;
    private readonly IProcessRunner _processRunner;
    private readonly Repository _repository;

    public AgentsSubmodulePublisher(
        IProcessRunner processRunner,
        Repository repository,
        ILoopConsole console,
        IAgentsSubmodulePublishPreflight? preflight = null)
    {
        _processRunner = processRunner;
        _repository = repository;
        _publisher = new Infrastructure.Services.Git.AgentsSubmodulePublisher(
            processRunner,
            repository,
            console,
            new AgentsSubmodulePublisherOptions(ActorName: "loop"));
        _preflight = preflight ?? new NullAgentsSubmodulePublishPreflight();
    }

    public async Task<bool> PublishAsync(string commitMessage, CancellationToken cancellationToken)
    {
        try
        {
            EnsureSupportedTopology();
            await _preflight.EnsureFreshExportAsync(cancellationToken);
            bool committed = await _publisher.PublishAgentsAsync(commitMessage, cancellationToken);
            if (committed)
            {
                await _publisher.RecordParentGitlinkAsync(GitlinkPointerMessage, cancellationToken);
            }

            return committed;
        }
        catch (AgentsSubmodulePublisherException ex)
        {
            throw new LoopStepException(ex.Message, ex);
        }
    }

    internal void EnsureSupportedTopology()
    {
        // Scripted runners have their own topology assertions. The active production runner must prove that
        // `.agents` is an independent Git worktree before any `git add -A` can execute there; otherwise Git
        // would walk up to the parent repository and could stage unrelated source changes.
        if (_processRunner is not LoopRelay.Agents.Services.Process.ProcessRunner)
        {
            return;
        }

        string agents = Path.Combine(_repository.Path, ".agents");
        string gitAuthority = Path.Combine(agents, ".git");
        if (!Directory.Exists(agents))
        {
            throw new LoopStepException(
                "Repository publication requires a repository-owned .agents directory; none was found.");
        }

        if (!Directory.Exists(gitAuthority) && !File.Exists(gitAuthority))
        {
            throw new LoopStepException(
                "Repository publication blocked before mutation: .agents is an ordinary parent-repository directory, not an independent Git repository or submodule.");
        }
    }
}
