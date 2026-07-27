using System.Security.Cryptography;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Infrastructure.Tests.Services.Effects;

public sealed class StorageAuthorityPromotionEffectExecutorTests
{
    [Fact]
    public async Task Promotion_converges_when_a_stale_temp_file_survives_an_interrupted_attempt()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-storage-effect").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "staging"));
            Directory.CreateDirectory(Path.Combine(root, "canonical"));
            string source = Path.Combine(root, "staging", "authority.db");
            string target = Path.Combine(root, "canonical", "authority.db");
            await File.WriteAllTextAsync(source, "authority bytes");
            string hash = Convert.ToHexStringLower(SHA256.HashData("authority bytes"u8.ToArray()));
            var payload = new StorageAuthorityPromotionEffectPayload(
                "staging/authority.db", "canonical/authority.db", hash);
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var intent = new EffectIntent(EffectIntentIdentity.New(), new(WorkspaceIdentity.New(), RunIdentity.New(),
                WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New()), "storage:test",
                WorkspaceEffectExecutorKeys.StorageAuthorityPromotion, "1",
                new("database", "canonical/authority.db", "{}"), json, hash, 0, [],
                EffectRequiredness.BlockingLocal, new("source-hash", "{}"), new("target-hash", "{}"),
                "hash", "storage-effect-test", DateTimeOffset.UtcNow);
            var executor = new StorageAuthorityPromotionEffectExecutor(
                new Repository { Id = Guid.NewGuid(), Name = "fixture", Path = root });

            // Reproduce the crash window between the verified copy and the atomic move: the target is
            // still absent, but the stable per-intent temp file from the interrupted attempt survives.
            string promotion = target + $".promotion-{intent.Identity.Value}";
            await File.WriteAllTextAsync(promotion, "stale leftover");

            EffectExecutionObservation observation = await executor.ExecuteAsync(intent, CancellationToken.None);

            Assert.Equal(EffectLifecycle.Succeeded, observation.State);
            Assert.True(observation.PostconditionSatisfied);
            Assert.Equal("authority bytes", await File.ReadAllTextAsync(target));
            Assert.False(File.Exists(promotion));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
