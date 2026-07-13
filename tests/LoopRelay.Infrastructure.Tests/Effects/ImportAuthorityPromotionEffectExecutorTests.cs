using System.Security.Cryptography;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Tests.Effects;

public sealed class ImportAuthorityPromotionEffectExecutorTests
{
    [Fact]
    public async Task Promotion_archives_old_authority_and_is_idempotently_observable()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-import-effect").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "source.db"), "canonical");
            File.WriteAllText(Path.Combine(root, "target.db"), "legacy");
            string hash = Convert.ToHexStringLower(SHA256.HashData("canonical"u8.ToArray()));
            var payload = new ImportAuthorityPromotionEffectPayload("source.db", "target.db", "target.legacy.db", hash);
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var intent = new EffectIntent(EffectIntentIdentity.New(), new(WorkspaceIdentity.New(), RunIdentity.New(),
                WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New()), "import:test",
                WorkspaceEffectExecutorKeys.ImportAuthorityPromotion, "1", new("database", "target.db", "{}"),
                json, hash, 0, [], EffectRequiredness.BlockingLocal, new("source-hash", "{}"),
                new("target-hash", "{}"), "hash", "import-effect-test", DateTimeOffset.UtcNow);
            var executor = new ImportAuthorityPromotionEffectExecutor(
                new Repository { Id = Guid.NewGuid(), Name = "fixture", Path = root });

            EffectExecutionObservation first = await executor.ExecuteAsync(intent, CancellationToken.None);
            EffectExecutionObservation second = await executor.ExecuteAsync(intent, CancellationToken.None);
            EffectReconciliationObservation reconciled = await new ImportAuthorityPromotionEffectReconciler(
                new Repository { Id = Guid.NewGuid(), Name = "fixture", Path = root })
                .ReconcileAsync(intent, CancellationToken.None);

            Assert.Equal(EffectLifecycle.Succeeded, first.State);
            Assert.Equal(EffectLifecycle.Succeeded, second.State);
            Assert.Equal("legacy", File.ReadAllText(Path.Combine(root, "target.legacy.db")));
            Assert.Equal("canonical", File.ReadAllText(Path.Combine(root, "target.db")));
            Assert.Equal(EffectReconciliationVerdict.Succeeded, reconciled.Verdict);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
