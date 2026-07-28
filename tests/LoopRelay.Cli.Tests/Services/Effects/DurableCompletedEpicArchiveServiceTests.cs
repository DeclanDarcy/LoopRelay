using LoopRelay.Cli.Services.Effects;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
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
