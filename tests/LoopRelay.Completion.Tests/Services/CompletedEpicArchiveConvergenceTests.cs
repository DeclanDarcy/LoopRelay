using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using Xunit;

namespace LoopRelay.Completion.Tests.Services;

/// <summary>
/// Convergence gate for completed-epic archival.
///
/// The synthesis prompt is the one non-retractable outward effect in this pipeline, so the
/// assertion that carries the weight in every test here is the prompt-invocation count. A repeat
/// execution of the same durable effect payload must not increase it.
/// </summary>
public sealed class CompletedEpicArchiveConvergenceTests
{
    private const string ArchiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory;

    [Fact]
    public async Task Re_executing_after_an_interrupted_archival_observes_it_and_re_runs_no_prompt()
    {
        var harness = ArchiveHarness.Create();
        await harness.SeedExecutionWorkspaceAsync();

        // Interrupt partway: the synthesis prompt writes its output and the process then dies
        // before the outcome is durably recorded. The archive is materialized; the effect is not
        // known to have succeeded, so it is re-executed against the same durable payload.
        harness.Prompts.Handler = async _ =>
        {
            await harness.WriteAsync($"{ArchiveRoot}/1.md", "# Completed Epic\n\nSynthesis.");
            throw new InvalidOperationException("interrupted before the archival outcome was recorded");
        };

        // The index is an input, exactly as CompletionArchiveEffectExecutor now supplies it from
        // CompletionArchiveEffectPayload.Index.
        var request = new CompletedEpicArchiveRequest(harness.Repository, ArchiveIndex: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Archive.ArchiveAndSynthesizeAsync(request));
        Assert.Single(harness.Prompts.Invocations);

        harness.Prompts.Handler = _ =>
            throw new InvalidOperationException("the synthesis prompt must not be invoked a second time");

        CompletedEpicArchiveResult replayed = await harness.Archive.ArchiveAndSynthesizeAsync(request);

        Assert.Equal(1, replayed.Index);
        Assert.Equal($"{ArchiveRoot}/1", replayed.ArchiveDirectory);
        Assert.Equal($"{ArchiveRoot}/1.md", replayed.SynthesisPath);
        Assert.Equal("# Completed Epic\n\nSynthesis.", replayed.SynthesisContent);
        Assert.Single(await harness.ListDirectoriesAsync(ArchiveRoot));
        Assert.Single(await harness.SynthesisFilesAsync());
        Assert.Single(harness.Prompts.Invocations);
    }

    [Fact]
    public async Task Archiving_the_same_durable_payload_twice_converges_on_one_archive()
    {
        var harness = ArchiveHarness.Create();
        await harness.SeedExecutionWorkspaceAsync();
        harness.Prompts.Handler = _ => Task.FromResult("# Completed Epic\n\nSynthesis.");
        var request = new CompletedEpicArchiveRequest(harness.Repository, ArchiveIndex: 1);

        CompletedEpicArchiveResult first = await harness.Archive.ArchiveAndSynthesizeAsync(request);
        CompletedEpicArchiveResult second = await harness.Archive.ArchiveAndSynthesizeAsync(request);

        Assert.Single(harness.Prompts.Invocations);
        Assert.Equal(first.Index, second.Index);
        Assert.Equal(first.ArchiveDirectory, second.ArchiveDirectory);
        Assert.Equal(first.SynthesisPath, second.SynthesisPath);
        Assert.Equal(first.SynthesisContent, second.SynthesisContent);
        Assert.Single(await harness.ListDirectoriesAsync(ArchiveRoot));
        Assert.Single(await harness.SynthesisFilesAsync());
    }

    [Fact]
    public async Task Re_executing_a_partially_materialized_archive_still_trips_the_collision_guard()
    {
        var harness = ArchiveHarness.Create();
        await harness.SeedExecutionWorkspaceAsync();
        harness.Prompts.Handler = _ =>
            throw new InvalidOperationException("interrupted before any synthesis was produced");
        var request = new CompletedEpicArchiveRequest(harness.Repository, ArchiveIndex: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Archive.ArchiveAndSynthesizeAsync(request));

        // Archive structure without a synthesis is not a satisfied postcondition. Convergence must
        // not swallow it: the collision guard still refuses, rather than silently repeating.
        CompletionCertificationException collision =
            await Assert.ThrowsAsync<CompletionCertificationException>(
                () => harness.Archive.ArchiveAndSynthesizeAsync(request));

        Assert.Contains("Completed epic archive collision", collision.Message, StringComparison.Ordinal);
        Assert.Single(harness.Prompts.Invocations);
        Assert.Single(await harness.ListDirectoriesAsync(ArchiveRoot));
        Assert.Empty(await harness.SynthesisFilesAsync());
    }

