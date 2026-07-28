using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Services.Effects;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Effects;

/// <summary>
/// The durable wrapper is the only production entry into completed-epic archival, so its planned
/// index - not just the inner service's derivation - must survive gaps left by rotation. A
/// count-derived index lands on the newest survivor and the inner convergence path then returns
/// the OLD epic's archive as this epic's result, silently.
/// </summary>
public sealed class DurableCompletedEpicArchiveServiceTests
{
    [Fact]
    public async Task The_durable_wrapper_plans_one_past_the_highest_survivor_never_the_count()
    {
        Repository repository = CreateRepository();
        var store = new MemoryArtifactStore();
        var artifacts = new CompletionArtifacts(store, repository);
        string root = CompletionArtifactPaths.CompletedEpicsDirectory;
        // Archives 1 and 3 survive; 2 was rotated away. Count+1 plans 3 and converges onto the
        // survivor; highest+1 must plan 4.
        await artifacts.WriteAsync($"{root}/1/epic.md", "# epic one");
        await artifacts.WriteAsync($"{root}/1.md", "# synthesis one");
        await artifacts.WriteAsync($"{root}/3/epic.md", "# epic three");
        await artifacts.WriteAsync($"{root}/3.md", "# synthesis three");
        var inner = new RecordingArchiveService(store);
        var wrapper = new DurableCompletedEpicArchiveService(
            repository, NewCausality(), store, inner);

        CompletedEpicArchiveResult result = await wrapper.ArchiveAndSynthesizeAsync(
            new CompletedEpicArchiveRequest(repository));

        Assert.Equal(4, inner.ReceivedIndex);
        Assert.Equal(4, result.Index);
        Assert.Equal($"{root}/4.md", result.SynthesisPath);
        // The survivor was not converged on or overwritten.
        Assert.Equal("# synthesis three", await artifacts.ReadAsync($"{root}/3.md"));
    }

    [Fact]
    public async Task Reconciler_accepts_a_materialized_epicless_archive()
    {
        Repository repository = CreateRepository();
        var store = new MemoryArtifactStore();
        var artifacts = new CompletionArtifacts(store, repository);
        string root = CompletionArtifactPaths.CompletedEpicsDirectory;
        // Fully materialized epicless archive: sources archived, synthesis written, and no
        // epic.md anywhere because the workspace never had a live epic.
        await artifacts.WriteAsync($"{root}/1/details.md", "DETAILS");
        await artifacts.WriteAsync($"{root}/1.md", "# Synthesis");
        var reconciler = new CompletionArchiveEffectReconciler(repository, store);

        EffectReconciliationObservation observation = await reconciler.ReconcileAsync(
            ArchiveIntent(Payload(root)), CancellationToken.None);

        Assert.Equal(EffectReconciliationVerdict.Succeeded, observation.Verdict);
    }

    [Fact]
    public async Task Reconciler_still_refuses_an_archive_missing_the_epic_it_should_have_copied()
    {
        Repository repository = CreateRepository();
        var store = new MemoryArtifactStore();
        var artifacts = new CompletionArtifacts(store, repository);
        string root = CompletionArtifactPaths.CompletedEpicsDirectory;
        // The live epic exists, so a completed archive must carry its copy; this one does not.
        await artifacts.WriteAsync(CompletionArtifactPaths.ActiveEpic, "# Epic\n\nIntent.");
        await artifacts.WriteAsync($"{root}/1/details.md", "DETAILS");
        await artifacts.WriteAsync($"{root}/1.md", "# Synthesis");
        var reconciler = new CompletionArchiveEffectReconciler(repository, store);

        EffectReconciliationObservation observation = await reconciler.ReconcileAsync(
            ArchiveIntent(Payload(root)), CancellationToken.None);

        // Succeeded is the only verdict the epicless relaxation could wrongly produce here. Which
        // refusal it lands on is decided by whether the store models the archive directory as an
        // existing entity, which this in-memory store does not; that is not what this pins.
        Assert.NotEqual(EffectReconciliationVerdict.Succeeded, observation.Verdict);
    }

    private static CompletionArchiveEffectPayload Payload(string root) => new(
        CompletionArtifactPaths.ActiveEpic, root, 1, $"{root}/1", $"{root}/1.md");

    // Mirrors the intent the durable wrapper plans (anchor: `new EffectIntent(` in
    // DurableCompletedEpicArchiveService.cs); only the payload matters to the reconciler.
    private static EffectIntent ArchiveIntent(CompletionArchiveEffectPayload payload)
    {
        string payloadJson = JsonSerializer.Serialize(
            payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        CanonicalCausalContext causality = NewCausality();
        return new EffectIntent(
            EffectIntentIdentity.New(), causality, "completion:archive-and-synthesize",
            WorkspaceEffectExecutorKeys.CompletionArchive, "1",
            new EffectTargetDescriptor("CompletedEpicArchive", payload.ArchiveDirectory, payloadJson),
            payloadJson, payloadHash, 0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("archive-targets-absent", payloadJson),
            new EffectCondition("archive-and-synthesis-present", payloadJson),
            "archive-structure-and-synthesis-observation",
            $"completion-archive:{causality.TransitionRun.Value}:{payload.Index}:{payloadHash}",
            DateTimeOffset.UtcNow);
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-cli-durable-archive-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static CanonicalCausalContext NewCausality() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(),
        AttemptIdentity.New());

    /// <summary>
    /// Stands in for the real archival: records the index the wrapper forced, materializes the
    /// files the executor's satisfied-predicate and the wrapper's terminal read require.
    /// </summary>
    private sealed class RecordingArchiveService(IArtifactStore _store) : ICompletedEpicArchiveService
    {
        public int? ReceivedIndex { get; private set; }

        public async Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
            CompletedEpicArchiveRequest request,
            CancellationToken cancellationToken = default)
        {
            ReceivedIndex = request.ArchiveIndex;
            int index = request.ArchiveIndex!.Value;
            var artifacts = new CompletionArtifacts(_store, request.Repository);
            string archiveDirectory = $"{request.ArchiveRoot}/{index}";
            string synthesisPath = $"{request.ArchiveRoot}/{index}.md";
            await artifacts.WriteAsync($"{archiveDirectory}/epic.md", "# archived epic");
            await artifacts.WriteAsync(synthesisPath, "# synthesized");
            return new CompletedEpicArchiveResult(index, archiveDirectory, synthesisPath, "# synthesized");
        }
    }
}
