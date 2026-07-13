using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Infrastructure.Tests.Services.Effects;

public sealed class WorkspaceCheckpointCleanupEffectExecutorTests
{
    [Fact]
    public async Task CleanupIsIdempotentAndIndependentlyReconcilable()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "checkpoint-cleanup",
            Path = Directory.CreateTempSubdirectory("looprelay-checkpoint-cleanup").FullName,
        };
        await SeedAsync(repository, "execution_warm_session.v1", "completion_certification.v1");
        EffectIntent intent = Intent();
        var executor = new WorkspaceCheckpointCleanupEffectExecutor(repository);
        var reconciler = new WorkspaceCheckpointCleanupReconciler(repository);

        EffectExecutionObservation first = await executor.ExecuteAsync(intent, CancellationToken.None);
        EffectExecutionObservation repeated = await executor.ExecuteAsync(intent, CancellationToken.None);
        EffectReconciliationObservation observed = await reconciler.ReconcileAsync(intent, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, first.State);
        Assert.Equal("2", first.BeforeFacts);
        Assert.Equal(EffectLifecycle.Succeeded, repeated.State);
        Assert.Equal("0", repeated.BeforeFacts);
        Assert.Equal(EffectReconciliationVerdict.Succeeded, observed.Verdict);
    }

    private static EffectIntent Intent()
    {
        var payload = new WorkspaceCheckpointCleanupPayload(
            ["execution_warm_session.v1", "completion_certification.v1"]);
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EffectIntent(
            EffectIntentIdentity.New(),
            new CanonicalCausalContext(
                new WorkspaceIdentity("ws_test"),
                new RunIdentity("run_test"),
                new WorkflowInstanceIdentity("workflow_test"),
                new TransitionRunIdentity("transition_test"),
                new AttemptIdentity("attempt_test")),
            "workspace:retire-completion-checkpoints",
            WorkspaceEffectExecutorKeys.CheckpointCleanup,
            "1",
            new EffectTargetDescriptor("WorkspaceMetadata", "workspace_metadata", "{}"),
            json,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            0,
            [],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("always", "{}"),
            new EffectCondition("metadata-absent", "{}"),
            "independent-observation",
            "checkpoint-cleanup-test",
            DateTimeOffset.UtcNow);
    }

    private static async Task SeedAsync(Repository repository, params string[] keys)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(database);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        foreach (string key in keys)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO workspace_metadata (key, value) VALUES ($key, 'test');";
            command.Parameters.AddWithValue("$key", key);
            await command.ExecuteNonQueryAsync();
        }
    }
}
