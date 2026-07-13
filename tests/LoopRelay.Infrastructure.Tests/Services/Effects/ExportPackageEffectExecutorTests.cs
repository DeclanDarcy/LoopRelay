using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using Xunit;

namespace LoopRelay.Infrastructure.Tests.Services.Effects;

public sealed class ExportPackageEffectExecutorTests
{
    [Fact]
    public async Task PackageWriteIsAtomicIdempotentAndIndependentlyReconciled()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(), Name = "export-effect",
            Path = Directory.CreateTempSubdirectory("looprelay-export-effect").FullName,
        };
        byte[] package = Encoding.UTF8.GetBytes("export-package-v1");
        string contentHash = Convert.ToHexStringLower(SHA256.HashData(package));
        var payload = new ExportPackageEffectPayload(
            ".LoopRelay/exports/workspace.bin", Convert.ToBase64String(package), contentHash);
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var intent = new EffectIntent(
            EffectIntentIdentity.New(),
            new CanonicalCausalContext(
                new WorkspaceIdentity("ws_test"), new RunIdentity("run_test"),
                new WorkflowInstanceIdentity("workflow_test"), new TransitionRunIdentity("transition_test"),
                new AttemptIdentity("attempt_test")),
            "export:write-package", WorkspaceEffectExecutorKeys.ExportPackageWrite, "1",
            new EffectTargetDescriptor("ExportPackage", payload.TargetRelativePath, "{}"),
            json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("workspace-contained", "{}"),
            new EffectCondition("byte-hash", "{}"),
            "independent-byte-hash", "export-package-test", DateTimeOffset.UtcNow);
        var executor = new ExportPackageEffectExecutor(repository);
        var reconciler = new ExportPackageEffectReconciler(repository);

        EffectExecutionObservation first = await executor.ExecuteAsync(intent, CancellationToken.None);
        EffectReconciliationObservation observed = await reconciler.ReconcileAsync(intent, CancellationToken.None);
        EffectExecutionObservation repeated = await executor.ExecuteAsync(intent, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, first.State);
        Assert.Equal(EffectReconciliationVerdict.Succeeded, observed.Verdict);
        Assert.Equal(first.AfterFacts, repeated.BeforeFacts);
        Assert.Equal(package, await File.ReadAllBytesAsync(Path.Combine(
            repository.Path, ".LoopRelay", "exports", "workspace.bin")));
    }
}