    [Fact]
    public async Task Archiving_without_a_supplied_index_still_allocates_the_next_archive()
    {
        var harness = ArchiveHarness.Create();
        await harness.SeedExecutionWorkspaceAsync();
        harness.Prompts.Handler = _ => Task.FromResult("# Completed Epic\n\nSynthesis.");

        CompletedEpicArchiveResult first = await harness.Archive.ArchiveAndSynthesizeAsync(
            new CompletedEpicArchiveRequest(harness.Repository));
        await harness.WriteAsync(CompletionArtifactPaths.ActiveEpic, "# Epic\n\nNext intent.");
        CompletedEpicArchiveResult next = await harness.Archive.ArchiveAndSynthesizeAsync(
            new CompletedEpicArchiveRequest(harness.Repository));

        Assert.Equal(1, first.Index);
        Assert.Equal(2, next.Index);
        Assert.Equal(2, harness.Prompts.Invocations.Count);
    }

    private sealed class ArchiveHarness(
        MemoryArtifactStore store,
        Repository repository,
        CountingPromptRunner prompts,
        ICompletedEpicArchiveService archive)
    {
        public Repository Repository { get; } = repository;

        public CountingPromptRunner Prompts { get; } = prompts;

        public ICompletedEpicArchiveService Archive { get; } = archive;

        public static ArchiveHarness Create()
        {
            var store = new MemoryArtifactStore();
            var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" };
            var prompts = new CountingPromptRunner();
            return new ArchiveHarness(store, repository, prompts, new CompletedEpicArchiveService(store, prompts));
        }

        public async Task SeedExecutionWorkspaceAsync()
        {
            await WriteAsync(CompletionArtifactPaths.ActiveEpic, "# Epic\n\nIntent.");
            await WriteAsync(CompletionArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context\n\nCurrent.");
            await WriteAsync(CompletionArtifactPaths.ExecutionPlan, "PLAN");
            await WriteAsync(CompletionArtifactPaths.Details, "DETAILS");
            await WriteAsync(CompletionArtifactPaths.OperationalContext, "OPCTX");
            await WriteAsync(".agents/milestones/m1.md", "- [x] milestone");
            await WriteAsync(".agents/decisions/decisions.md", "DECISION");
            await WriteAsync(".agents/deltas/operational_delta.0001.md", "DELTA");
            await WriteAsync(".agents/handoffs/handoff.md", "HANDOFF");
        }

        public Task WriteAsync(string relativePath, string content) =>
            store.WriteAsync(Resolve(relativePath), content);

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory) =>
            store.ListDirectoriesAsync(Resolve(relativeDirectory));

        /// <summary>
        /// The synthesis files are the markdown files directly under the archive root; the store's
        /// ListAsync is prefix-based and would otherwise also return every archived artifact.
        /// </summary>
        public async Task<IReadOnlyList<string>> SynthesisFilesAsync()
        {
            string root = Normalize(Resolve(ArchiveRoot)).TrimEnd('/');
            return (await store.ListAsync(Resolve(ArchiveRoot), "*.md"))
                .Select(Normalize)
                .Where(path => !path[(root.Length + 1)..].Contains('/', StringComparison.Ordinal))
                .ToArray();
        }

        private static string Normalize(string path) => path.Replace('\\', '/');

        private string Resolve(string relativePath) =>
            ArtifactPath.ResolveRepositoryPath(Repository, relativePath);
    }

    private sealed class CountingPromptRunner : ICompletionPromptRunner
    {
        public List<CompletionRuntimePromptInvocation> Invocations { get; } = [];

        public Func<CompletionRuntimePromptInvocation, Task<string>> Handler { get; set; } =
            invocation => throw new InvalidOperationException(invocation.RuntimePromptName);

        public async Task<string> RunAsync(
            CompletionRuntimePromptInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(invocation);
            return await Handler(invocation);
        }
    }
}
