using LoopRelay.Agents.Abstractions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Infrastructure.Models.Git;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Agents;

internal interface IAgentsSubmodulePublishPreflight
{
    Task EnsureFreshExportAsync(CancellationToken cancellationToken);
}

internal sealed class NullAgentsSubmodulePublishPreflight : IAgentsSubmodulePublishPreflight
{
    public Task EnsureFreshExportAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class SqliteAgentsSubmodulePublishPreflight(
    IArtifactStore _store,
    Repository _repository) : IAgentsSubmodulePublishPreflight
{
    public async Task EnsureFreshExportAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!LoopWorkspaceDatabase.HasUsableLoopHistoryDatabase(_repository))
        {
            return;
        }

        string databasePath = LoopWorkspaceDatabase.Resolve(_repository);
        await using SqliteConnection connection = LoopWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        await ExportTableAsync(
            connection,
            """
            SELECT logical_path, body
            FROM loop_history
            ORDER BY kind, sequence;
            """,
            cancellationToken);

        if (await TableExistsAsync(connection, "execution_evidence", cancellationToken))
        {
            await ExportTableAsync(
                connection,
                """
                SELECT logical_path, body
                FROM execution_evidence
                ORDER BY stem, sequence;
                """,
                cancellationToken);
        }
    }

    private async Task ExportTableAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string relativePath = reader.GetString(0);
            string body = reader.GetString(1);
            await _store.WriteAsync(ArtifactPath.ResolveRepositoryPath(_repository, relativePath), body);
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar) == 1;
    }
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

    public AgentsSubmodulePublisher(
        IProcessRunner processRunner,
        Repository repository,
        ILoopConsole console,
        IAgentsSubmodulePublishPreflight? preflight = null)
    {
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
}
