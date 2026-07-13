using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using Xunit;

namespace LoopRelay.Infrastructure.Tests.Services.Effects;

public sealed class FilesystemWriteEffectExecutorTests
{
    [Fact]
    public async Task WriteIsIdempotentAndReceiptLossIsIndependentlyReconciled()
    {
        Repository repository = Repository();
        EffectIntent intent = Intent(".LoopRelay/evidence/marker.md", "canonical marker");
        var executor = new FilesystemWriteEffectExecutor(repository);
        var reconciler = new FilesystemWriteEffectReconciler(repository);

        EffectExecutionObservation written = await executor.ExecuteAsync(intent, CancellationToken.None);
        EffectReconciliationObservation reconciled = await reconciler.ReconcileAsync(intent, CancellationToken.None);
        EffectExecutionObservation repeated = await executor.ExecuteAsync(intent, CancellationToken.None);

        Assert.Equal(EffectLifecycle.Succeeded, written.State);
        Assert.Equal(EffectReconciliationVerdict.Succeeded, reconciled.Verdict);
        Assert.Equal(written.AfterFacts, repeated.BeforeFacts);
        Assert.Equal("canonical marker", await File.ReadAllTextAsync(
            Path.Combine(repository.Path, ".LoopRelay", "evidence", "marker.md")));
    }

    [Fact]
    public async Task WriteRejectsTargetsOutsideWorkspace()
    {
        Repository repository = Repository();
        EffectIntent intent = Intent("../escape.md", "forbidden");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FilesystemWriteEffectExecutor(repository).ExecuteAsync(intent, CancellationToken.None));
    }

    private static Repository Repository() => new()
    {
        Id = Guid.NewGuid(),
        Name = "filesystem-effect",
        Path = Directory.CreateTempSubdirectory("looprelay-filesystem-effect").FullName,
    };

    private static EffectIntent Intent(string path, string content)
    {
        string json = JsonSerializer.Serialize(
            new FilesystemWriteEffectPayload(path, content),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EffectIntent(
            EffectIntentIdentity.New(),
            new CanonicalCausalContext(
                new WorkspaceIdentity("ws_test"), new RunIdentity("run_test"),
                new WorkflowInstanceIdentity("workflow_test"), new TransitionRunIdentity("transition_test"),
                new AttemptIdentity("attempt_test")),
            "filesystem:test-write", WorkspaceEffectExecutorKeys.FilesystemWrite, "1",
            new EffectTargetDescriptor("Filesystem", path, "{}"),
            json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("workspace-contained", "{}"),
            new EffectCondition("content-hash", "{}"),
            "independent-content-hash", "filesystem-write-test", DateTimeOffset.UtcNow);
    }
}
